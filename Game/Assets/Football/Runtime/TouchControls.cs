using UnityEngine;
using UnityEngine.UI;
using Football.Core.Common;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>
    /// Controles táctiles del partido: joystick flotante a un lado y botones que cambian según la situación al otro
    /// (se puede invertir para zurdos y agrandar).
    /// <list type="bullet">
    /// <item>Con el balón: PASE, TIRO (mantener para cargar), DESPEJAR (en tu campo) o FILTRADO (en el rival, opcional), CENTRO (opcional).</item>
    /// <item>Sin el balón: BARRER, PRESIONAR (mantener: persigue y entra de pie), CAMBIAR, ARQUERO (tomar o soltar al arquero).</item>
    /// <item>Siempre: ESPRINT (mantener) y pausa.</item>
    /// </list>
    /// También acepta el teclado para probar en el ordenador. Produce un <see cref="MatchInput"/> por fotograma.
    /// </summary>
    public sealed class TouchControls
    {
        private const float JoystickRadius = 130f;
        private const float ShootChargeSeconds = 0.8f;
        private const float ShootMinPower = 0.35f;
        private const float DeadZone = 0.12f;
        private const float BigScale = 1.2f;
        private const int NoPointer = -1;

        // Posiciones (para diestros) y tamaños de las cuatro casillas de acción y del esprint.
        private static readonly Vector2 SlotA = new Vector2(1560f, 250f);
        private static readonly Vector2 SlotB = new Vector2(1775f, 370f);
        private static readonly Vector2 SlotC = new Vector2(1585f, 490f);
        private static readonly Vector2 SlotD = new Vector2(1800f, 580f);
        private static readonly Vector2 SprintSlot = new Vector2(1345f, 160f);
        private const float SizeA = 190f, SizeB = 175f, SizeC = 145f, SizeD = 135f, SizeSprint = 150f;

        private sealed class Control
        {
            public UiButton Ui;
            public int PointerId = NoPointer;
            public bool Down;
            public bool JustDown;
            public bool JustUp;
            public float Held;
            public Vector2 Slot;
            public float Size;
        }

        private readonly Canvas _canvas;
        private readonly Control _pass, _shoot, _clear, _through, _cross;
        private readonly Control _slide, _press, _switch, _keeper;
        private readonly Control _sprint, _pause;
        private readonly Control[] _controls;
        private readonly GameObject _joystickBase;
        private readonly RectTransform _joystickBaseRect;
        private readonly RectTransform _joystickKnob;

        private int _joystickPointer = NoPointer;
        private Vector2 _joystickOrigin;
        private Vector2 _joystick;
        private bool _leftHanded;
        private bool _big;
        private bool _enabled = true;
        private bool _hasBall = true;
        private bool _ownHalf;
        private bool _keeperLocked;
        private bool _showThrough;
        private bool _showCross;
        private bool _keyboardShootWasHeld;

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
            _pass = Make(t, "Pase", "PASE", new Color(0.15f, 0.65f, 0.25f, 0.8f), SlotA, SizeA);
            _shoot = Make(t, "Tiro", "TIRO", new Color(0.85f, 0.15f, 0.15f, 0.8f), SlotB, SizeB);
            _clear = Make(t, "Despejar", "DESPEJAR", new Color(0.55f, 0.4f, 0.2f, 0.8f), SlotC, SizeC);
            _through = Make(t, "Filtrado", "FILTRADO", new Color(0.9f, 0.75f, 0.1f, 0.8f), SlotC, SizeC);
            _cross = Make(t, "Centro", "CENTRO", new Color(0.2f, 0.45f, 0.9f, 0.8f), SlotD, SizeD);
            _slide = Make(t, "Barrer", "BARRER", new Color(0.85f, 0.3f, 0.1f, 0.8f), SlotA, SizeA);
            _press = Make(t, "Presionar", "PRESIONAR", new Color(0.2f, 0.55f, 0.85f, 0.8f), SlotB, SizeB);
            _switch = Make(t, "Cambiar", "CAMBIAR", new Color(0.5f, 0.3f, 0.8f, 0.8f), SlotC, SizeC);
            _keeper = Make(t, "Arquero", "ARQUERO", new Color(0.55f, 0.8f, 0.15f, 0.8f), SlotD, SizeD);
            _sprint = Make(t, "Esprint", "ESPRINT", new Color(0.35f, 0.35f, 0.4f, 0.8f), SprintSlot, SizeSprint);
            _pause = Make(t, "Pausa", "II", new Color(0f, 0f, 0f, 0.45f), new Vector2(UiKit.Reference.x * 0.5f + 260f, UiKit.Reference.y - 60f), 90f);
            _controls = new[] { _pass, _shoot, _clear, _through, _cross, _slide, _press, _switch, _keeper, _sprint, _pause };

            Image ring = UiKit.Panel(t, "JoystickBase", Vector2.zero, new Vector2(JoystickRadius * 2f, JoystickRadius * 2f),
                new Color(1f, 1f, 1f, 0.18f), true);
            _joystickBase = ring.gameObject;
            _joystickBaseRect = (RectTransform)ring.transform;
            Image knob = UiKit.Panel(ring.transform, "Knob", new Vector2(JoystickRadius, JoystickRadius), new Vector2(110f, 110f),
                new Color(1f, 1f, 1f, 0.5f), true);
            _joystickKnob = (RectTransform)knob.transform;
            _joystickBase.SetActive(false);
            Layout(false, false, false, false);
        }

        private static Control Make(Transform parent, string name, string text, Color color, Vector2 slot, float size)
        {
            UiButton button = UiKit.Button(parent, name, slot, new Vector2(size, size), text, size > 160f ? 34 : 26, color, true);
            return new Control { Ui = button, Slot = slot, Size = size };
        }

        /// <summary>Coloca los botones según las preferencias.</summary>
        /// <param name="leftHanded">Invierte los lados (joystick a la derecha).</param>
        /// <param name="big">Botones más grandes.</param>
        /// <param name="showThrough">Mostrar el botón de pase filtrado.</param>
        /// <param name="showCross">Mostrar el botón de centro.</param>
        public void Layout(bool leftHanded, bool big, bool showThrough, bool showCross)
        {
            _leftHanded = leftHanded;
            _big = big;
            _showThrough = showThrough;
            _showCross = showCross;
            for (int i = 0; i < _controls.Length; i++)
            {
                Control c = _controls[i];
                if (c == _pause)
                {
                    continue;
                }

                float size = c.Size * (big ? BigScale : 1f);
                Vector2 slot = c.Slot;
                if (big)
                {
                    // Al agrandar, se separan un poco del borde para que no se salgan.
                    slot = new Vector2(slot.x - 40f, slot.y + 20f);
                }

                c.Ui.Place(new Vector2(leftHanded ? UiKit.Reference.x - slot.x : slot.x, slot.y), new Vector2(size, size));
            }

            ApplyVisibility();
        }

        /// <summary>Indica la situación para mostrar los botones que corresponden.</summary>
        /// <param name="hasBall">Tu equipo tiene el balón.</param>
        /// <param name="ownHalf">El balón está en tu campo (se ofrece DESPEJAR en lugar de FILTRADO).</param>
        /// <param name="keeperLocked">Estás controlando al arquero.</param>
        public void SetContext(bool hasBall, bool ownHalf, bool keeperLocked)
        {
            if (hasBall == _hasBall && ownHalf == _ownHalf && keeperLocked == _keeperLocked)
            {
                return;
            }

            _hasBall = hasBall;
            _ownHalf = ownHalf;
            _keeperLocked = keeperLocked;
            _keeper.Ui.Label.text = keeperLocked ? "JUGADOR" : "ARQUERO";
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            bool attack = _enabled && _hasBall;
            bool defense = _enabled && !_hasBall;
            _pass.Ui.SetVisible(attack);
            _shoot.Ui.SetVisible(attack);
            _clear.Ui.SetVisible(attack && (_ownHalf || !_showThrough));
            _through.Ui.SetVisible(attack && !_ownHalf && _showThrough);
            _cross.Ui.SetVisible(attack && _showCross);
            _slide.Ui.SetVisible(defense);
            _press.Ui.SetVisible(defense);
            _switch.Ui.SetVisible(defense);
            _keeper.Ui.SetVisible(defense);
            _sprint.Ui.SetVisible(_enabled);
            for (int i = 0; i < _controls.Length; i++)
            {
                if (!_controls[i].Ui.Root.activeSelf)
                {
                    Release(_controls[i]);
                }
            }
        }

        /// <summary>Muestra u oculta todos los controles (por ejemplo durante la pausa).</summary>
        public void SetActive(bool active)
        {
            _enabled = active;
            ApplyVisibility();
            _pause.Ui.SetVisible(active);
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

        private static void Release(Control c)
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
                Clear = _clear.JustDown || kb.ClearDown,
                Slide = _slide.JustDown || kb.SlideDown,
                Press = _press.Down || kb.PressHeld,
                Switch = _switch.JustDown || kb.SwitchDown,
                Keeper = _keeper.JustDown || kb.KeeperDown
            };

            // Tiro: se carga mientras se mantiene pulsado y se dispara al soltar.
            bool keyboardHeld = kb.ShootHeld;
            if (keyboardHeld)
            {
                _shoot.Held += Time.unscaledDeltaTime;
            }

            if (_shoot.JustUp || (_keyboardShootWasHeld && !keyboardHeld))
            {
                input.Shoot = true;
                input.ShootPower = Mathf.Max(ShootMinPower, Mathf.Clamp01(_shoot.Held / ShootChargeSeconds));
                _shoot.Held = 0f;
            }

            _keyboardShootWasHeld = keyboardHeld;

            if (input.Pass || input.Through || input.Cross || input.Shoot || input.Clear || input.Slide || input.Switch || input.Keeper)
            {
                LastActionTime = realTime;
            }

            return input;
        }

        /// <summary>Carga del tiro de 0 a 1 (para dibujar una barra), 0 si no se está cargando.</summary>
        public float ShootCharge => _shoot.Down || _keyboardShootWasHeld ? Mathf.Clamp01(_shoot.Held / ShootChargeSeconds) : 0f;
    }
}
