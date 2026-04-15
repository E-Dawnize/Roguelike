using System;
using System.Collections.Generic;
using Core.Events.EventInterfaces;
using Core.Tools;
using UnityEngine;

namespace Core.Events
{
    public class EventManager:IEventCenter,IDisposable
    {
        //type为事件结构体
        private readonly Dictionary<Type, Delegate> _eventHandlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            if (_eventHandlers.TryGetValue(type, out var existing))
            {
                _eventHandlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _eventHandlers[type] = handler;
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            if (!_eventHandlers.ContainsKey(type))return;
            var newHandler = Delegate.Remove(_eventHandlers[type], handler);
            if (newHandler == null)
            {
                _eventHandlers.Remove(type);
            }
            else
            {
                _eventHandlers[type] = newHandler;
            }
        }

        public void Publish<T>(T message) where T : struct
        {
            Type type = typeof(T);
            if (_eventHandlers.TryGetValue(type, out var handler))
            {
                (handler as Action<T>)?.Invoke(message);
            }
        }

        public void Dispose()
        {
            _eventHandlers.Clear();
        }
    }
}