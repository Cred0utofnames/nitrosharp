﻿using System;
using System.Collections.Generic;
using System.Numerics;
using NitroSharp.Animation;
using NitroSharp.Content;
using NitroSharp.Graphics;
using NitroSharp.Media;
using NitroSharp.Primitives;
using NitroSharp.Text;
using NitroSharp.Utilities;
using Veldrid;

namespace NitroSharp
{
    internal enum WorldKind
    {
        Primary,
        Secondary
    }

    internal sealed class World
    {
        public const ushort InitialCapacity = 1024;
        public const ushort InitialSpriteCount = 512;
        public const ushort InitialRectangleCount = 32;
        public const ushort InitialTextLayoutCount = 32;
        public const ushort InitialAudioClipCount = 64;
        public const ushort InitialVideoClipCount = 4;

        private readonly Dictionary<string, Entity> _entities;
        private readonly List<(string entity, string alias)> _aliases;
        private readonly List<EntityTable> _tables;
        private ArrayBuilder<EntityEvent> _entityEvents;
        private ushort _nextEntityId = 1;

        private readonly Dictionary<AnimationDictionaryKey, PropertyAnimation> _attachedAnimations;
        private readonly List<(AnimationDictionaryKey key, PropertyAnimation anim)> _animationsToDetach;
        private readonly List<AnimationEvent> _animationEvents;

        public World(WorldKind kind)
        {
            Kind = kind;
            _entities = new Dictionary<string, Entity>(InitialCapacity);
            _aliases = new List<(string entity, string alias)>();
            _entityEvents = new ArrayBuilder<EntityEvent>();
            _tables = new List<EntityTable>(8);

            Threads = RegisterTable(new ThreadTable(this, 32));
            Sprites = RegisterTable(new SpriteTable(this, InitialSpriteCount));
            Rectangles = RegisterTable(new RectangleTable(this, InitialRectangleCount));
            TextInstances = RegisterTable(new TextInstanceTable(this, InitialTextLayoutCount));
            AudioClips = RegisterTable(new AudioClipTable(this, InitialAudioClipCount));
            VideoClips = RegisterTable(new VideoClipTable(this, InitialVideoClipCount));

            _attachedAnimations = new Dictionary<AnimationDictionaryKey, PropertyAnimation>();
            _animationsToDetach = new List<(AnimationDictionaryKey key, PropertyAnimation anim)>();
            _animationEvents = new List<AnimationEvent>();
        }

        public WorldKind Kind { get; }
        public bool IsPrimary => Kind == WorldKind.Primary;

        public ThreadTable Threads { get; }
        public SpriteTable Sprites { get; }
        public RectangleTable Rectangles { get; }
        public TextInstanceTable TextInstances { get; }
        public AudioClipTable AudioClips { get; }
        public VideoClipTable VideoClips { get; }

        public Dictionary<string, Entity>.Enumerator EntityEnumerator => _entities.GetEnumerator();
        public Dictionary<AnimationDictionaryKey, PropertyAnimation>.ValueCollection AttachedAnimations
            => _attachedAnimations.Values;

        private T RegisterTable<T>(T table) where T : EntityTable
        {
            _tables.Add(table);
            return table;
        }

        public T GetTable<T>(Entity entity) where T : EntityTable
            => (T)_tables[(int)entity.Kind];

        public bool TryGetEntity(string name, out Entity entity)
            => _entities.TryGetValue(name, out entity);

        public bool IsEntityAlive(Entity entity)
        {
            var table = GetTable<EntityTable>(entity);
            return table.EntityExists(entity);
        }

        public void SetAlias(string originalName, string alias)
        {
            if (TryGetEntity(originalName, out Entity entity))
            {
                _entities[alias] = entity;
                _aliases.Add((originalName, alias));
                ref EntityEvent evt = ref _entityEvents.Add();
                evt.EventKind = EntityEventKind.AliasAdded;
                evt.Entity = entity;
                evt.EntityName = originalName;
                evt.Alias = alias;
            }
        }

        public Entity CreateThreadEntity(string name)
        {
            return CreateEntity(name, EntityKind.Thread);
        }

        public Entity CreateSprite(
            string name, string image, in RectangleF sourceRectangle,
            int renderPriority, SizeF size, ref RgbaFloat color)
        {
            Entity entity = CreateVisual(name, EntityKind.Sprite, renderPriority, size, ref color);
            Sprites.ImageSources.Set(entity, new ImageSource(image, sourceRectangle));
            return entity;
        }

