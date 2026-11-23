using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Vista común de una alineación (la de la pantalla «Alineación» del amistoso): a la izquierda la cancha con los once titulares como fichas
    /// (media dentro, puesto arriba y apellido abajo), arriba a la derecha el dibujo con sus flechas y la media del equipo, y en dos columnas los
    /// suplentes y los no convocados. La usan la alineación del amistoso, la de la carrera y la presentación previa al partido, para que se vean
    /// igual en todo el juego. Solo dibuja: cada pantalla le pone los datos y decide qué hace cada toque.
    /// </summary>
    public sealed class LineupBoard
    {
        /// <summary>Una ficha de titular sobre la cancha.</summary>
        public sealed class Token
        {
            /// <summary>Círculo tocable con la media.</summary>
            public UiButton Circle;
            /// <summary>Código del puesto, arriba de la ficha.</summary>
            public Text Role;
            /// <summary>Apellido, debajo de la ficha.</summary>
            public Text Name;
            /// <summary>Dorsal pequeño al costado (opcional).</summary>
            public Text Extra;
        }

        private const float PitchCenterX = 520f, PitchCenterY = 505f, PitchWidth = 820f, PitchHeight = 790f;
        private const float BenchX = 1265f, ReserveX = 1675f, RowTop = 880f, RowStep = 56f, RowWidth = 390f, RowHeight = 50f;
        private const float TokenSize = 92f;

        private readonly Vector2 _scale, _offset;
        private readonly float _size;

        /// <summary>Fichas de los titulares.</summary>
        public readonly Token[] Tokens = new Token[11];
        /// <summary>Filas de suplentes.</summary>
        public readonly UiButton[] Bench;
        /// <summary>Filas de no convocados.</summary>
        public readonly UiButton[] Reserves;
        /// <summary>Título («ALINEACIÓN · CLUB»).</summary>
        public readonly Text Title;
        /// <summary>Ayuda bajo el título.</summary>
        public readonly Text Hint;
        /// <summary>Dibujo actual.</summary>
        public readonly Text Formation;
        /// <summary>Media del once.</summary>
        public readonly Text Rating;
        /// <summary>Encabezado de la columna de no convocados.</summary>
        public readonly Text ReservesHeader;
        /// <summary>Encabezado de la columna de suplentes.</summary>
        public readonly Text BenchHeader;
        /// <summary>Flecha para el dibujo anterior.</summary>
        public readonly UiButton FormationPrevious;
        /// <summary>Flecha para el dibujo siguiente.</summary>
        public readonly UiButton FormationNext;
        /// <summary>Botón «Automática».</summary>
        public readonly UiButton Auto;
        /// <summary>Botón principal («Listo» o «Volver»).</summary>
        public readonly UiButton Done;

        /// <summary>Crea la vista.</summary>
        /// <param name="parent">Dónde se dibuja.</param>
        /// <param name="benchRows">Filas de suplentes.</param>
        /// <param name="reserveRows">Filas de no convocados.</param>
        /// <param name="positionScale">Escala de las posiciones (x, y) respecto del lienzo completo; (1, 1) ocupa toda la pantalla.</param>
        /// <param name="positionOffset">Desplazamiento de las posiciones, ya escaladas.</param>
        /// <param name="sizeScale">Escala de los tamaños de fichas, filas y letras.</param>
        /// <param name="withButtons">Dibuja los botones «Automática» y «Listo».</param>
        public LineupBoard(Transform parent, int benchRows, int reserveRows, Vector2 positionScale, Vector2 positionOffset, float sizeScale, bool withButtons)
        {
            _scale = positionScale;
            _offset = positionOffset;
            _size = sizeScale;
            Bench = new UiButton[benchRows];
            Reserves = new UiButton[reserveRows];

            UiKit.Panel(parent, "AcentoAlineacion", At(new Vector2(68f, 1005f)), Sz(8f, 64f), UiTheme.Accent);
            Title = UiKit.Label(parent, "TituloAlineacion", At(new Vector2(600f, 1005f)), Sz(1000f, 80f), string.Empty, Font(54), UiTheme.Text, TextAnchor.MiddleLeft);
            Hint = UiKit.Label(parent, "AyudaAlineacion", At(new Vector2(600f, 950f)), Sz(1000f, 40f), string.Empty, Font(26), UiTheme.TextDim, TextAnchor.MiddleLeft);
            BuildPitch(parent);

            FormationPrevious = UiKit.Button(parent, "FormacionAnterior", At(new Vector2(1180f, 990f)), Sz(80f, 76f), "<", Font(40), UiTheme.Button);
            Formation = UiKit.Label(parent, "Formacion", At(new Vector2(1340f, 990f)), Sz(220f, 76f), string.Empty, Font(40), UiTheme.Text);
            FormationNext = UiKit.Button(parent, "FormacionSiguiente", At(new Vector2(1500f, 990f)), Sz(80f, 76f), ">", Font(40), UiTheme.Button);
            Rating = UiKit.Label(parent, "MediaEquipo", At(new Vector2(1740f, 990f)), Sz(300f, 76f), string.Empty, Font(30), UiTheme.TextDim);

            BenchHeader = UiKit.Label(parent, "Suplentes", At(new Vector2(BenchX, 930f)), Sz(380f, 44f), "SUPLENTES", Font(30), UiTheme.AccentText);
            ReservesHeader = UiKit.Label(parent, "Reservas", At(new Vector2(ReserveX, 930f)), Sz(380f, 44f), "NO CONVOCADOS", Font(30), UiTheme.AccentText);
            for (int i = 0; i < benchRows; i++)
            {
                Bench[i] = Row("Suplente" + i, parent, BenchX, i);
            }

            for (int i = 0; i < reserveRows; i++)
            {
                Reserves[i] = Row("Reserva" + i, parent, ReserveX, i);
            }

            for (int i = 0; i < Tokens.Length; i++)
            {
                var token = new Token();
                token.Circle = UiKit.Button(parent, "Titular" + i, Vector2.zero, new Vector2(TokenSize * _size, TokenSize * _size), string.Empty, Font(32), Color.white, true);
                token.Role = UiKit.Label(parent, "Puesto" + i, Vector2.zero, new Vector2(120f * _size, 30f * _size), string.Empty, Font(20), UiTheme.TextDim);
                token.Name = UiKit.Label(parent, "NombreTitular" + i, Vector2.zero, new Vector2(200f * _size, 30f * _size), string.Empty, Font(24), UiTheme.Text);
                Tokens[i] = token;
            }

            if (withButtons)
            {
                Auto = UiKit.Button(parent, "Automatica", At(new Vector2(BenchX, 55f)), Sz(380f, 80f), "AUTOMÁTICA", Font(32), UiTheme.Button);
                Done = UiKit.Button(parent, "Listo", At(new Vector2(ReserveX, 55f)), Sz(380f, 80f), "LISTO", Font(36), UiTheme.AccentDark);
            }
        }

        private Vector2 At(Vector2 p) => new Vector2(p.x * _scale.x, p.y * _scale.y) + _offset;

        private Vector2 Sz(float w, float h) => new Vector2(w * _size, h * _size);

        private int Font(int size) => Mathf.Max(10, Mathf.RoundToInt(size * _size));

        private UiButton Row(string name, Transform parent, float x, int index)
        {
            UiButton row = UiKit.Button(parent, name, At(new Vector2(x, RowTop - index * RowStep)), new Vector2(RowWidth * _size, RowHeight * Mathf.Min(1f, _scale.y)), string.Empty, Font(24), UiTheme.Button);
            row.Label.alignment = TextAnchor.MiddleLeft;
            return row;
        }

        private void BuildPitch(Transform parent)
        {
            var grass = new Color(0.1f, 0.36f, 0.16f, 0.95f);
            var line = new Color(1f, 1f, 1f, 0.35f);
            Vector2 center = At(new Vector2(PitchCenterX, PitchCenterY));
            Vector2 size = new Vector2(PitchWidth * _scale.x, PitchHeight * _scale.y);
            UiKit.Panel(parent, "Cancha", center, size, grass);
            UiKit.Panel(parent, "LineaMedia", center, new Vector2(size.x, 3f), line);
            UiKit.Panel(parent, "AreaPropia", center + new Vector2(0f, -size.y * 0.5f + 70f * _scale.y), new Vector2(380f * _scale.x, 3f), line);
            UiKit.Panel(parent, "AreaRival", center + new Vector2(0f, size.y * 0.5f - 70f * _scale.y), new Vector2(380f * _scale.x, 3f), line);
            UiKit.Label(parent, "Ataque", center + new Vector2(0f, size.y * 0.5f - 22f * _scale.y), new Vector2(400f, 30f), "ATAQUE", Font(20), line);
        }

        /// <summary>Punto de la cancha de un puesto: avance 0–0,75 de la propia portería hacia arriba y lado positivo = izquierda del equipo.</summary>
        /// <param name="advance">Avance del puesto (<c>FormationSlot.X</c>).</param>
        /// <param name="side">Lado del puesto (<c>FormationSlot.Y</c>).</param>
        public Vector2 SlotPosition(float advance, float side)
        {
            float y = PitchCenterY - PitchHeight * 0.5f + 70f + Mathf.Clamp01(advance / 0.75f) * (PitchHeight - 150f);
            float x = PitchCenterX - side * (PitchWidth * 0.5f - 90f);
            return At(new Vector2(x, y));
        }

        /// <summary>Pone una ficha de titular en su puesto con sus datos.</summary>
        /// <param name="i">Número de ficha (0–10).</param>
        /// <param name="position">Posición de la ficha (de <see cref="SlotPosition"/>).</param>
        /// <param name="role">Código del puesto.</param>
        /// <param name="name">Apellido.</param>
        /// <param name="rating">Media.</param>
        /// <param name="fill">Color de la ficha.</param>
        /// <param name="ratingColor">Color del número.</param>
        public void SetToken(int i, Vector2 position, string role, string name, int rating, Color fill, Color ratingColor)
        {
            Token token = Tokens[i];
            token.Circle.SetVisible(true);
            token.Role.gameObject.SetActive(true);
            token.Name.gameObject.SetActive(true);
            token.Circle.Place(position, token.Circle.Size);
            token.Circle.Background.color = fill;
            token.Circle.Label.text = rating.ToString();
            token.Circle.Label.color = ratingColor;
            Place(token.Role, position + new Vector2(0f, 62f * _size));
            token.Role.text = role;
            Place(token.Name, position + new Vector2(0f, -64f * _size));
            token.Name.text = name;
        }

        /// <summary>Oculta las fichas que sobran (formaciones de menos de once).</summary>
        /// <param name="used">Fichas en uso.</param>
        public void HideTokensFrom(int used)
        {
            for (int i = used; i < Tokens.Length; i++)
            {
                Tokens[i].Circle.SetVisible(false);
                Tokens[i].Role.gameObject.SetActive(false);
                Tokens[i].Name.gameObject.SetActive(false);
            }
        }

        /// <summary>Muestra u oculta todo el tablero (los textos y filas se ocultan con su panel padre).</summary>
        public static void Place(Text label, Vector2 position)
        {
            ((RectTransform)label.transform).anchoredPosition = position;
        }

        /// <summary>Color legible para el número de una ficha según el color de fondo.</summary>
        /// <param name="background">Color de la ficha.</param>
        public static Color ReadableOn(Color background)
        {
            float brightness = background.r * 0.3f + background.g * 0.59f + background.b * 0.11f;
            return brightness > 0.6f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
        }
    }
}
