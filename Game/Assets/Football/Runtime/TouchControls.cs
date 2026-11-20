using UnityEngine;
using UnityEngine.UI;
using Football.Core.Common;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>
    /// Controles táctiles del partido: joystick flotante a un lado y botones contextuales al otro (se puede invertir
    /// para zurdos). También acepta el teclado para probar en el ordenador. Produce un <see cref="MatchInput"/> por fotograma.
    /// </summary>
    public sealed class TouchControls
    {
        private const float JoystickRadius = 130f;
        private const float ShootChargeSeconds = 0.8f;
        private const float ShootMinPower = 0.35f;
        private const float DeadZone = 0.12f;
        private const float SideMargin = 90f;
        private const int NoPointer = -1;

        private sealed class Control
        {
            public UiButton Ui;
            public int PointerId = NoPointer;
            public bool Down;
            public bool JustDown;
            public bool JustUp;
            public float Held;
        }

        private readonly Canvas _canvas;
        private readonly Control _pass, _through, _cross, _shoot, _sprint, _tackle, _switch, _pause;
        private readonly Control[] _controls;
        private readonly GameObject _joystickBase;
        private readonly RectTransform _joystickBaseRect;
        private readonly RectTransform _joystickKnob;

        private int _joystickPointer = NoPointer;
        private Vector2 _joystickOrigin;
        private Vector2 _joystick;
        private bool _leftHanded;
        private bool _hasBall;
        private bool _enabled = true;

        /// <summary>Dirección del joystick (longitud hasta 1) con la zona muerta ya aplicada.</summary>
        public Vector2 Stick { get; private set; }

        /// <summary>Se ha pulsado el botón de pausa en este fotograma.</summary>
        public bool PauseDown => _pause.JustDown;

        /// <summary>Instante (tiempo real) de la última pulsación de una acción, para medir la latencia.</summary>
        public float LastActionTime { get; private set; }

        /// <summary>Crea los controles dentro del lienzo dado.</summary>
        public TouchControls(Canvas canvas)
        {
            _canvas = canvas;
            Transform t = canvas.transform;
            _pass = Make(t, "Pase", "PASE", new Color(0.15f, 0.65f, 0.25f, 0.75f), 170f);
            _through = Make(t, "Filtrado", "FILTRADO", new Color(0.9f, 0.75f, 0.1f, 0.75f), 130f);
            _cross = Make(t, "Centro", "CENTRO", new Color(0.2f, 0.45f, 0.9f, 0.75f), 130f);
            _shoot = Make(t, "Tiro", "TIRO", new Color(0.85f, 0.15f, 0.15f, 0.75f), 170f);
            _sprint = Make(t, "Esprint", "ESPRINT", new Color(0.35f, 0.35f, 0.4f, 0.75f), 120f);
            _tackle = Make(t, "Presionar", "PRESIONAR", new Color(0.85f, 0.35f, 0.1f, 0.75f), 170f);
            _switch = Make(t, "Cambiar", "CAMBIAR", new Color(0.5f, 0.3f, 0.8f, 0.75f), 130f);
            _pause = Make(t, "Pausa", "II", new Color(0f, 0f, 0f, 0.45f), 90f);
            _controls = new[] { _pass, _through, _cross, _shoot, _sprint, _tackle, _switch, _pause };

            Image ring = UiKit.Panel(t, "JoystickBase", Vector2.zero, new Vector2(JoystickRadius * 2f, JoystickRadius * 2f),
                new Color(1f, 1f, 1f, 0.18f), true);
            _joystickBase = ring.gameObject;
            _joystickBaseRect = (RectTransform)ring.transform;
            Image knob = UiKit.Panel(ring.transform, "Knob", new Vector2(JoystickRadius, JoystickRadius), new Vector2(110f, 110f),
                new Color(1f, 1f, 1f, 0.5f), true);
            _joystickKnob = (RectTransform)knob.transform;
            _joystickBase.SetActive(false);
            Layout(false);
            SetHasBall(true);
        }

        private static Control Make(Transform parent, string name, string text, Color color, float size)
        {
            UiButton button = UiKit.Button(parent, name, Vector2.zero, new Vector2(size, size), text, size > 150f ? 34 : 26, color, true);
            return new Control { Ui = button };
        }

        /// <summary>Coloca los botones; <paramref name="leftHanded"/> invierte los lados.</summary>
        public void Layout(bool leftHanded)
        {
            _leftHanded = leftHanded;
            Place(_pass, 1540f, 250f);
            Place(_shoot, 1760f, 330f);
            Place(_through, 1560f, 470f);
            Place(_cross, 1790f, 560f);
            Place(_tackle, 1540f, 250f);
            Place(_switch, 1560f, 470f);
            Place(_sprint, 1340f, 150f);
            _pause.Ui.Place(new Vector2(UiKit.Reference.x * 0.5f + 260f, UiKit.Reference.y - 60f), new Vector2(90f, 90f));
        }

        private void Place(Control c, float x, float y)
        {
            c.Ui.Place(new Vector2(_leftHanded ? UiKit.Reference.x - x : x, y), c.Ui.Size);
        }

        /// <summary>Muestra los botones de ataque (con balón) o de defensa (sin balón).</summary>
        public void SetHasBall(bool hasBall)
        {
            _hasBall = hasBall;
            _pass.Ui.SetVisible(_enabled && hasBall);
            _through.Ui.SetVisible(_enabled && hasBall);
            _cross.Ui.SetVisible(_enabled && hasBall);
            _shoot.Ui.SetVisible(_enabled && hasBall);
            _tackle.Ui.SetVisible(_enabled && !hasBall);
            _switch.Ui.SetVisible(_enabled && !hasBall);
        }

        /// <summary>Oculta o muestra todos los controles (por ejemplo durante un menú).</summary>
        public void SetActive(bool active)
        {
            _enabled = active;
            for (int i = 0; i < _controls.Length; i++)
            {
                _controls[i].Ui.SetVisible(false);
                ReleaseControl(_controls[i]);
            }

            if (active)
            {
                SetHasBall(_hasBall);
                _sprint.Ui.SetVisible(true);
                _pause.Ui.SetVisible(true);
            }

            _joystickBase.SetActive(false);
            _joystickPointer = NoPointer;
            _joystick = Vector2.zero;
        }

        /// <summary>Deja visible solo el botón de pausa (por ejemplo en el partido de medición, que juega solo).</summary>
        public void ShowPauseOnly()
        {
            SetActive(false);
            _pause.Ui.SetVisible(true);
        }

        private static void ReleaseControl(Control c)
        {
            c.PointerId = NoPointer;
            c.Down = false;
            c.Held = 0f;
        }

        private bool OnJoystickSide(Vector2 canvasPoint)
        {
            float half = UiKit.Reference.x * 0.5f;
            return _leftHanded ? canvasPoint.x > half : canvasPoint.x < half;
        }

        /// <summary>Procesa los punteros del fotograma y devuelve la entrada para el motor.</summary>
        public MatchInput Read(PointerInput pointers, float realTime)
        {
            for (int i = 0; i < _controls.Length; i++)
            {
                _controls[i].JustDown = false;
                _controls[i].JustUp = false;
            }

            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            for (int i = 0; i < pointers.Count; i++)
            {
                PointerState p = pointers.Pointers[i];
                Vector2 canvasPoint = p.Position / scale;
                switch (p.Phase)
                {
                    case PointerPhase.Began:
                        OnBegan(p.Id, canvasPoint);
                        break;
                    case PointerPhase.Held:
                        OnHeld(p.Id, canvasPoint);
                        break;
                    default:
                        OnEnded(p.Id);
                        break;
                }
            }

            for (int i = 0; i < _controls.Length; i++)
            {
                if (_controls[i].Down)
                {
                    _controls[i].Held += Time.unscaledDeltaTime;
                }
            }

            return Compose(pointers.Keyboard, realTime);
        }

        private void OnBegan(int id, Vector2 canvasPoint)
        {
            for (int i = 0; i < _controls.Length; i++)
            {
                Control c = _controls[i];
                if (c.PointerId == NoPointer && c.Ui.Contains(canvasPoint))
                {
                    c.PointerId = id;
                    c.Down = true;
                    c.JustDown = true;
                    c.Held = 0f;
                    return;
                }
            }

            if (_joystickPointer == NoPointer && OnJoystickSide(canvasPoint))
            {
                _joystickPointer = id;
                _joystickOrigin = canvasPoint;
                _joystick = Vector2.zero;
                _joystickBase.SetActive(true);
                _joystickBaseRect.anchoredPosition = canvasPoint;
                _joystickKnob.anchoredPosition = new Vector2(JoystickRadius, JoystickRadius);
            }
        }

        private void OnHeld(int id, Vector2 canvasPoint)
        {
            if (id != _joystickPointer)
            {
                return;
            }

            Vector2 offset = canvasPoint - _joystickOrigin;
            if (offset.magnitude > JoystickRadius)
            {
                // El joystick flotante sigue al dedo cuando se sale del círculo.
                _joystickOrigin = canvasPoint - offset.normalized * JoystickRadius;
                _joystickBaseRect.anchoredPosition = _joystickOrigin;
                offset = canvasPoint - _joystickOrigin;
            }

            _joystick = offset / JoystickRadius;
            _joystickKnob.anchoredPosition = new Vector2(JoystickRadius, JoystickRadius) + offset;
        }

        private void OnEnded(int id)
        {
            if (id == _joystickPointer)
            {
                _joystickPointer = NoPointer;
                _joystick = Vector2.zero;
                _joystickBase.SetActive(false);
                return;
            }

            for (int i = 0; i < _controls.Length; i++)
            {
                Control c = _controls[i];
                if (c.PointerId == id)
                {
                    c.PointerId = NoPointer;
                    c.Down = false;
                    c.JustUp = true;
                }
            }
        }

        private MatchInput Compose(KeyboardState kb, float realTime)
        {
            Vector2 stick = _joystick.sqrMagnitude > 1f ? _joystick.normalized : _joystick;
            if (stick.magnitude < DeadZone)
            {
                stick = Vector2.zero;
            }

            if (kb.Move.sqrMagnitude > 0f)
            {
                stick = kb.Move.normalized;
            }

            Stick = stick;
            var input = new MatchInput
            {
                Move = new Vec2(stick.x, stick.y),
                Sprint = _sprint.Down || kb.Sprint,
                Pass = _pass.JustDown || kb.PassDown,
                Through = _through.JustDown || kb.ThroughDown,
                Cross = _cross.JustDown || kb.CrossDown,
                Tackle = _tackle.JustDown || kb.TackleDown,
                Switch = _switch.JustDown || kb.SwitchDown
            };

            // Tiro: se carga mientras se mantiene pulsado y se dispara al soltar.
            bool keyboardHeld = kb.ShootHeld;
            if (keyboardHeld)
            {
                _shoot.Held += Time.unscaledDeltaTime;
            }

            bool shootReleased = _shoot.JustUp || (_keyboardShootWasHeld && !keyboardHeld);
            if (shootReleased)
            {
                input.Shoot = true;
                input.ShootPower = Mathf.Max(ShootMinPower, Mathf.Clamp01(_shoot.Held / ShootChargeSeconds));
                _shoot.Held = 0f;
            }

            _keyboardShootWasHeld = keyboardHeld;

            if (input.Pass || input.Through || input.Cross || input.Shoot || input.Tackle || input.Switch)
            {
                LastActionTime = realTime;
            }

            return input;
        }

        private bool _keyboardShootWasHeld;

        /// <summary>Carga del tiro de 0 a 1 (para dibujar una barra), 0 si no se está cargando.</summary>
        public float ShootCharge => _shoot.Down || _keyboardShootWasHeld ? Mathf.Clamp01(_shoot.Held / ShootChargeSeconds) : 0f;

        /// <summary>Hay un tiro cargándose.</summary>
        public bool IsCharging => _shoot.Down || _keyboardShootWasHeld;

        /// <summary>Ancho útil de margen lateral, expuesto para que otras partes de la interfaz lo respeten.</summary>
        public static float Margin => SideMargin;
    }
}