        public Entity CreateRectangle(string name, int renderPriority, SizeF size, ref RgbaFloat color)
        {
            Entity entity = CreateVisual(name, EntityKind.Rectangle, renderPriority, size, ref color);
            return entity;
        }

        public Entity CreateTextInstance(string name, TextLayout layout, int renderPriority, ref RgbaFloat color)
        {
            var bounds = new SizeF(layout.MaxBounds.Width, layout.MaxBounds.Height);
            Entity entity = CreateVisual(name, EntityKind.Text, renderPriority, bounds, ref color);
            TextInstances.Layouts.Set(entity, ref layout);
            TextInstances.ClearFlags.Set(entity, true);
            return entity;
        }

        public Entity CreateAudioClip(string name, AssetId asset, bool enableLooping)
        {
            Entity entity = CreateEntity(name, EntityKind.AudioClip);
            AudioClips.Asset.Set(entity, asset);
            AudioClips.LoopData.Set(entity, new MediaClipLoopData(enableLooping, null));
            AudioClips.Volume.Set(entity, 1.0f);
            return entity;
        }

        public Entity CreateVideoClip(string name, AssetId asset, bool enableLooping, int renderPriority, ref RgbaFloat color)
        {
            Entity entity = CreateVisual(name, EntityKind.VideoClip, renderPriority, default, ref color);
            VideoClips.Asset.Set(entity, asset);
            VideoClips.LoopData.Set(entity, new MediaClipLoopData(enableLooping, null));
            VideoClips.Volume.Set(entity, 1.0f);
            return entity;
        }

        private Entity CreateVisual(
            string name, EntityKind kind,
            int renderPriority, SizeF size, ref RgbaFloat color)
        {
            Entity entity = CreateEntity(name, kind);
            VisualTable table = GetTable<VisualTable>(entity);

            if (renderPriority > 0)
            {
                renderPriority += entity.Id;
            }

            table.RenderPriorities.Set(entity, renderPriority);
            table.Bounds.Set(entity, size);
            table.Colors.Set(entity, ref color);
            table.TransformComponents.Mutate(entity).Scale = Vector3.One;
            return entity;
        }

        public void ActivateAnimation<T>(T animation) where T : PropertyAnimation
        {
            var key = new AnimationDictionaryKey(animation.Entity, typeof(T));
            _attachedAnimations[key] = animation;
            _animationEvents.Add(new AnimationEvent(key, AnimationEventKind.AnimationActivated));
        }

        public void DeactivateAnimation(PropertyAnimation animation)
        {
            var key = new AnimationDictionaryKey(animation.Entity, animation.GetType());
            _animationsToDetach.Add((key, animation));
            _animationEvents.Add(new AnimationEvent(key, AnimationEventKind.AnimationDeactivated));
        }

        public bool TryGetAnimation<T>(Entity entity, out T animation) where T : PropertyAnimation
        {
            var key = new AnimationDictionaryKey(entity, typeof(T));
            bool result = _attachedAnimations.TryGetValue(key, out PropertyAnimation val);
            animation = val as T;
            return result;
        }

        public void FlushDetachedAnimations()
        {
            foreach ((var dictKey, var anim) in _animationsToDetach)
            {
                if (_attachedAnimations.TryGetValue(dictKey, out var value) && value == anim)
                {
                    _attachedAnimations.Remove(dictKey);
                }
            }
            _animationsToDetach.Clear();
        }

        public void FlushEvents()
        {
            foreach (EntityTable table in _tables)
            {
                table.BeginFrame();
            }
        }

        private Entity CreateEntity(string name, EntityKind kind)
        {
            if (_entities.TryGetValue(name, out Entity existing))
            {
                RemoveExisting(name, existing);
            }

            EntityTable table = _tables[(int)kind];
            var handle = new Entity(_nextEntityId++, kind);
            table.Insert(handle);
            _entities[name] = handle;
            ref EntityEvent evt = ref _entityEvents.Add();
            evt.Entity = handle;
            evt.EntityName = name;
            evt.EventKind = EntityEventKind.EntityAdded;
            return handle;
        }

