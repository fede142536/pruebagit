using UnityEngine;
using UnityEngine.UI;
using Football.Core.Common;
using Football.Core.MatchCore;
using Football.Core.Domain;

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
        private static float JoystickRadius => ViewTuning.Current.Controls.JoystickRadius;
        private static float ShootChargeSeconds => ViewTuning.Current.Controls.ShootChargeSeconds;
        private static float PassChargeSeconds => ViewTuning.Current.Controls.PassChargeSeconds;
        private static float PassTapSeconds => ViewTuning.Current.Controls.PassTapSeconds; // un toque más corto es un pase automático
        private const float ChargeBarHeight = 14f;
        private static float ShootMinPower => ViewTuning.Current.Controls.ShootMinPower;
        private float _deadZone = ViewTuning.Current.Controls.DeadZones[System.Math.Min(1, ViewTuning.Current.Controls.DeadZones.Length - 1)];

        /// <summary>Elige la zona muerta del joystick (fracción del radio donde no se mueve).</summary>
        /// <param name="fraction">Fracción del radio, entre 0 y 0,5.</param>
        public void SetDeadZone(float fraction)
        {
            _deadZone = Mathf.Clamp(fraction, 0f, 0.5f);
        }
        private static float BigScale => ViewTuning.Current.Controls.BigScale;
        private const int NoPointer = -1;

        // Posiciones (para diestros) y tamaños de las cuatro casillas de acción y del esprint.
        // Posiciones pensadas para un ancho de 1920: los botones se miden desde el borde derecho real (ver ToScreenEdge).
        private static readonly Vector2 SlotA = new Vector2(1560f, 250f);
        private static readonly Vector2 SlotB = new Vector2(1775f, 370f);
        private static readonly Vector2 SlotC = new Vector2(1585f, 490f);
        private static readonly Vector2 SlotD = new Vector2(1800f, 580f);
        private static readonly Vector2 SprintSlot = new Vector2(1345f, 160f);
        private static readonly Vector2 SlotE = new Vector2(1610f, 700f); // segunda marca, sobre los botones de defensa
        // Un 15 % más chicos que la primera versión y translúcidos para no tapar a los jugadores.
        private const float SizeA = 160f, SizeB = 150f, SizeC = 124f, SizeD = 115f, SizeSprint = 128f;
        private const float ButtonAlpha = 0.55f;

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
            public Color BaseColor;
        }

        private readonly Canvas _canvas;
        private readonly Control _pass, _shoot, _clear, _through, _cross;
        private readonly Control _slide, _press, _secondPress, _switch, _keeper;
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
        private bool _forceCross;
        private bool _keyboardCrossWasHeld;
        private float _keyboardCrossHeld;
        private bool _keyboardPassWasHeld;
        private float _keyboardPassHeld;
        private RectTransform _passBar, _shootBar;
        private float _width = UiKit.Reference.x;
        private int _layoutScreenWidth;
        private Rect _layoutSafeArea;
        private static float PauseMargin => ViewTuning.Current.Controls.PauseMargin;

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
            _pass = Make(t, "Pase", "PASE", new Color(0.15f, 0.65f, 0.25f, ButtonAlpha), SlotA, SizeA);
            _shoot = Make(t, "Tiro", "TIRO", new Color(0.85f, 0.15f, 0.15f, ButtonAlpha), SlotB, SizeB);
            _clear = Make(t, "Despejar", "DESPEJE", new Color(0.55f, 0.4f, 0.2f, ButtonAlpha), SlotC, SizeC);
            _through = Make(t, "Filtrado", "FILTRO", new Color(0.9f, 0.75f, 0.1f, ButtonAlpha), SlotC, SizeC);
            _cross = Make(t, "Centro", "CENTRO", new Color(0.2f, 0.45f, 0.9f, ButtonAlpha), SlotD, SizeD);
            _slide = Make(t, "Barrer", "BARRER", new Color(0.85f, 0.3f, 0.1f, ButtonAlpha), SlotA, SizeA);
            _press = Make(t, "Presionar", "PRESIÓN", new Color(0.2f, 0.55f, 0.85f, ButtonAlpha), SlotB, SizeB);
            _secondPress = Make(t, "SegundaMarca", "2ª MARCA", new Color(0.15f, 0.4f, 0.65f, ButtonAlpha), SlotE, SizeD);
            _switch = Make(t, "Cambiar", "CAMBIAR", new Color(0.5f, 0.3f, 0.8f, ButtonAlpha), SlotC, SizeC);
            _keeper = Make(t, "Arquero", "ARQUERO", new Color(0.55f, 0.8f, 0.15f, ButtonAlpha), SlotD, SizeD);
            _sprint = Make(t, "Esprint", "ESPRINT", new Color(0.35f, 0.35f, 0.4f, ButtonAlpha), SprintSlot, SizeSprint);
            _pause = Make(t, "Pausa", "II", new Color(0f, 0f, 0f, 0.45f), new Vector2(UiKit.Reference.x - PauseMargin, UiKit.Reference.y - PauseMargin), 90f);
            _controls = new[] { _pass, _shoot, _clear, _through, _cross, _slide, _press, _secondPress, _switch, _keeper, _sprint, _pause };
            _passBar = ChargeBar(_pass);
            _shootBar = ChargeBar(_shoot);

            // El joystick se mueve cada fotograma: va en su propio lienzo para no reconstruir los botones.
            var joystickLayer = new GameObject("CapaJoystick", typeof(RectTransform));
            joystickLayer.transform.SetParent(t, false);
            var layerRect = (RectTransform)joystickLayer.transform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            joystickLayer.AddComponent<Canvas>();
            Image ring = UiKit.Panel(joystickLayer.transform, "JoystickBase", Vector2.zero, new Vector2(JoystickRadius * 2f, JoystickRadius * 2f),
                new Color(1f, 1f, 1f, 0.18f), true);
            _joystickBase = ring.gameObject;
            _joystickBaseRect = (RectTransform)ring.transform;
            Image knob = UiKit.Panel(ring.transform, "Knob", new Vector2(JoystickRadius, JoystickRadius), new Vector2(110f, 110f),
                new Color(1f, 1f, 1f, 0.5f), true);
            _joystickKnob = (RectTransform)knob.transform;
            HideJoystick();
            Layout(false, false, false, false);
        }

        /// <summary>Barra amarilla dentro del botón que muestra la carga de potencia mientras se mantiene pulsado.</summary>
        private static RectTransform ChargeBar(Control c)
        {
            Image bar = UiKit.Panel(c.Ui.Root.transform, "Carga", new Vector2(c.Size * 0.5f, c.Size * 0.22f),
                new Vector2(0f, ChargeBarHeight), new Color(1f, 0.85f, 0.2f, 0.95f));
            return (RectTransform)bar.transform;
        }

        private static void ShowCharge(RectTransform bar, Control c, float charge)
        {
            float width = charge > 0f ? c.Ui.Size.x * 0.7f * charge : 0f;
            if (Mathf.Abs(bar.sizeDelta.x - width) > 0.5f)
            {
                bar.sizeDelta = new Vector2(width, ChargeBarHeight);
            }
        }

        private static Control Make(Transform parent, string name, string text, Color color, Vector2 slot, float size)
        {
            UiButton button = UiKit.Button(parent, name, slot, new Vector2(size, size), text, size >= 150f ? 30 : 24, color, true);
            return new Control { Ui = button, Slot = slot, Size = size, BaseColor = color }; // color base: para cambiar la opacidad
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
            _width = UiKit.CanvasWidth;
            _layoutScreenWidth = Screen.width;
            _layoutSafeArea = Screen.safeArea;
            // Zona segura: la muesca o la cámara frontal no deben tapar ningún botón.
            float safeLeft = UiKit.SafeLeft, safeRight = UiKit.SafeRight, safeBottom = UiKit.SafeBottom;
            float pauseX = leftHanded ? safeLeft + PauseMargin : _width - safeRight - PauseMargin;
            _pause.Ui.Place(new Vector2(pauseX, UiKit.CanvasHeight - UiKit.SafeTop - PauseMargin), new Vector2(_pause.Size, _pause.Size));
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

                // Misma distancia al borde derecho que en 1920 de ancho, sea cual sea el ancho real de la pantalla.
                float fromRight = UiKit.Reference.x - slot.x;
                float x = leftHanded ? safeLeft + fromRight : _width - safeRight - fromRight;
                c.Ui.Place(new Vector2(x, slot.y + safeBottom), new Vector2(size, size));
            }

            if (_joystickPointer == NoPointer)
            {
                HideJoystick(); // el joystick fijo se recoloca (zurdos o cambio de pantalla)
            }

            ApplyVisibility();
        }

        /// <summary>Indica la situación para mostrar los botones que corresponden.</summary>
        /// <param name="hasBall">Tu equipo tiene el balón.</param>
        /// <param name="ownHalf">El balón está en tu campo (se ofrece DESPEJAR en lugar de FILTRADO).</param>
        /// <param name="keeperLocked">Estás controlando al arquero.</param>
        /// <param name="forceCross">Mostrar CENTRO aunque esté desactivado en Ajustes (córners y tiros libres propios).</param>
        public void SetContext(bool hasBall, bool ownHalf, bool keeperLocked, bool forceCross = false)
        {
            if (hasBall == _hasBall && ownHalf == _ownHalf && keeperLocked == _keeperLocked && forceCross == _forceCross)
            {
                return;
            }

            _forceCross = forceCross;
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
            _cross.Ui.SetVisible(attack && (_showCross || _forceCross));
            _slide.Ui.SetVisible(defense);
            _press.Ui.SetVisible(defense);
            _secondPress.Ui.SetVisible(defense);
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

        private static float FixedJoystickMargin => ViewTuning.Current.Controls.FixedJoystickMargin;
        private const float FixedCatchFactor = 1.8f;
        private bool _fixedJoystick;
        private float _opacity = 0.55f;

        private Vector2 FixedCenter => new Vector2(_leftHanded ? _width - UiKit.SafeRight - FixedJoystickMargin : UiKit.SafeLeft + FixedJoystickMargin,
            UiKit.SafeBottom + FixedJoystickMargin);

        /// <summary>Estilo de los controles: opacidad de los botones y joystick fijo o flotante.</summary>
        /// <param name="opacity">Opacidad de 0 a 1.</param>
        /// <param name="fixedJoystick">Joystick fijo (siempre visible en la esquina) en lugar de flotante.</param>
        public void SetStyle(float opacity, bool fixedJoystick)
        {
            _opacity = opacity;
            _fixedJoystick = fixedJoystick;
            for (int i = 0; i < _controls.Length; i++)
            {
                Control c = _controls[i];
                if (c == _pause)
                {
                    continue;
                }

                Color color = c.BaseColor;
                color.a = opacity;
                c.Ui.Background.color = color;
            }

            HideJoystick();
        }

        /// <summary>Joystick en reposo: el flotante desaparece; el fijo queda visible en su lugar con la palanca centrada.</summary>
        private void HideJoystick()
        {
            bool showFixed = _fixedJoystick && _enabled;
            if (_joystickBase.activeSelf != showFixed)
            {
                _joystickBase.SetActive(showFixed);
            }

            if (showFixed)
            {
                _joystickBaseRect.anchoredPosition = FixedCenter;
                _joystickKnob.anchoredPosition = new Vector2(JoystickRadius, JoystickRadius);
            }
        }

        /// <summary>Muestra u oculta todos los controles (por ejemplo durante la pausa).</summary>
        public void SetActive(bool active)
        {
            _enabled = active;
            ApplyVisibility();
            _pause.Ui.SetVisible(active);
            HideJoystick();
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
            float half = _width * 0.5f;
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

            if (Screen.width != _layoutScreenWidth || Screen.safeArea != _layoutSafeArea)
            {
                Layout(_leftHanded, _big, _showThrough, _showCross); // cambió la pantalla (giro o ventana)
            }

            ReleaseVanishedPointers(pointers);
            for (int i = 0; i < pointers.Count; i++)
            {
                PointerState p = pointers.Pointers[i];
                Vector2 canvasPoint = UiKit.ToCanvas(p.Position);
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

        /// <summary>
        /// Suelta los botones y el joystick cuyo dedo ya no existe (Android puede no avisar del final de un toque si una
        /// notificación o salir de la aplicación se lo lleva). Sin esto, el esprint quedaría pulsado para siempre.
        /// </summary>
        private void ReleaseVanishedPointers(PointerInput pointers)
        {
            if (_joystickPointer != NoPointer && !HasPointer(pointers, _joystickPointer))
            {
                _joystickPointer = NoPointer;
                _joystick = Vector2.zero;
                HideJoystick();
            }

            for (int i = 0; i < _controls.Length; i++)
            {
                Control c = _controls[i];
                if (c.PointerId != NoPointer && !HasPointer(pointers, c.PointerId))
                {
                    c.PointerId = NoPointer;
                    c.Down = false;
                    c.JustUp = c == _shoot; // un tiro cargado se suelta; el resto simplemente se libera
                }
            }
        }

        private static bool HasPointer(PointerInput pointers, int id)
        {
            for (int i = 0; i < pointers.Count; i++)
            {
                if (pointers.Pointers[i].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Suelta todo (al pausar o salir de la aplicación).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _controls.Length; i++)
            {
                Release(_controls[i]);
            }

            _joystickPointer = NoPointer;
            _joystick = Vector2.zero;
            HideJoystick();
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

            if (_joystickPointer == NoPointer && _fixedJoystick && _enabled)
            {
                // Joystick fijo: solo responde si el dedo apoya sobre él (o muy cerca); su centro no se mueve.
                if ((canvasPoint - FixedCenter).magnitude <= JoystickRadius * FixedCatchFactor)
                {
                    _joystickPointer = id;
                    _joystickOrigin = FixedCenter;
                    _joystick = Vector2.zero;
                    OnHeld(id, canvasPoint);
                }

                return;
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
            if (_fixedJoystick && offset.magnitude > JoystickRadius)
            {
                offset = offset.normalized * JoystickRadius; // el fijo no sigue al dedo
            }
            else if (offset.magnitude > JoystickRadius)
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
                HideJoystick();
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
            if (stick.magnitude < _deadZone)
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
                // El pase sale al soltar: un toque corto es automático; mantener carga la potencia.
                Pass = false,
                Through = _through.JustDown || kb.ThroughDown,
                // El centro también sale al soltar: mantener carga la distancia (decisiva en córners y saques).
                Cross = false,
                Clear = _clear.JustDown || kb.ClearDown,
                Slide = _slide.JustDown || kb.SlideDown,
                Press = _press.Down || kb.PressHeld,
                SecondPress = _secondPress.Down || kb.SecondPressHeld,
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

            if (kb.PassHeld)
            {
                _keyboardPassHeld += Time.unscaledDeltaTime;
            }

            float passHeld = -1f;
            if (_pass.JustUp)
            {
                passHeld = _pass.Held;
                _pass.Held = 0f;
            }
            else if (_keyboardPassWasHeld && !kb.PassHeld)
            {
                passHeld = _keyboardPassHeld;
            }

            if (passHeld >= 0f)
            {
                input.Pass = true;
                input.PassPower = passHeld < PassTapSeconds ? 0f : Mathf.Clamp01((passHeld - PassTapSeconds) / PassChargeSeconds);
            }

            if (!kb.PassHeld)
            {
                _keyboardPassHeld = 0f;
            }

            _keyboardPassWasHeld = kb.PassHeld;

            if (kb.CrossHeld)
            {
                _keyboardCrossHeld += Time.unscaledDeltaTime;
            }

            float crossHeld = -1f;
            if (_cross.JustUp)
            {
                crossHeld = _cross.Held;
                _cross.Held = 0f;
            }
            else if (_keyboardCrossWasHeld && !kb.CrossHeld)
            {
                crossHeld = _keyboardCrossHeld;
            }

            if (crossHeld >= 0f)
            {
                input.Cross = true;
                input.CrossPower = crossHeld < PassTapSeconds ? 0f : Mathf.Clamp01((crossHeld - PassTapSeconds) / PassChargeSeconds);
            }

            if (!kb.CrossHeld)
            {
                _keyboardCrossHeld = 0f;
            }

            _keyboardCrossWasHeld = kb.CrossHeld;
            float crossNow = _cross.Down ? _cross.Held : _keyboardCrossHeld;
            CrossCharge = crossNow < PassTapSeconds ? 0f : Mathf.Clamp01((crossNow - PassTapSeconds) / PassChargeSeconds);
            float heldNow = _pass.Down ? _pass.Held : _keyboardPassHeld;
            PassCharge = heldNow < PassTapSeconds ? 0f : Mathf.Clamp01((heldNow - PassTapSeconds) / PassChargeSeconds);
            ShowCharge(_passBar, _pass, PassCharge);
            ShowCharge(_shootBar, _shoot, ShootCharge);

            if (input.Pass || input.Through || input.Cross || input.Shoot || input.Clear || input.Slide || input.Switch || input.Keeper)
            {
                LastActionTime = realTime;
            }

            return input;
        }

        /// <summary>Carga del pase de 0 a 1 (para dibujar una barra), 0 si es un toque o no se está pulsando.</summary>
        public float PassCharge { get; private set; }

        /// <summary>Carga del centro de 0 a 1, 0 si es un toque o no se está pulsando.</summary>
        public float CrossCharge { get; private set; }

        /// <summary>Carga del tiro de 0 a 1 (para dibujar una barra), 0 si no se está cargando.</summary>
        public float ShootCharge => _shoot.Down || _keyboardShootWasHeld ? Mathf.Clamp01(_shoot.Held / ShootChargeSeconds) : 0f;
    }
}
