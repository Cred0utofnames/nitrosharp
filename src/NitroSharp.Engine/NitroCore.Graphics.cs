﻿using NitroSharp.Graphics;
using NitroSharp.NsScript;
using NitroSharp.Foundation;
using System;
using System.Drawing;
using System.Numerics;
using NitroSharp.Foundation.Graphics;

namespace NitroSharp
{
    public sealed partial class NitroCore
    {
        private System.Drawing.Size _viewport;

        public override void AddRectangle(string entityName, int priority,
            NsCoordinate x, NsCoordinate y, int width, int height, NsColor color)
        {
            var rgba = new RgbaValueF(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
            var rect = new Graphics.RectangleVisual(width, height, rgba, 1.0f, priority);

            var entity = _entities.Create(entityName, replace: true)
                .WithComponent(rect)
                .WithPosition(x, y);
        }

        public override void LoadImage(string entityName, string fileName)
        {
            var sprite = new Sprite(_content.Get<Texture2D>(fileName), null, 1.0f, 0);
            _entities.Create(entityName, replace: true).WithComponent(sprite);
        }

        public override void AddTexture(string entityName, int priority,
            NsCoordinate x, NsCoordinate y, string fileOrExistingEntityName)
        {
            if (fileOrExistingEntityName.Equals("SCREEN", StringComparison.OrdinalIgnoreCase))
            {
                AddScreencap(entityName, x, y, priority);
            }
            else
            {
                AddTextureCore(entityName, fileOrExistingEntityName, x, y, priority);
            }
        }

        private void AddScreencap(string entityName, NsCoordinate x, NsCoordinate y, int priority)
        {
            var screencap = new Screenshot
            {
                Priority = priority,
            };

            _entities.Create(entityName, replace: true)
                .WithComponent(screencap)
                .WithPosition(x, y);
        }

        public override void AddClippedTexture(string entityName, int priority, NsCoordinate dstX, NsCoordinate dstY,
            NsCoordinate srcX, NsCoordinate srcY, int width, int height, string srcEntityName)
        {
            var srcRectangle = new RectangleF(srcX.Value, srcY.Value, width, height);
            AddTextureCore(entityName, srcEntityName, dstX, dstY, priority, srcRectangle);
        }

        private void AddTextureCore(string entityName, string fileOrExistingEntityName,
            NsCoordinate x, NsCoordinate y, int priority, RectangleF? srcRect = null)
        {
            Entity parentEntity = null;
            int idxSlash = entityName.IndexOf('/');
            if (idxSlash > 0)
            {
                string parentEntityName = entityName.Substring(0, idxSlash);
                _entities.TryGet(parentEntityName, out parentEntity);
            }

            string source = fileOrExistingEntityName;
            if (_entities.TryGet(fileOrExistingEntityName, out var existingEnitity))
            {
                var existingSprite = existingEnitity.GetComponent<Sprite>();
                if (existingSprite != null)
                {
                    source = existingSprite.Source.Id;
                }
            }

            var texture = new Sprite(_content.Get<Texture2D>(source), srcRect, 1.0f, priority);
            _entities.Create(entityName, replace: true)
                .WithComponent(texture)
                .WithParent(parentEntity)
                .WithPosition(x, y);
        }

        public override int GetTextureWidth(string entityName)
        {
            return _entities.TryGet(entityName, out var entity) ? (int)entity.Transform.Bounds.Width : 0;
        }

        internal static void SetPosition(Transform transform, NsCoordinate x, NsCoordinate y)
        {
            transform.SetMarginX(x.Origin == NsCoordinateOrigin.CurrentValue ? transform.Margin.X + x.Value : x.Value);
            transform.SetMarginY(y.Origin == NsCoordinateOrigin.CurrentValue ? transform.Margin.Y + y.Value : y.Value);
            transform.AnchorPoint = new Vector2(x.AnchorPoint, y.AnchorPoint);

            switch (x.Origin)
            {
                case NsCoordinateOrigin.Left:
                default:
                    transform.SetTranslateOriginX(0.0f);
                    break;

                case NsCoordinateOrigin.Center:
                    transform.SetTranslateOriginX(0.5f);
                    break;

                case NsCoordinateOrigin.Right:
                    transform.SetTranslateOriginX(1.0f);
                    break;
            }

            switch (y.Origin)
            {
                case NsCoordinateOrigin.Top:
                default:
                    transform.SetTranslateOriginY(0.0f);
                    break;

                case NsCoordinateOrigin.Center:
                    transform.SetTranslateOriginY(0.5f);
                    break;

                case NsCoordinateOrigin.Bottom:
                    transform.SetTranslateOriginY(1.0f);
                    break;
            }
        }
    }
}