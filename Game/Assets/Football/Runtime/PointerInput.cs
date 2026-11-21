using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;
#endif

namespace Football.Game
{
    /// <summary>Fase de un puntero (dedo o ratón) en el fotograma actual.</summary>
    public enum PointerPhase
    {
        /// <summary>Acaba de tocar la pantalla.</summary>
        Began,
        /// <summary>Sigue apoyado.</summary>
        Held,
        /// <summary>Acaba de soltarse.</summary>
        Ended
    }

    /// <summary>Un puntero activo en este fotograma.</summary>
    public struct PointerState
    {
        /// <summary>Identificador estable mientras dure el toque.</summary>
        public int Id;
        /// <summary>Posición en píxeles de pantalla (origen abajo a la izquierda).</summary>
        public Vector2 Position;
        /// <summary>Fase.</summary>
        public PointerPhase Phase;
    }

    /// <summary>
    /// Estado del teclado en el ordenador. Las teclas se eligen en Ajustes → Controles de teclado (ver <see cref="KeyBindings"/>);
    /// por defecto: WASD, Shift esprint, J pase, K tiro, L filtrado, I centro, R despejar, Q barrer, E presionar (mantener),
    /// G arquero, Tab cambiar, Esc pausa.
    /// </summary>
    public struct KeyboardState
    {
        /// <summary>Dirección de movimiento.</summary>
        public Vector2 Move;
        /// <summary>Esprint mantenido.</summary>
        public bool Sprint;
        /// <summary>Pase pulsado en este fotograma.</summary>
        public bool PassDown;
        /// <summary>Pase mantenido (para cargar la potencia).</summary>
        public bool PassHeld;
        /// <summary>Pase filtrado pulsado en este fotograma.</summary>
        public bool ThroughDown;
        /// <summary>Centro pulsado en este fotograma.</summary>
        public bool CrossDown;
        /// <summary>Tiro mantenido.</summary>
        public bool ShootHeld;
        /// <summary>Barrida pulsada en este fotograma.</summary>
        public bool SlideDown;
        /// <summary>Presión mantenida.</summary>
        public bool PressHeld;
        /// <summary>Despeje pulsado en este fotograma.</summary>
        public bool ClearDown;
        /// <summary>Control del arquero pulsado en este fotograma.</summary>
        public bool KeeperDown;
        /// <summary>Cambio de jugador pulsado en este fotograma.</summary>
        public bool SwitchDown;
        /// <summary>Pausa pulsada en este fotograma.</summary>
        public bool PauseDown;
    }

    /// <summary>
    /// Lee toques, ratón y teclado con independencia de qué sistema de entrada tenga activo el proyecto
    /// (el nuevo "Input System" o el antiguo). Reutiliza buffers: no genera basura.
    /// </summary>
    public sealed class PointerInput
    {
        /// <summary>Máximo de punteros simultáneos.</summary>
        public const int MaxPointers = 10;

        private const int MouseId = 1000;

        /// <summary>Punteros de este fotograma.</summary>
        public readonly PointerState[] Pointers = new PointerState[MaxPointers];

        /// <summary>Cuántos punteros hay en <see cref="Pointers"/>.</summary>
        public int Count { get; private set; }

        /// <summary>Estado del teclado.</summary>
        public KeyboardState Keyboard;

        private bool _enabled;

        /// <summary>Lee la entrada del fotograma actual.</summary>
        public void Update()
        {
            if (!_enabled)
            {
#if ENABLE_INPUT_SYSTEM
                EnhancedTouch.EnhancedTouchSupport.Enable();
#endif
                _enabled = true;
            }

            Count = 0;
            Keyboard = default;
            ReadTouches();
            ReadMouse();
            ReadKeyboard();
        }

