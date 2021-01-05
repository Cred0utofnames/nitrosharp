using System.Collections.Generic;
using System.Numerics;
using NitroSharp.NsScript.VM;
using NitroSharp.Saving;
using NitroSharp.Text;

#nullable enable

namespace NitroSharp.Graphics
{
    internal sealed class DialoguePage : RenderItem2D
    {
        private enum ConsumeResult
        {
            KeepGoing,
            Halt,
            AllDone
        }

        private readonly Size? _bounds;
        private readonly float _lineHeight;
        private readonly NsScriptThread _dialogueThread;
        private readonly TextLayout _layout;
        private readonly List<string> _pxmlLines = new();
        private readonly Queue<TextBufferSegment> _remainingSegments = new();

        private TypewriterAnimation? _animation;

        public DialoguePage(
            in ResolvedEntityPath path,
            int priority,
            Size? bounds,
            float lineHeight,
            in Vector4 margin,
            NsScriptThread dialogueThread)
            : base(path, priority)
        {
            Margin = margin;
            _bounds = bounds;
            _lineHeight = lineHeight;
            _dialogueThread = dialogueThread;
            _layout = new TextLayout(bounds?.Width, bounds?.Height, lineHeight);
        }

        public override EntityKind Kind => EntityKind.DialoguePage;

        public Vector4 Margin { get; }
        public override bool IsIdle => _dialogueThread.DoneExecuting && LineRead;
        public bool LineRead { get; private set; }

        public DialoguePage(in ResolvedEntityPath path, in DialoguePageSaveData saveData, GameLoadingContext loadCtx)
            : base(path, saveData.Common)
        {
            _bounds = saveData.Bounds;
            _lineHeight = saveData.LineHeight;
            _layout = new TextLayout(_bounds?.Width, _bounds?.Height, _lineHeight);
            _dialogueThread = loadCtx.Process.VmProcess.GetThread(saveData.DialogueThreadId);
            Margin = saveData.Margin;

            foreach (string pxmlLine in saveData.PXmlLines)
            {
                _pxmlLines.Add(pxmlLine);
                FontConfiguration fontConfig = loadCtx.Process.FontConfig;
                var buffer = TextBuffer.FromPXmlString(pxmlLine, fontConfig);
                foreach (TextBufferSegment seg in buffer.Segments)
                {
                    _remainingSegments.Enqueue(seg);
                }
            }

            while (_remainingSegments.Count != saveData.SegmentsRemaining)
            {
                ConsumeSegment(loadCtx.Rendering, loadCtx.Backlog);
            }

            loadCtx.Rendering.Text.RequestGlyphs(_layout);
        }

        public void Append(
            RenderContext renderCtx,
            string pxmlLine,
            FontConfiguration fontConfig,
            Backlog backlog)
        {
            _pxmlLines.Add(pxmlLine);
            var buffer = TextBuffer.FromPXmlString(pxmlLine, fontConfig);
            foreach (TextBufferSegment seg in buffer.Segments)
            {
                _remainingSegments.Enqueue(seg);
            }

            Advance(renderCtx, backlog);
            renderCtx.Text.RequestGlyphs(_layout);
            LineRead = false;
        }

        private void Advance(RenderContext renderCtx, Backlog backlog)
        {
            if (_animation is object)
            {
                if (!_animation.Skipping)
                {
                    _animation.Skip();
                }

                return;
            }

            int start = _layout.GlyphRuns.Length;
            while (ConsumeSegment(renderCtx, backlog) == ConsumeResult.KeepGoing)
            {
            }

        exit:
            if (_layout.GlyphRuns.Length != start)
            {
                _animation = new TypewriterAnimation(_layout, _layout.GlyphRuns[start..], 40);
                renderCtx.Icons.WaitLine.Reset();
            }
        }

        private ConsumeResult ConsumeSegment(RenderContext renderCtx, Backlog backlog)
        {
            if (_remainingSegments.TryDequeue(out TextBufferSegment? seg))
            {
                switch (seg.SegmentKind)
                {
                    case TextBufferSegmentKind.Text:
                        var textSegment = (TextSegment)seg;
                        _layout.Append(renderCtx.GlyphRasterizer, textSegment.TextRuns.AsSpan());
                        backlog.Append(textSegment);
                        return ConsumeResult.KeepGoing;
                    case TextBufferSegmentKind.Marker:
                        var marker = (MarkerSegment)seg;
                        switch (marker.MarkerKind)
                        {
                            case MarkerKind.Halt:
                                return ConsumeResult.Halt;
                        }
                        break;
                }

                return ConsumeResult.KeepGoing;
            }

            return ConsumeResult.AllDone;
        }

