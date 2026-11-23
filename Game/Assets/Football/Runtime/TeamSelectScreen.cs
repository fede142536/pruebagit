using System;
using UnityEngine;
using UnityEngine.UI;
using Football.Core.Common;
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

    /// <summary>
    /// Elección de clubes para el amistoso. Arriba, las pestañas TU EQUIPO / EQUIPO RIVAL y el botón ALEATORIO. A la izquierda,
    /// una grilla de tarjetas cuadradas con el escudo procedural, el nombre y la media (OVR) de cada club; la elegida se
    /// resalta. Con los 30 clubes de Primera la grilla se pagina (16 por página, con flechas y contador) y se puede filtrar por
    /// jerarquía (TODOS, GRANDES, MEDIA TABLA, EN DESARROLLO). A la derecha, el escudo grande, el nombre, las estrellas, las camisetas CASA y VISITANTE lado a lado (se
    /// alternan con un toque) y el jugador 3D con la camiseta elegida. Sin lógica de juego: solo cambia la elección.
    /// </summary>
    public sealed class TeamSelectScreen : MenuScreenBase
    {
        private const int Columns = 4;
        private const float CardSize = 168f;
        private const float CardGap = 14f;
        private const float GridLeft = 70f;
        private const float GridTop = 880f;
        private const int Rows = 4;
        private const int PageSize = Columns * Rows;
        private const float PagerY = 125f;
        private static readonly string[] FilterTiers = { null, "Big", "Mid", "Development" };
        private static readonly string[] FilterLabels = { "TODOS", "GRANDES", "MEDIA TABLA", "EN DESARROLLO" };
        private const float KitScale = 0.62f;
        private static readonly Vector2 KitBoxSize = new Vector2(300f, 330f);
        private static readonly Color CardColor = new Color(0.12f, 0.15f, 0.2f, 0.92f);

        private sealed class ClubCard
        {
            public UiButton Button;
            public Image Frame;
            public ClubCrest Crest;
            public Text Name, Rating;

            /// <summary>Mueve toda la tarjeta (marco, botón, escudo y textos) a otro lugar de la grilla.</summary>
            public void Move(Vector2 center)
            {
                Frame.rectTransform.anchoredPosition = center;
                Button.Place(center, new Vector2(CardSize, CardSize));
                Crest.Root.anchoredPosition = center + new Vector2(0f, 18f);
                Name.rectTransform.anchoredPosition = center + new Vector2(0f, -52f);
                Rating.rectTransform.anchoredPosition = center + new Vector2(CardSize * 0.32f, CardSize * 0.36f);
            }

            /// <summary>Muestra u oculta toda la tarjeta.</summary>
            public void SetVisible(bool visible)
            {
                Button.SetVisible(visible);
                Crest.SetVisible(visible);
                Name.gameObject.SetActive(visible);
                Rating.gameObject.SetActive(visible);
                if (!visible)
                {
                    Frame.gameObject.SetActive(false);
                }
            }
        }

        private readonly FriendlySetup _setup;
        private readonly ClubCard[] _clubs;
        private readonly UiButton[] _sideTabs = new UiButton[2];
        private readonly UiButton _random, _back, _continue, _lineup;
        private readonly UiButton[] _kitButtons = new UiButton[2];
        private readonly Image[] _kitFrames = new Image[2];
        private readonly KitPreview[] _kits = new KitPreview[2];
        private readonly ClubCrest _bigCrest;
        private readonly Text _name, _info, _versus, _clash;
        private readonly StarRating _stars;
        private readonly KitShowroom _showroom;
        private readonly IRandom _randomSource = new SeededRandom((ulong)DateTime.Now.Ticks);
        private readonly UiButton _prevPage, _nextPage, _filter;
        private readonly Text _pageLabel;
        private readonly System.Collections.Generic.List<int> _shown = new System.Collections.Generic.List<int>();
        private int _side, _page, _filterIndex;

        /// <summary>Acción pedida en este fotograma.</summary>
        public TeamSelectAction Action { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public TeamSelectScreen(Transform parent, PointerInput pointers, FriendlySetup setup) : base(parent, pointers, "EquiposUI")
        {
            _setup = setup;
            Title("PARTIDO AMISTOSO");
            _sideTabs[0] = UiKit.Button(Root, "TuEquipo", new Vector2(1050f, 1005f), new Vector2(300f, 70f), "TU EQUIPO", 30, UiTheme.Button);
            _sideTabs[1] = UiKit.Button(Root, "Rival", new Vector2(1370f, 1005f), new Vector2(300f, 70f), "EQUIPO RIVAL", 30, UiTheme.Button);
            _random = UiKit.Button(Root, "Aleatorio", new Vector2(1720f, 1005f), new Vector2(280f, 70f), "ALEATORIO", 30, UiTheme.AccentDark);
            _filter = UiKit.Button(Root, "Filtro", new Vector2(720f, 1005f), new Vector2(300f, 70f), FilterLabels[0], 22, UiTheme.Button);
            _prevPage = UiKit.Button(Root, "PaginaAnterior", new Vector2(250f, PagerY), new Vector2(110f, 50f), "<", 30, UiTheme.Button);
            _nextPage = UiKit.Button(Root, "PaginaSiguiente", new Vector2(610f, PagerY), new Vector2(110f, 50f), ">", 30, UiTheme.Button);
            _pageLabel = UiKit.Label(Root, "Pagina", new Vector2(430f, PagerY), new Vector2(200f, 40f), string.Empty, 24, UiTheme.TextDim);

            TeamDefinition[] teams = TeamCatalog.Teams;
            _clubs = new ClubCard[teams.Length];
            for (int i = 0; i < teams.Length; i++)
            {
                _clubs[i] = BuildClubCard(i, teams[i]);
            }

            // Panel derecho: escudo grande, nombre, estrellas, camisetas CASA / VISITANTE y jugador 3D.
            UiTheme.Frame(Root, "Panel", new Vector2(1390f, 520f), new Vector2(940f, 820f));
            _bigCrest = new ClubCrest(Root, new Vector2(1060f, 800f), 210f);
            _name = UiKit.Label(Root, "Nombre", new Vector2(1430f, 850f), new Vector2(560f, 70f), string.Empty, 42, UiTheme.Text, TextAnchor.MiddleLeft);
            _stars = new StarRating(Root, new Vector2(1280f, 785f));
            _info = UiKit.Label(Root, "Info", new Vector2(1430f, 730f), new Vector2(560f, 40f), string.Empty, 24, UiTheme.TextDim, TextAnchor.MiddleLeft);
            string[] kitNames = { "CASA", "VISITANTE" };
            for (int k = 0; k < 2; k++)
            {
                var center = new Vector2(1060f + k * 260f, 420f);
                _kitFrames[k] = UiKit.Panel(Root, "MarcoCamiseta" + k, center, new Vector2(230f, 320f), UiTheme.Button);
                _kits[k] = KitBox(Root, center + new Vector2(0f, 20f));
                _kitButtons[k] = UiKit.Button(Root, "Camiseta" + k, center + new Vector2(0f, -130f), new Vector2(210f, 50f), kitNames[k], 24, UiTheme.Button);
                _kitButtons[k].Size = new Vector2(230f, 320f); // todo el recuadro es tocable
                _kitButtons[k].Center = center;
            }

            _showroom = new KitShowroom(Root, new Vector2(1680f, 470f), new Vector2(300f, 520f), 0, 200f);
            _lineup = UiKit.Button(Root, "Alineacion", new Vector2(1390f, 170f), new Vector2(460f, 76f), "EDITAR ALINEACIÓN", 28, UiTheme.Button);
            _versus = UiKit.Label(Root, "Versus", new Vector2(960f, 115f), new Vector2(1400f, 40f), string.Empty, 26, UiTheme.Text);
            _clash = UiKit.Label(Root, "Choque", new Vector2(960f, 82f), new Vector2(1400f, 30f), string.Empty, 22, UiTheme.Warning);
            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
            _continue = UiKit.Button(Root, "Continuar", new Vector2(1680f, 60f), new Vector2(400f, 84f), "CONTINUAR", 38, UiTheme.AccentDark);
        }

        private static Vector2 SlotCenter(int slot) =>
            new Vector2(GridLeft + CardSize * 0.5f + (slot % Columns) * (CardSize + CardGap), GridTop - CardSize * 0.5f - (slot / Columns) * (CardSize + CardGap));

        private ClubCard BuildClubCard(int index, TeamDefinition team)
        {
            Vector2 center = SlotCenter(index % PageSize);
            var card = new ClubCard
            {
                Frame = UiKit.Panel(Root, "Marco" + index, center, new Vector2(CardSize + 8f, CardSize + 8f), UiTheme.Accent)
            };
            card.Button = UiKit.Button(Root, "Club" + index, center, new Vector2(CardSize, CardSize), string.Empty, 10, CardColor);
            card.Crest = new ClubCrest(Root, center + new Vector2(0f, 18f), CardSize * 0.5f);
            card.Crest.Set(team);
            card.Name = UiKit.Label(Root, "NombreClub" + index, center + new Vector2(0f, -52f), new Vector2(CardSize - 8f, 30f),
                TeamCatalog.ScoreboardName(team), 18, UiTheme.Text);
            card.Rating = UiKit.Label(Root, "Media" + index, center + new Vector2(CardSize * 0.32f, CardSize * 0.36f), new Vector2(50f, 30f),
                Mathf.RoundToInt(TeamCatalog.Rating(index)).ToString(), 22, UiTheme.Gold);
            return card;
        }

        /// <summary>Camiseta dentro de un recuadro escalado.</summary>
        private static KitPreview KitBox(Transform parent, Vector2 center)
        {
            var go = new GameObject("Camiseta", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = KitBoxSize;
            rect.localScale = new Vector3(KitScale, KitScale, 1f);
            return new KitPreview(go.transform, KitBoxSize * 0.5f);
        }

        /// <inheritdoc />
        public override void Show()
        {
            Action = TeamSelectAction.None;
            base.Show();
            Refresh();
            FollowChosen();
            Refresh();
            _showroom.SetVisible(true);
        }

        /// <inheritdoc />
        public override void Hide()
        {
            base.Hide();
            _showroom?.SetVisible(false); // sin pantalla a la vista, la cámara de la vitrina no dibuja
        }

        /// <summary>Pasa a la página donde está el club elegido (si el filtro lo deja a la vista).</summary>
        private void FollowChosen()
        {
            int position = _shown.IndexOf(_setup.Team(_side));
            if (position < 0 && _filterIndex != 0)
            {
                _filterIndex = 0; // el club elegido quedó fuera del filtro: se vuelve a ver todos
                Refresh();
                position = _shown.IndexOf(_setup.Team(_side));
            }

            if (position >= 0)
            {
                _page = position / PageSize;
            }
        }

        private void Refresh()
        {
            int chosen = _setup.Team(_side), other = _setup.Team(1 - _side);
            _shown.Clear();
            for (int i = 0; i < _clubs.Length; i++)
            {
                if (FilterTiers[_filterIndex] == null || TeamCatalog.Teams[i].Club?.Tier == FilterTiers[_filterIndex])
                {
                    _shown.Add(i);
                }
            }

            int pages = Mathf.Max(1, (_shown.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _pageLabel.text = (_page + 1) + " / " + pages + "  ·  " + _shown.Count;
            _filter.Label.text = FilterLabels[_filterIndex];
            _prevPage.SetVisible(pages > 1);
            _nextPage.SetVisible(pages > 1);
            for (int i = 0; i < _clubs.Length; i++)
            {
                ClubCard card = _clubs[i];
                int position = _shown.IndexOf(i);
                bool onPage = position >= _page * PageSize && position < (_page + 1) * PageSize;
                card.SetVisible(onPage);
                if (!onPage)
                {
                    continue;
                }

                card.Move(SlotCenter(position - _page * PageSize));
                card.Frame.gameObject.SetActive(i == chosen);
                card.Button.Background.color = i == chosen ? UiTheme.Selected : i == other ? new Color(0.08f, 0.09f, 0.12f, 0.92f) : CardColor;
                card.Name.color = i == other ? UiTheme.TextMuted : UiTheme.Text;
            }

            for (int s = 0; s < 2; s++)
            {
                _sideTabs[s].Background.color = s == _side ? UiTheme.Selected : UiTheme.Button;
            }

            TeamDefinition team = _setup.Definition(_side);
            float rating = TeamCatalog.Rating(chosen);
            _bigCrest.Set(team);
            _name.text = team.Name;
            _stars.Set(rating);
            _info.text = "Media " + rating.ToString("F0") + "  ·  " + TeamCatalog.StyleLabel(team.Style) + "  ·  " + _setup.Squad(_side).Formation.Name;
            int kit = _setup.Kit(_side);
            _kits[0].Set(KitColors.From(team.Home));
            _kits[1].Set(KitColors.From(team.Away));
            for (int k = 0; k < 2; k++)
            {
                _kitFrames[k].color = k == kit ? UiTheme.Selected : UiTheme.Button;
                _kitButtons[k].Background.color = k == kit ? UiTheme.AccentDark : UiTheme.Button;
            }

            _showroom.SetKit(_setup.KitOf(_side));
            _versus.text = _setup.Definition(0).Name.ToUpperInvariant() + "   VS   " + _setup.Definition(1).Name.ToUpperInvariant();
            _clash.text = _setup.KitsClash ? "Las camisetas se parecen: el rival usará la otra para distinguir a los equipos." : string.Empty;
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Action = TeamSelectAction.None;
            _showroom.Update(Time.unscaledDeltaTime);
            if (BackKey)
            {
                Action = TeamSelectAction.Back;
                return;
            }

            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                for (int s = 0; s < 2; s++)
                {
                    if (_sideTabs[s].Contains(p))
                    {
                        _side = s;
                    }

                    if (_kitButtons[s].Contains(p))
                    {
                        _setup.SetKit(_side, s);
                    }
                }

                for (int i = 0; i < _clubs.Length; i++)
                {
                    if (_clubs[i].Button.Root.activeSelf && _clubs[i].Button.Contains(p))
                    {
                        _setup.SetTeam(_side, i);
                    }
                }

                if (_filter.Contains(p))
                {
                    _filterIndex = (_filterIndex + 1) % FilterTiers.Length;
                    _page = 0;
                }
                else if (_prevPage.Root.activeSelf && _prevPage.Contains(p))
                {
                    _page--;
                }
                else if (_nextPage.Root.activeSelf && _nextPage.Contains(p))
                {
                    _page++;
                }
                else if (_random.Contains(p))
                {
                    int count = TeamCatalog.Teams.Length;
                    for (int tries = 0; tries < count && !_setup.SetTeam(_side, _randomSource.NextInt(0, count)); tries++)
                    {
                    }

                    FollowChosen();
                }
                else if (_lineup.Contains(p))
                {
                    Action = _side == 0 ? TeamSelectAction.EditHomeLineup : TeamSelectAction.EditAwayLineup;
                }
                else if (_back.Contains(p))
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