        private void Add(int id, Vector2 position, PointerPhase phase)
        {
            if (Count >= MaxPointers)
            {
                return;
            }

            Pointers[Count].Id = id;
            Pointers[Count].Position = position;
            Pointers[Count].Phase = phase;
            Count++;
        }

#if ENABLE_INPUT_SYSTEM
        private void ReadTouches()
        {
            var touches = EnhancedTouch.Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                EnhancedTouch.Touch t = touches[i];
                PointerPhase phase;
                switch (t.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        phase = PointerPhase.Began;
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        phase = PointerPhase.Ended;
                        break;
                    default:
                        phase = PointerPhase.Held;
                        break;
                }

                Add(t.touchId, t.screenPosition, phase);
            }
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Add(MouseId, position, PointerPhase.Began);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                Add(MouseId, position, PointerPhase.Ended);
            }
            else if (mouse.leftButton.isPressed)
            {
                Add(MouseId, position, PointerPhase.Held);
            }
        }

        private UnityEngine.InputSystem.Key[] _keys;
        private UnityEngine.InputSystem.Key[] _assignableKeys;
        private int _keysVersion = -1;

        private static UnityEngine.InputSystem.Key ToKey(KeyCode code) =>
            System.Enum.TryParse(KeyBindings.InputSystemName(code), out UnityEngine.InputSystem.Key key) ? key : UnityEngine.InputSystem.Key.None;

        private void RefreshKeys()
        {
            if (_keys != null && _keysVersion == KeyBindings.Version)
            {
                return;
            }

            _keysVersion = KeyBindings.Version;
            _keys = new UnityEngine.InputSystem.Key[KeyBindings.Count];
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            for (int i = 0; i < _keys.Length; i++)
            {
                UnityEngine.InputSystem.Key k = ToKey(KeyBindings.Get((GameAction)i));
                try
                {
                    _ = kb[k];
                }
                catch (System.Exception)
                {
                    k = UnityEngine.InputSystem.Key.None;
                }

                _keys[i] = k;
            }
        }

        private bool Held(UnityEngine.InputSystem.Keyboard kb, GameAction a)
        {
            UnityEngine.InputSystem.Key k = _keys[(int)a];
            return k != UnityEngine.InputSystem.Key.None && kb[k].isPressed;
        }

        private bool Pressed(UnityEngine.InputSystem.Keyboard kb, GameAction a)
        {
            UnityEngine.InputSystem.Key k = _keys[(int)a];
            return k != UnityEngine.InputSystem.Key.None && kb[k].wasPressedThisFrame;
        }

        /// <summary>Tecla pulsada en este fotograma (para asignarla a una acción), si hay alguna.</summary>
        public bool TryGetPressedKey(out KeyCode key)
        {
            key = KeyCode.None;
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            var assignable = KeyBindings.Assignable;
            if (_assignableKeys == null)
            {
                _assignableKeys = new UnityEngine.InputSystem.Key[assignable.Count];
                for (int i = 0; i < _assignableKeys.Length; i++)
                {
                    UnityEngine.InputSystem.Key k = ToKey(assignable[i]);
                    try
                    {
                        _ = kb[k]; // algunas teclas no existen en todos los teclados
                    }
                    catch (System.Exception)
                    {
                        k = UnityEngine.InputSystem.Key.None;
                    }

                    _assignableKeys[i] = k;
                }
            }

            for (int i = 0; i < _assignableKeys.Length; i++)
            {
                if (_assignableKeys[i] != UnityEngine.InputSystem.Key.None && kb[_assignableKeys[i]].wasPressedThisFrame)
                {
                    key = assignable[i];
                    return true;
                }
            }

            return false;
        }

        private void ReadKeyboard()
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
            {
                return;
            }

            RefreshKeys();
            float x = (Held(kb, GameAction.Right) ? 1f : 0f) - (Held(kb, GameAction.Left) ? 1f : 0f);
            float y = (Held(kb, GameAction.Up) ? 1f : 0f) - (Held(kb, GameAction.Down) ? 1f : 0f);
            Keyboard.Move = new Vector2(x, y);
            Keyboard.Sprint = Held(kb, GameAction.Sprint);
            Keyboard.PassDown = Pressed(kb, GameAction.Pass);
            Keyboard.PassHeld = Held(kb, GameAction.Pass);
            Keyboard.ThroughDown = Pressed(kb, GameAction.Through);
            Keyboard.CrossDown = Pressed(kb, GameAction.Cross);
            Keyboard.ShootHeld = Held(kb, GameAction.Shoot);
            Keyboard.SlideDown = Pressed(kb, GameAction.Slide);
            Keyboard.PressHeld = Held(kb, GameAction.Press);
            Keyboard.ClearDown = Pressed(kb, GameAction.Clear);
            Keyboard.KeeperDown = Pressed(kb, GameAction.Keeper);
            Keyboard.SwitchDown = Pressed(kb, GameAction.Switch);
            Keyboard.PauseDown = Pressed(kb, GameAction.Pause) || kb.escapeKey.wasPressedThisFrame;
        }