        protected override void AdvanceAnimations(RenderContext ctx, float dt, bool assetsReady)
        {
            AdvanceAnimation(ref _animation, dt);
            if (_animation is null)
            {
                ctx.Icons.WaitLine.Update(dt);
            }
            base.AdvanceAnimations(ctx, dt, assetsReady);
        }

        protected override void Update(GameContext ctx)
        {
            bool advance = ctx.InputContext.VKeyDown(VirtualKey.Advance);
            if (advance)
            {
                LineRead = _remainingSegments.Count == 0 && _animation is null;
                Advance(ctx.RenderContext, ctx.Backlog);
            }

            ctx.RenderContext.Text.RequestGlyphs(_layout);
        }

        protected override void Render(RenderContext ctx, DrawBatch batch)
        {
            RectangleF br = BoundingRect;
            var rect = new RectangleU((uint)br.X, (uint)br.Y, (uint)br.Width, (uint)br.Height);
            ctx.Text.Render(ctx, batch, _layout, WorldMatrix, Margin.XY(), rect, Color.A);

            if (_animation is null)
            {
                float x = ctx.SystemVariables.PositionXTextIcon.AsNumber()!.Value;
                float y = ctx.SystemVariables.PositionYTextIcon.AsNumber()!.Value;
                ctx.Icons.WaitLine.Render(ctx, new Vector2(x, y));
            }

            return;

            RectangleF bb = _layout.BoundingBox;
            ctx.MainBatch.PushQuad(
                QuadGeometry.Create(
                    new SizeF(bb.Size.Width, bb.Size.Height),
                    WorldMatrix * Matrix4x4.CreateTranslation(new Vector3(Margin.XY() + bb.Position, 0)),
                    Vector2.Zero,
                    Vector2.One,
                    new Vector4(0, 0.8f, 0.0f, 0.3f)
                ).Item1,
                ctx.WhiteTexture,
                ctx.WhiteTexture,
                default,
                BlendMode,
                FilterMode
            );

            var rasterizer = ctx.Text.GlyphRasterizer;
            foreach (var glyphRun in _layout.GlyphRuns)
            {
                var glyphs = _layout.GetGlyphs(glyphRun.GlyphSpan);
                var font = rasterizer.GetFontData(glyphRun.Font);
                foreach (PositionedGlyph g in glyphs)
                {
                    var dims = font.GetGlyphDimensions(g.Index, glyphRun.FontSize);

                    ctx.MainBatch.PushQuad(
                        QuadGeometry.Create(
                            new SizeF(dims.Width, dims.Height),
                            WorldMatrix * Matrix4x4.CreateTranslation(new Vector3(Margin.XY() + g.Position + new Vector2(0, 0), 0)),
                            Vector2.Zero,
                            Vector2.One,
                            new Vector4(0.8f, 0.0f, 0.0f, 0.3f)
                        ).Item1,
                        ctx.WhiteTexture,
                        ctx.WhiteTexture,
                        default,
                        BlendMode,
                        FilterMode
                    );
                }
            }
        }

        public override Size GetUnconstrainedBounds(RenderContext ctx)
        {
            RectangleF bb = _layout.BoundingBox;
            var size = new Size(
                (uint)(Margin.X + bb.Right + Margin.Z),
                (uint)(Margin.Y + bb.Bottom + Margin.W)
            );
            return size.Constrain(_layout.MaxBounds);
        }

        public void Clear()
        {
            _layout.Clear();
            _remainingSegments.Clear();
            _pxmlLines.Clear();
        }

        public new DialoguePageSaveData ToSaveData(GameSavingContext ctx) => new()
        {
            Common = base.ToSaveData(ctx),
            Bounds = _bounds,
            LineHeight = _lineHeight,
            Margin = Margin,
            DialogueThreadId = _dialogueThread.Id,
            PXmlLines = _pxmlLines.ToArray(),
            SegmentsRemaining = _remainingSegments.Count
        };
    }

    [Persistable]
    internal readonly partial struct DialoguePageSaveData : IEntitySaveData
    {
        public RenderItemSaveData Common { get; init; }
        public Size? Bounds { get; init; }
        public float LineHeight { get; init; }
        public Vector4 Margin { get; init; }
        public uint DialogueThreadId { get; init; }
        public string[] PXmlLines { get; init; }
        public int SegmentsRemaining { get; init; }

        public EntitySaveData CommonEntityData => Common.EntityData;
    }
}