        private void RemoveExisting(string name, Entity entity)
        {
            var table = GetTable<EntityTable>(entity);
            table.Remove(entity);
            ref EntityEvent e = ref _entityEvents.Add();
            e.EntityName = name;
            e.Entity = entity;
            e.EventKind = EntityEventKind.EntityRemoved;
        }

        public void RemoveEntity(string name)
        {
            Entity entity = RemoveEntityCore(name);
            if (entity.IsValid)
            {
                ref EntityEvent evt = ref _entityEvents.Add();
                evt.EntityName = name;
                evt.Entity = entity;
                evt.EventKind = EntityEventKind.EntityRemoved;
            }
        }

        private Entity RemoveEntityCore(string name)
        {
            if (_entities.TryGetValue(name, out Entity entity))
            {
                _entities.Remove(name);
                for (int i = 0; i < _aliases.Count; i++)
                {
                    (string originalName, string alias) = _aliases[i];
                    if (originalName == name || alias == name)
                    {
                        _entities.Remove(originalName);
                        _entities.Remove(alias);
                        _aliases.RemoveAt(i);
                        break;
                    }
                }

                var table = GetTable<EntityTable>(entity);
                table.Remove(entity);
                return entity;
            }

            return Entity.Invalid;
        }

        public void MergeChanges(World target)
        {
            if (_entityEvents.Count > 0 && target._entityEvents.Count > 0)
            {
                ThrowCannotMerge();
            }

            for (int i = 0; i < _entityEvents.Count; i++)
            {
                ref EntityEvent evt = ref _entityEvents[i];
                var table = target.GetTable<EntityTable>(evt.Entity);
                switch (evt.EventKind)
                {
                    case EntityEventKind.EntityAdded:
                        table.Insert(evt.Entity);
                        target._entities[evt.EntityName] = evt.Entity;
                        target._nextEntityId++;
                        break;
                    case EntityEventKind.EntityRemoved:
                        target.RemoveEntityCore(evt.EntityName);
                        break;
                    case EntityEventKind.AliasAdded:
                        _entities[evt.Alias] = evt.Entity;
                        _aliases.Add((evt.EntityName, evt.Alias));
                        break;
                }
            }

            for (int i = 0; i < _tables.Count; i++)
            {
                _tables[i].MergeChanges(target._tables[i]);
                EntityTable.Debug_CompareTables(_tables[i], target._tables[i]);
            }

            foreach (AnimationEvent ae in _animationEvents)
            {
                if (ae.EventKind == AnimationEventKind.AnimationActivated)
                {
                    if (_attachedAnimations.TryGetValue(ae.Key, out PropertyAnimation animation))
                    {
                        target._attachedAnimations[ae.Key] = animation;
                    }
                }
                else
                {
                    target._attachedAnimations.Remove(ae.Key);
                }
            }

            _animationEvents.Clear();
            _entityEvents.Reset();
        }

        private void ThrowCannotMerge()
            => throw new InvalidOperationException("Copies of the game state that have conflicting change sets cannot be merged.");

        private struct EntityEvent
        {
            public string EntityName;
            public string Alias;
            public Entity Entity;
            public EntityEventKind EventKind;
        }

        private enum EntityEventKind
        {
            EntityAdded,
            EntityRemoved,
            AliasAdded
        }

        private readonly struct AnimationEvent
        {
            public AnimationEvent(AnimationDictionaryKey key, AnimationEventKind kind)
            {
                Key = key;
                EventKind = kind;
            }

            public readonly AnimationDictionaryKey Key;
            public readonly AnimationEventKind EventKind;
        }

        private enum AnimationEventKind
        {
            AnimationActivated,
            AnimationDeactivated
        }

        internal readonly struct AnimationDictionaryKey : IEquatable<AnimationDictionaryKey>
        {
            public readonly Entity Entity;
            public readonly Type RuntimeType;

            public AnimationDictionaryKey(Entity entity, Type runtimeType)
            {
                Entity = entity;
                RuntimeType = runtimeType;
            }

            public bool Equals(AnimationDictionaryKey other)
                => Entity.Equals(other.Entity) && RuntimeType.Equals(other.RuntimeType);

            public override bool Equals(object obj)
                => obj is AnimationDictionaryKey other && Equals(other);

            public override int GetHashCode()
                => HashHelper.Combine(Entity.GetHashCode(), RuntimeType.GetHashCode());
        }
    }
}
