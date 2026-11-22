using UnityEngine;
using UnityEngine.UI;
using Football.Core.Data;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Acción pedida en la pantalla de equipos.</summary>
    public enum TeamSelectAction
    {
        /// <summary>Ninguna.</summary>
        None,
        /// <summary>Volver al menú principal.</summary>
        Back,
        /// <summary>Pasar a los ajustes del partido.</summary>
        Continue,
        /// <summary>Editar la alineación de tu equipo.</summary>
        EditHomeLineup,
        /// <summary>Editar la alineación del rival.</summary>
        EditAwayLineup
    }

    /// <summary>
    /// Elección de equipos para el amistoso, al estilo EA FC: a los costados, un jugador 3D de cuerpo entero con la
    /// equipación elegida; en el centro, dos paneles (TU EQUIPO / RIVAL) con el selector de club y un carrusel de camisetas
    /// (la elegida en grande y la otra en miniatura a los costados).
    /// </summary>
    public sealed class TeamSelectScreen : MenuScreenBase
    {
        private const float PanelWidth = 500f;
        private const float PanelHeight = 800f;
        private const float MainKitScale = 1.15f;
        private const float SideKitScale = 0.45f;
        private static readonly Vector2 KitBoxSize = new Vector2(300f, 330f);

        private sealed class Card
        {
            public UiButton Previous, Next, KitPrevious, KitNext, Lineup;
            public Text Name, Info, Star, KitName, Figure;
            public StarRating Stars;
            public KitPreview Kit, LeftKit, RightKit;
            public KitShowroom Showroom;
        }

        private readonly FriendlySetup _setup;
        private readonly Card[] _cards = new Card[2];
        private readonly UiButton _back;
        private readonly UiButton _continue;
        private readonly Text _clash;

        /// <summary>Acción pedida en este fotograma.</summary>
        public TeamSelectAction Action { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public TeamSelectScreen(Transform parent, PointerInput pointers, FriendlySetup setup) : base(parent, pointers, "EquiposUI")
        {
            _setup = setup;
            Title("PARTIDO AMISTOSO", "Elige los clubes y sus camisetas");
            _cards[0] = BuildCard(new Vector2(700f, 480f), "TU EQUIPO");
            _cards[1] = BuildCard(new Vector2(1220f, 480f), "RIVAL");
            _cards[0].Showroom = new KitShowroom(Root, new Vector2(225f, 520f), new Vector2(360f, 600f), 0, 200f);
            _cards[1].Showroom = new KitShowroom(Root, new Vector2(1695f, 520f), new Vector2(360f, 600f), 1, 160f);
            _cards[0].Figure = UiKit.Label(Root, "FiguraLocal", new Vector2(225f, 195f), new Vector2(400f, 40f), string.Empty, 26, UiTheme.Text);
            _cards[1].Figure = UiKit.Label(Root, "FiguraRival", new Vector2(1695f, 195f), new Vector2(400f, 40f), string.Empty, 26, UiTheme.Text);
            UiKit.Label(Root, "Vs", new Vector2(960f, 785f), new Vector2(120f, 60f), "VS", 44, UiTheme.TextDim);
            _clash = UiKit.Label(Root, "Choque", new Vector2(960f, 55f), new Vector2(1000f, 40f), string.Empty, 24, UiTheme.Warning);
            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
            _continue = UiKit.Button(Root, "Continuar", new Vector2(1680f, 60f), new Vector2(400f, 84f), "CONTINUAR", 38, UiTheme.AccentDark);
        }

        private Card BuildCard(Vector2 center, string heading)
        {
            Image panel = UiKit.Panel(Root, "Tarjeta" + heading, center, new Vector2(PanelWidth, PanelHeight), UiTheme.Panel);
            Transform t = panel.transform;
            Vector2 c = new Vector2(PanelWidth * 0.5f, PanelHeight * 0.5f);
            var card = new Card();
            UiKit.Label(t, "Encabezado", c + new Vector2(0f, 360f), new Vector2(460f, 50f), heading, 34, UiTheme.Accent);
            // Selector de club.
            card.Previous = UiKit.Button(t, "Anterior", c + new Vector2(-200f, 290f), new Vector2(76f, 76f), "<", 44, UiTheme.Button);
            card.Next = UiKit.Button(t, "Siguiente", c + new Vector2(200f, 290f), new Vector2(76f, 76f), ">", 44, UiTheme.Button);
            card.Name = UiKit.Label(t, "Nombre", c + new Vector2(0f, 290f), new Vector2(320f, 80f), string.Empty, 32, UiTheme.Text);
            card.Stars = new StarRating(t, c + new Vector2(0f, 228f));
            card.Info = UiKit.Label(t, "Info", c + new Vector2(0f, 182f), new Vector2(470f, 40f), string.Empty, 24, UiTheme.TextDim);
            card.Star = UiKit.Label(t, "Figura", c + new Vector2(0f, 146f), new Vector2(470f, 40f), string.Empty, 22, UiTheme.TextDim);
            // Carrusel de camisetas: la otra en miniatura a cada lado, la elegida grande al centro.
            card.LeftKit = KitBox(t, c + new Vector2(-185f, -20f), SideKitScale);
            card.RightKit = KitBox(t, c + new Vector2(185f, -20f), SideKitScale);
            card.Kit = KitBox(t, c + new Vector2(0f, -10f), MainKitScale);
            card.KitPrevious = UiKit.Button(t, "CamisetaAnterior", c + new Vector2(-185f, -215f), new Vector2(76f, 70f), "<", 40, UiTheme.Button);
            card.KitNext = UiKit.Button(t, "CamisetaSiguiente", c + new Vector2(185f, -215f), new Vector2(76f, 70f), ">", 40, UiTheme.Button);
            card.KitName = UiKit.Label(t, "NombreCamiseta", c + new Vector2(0f, -215f), new Vector2(260f, 60f), string.Empty, 28, UiTheme.Text);
            card.Lineup = UiKit.Button(t, "Alineacion", c + new Vector2(0f, -330f), new Vector2(420f, 80f), "EDITAR ALINEACIÓN", 28, UiTheme.Button);
            // Las zonas táctiles de los botones de la tarjeta se miden en coordenadas del lienzo.
            Offset(card.Previous, center, c);
            Offset(card.Next, center, c);
            Offset(card.KitPrevious, center, c);
            Offset(card.KitNext, center, c);
            Offset(card.Lineup, center, c);
            return card;
        }

        /// <summary>Camiseta dentro de un recuadro escalado (grande en el centro del carrusel, chica a los lados).</summary>
        private static KitPreview KitBox(Transform parent, Vector2 center, float scale)
        {
            var go = new GameObject("CarruselCamiseta", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = KitBoxSize;
            rect.localScale = new Vector3(scale, scale, 1f);
            return new KitPreview(go.transform, KitBoxSize * 0.5f);
        }

        private static void Offset(UiButton button, Vector2 panelCenter, Vector2 localCenter)
        {
            // Place() dejó el centro en coordenadas locales de la tarjeta; se guarda también el centro absoluto para tocarlo.
            Vector2 local = button.Center;
            button.Center = panelCenter + (local - localCenter);
        }

        /// <inheritdoc />
        public override void Show()
        {
            Action = TeamSelectAction.None;
            base.Show();
            Refresh();
            for (int side = 0; side < 2; side++)
            {
                _cards[side].Showroom.SetVisible(true);
            }
        }

        /// <inheritdoc />
        public override void Hide()
        {
            base.Hide();
            for (int side = 0; side < 2; side++)
            {
                _cards[side].Showroom?.SetVisible(false); // sin pantalla a la vista, las cámaras de las vitrinas no dibujan
            }
        }

        private void Refresh()
        {
            for (int side = 0; side < 2; side++)
            {
                Card card = _cards[side];
                TeamDefinition team = _setup.Definition(side);
                float rating = TeamCatalog.Rating(_setup.Team(side));
                card.Name.text = team.Name;
                card.Stars.Set(rating);
                SquadSelection squad = _setup.Squad(side);
                card.Info.text = "Media " + rating.ToString("F0") + "  ·  " + TeamCatalog.StyleLabel(team.Style) + "  ·  " + squad.Formation.Name;
                PlayerDefinition best = team.Players[0];
                foreach (PlayerDefinition p in team.Players)
                {
                    if (p.Rating > best.Rating)
                    {
                        best = p;
                    }
                }

                card.Star.text = "Figura: " + best.Name + " (" + best.Rating + ")";
                KitColors chosen = _setup.KitOf(side);
                KitColors other = KitColors.From(_setup.Kit(side) == 0 ? team.Away : team.Home);
                card.Kit.Set(chosen);
                card.LeftKit.Set(other);
                card.RightKit.Set(other);
                card.KitName.text = _setup.Kit(side) == 0 ? "TITULAR" : "ALTERNATIVA";
                card.Showroom.SetKit(chosen);
                card.Figure.text = (best.Number > 0 ? best.Number + "  ·  " : string.Empty) + TeamBuilder.DisplayName(best).ToUpperInvariant();
            }

            _clash.text = _setup.KitsClash ? "Las camisetas se parecen: el rival usará la otra para distinguir a los equipos." : string.Empty;
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Action = TeamSelectAction.None;
            for (int side = 0; side < 2; side++)
            {
                _cards[side].Showroom.Update(Time.unscaledDeltaTime);
            }

            if (BackKey)
            {
                Action = TeamSelectAction.Back;
                return;
            }

            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                for (int side = 0; side < 2; side++)
                {
                    Card card = _cards[side];
                    if (card.Previous.Contains(p))
                    {
                        _setup.CycleTeam(side, -1);
                    }
                    else if (card.Next.Contains(p))
                    {
                        _setup.CycleTeam(side, 1);
                    }
                    else if (card.KitPrevious.Contains(p) || card.KitNext.Contains(p))
                    {
                        _setup.ToggleKit(side);
                    }
                    else if (card.Lineup.Contains(p))
                    {
                        Action = side == 0 ? TeamSelectAction.EditHomeLineup : TeamSelectAction.EditAwayLineup;
                    }
                }

                if (_back.Contains(p))
                {
                    Action = TeamSelectAction.Back;
                }
                else if (_continue.Contains(p))
                {
                    Action = TeamSelectAction.Continue;
                }

                Refresh();
            }
        }
    }
}