#else
        private void ReadTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                PointerPhase phase;
                switch (t.phase)
                {
                    case UnityEngine.TouchPhase.Began:
                        phase = PointerPhase.Began;
                        break;
                    case UnityEngine.TouchPhase.Ended:
                    case UnityEngine.TouchPhase.Canceled:
                        phase = PointerPhase.Ended;
                        break;
                    default:
                        phase = PointerPhase.Held;
                        break;
                }

                Add(t.fingerId, t.position, phase);
            }
        }

        private void ReadMouse()
        {
            // En el móvil Unity simula un ratón por cada toque: se ignora para no duplicar punteros.
            if (Input.touchCount > 0)
            {
                return;
            }

            Vector2 position = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                Add(MouseId, position, PointerPhase.Began);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                Add(MouseId, position, PointerPhase.Ended);
            }
            else if (Input.GetMouseButton(0))
            {
                Add(MouseId, position, PointerPhase.Held);
            }
        }

        private static bool Held(GameAction a) => Input.GetKey(KeyBindings.Get(a));

        private static bool Pressed(GameAction a) => Input.GetKeyDown(KeyBindings.Get(a));

        /// <summary>Tecla pulsada en este fotograma (para asignarla a una acción), si hay alguna.</summary>
        public bool TryGetPressedKey(out KeyCode key)
        {
            var assignable = KeyBindings.Assignable;
            for (int i = 0; i < assignable.Count; i++)
            {
                if (Input.GetKeyDown(assignable[i]))
                {
                    key = assignable[i];
                    return true;
                }
            }

            key = KeyCode.None;
            return false;
        }

        private void ReadKeyboard()
        {
            float x = (Held(GameAction.Right) ? 1f : 0f) - (Held(GameAction.Left) ? 1f : 0f);
            float y = (Held(GameAction.Up) ? 1f : 0f) - (Held(GameAction.Down) ? 1f : 0f);
            Keyboard.Move = new Vector2(x, y);
            Keyboard.Sprint = Held(GameAction.Sprint);
            Keyboard.PassDown = Pressed(GameAction.Pass);
            Keyboard.PassHeld = Held(GameAction.Pass);
            Keyboard.ThroughDown = Pressed(GameAction.Through);
            Keyboard.CrossDown = Pressed(GameAction.Cross);
            Keyboard.ShootHeld = Held(GameAction.Shoot);
            Keyboard.SlideDown = Pressed(GameAction.Slide);
            Keyboard.PressHeld = Held(GameAction.Press);
            Keyboard.ClearDown = Pressed(GameAction.Clear);
            Keyboard.KeeperDown = Pressed(GameAction.Keeper);
            Keyboard.SwitchDown = Pressed(GameAction.Switch);
            Keyboard.PauseDown = Pressed(GameAction.Pause) || Input.GetKeyDown(KeyCode.Escape);
        }
#endif
    }
}
