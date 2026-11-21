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
    /// Estado del teclado para probar en el ordenador: WASD o flechas, Shift esprint, J pase, K tiro, L filtrado, I centro,
    /// R despejar, Q barrer, E presionar (mantener), G arquero, Tab cambiar, Esc pausa.
    /// </summary>
    public struct KeyboardState
    {
        /// <summary>Dirección de movimiento.</summary>
        public Vector2 Move;
        /// <summary>Esprint mantenido.</summary>
        public bool Sprint;
        /// <summary>Pase pulsado en este fotograma.</summary>
        public bool PassDown;
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

        private void ReadKeyboard()
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
            {
                return;
            }

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            Keyboard.Move = new Vector2(x, y);
            Keyboard.Sprint = kb.leftShiftKey.isPressed;
            Keyboard.PassDown = kb.jKey.wasPressedThisFrame;
            Keyboard.ThroughDown = kb.lKey.wasPressedThisFrame;
            Keyboard.CrossDown = kb.iKey.wasPressedThisFrame;
            Keyboard.ShootHeld = kb.kKey.isPressed;
            Keyboard.SlideDown = kb.qKey.wasPressedThisFrame;
            Keyboard.PressHeld = kb.eKey.isPressed;
            Keyboard.ClearDown = kb.rKey.wasPressedThisFrame;
            Keyboard.KeeperDown = kb.gKey.wasPressedThisFrame;
            Keyboard.SwitchDown = kb.tabKey.wasPressedThisFrame;
            Keyboard.PauseDown = kb.escapeKey.wasPressedThisFrame;
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

        private void ReadKeyboard()
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                      - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                      - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            Keyboard.Move = new Vector2(x, y);
            Keyboard.Sprint = Input.GetKey(KeyCode.LeftShift);
            Keyboard.PassDown = Input.GetKeyDown(KeyCode.J);
            Keyboard.ThroughDown = Input.GetKeyDown(KeyCode.L);
            Keyboard.CrossDown = Input.GetKeyDown(KeyCode.I);
            Keyboard.ShootHeld = Input.GetKey(KeyCode.K);
            Keyboard.SlideDown = Input.GetKeyDown(KeyCode.Q);
            Keyboard.PressHeld = Input.GetKey(KeyCode.E);
            Keyboard.ClearDown = Input.GetKeyDown(KeyCode.R);
            Keyboard.KeeperDown = Input.GetKeyDown(KeyCode.G);
            Keyboard.SwitchDown = Input.GetKeyDown(KeyCode.Tab);
            Keyboard.PauseDown = Input.GetKeyDown(KeyCode.Escape);
        }
#endif
    }
}
