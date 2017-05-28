﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CommitteeOfZero.Nitro.Foundation
{
    public sealed class Entity
    {
        private readonly EntityManager _manager;
        private readonly Dictionary<Type, IList<Component>> _components;
        private Dictionary<string, object> _additionalProperties;

        internal Entity(EntityManager manager, ulong id, string name, TimeSpan creationTime)
        {
            _manager = manager;
            Id = id;
            Name = name;
            CreationTime = creationTime;

            _components = new Dictionary<Type, IList<Component>>();

            var transform = new Transform();
            AddComponent(transform);
            Transform = transform;
        }

        public ulong Id { get; }
        public string Name { get; }
        public TimeSpan CreationTime { get; }
        public Transform Transform { get; }

        public Dictionary<string, object> AdditionalProperties
        {
            get
            {
                if (_additionalProperties == null)
                {
                    _additionalProperties = new Dictionary<string, object>();
                }

                return _additionalProperties;
            }
        }

        internal Dictionary<Type, IList<Component>> Components => _components;

        public Entity WithComponent<T>(T component) where T : Component
        {
            AddComponent(component);
            return this;
        }

        public Entity WithParent(Entity parentEntity)
        {
            Transform.Parent = parentEntity?.Transform;
            return this;
        }

        public void AddComponent<T>(T component) where T : Component
        {
            var type = typeof(T);
            if (!_components.TryGetValue(type, out var collection))
            {
                collection = new List<Component>();
                _components.Add(type, collection);
            }

            collection.Add(component);
            component.AttachToEntity(this);
            _manager.RaiseEntityUpdated(this);
        }

        public bool HasComponent<T>() where T : Component => GetComponent<T>() != null;
        public bool HasComponent(Type type) => GetComponent(type) != null;

        public T GetComponent<T>() where T : Component => (T)GetComponent(typeof(T));
        public Component GetComponent(Type type)
        {
            if (_components.TryGetValue(type, out var collection))
            {
                return collection?.Count > 0 ? collection[0] : null;
            }
            else
            {
                foreach (var pair in _components)
                {
                    if (type.GetTypeInfo().IsAssignableFrom(pair.Key.GetTypeInfo()))
                    {
                        collection = pair.Value;
                        if (collection?.Count > 0)
                        {
                            return collection[0];
                        }
                    }
                }
            }

            return null;
        }

        public IEnumerable<T> GetComponents<T>() where T : Component
        {
            var type = typeof(T);
            if (_components.TryGetValue(type, out var collection))
            {
                foreach (var component in collection)
                {
                    yield return (T)component;
                }
            }
            else
            {
                foreach (var pair in _components.ToArray())
                {
                    if (type.GetTypeInfo().IsAssignableFrom(pair.Key.GetTypeInfo()))
                    {
                        collection = pair.Value;
                        foreach (var component in collection.ToArray())
                        {
                            yield return (T)component;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Schedules the specified component to be removed on the next update.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="component"></param>
        public void RemoveComponent<T>(T component) where T : Component
        {
            _manager.ScheduleComponentRemoval(this, component);
        }

        public void Destroy()
        {
            _manager.Remove(this);
        }

        internal void CommitDestroyComponent(Component component)
        {
            var type = component.GetType();
            if (_components.TryGetValue(type, out var collection))
            {
                collection.Remove(component);
            }
            else
            {
                foreach (var pair in _components)
                {
                    if (type.GetTypeInfo().IsAssignableFrom(pair.Key.GetTypeInfo()))
                    {
                        collection = pair.Value;
                        if (collection?.Remove(component) == true)
                        {
                            break;
                        }
                    }
                }
            }

            _manager.RaiseEntityUpdated(this);
        }

        public override string ToString() => Name;
    }
}