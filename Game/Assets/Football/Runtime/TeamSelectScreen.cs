using UnityEngine;
using UnityEngine.UI;
using Football.Core.Data;
using Football.Core.MatchCore;

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

    /// <summary>Elección de equipos para el amistoso: una tarjeta por lado con club, valoración, figura y camiseta.</summary>
    public sealed class TeamSelectScreen : MenuScreenBase
    {
        private sealed class Card
        {
            public UiButton Previous, Next, KitPrevious, KitNext, Lineup;
            public Text Name, Info, Star, KitName;
            public StarRating Stars;
            public KitPreview Kit;
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
            _cards[0] = BuildCard(new Vector2(530f, 500f), "TU EQUIPO");
            _cards[1] = BuildCard(new Vector2(1390f, 500f), "RIVAL");
            UiKit.Label(Root, "Vs", new Vector2(960f, 640f), new Vector2(120f, 80f), "VS", 56, UiTheme.TextDim);
            _clash = UiKit.Label(Root, "Choque", new Vector2(960f, 115f), new Vector2(1400f, 40f), string.Empty, 26, UiTheme.Warning);
            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
            _continue = UiKit.Button(Root, "Continuar", new Vector2(1680f, 60f), new Vector2(400f, 84f), "CONTINUAR", 38, UiTheme.AccentDark);
        }

        private Card BuildCard(Vector2 center, string heading)
        {
            Image panel = UiKit.Panel(Root, "Tarjeta" + heading, center, new Vector2(800f, 780f), UiTheme.Panel);
            Transform t = panel.transform;
            Vector2 c = new Vector2(400f, 390f);
            var card = new Card();
            UiKit.Label(t, "Encabezado", c + new Vector2(0f, 345f), new Vector2(700f, 50f), heading, 34, UiTheme.Accent);
            card.Previous = UiKit.Button(t, "Anterior", c + new Vector2(-330f, 270f), new Vector2(90f, 90f), "<", 50, UiTheme.Button);
            card.Next = UiKit.Button(t, "Siguiente", c + new Vector2(330f, 270f), new Vector2(90f, 90f), ">", 50, UiTheme.Button);
            card.Name = UiKit.Label(t, "Nombre", c + new Vector2(0f, 270f), new Vector2(560f, 80f), string.Empty, 44, UiTheme.Text);
            card.Stars = new StarRating(t, c + new Vector2(0f, 205f));
            card.Info = UiKit.Label(t, "Info", c + new Vector2(0f, 155f), new Vector2(740f, 40f), string.Empty, 28, UiTheme.TextDim);
            card.Star = UiKit.Label(t, "Figura", c + new Vector2(0f, 112f), new Vector2(740f, 40f), string.Empty, 26, UiTheme.TextDim);
            card.Kit = new KitPreview(t, c + new Vector2(0f, -40f));
            card.KitPrevious = UiKit.Button(t, "CamisetaAnterior", c + new Vector2(-230f, -195f), new Vector2(76f, 70f), "<", 40, UiTheme.Button);
            card.KitNext = UiKit.Button(t, "CamisetaSiguiente", c + new Vector2(230f, -195f), new Vector2(76f, 70f), ">", 40, UiTheme.Button);
            card.KitName = UiKit.Label(t, "NombreCamiseta", c + new Vector2(0f, -195f), new Vector2(360f, 60f), string.Empty, 30, UiTheme.Text);
            card.Lineup = UiKit.Button(t, "Alineacion", c + new Vector2(0f, -310f), new Vector2(460f, 80f), "EDITAR ALINEACIÓN", 30, UiTheme.Button);
            // Las zonas táctiles de los botones de la tarjeta se miden en coordenadas del lienzo.
            Offset(card.Previous, center, c);
            Offset(card.Next, center, c);
            Offset(card.KitPrevious, center, c);
            Offset(card.KitNext, center, c);
            Offset(card.Lineup, center, c);
            return card;
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
                card.Kit.Set(_setup.KitOf(side));
                card.KitName.text = _setup.Kit(side) == 0 ? "TITULAR" : "ALTERNATIVA";
            }

            _clash.text = _setup.KitsClash ? "Las camisetas se parecen: el rival usará la otra para distinguir a los equipos." : string.Empty;
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Action = TeamSelectAction.None;
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
