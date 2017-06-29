﻿using CommitteeOfZero.NitroSharp.Foundation;
using CommitteeOfZero.NitroSharp.Foundation.Content;
using CommitteeOfZero.NitroSharp.Foundation.Graphics;
using System.Drawing;

namespace CommitteeOfZero.NitroSharp.Graphics
{
    public class Sprite : Visual
    {
        public Sprite(AssetRef<TextureAsset> source, RectangleF? sourceRectangle, float opacity, int priority)
            : base(RgbaValueF.White, opacity, priority)
        {
            Source = source;
            SourceRectangle = sourceRectangle;
        }

        public AssetRef<TextureAsset> Source { get; set; }
        public RectangleF? SourceRectangle { get; set; }

        public override void Render(INitroRenderer renderer)
        {
            renderer.DrawSprite(this);
        }

        public override SizeF Measure()
        {
            var deviceTexture = Source.Asset;
            if (SourceRectangle != null)
            {
                return new SizeF(SourceRectangle.Value.Width, SourceRectangle.Value.Height);
            }

            return new SizeF(deviceTexture.Width, deviceTexture.Height);

        }

        public override void OnRemoved()
        {
            Source.Dispose();
        }
    }
}