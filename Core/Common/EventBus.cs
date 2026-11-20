using System;
using System.Collections.Generic;

namespace Football.Core.Common
{
    /// <summary>
    /// Bus de eventos tipado. Los eventos son <c>struct</c> y publicar no genera allocations
    /// (los arrays de manejadores son inmutables y se reemplazan al suscribir o desuscribir).
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        /// <summary>Registra un manejador para eventos de tipo <typeparamref name="T"/>.</summary>
        /// <param name="handler">Manejador a invocar al publicar.</param>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Action<T>[] current = Current<T>();
            var next = new Action<T>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = handler;
            _handlers[typeof(T)] = next;
        }

        /// <summary>Elimina un manejador previamente registrado. Si no estaba registrado, no hace nada.</summary>
        /// <param name="handler">Manejador a quitar.</param>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Action<T>[] current = Current<T>();
            int index = Array.IndexOf(current, handler);
            if (index < 0)
            {
                return;
            }

            if (current.Length == 1)
            {
                _handlers.Remove(typeof(T));
                return;
            }

            var next = new Action<T>[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            _handlers[typeof(T)] = next;
        }

        /// <summary>Entrega el evento a todos los manejadores, en orden de suscripción.</summary>
        /// <param name="evt">Evento a publicar.</param>
        public void Publish<T>(in T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out object stored))
            {
                return;
            }

            // El array es inmutable: suscribir o desuscribir durante la publicación es seguro.
            var snapshot = (Action<T>[])stored;
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i](evt);
            }
        }

        private Action<T>[] Current<T>() where T : struct
        {
            return _handlers.TryGetValue(typeof(T), out object stored)
                ? (Action<T>[])stored
                : Array.Empty<Action<T>>();
        }
    }
}
