using System;
using Football.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Creación del DT (primer paso del Modo Carrera): nombre y apellido (teclado del teléfono; en el ordenador, nombres de
    /// ejemplo que se recorren tocando), nacionalidad con bandera (Argentina de fábrica), seis avatares y la figura 3D de
    /// cuerpo entero parada en el césped, con traje o buzo. Solo arma un <see cref="ManagerProfile"/>.
    /// </summary>
    public sealed class ManagerCreationScreen : CareerPage
    {
        private const int MaxNameLength = 16;
        private const int FlagColumns = 5;
        private const float FlagWidth = 132f, FlagHeight = 84f, FlagStep = 150f;
        private const float AvatarSize = 112f, AvatarStep = 132f;
        private const int ShowroomIndex = 2;
        private const float ShowroomHeading = 200f;

        private readonly UiButton _firstField, _lastField, _back, _next, _load, _suit, _tracksuit;
        private readonly UiButton[] _flags = new UiButton[NationFlag.All.Length];
        private readonly UiButton[] _avatars = new UiButton[ManagerLooks.Count];
        private readonly AvatarView[] _avatarViews = new AvatarView[ManagerLooks.Count];
        private readonly Text _nation, _preview;
        private readonly KitShowroom _figure;
        private readonly string[] _firstPresets, _lastPresets;
        private TouchScreenKeyboard _keyboard;
        private int _editing = -1; // 0 nombre, 1 apellido
        private int _firstPreset, _lastPreset;
        private bool _hasSave;

        /// <summary>Perfil que se está armando.</summary>
        public ManagerProfile Profile { get; } = new ManagerProfile();

        /// <summary>Crea la pantalla (oculta).</summary>
        public ManagerCreationScreen(Transform root, ICareerHost host) : base(root, "CrearDT", host)
        {
            Transform p = Panel;
            _back = UiKit.Button(p, "Volver", new Vector2(160f, 1020f), new Vector2(220f, 70f), Locale.Get("career.exit"), 28, UiTheme.Button);
            UiKit.Label(p, "Titulo", new Vector2(760f, 1020f), new Vector2(900f, 80f), Locale.Get("manager.title"), 50, UiTheme.Text, TextAnchor.MiddleLeft);

            UiKit.Panel(p, "Datos", new Vector2(540f, 520f), new Vector2(900f, 860f), UiTheme.Panel);
            UiKit.Label(p, "NombreTitulo", new Vector2(540f, 910f), new Vector2(820f, 40f), Locale.Get("manager.first_name"), 24, UiTheme.TextDim, TextAnchor.MiddleLeft);
            _firstField = UiKit.Button(p, "Nombre", new Vector2(540f, 855f), new Vector2(820f, 72f), string.Empty, 32, UiTheme.Button);
            UiKit.Label(p, "ApellidoTitulo", new Vector2(540f, 790f), new Vector2(820f, 40f), Locale.Get("manager.last_name"), 24, UiTheme.TextDim, TextAnchor.MiddleLeft);
            _lastField = UiKit.Button(p, "Apellido", new Vector2(540f, 735f), new Vector2(820f, 72f), string.Empty, 32, UiTheme.Button);

            UiKit.Label(p, "AyudaTexto", new Vector2(540f, 692f), new Vector2(820f, 26f), Locale.Get("manager.type_hint"), 18, UiTheme.TextMuted, TextAnchor.MiddleLeft);
            UiKit.Label(p, "NacionTitulo", new Vector2(540f, 668f), new Vector2(820f, 40f), Locale.Get("manager.nationality"), 24, UiTheme.TextDim, TextAnchor.MiddleLeft);
            for (int i = 0; i < _flags.Length; i++)
            {
                Vector2 c = new Vector2(240f + (i % FlagColumns) * FlagStep, 600f - (i / FlagColumns) * (FlagHeight + 22f));
                _flags[i] = UiKit.Button(p, "Nacion" + i, c, new Vector2(FlagWidth + 12f, FlagHeight + 12f), string.Empty, 10, UiTheme.Button);
                new FlagView(p, c, new Vector2(FlagWidth, FlagHeight)).Set(NationFlag.All[i]);
            }

            _nation = UiKit.Label(p, "Nacion", new Vector2(540f, 430f), new Vector2(820f, 44f), string.Empty, 28, UiTheme.Accent);
            UiKit.Label(p, "AvatarTitulo", new Vector2(540f, 370f), new Vector2(820f, 40f), Locale.Get("manager.avatar"), 24, UiTheme.TextDim, TextAnchor.MiddleLeft);
            for (int i = 0; i < _avatars.Length; i++)
            {
                Vector2 c = new Vector2(210f + i * AvatarStep, 270f);
                _avatars[i] = UiKit.Button(p, "Avatar" + i, c, new Vector2(AvatarSize, AvatarSize), string.Empty, 10, new Color(0f, 0f, 0f, 0f), true);
                _avatarViews[i] = new AvatarView(p, c, AvatarSize);
            }

            UiKit.Panel(p, "Figura", new Vector2(1420f, 560f), new Vector2(700f, 780f), UiTheme.Panel);
            _preview = UiKit.Label(p, "NombreDT", new Vector2(1420f, 905f), new Vector2(660f, 60f), string.Empty, 34, UiTheme.Text);
            _figure = new KitShowroom(p, new Vector2(1420f, 545f), new Vector2(420f, 660f), ShowroomIndex, ShowroomHeading, true);
            _suit = UiKit.Button(p, "Traje", new Vector2(1290f, 215f), new Vector2(240f, 70f), Locale.Get("manager.suit"), 28, UiTheme.Button);
            _tracksuit = UiKit.Button(p, "Buzo", new Vector2(1550f, 215f), new Vector2(240f, 70f), Locale.Get("manager.tracksuit"), 28, UiTheme.Button);

            _next = UiKit.Button(p, "Seguir", new Vector2(1420f, 100f), new Vector2(520f, 96f), Locale.Get("manager.next"), 38, UiTheme.AccentDark);
            _load = UiKit.Button(p, "Continuar", new Vector2(540f, 100f), new Vector2(520f, 86f), Locale.Get("career.continue"), 32, UiTheme.Button);

            _firstPresets = Locale.Get("manager.presets.first").Split(',');
            _lastPresets = Locale.Get("manager.presets.last").Split(',');
        }

        /// <summary>Prepara un DT nuevo.</summary>
        /// <param name="hasSave">Hay partida guardada (muestra «Continuar»).</param>
        public void Reset(bool hasSave)
        {
            _hasSave = hasSave;
            Profile.FirstName = _firstPresets[0].Trim();
            Profile.LastName = _lastPresets[0].Trim();
            Profile.Nationality = NationFlag.All[0].Code;
            Profile.AvatarIndex = 0;
            Profile.Outfit = ManagerOutfit.Suit;
            _firstPreset = _lastPreset = 0;
            _editing = -1;
            _keyboard = null;
            Rebuild();
        }

        /// <inheritdoc />
        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);
            _figure.SetVisible(visible);
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            string caret = _editing >= 0 && _caretOn ? "|" : string.Empty;
            _firstField.Label.text = Profile.FirstName + (_editing == 0 ? caret : string.Empty);
            _lastField.Label.text = Profile.LastName + (_editing == 1 ? caret : string.Empty);
            _firstField.Background.color = _editing == 0 ? UiTheme.Selected : UiTheme.Button;
            _lastField.Background.color = _editing == 1 ? UiTheme.Selected : UiTheme.Button;
            int nation = NationFlag.IndexOf(Profile.Nationality);
            for (int i = 0; i < _flags.Length; i++)
            {
                _flags[i].Background.color = i == nation ? UiTheme.Accent : UiTheme.Button;
            }

            _nation.text = Locale.Get("nation." + Profile.Nationality);
            for (int i = 0; i < _avatarViews.Length; i++)
            {
                _avatarViews[i].Set(i, i == Profile.AvatarIndex);
            }

            _suit.Background.color = Profile.Outfit == ManagerOutfit.Suit ? UiTheme.Selected : UiTheme.Button;
            _tracksuit.Background.color = Profile.Outfit == ManagerOutfit.Tracksuit ? UiTheme.Selected : UiTheme.Button;
            _preview.text = Locale.Get("manager.dt") + " " + Profile.FullName;
            _load.SetVisible(_hasSave);
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            _figure.Update(deltaTime);
            if (_keyboard == null)
            {
                TypeWithKeyboard(deltaTime);
                return;
            }

            string typed = Clean(_keyboard.text);
            string current = _editing == 0 ? Profile.FirstName : Profile.LastName;
            if (typed.Length > 0 && typed != current)
            {
                SetName(typed);
            }

            if (_keyboard.status != TouchScreenKeyboard.Status.Visible)
            {
                _keyboard = null;
                _editing = -1;
                Refresh();
            }
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p)) { Host.Request(CareerAction.Exit); return; }
            if (_load.Contains(p)) { Host.Request(CareerAction.LoadGame); return; }
            if (_next.Contains(p))
            {
                if (Profile.FirstName.Length == 0 || Profile.LastName.Length == 0)
                {
                    Host.SetStatus(Locale.Get("manager.name_missing"));
                    return;
                }

                Host.Request(CareerAction.PickClub);
                return;
            }

            if (_keyboard == null && !_firstField.Contains(p) && !_lastField.Contains(p))
            {
                _editing = -1; // un toque fuera de los campos deja de escribir
            }

            if (_firstField.Contains(p)) { Edit(0); }
            else if (_lastField.Contains(p)) { Edit(1); }
            else if (_suit.Contains(p)) { SetOutfit(ManagerOutfit.Suit); }
            else if (_tracksuit.Contains(p)) { SetOutfit(ManagerOutfit.Tracksuit); }

            for (int i = 0; i < _flags.Length; i++)
            {
                if (_flags[i].Contains(p))
                {
                    Profile.Nationality = NationFlag.All[i].Code;
                }
            }

            for (int i = 0; i < _avatars.Length; i++)
            {
                if (_avatars[i].Contains(p) && i != Profile.AvatarIndex)
                {
                    Profile.AvatarIndex = i;
                    Rebuild();
                }
            }

            Refresh();
        }

        private void Edit(int field)
        {
            _editing = field;
            if (TouchScreenKeyboard.isSupported)
            {
                string current = field == 0 ? Profile.FirstName : Profile.LastName;
                _keyboard = TouchScreenKeyboard.Open(current, TouchScreenKeyboardType.Default, false, false, false, false,
                    Locale.Get(field == 0 ? "manager.first_name" : "manager.last_name"));
                return;
            }

            // Sin teclado en pantalla (ordenador): se escribe con el teclado físico (ver TypeWithKeyboard).
            _caretTimer = 0f;
            Refresh();
        }

        private const float CaretBlinkSeconds = 0.5f;
        private float _caretTimer;
        private bool _caretOn;

        /// <summary>Escribe en el campo elegido con el teclado de la computadora: letras, espacio, guion y apóstrofe; retroceso borra; Enter termina.</summary>
        private void TypeWithKeyboard(float deltaTime)
        {
            if (_editing < 0)
            {
                return;
            }

            PointerInput input = Host.Input;
            bool changed = false;
            for (int i = 0; i < input.TypedCount; i++)
            {
                char c = input.Typed[i];
                string current = _editing == 0 ? Profile.FirstName : Profile.LastName;
                if (c == '\b')
                {
                    if (current.Length > 0)
                    {
                        SetName(current.Substring(0, current.Length - 1));
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    _editing = -1;
                    changed = true;
                    break;
                }
                else if ((char.IsLetter(c) || c == ' ' || c == '-' || c == '\'') && current.Length < MaxNameLength)
                {
                    SetName(current + c);
                }
            }

            _caretTimer += deltaTime;
            bool on = ((int)(_caretTimer / CaretBlinkSeconds) & 1) == 0;
            if (on != _caretOn || changed)
            {
                _caretOn = on;
                Refresh();
            }
        }

        private void SetName(string name)
        {
            if (_editing == 0)
            {
                Profile.FirstName = name;
            }
            else if (_editing == 1)
            {
                Profile.LastName = name;
            }
            else
            {
                return;
            }

            Refresh();
        }

        private void SetOutfit(ManagerOutfit outfit)
        {
            if (Profile.Outfit != outfit)
            {
                Profile.Outfit = outfit;
                Rebuild();
            }
        }

        private void Rebuild() => _figure.SetLook(ManagerLooks.Look(Profile.AvatarIndex, Profile.Outfit, null));

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength) : trimmed;
        }
    }
}
