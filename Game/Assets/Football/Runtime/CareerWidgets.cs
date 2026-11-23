using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Formato de números y textos de las pantallas de carrera (montos, porcentajes y textos con argumentos).</summary>
    public static class CareerText
    {
        /// <summary>Recorta un nombre largo con «…» para que entre en su columna; el nombre completo siempre que entre.</summary>
        /// <param name="name">Texto.</param>
        /// <param name="max">Máximo de letras.</param>
        public static string Fit(string name, int max) => name.Length <= max ? name : name.Substring(0, max - 1) + "…";

        private const long Million = 1_000_000;
        private const long Thousand = 1_000;

        /// <summary>Monto en millones de pesos («$25 M»).</summary>
        public static string Money(long amount) => "$" + (amount / Million).ToString("N0", CultureInfo.InvariantCulture) + " M";

        /// <summary>Monto con signo, en millones («+$3 M» o «-$1 M»).</summary>
        public static string SignedMoney(long amount) => (amount >= 0 ? "+" : "-") + Money(System.Math.Abs(amount));

        /// <summary>Precio de una entrada, en pesos con separador de miles.</summary>
        public static string Price(long amount) => "$" + amount.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Cantidad entera con separador de miles.</summary>
        public static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Entero sin separadores.</summary>
        public static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Proporción (0–1) como porcentaje entero.</summary>
        public static string Percent(float share) => Mathf.RoundToInt(share * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>Texto de la tabla de idioma con argumentos.</summary>
        public static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Locale.Get(key), args);

        /// <summary>Nombre de una ronda de la copa según cuántos cruces tiene (1 = final, 2 = semifinales, 4 = cuartos…).</summary>
        /// <param name="ties">Cruces de la ronda.</param>
        /// <param name="round">Número de ronda (desde 1).</param>
        public static string CupStage(int ties, int round)
        {
            if (ties == 1 && round > 1)
            {
                return Locale.Get("cup.stage.final");
            }

            switch (ties)
            {
                case 2: return Locale.Get("cup.stage.semis");
                case 4: return Locale.Get("cup.stage.quarters");
                case 8: return Locale.Get("cup.stage.r16");
                case 16: return Locale.Get("cup.stage.r32");
                default: return Format("cup.stage.round", round);
            }
        }

        /// <summary>Resultado de un partido («2 - 1», con los penales si los hubo); «—» si no se jugó.</summary>
        public static string Score(Football.Core.Domain.Fixture f)
        {
            if (!f.IsPlayed)
            {
                return Locale.Get("calendar.pending");
            }

            string score = f.HomeGoals + " - " + f.AwayGoals;
            return f.HomePenalties.HasValue && f.AwayPenalties.HasValue ? score + " (" + f.HomePenalties + "-" + f.AwayPenalties + ")" : score;
        }

        /// <summary>Miles como texto corto («57,2k»).</summary>
        public static string Thousands(int value) => (value / (float)Thousand).ToString("0.#", CultureInfo.InvariantCulture) + "k";
    }

    /// <summary>Colores de estado compartidos (verde, amarillo, rojo).</summary>
    public static class StatusColors
    {
        /// <summary>Bien.</summary>
        public static readonly Color Good = UiTheme.Accent;
        /// <summary>Atención.</summary>
        public static readonly Color Fair = UiTheme.Amber;
        /// <summary>Mal.</summary>
        public static readonly Color Bad = UiTheme.Danger;

        private const float GoodFrom = 0.66f;
        private const float FairFrom = 0.33f;

        /// <summary>Color según una proporción (0–1): rojo, amarillo o verde.</summary>
        public static Color For(float share) => share >= GoodFrom ? Good : share >= FairFrom ? Fair : Bad;
    }

    /// <summary>Barra de progreso horizontal (fondo y relleno) que se llena de izquierda a derecha.</summary>
    public sealed class UiBar
    {
        private static readonly Color Track = new Color(0.165f, 0.231f, 0.204f); // pista gris del manual (neutro 300)
        private readonly Image _track, _fill;
        private readonly Vector2 _center, _size;

        /// <summary>Crea la barra vacía.</summary>
        /// <param name="parent">Padre.</param>
        /// <param name="name">Nombre del objeto.</param>
        /// <param name="center">Centro.</param>
        /// <param name="size">Tamaño total.</param>
        public UiBar(Transform parent, string name, Vector2 center, Vector2 size)
        {
            _center = center;
            _size = size;
            _track = UiKit.Panel(parent, name, center, size, Track);
            _fill = UiKit.Panel(parent, name + "Relleno", center, size, UiTheme.Accent);
            Set(0f, UiTheme.Accent);
        }

        /// <summary>Llena la barra.</summary>
        /// <param name="share">Proporción (0–1).</param>
        /// <param name="color">Color del relleno.</param>
        public void Set(float share, Color color)
        {
            float w = _size.x * Mathf.Clamp01(share);
            RectTransform rect = _fill.rectTransform;
            rect.sizeDelta = new Vector2(w, _size.y);
            rect.anchoredPosition = new Vector2(_center.x - _size.x * 0.5f + w * 0.5f, _center.y);
            _fill.color = color;
        }

        /// <summary>Muestra u oculta la barra.</summary>
        public void SetVisible(bool visible)
        {
            _track.gameObject.SetActive(visible);
            _fill.gameObject.SetActive(visible);
        }
    }

    /// <summary>Gráfico de anillo: porciones radiales sobre un círculo, con un agujero del color del panel.</summary>
    public sealed class DonutChart
    {
        private const float HoleShare = 0.58f;
        private readonly Image[] _slices;

        /// <summary>Crea el anillo con un número fijo de porciones.</summary>
        /// <param name="parent">Padre.</param>
        /// <param name="center">Centro.</param>
        /// <param name="diameter">Diámetro.</param>
        /// <param name="slices">Cantidad de porciones.</param>
        /// <param name="hole">Color del agujero (el del panel de fondo).</param>
        public DonutChart(Transform parent, Vector2 center, float diameter, int slices, Color hole)
        {
            _slices = new Image[slices];
            UiKit.Panel(parent, "AnilloFondo", center, new Vector2(diameter, diameter), new Color(1f, 1f, 1f, 0.08f), true);
            for (int i = 0; i < slices; i++)
            {
                Image slice = UiKit.Panel(parent, "Porcion" + i, center, new Vector2(diameter, diameter), Color.white, true);
                slice.type = Image.Type.Filled;
                slice.fillMethod = Image.FillMethod.Radial360;
                slice.fillOrigin = (int)Image.Origin360.Top;
                slice.fillClockwise = true;
                slice.fillAmount = 0f;
                _slices[i] = slice;
            }

            UiKit.Panel(parent, "AnilloAgujero", center, new Vector2(diameter * HoleShare, diameter * HoleShare), hole, true);
        }

        /// <summary>Reparte el anillo según los valores (los negativos o cero no ocupan lugar).</summary>
        /// <param name="values">Valor de cada porción.</param>
        /// <param name="colors">Color de cada porción.</param>
        public void Set(long[] values, Color[] colors)
        {
            double total = 0;
            for (int i = 0; i < _slices.Length && i < values.Length; i++)
            {
                total += System.Math.Max(0L, values[i]);
            }

            float start = 0f;
            for (int i = 0; i < _slices.Length; i++)
            {
                float share = total > 0 && i < values.Length ? (float)(System.Math.Max(0L, values[i]) / total) : 0f;
                Image slice = _slices[i];
                slice.color = colors[i % colors.Length];
                slice.fillAmount = share;
                slice.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -start * 360f);
                start += share;
            }
        }
    }

    /// <summary>Tabla de textos en filas y columnas (se crea una vez; después solo cambian los textos).</summary>
    public sealed class TextGrid
    {
        private readonly Text[,] _cells;
        private readonly Image[] _marks;
        private readonly float _top, _rowHeight, _left, _right;

        /// <summary>Crea la tabla vacía.</summary>
        /// <param name="parent">Padre.</param>
        /// <param name="name">Nombre de los objetos.</param>
        /// <param name="topLeft">Esquina de arriba a la izquierda (centro de la primera fila en Y).</param>
        /// <param name="rowHeight">Alto de cada fila.</param>
        /// <param name="columnCentres">Centro de cada columna.</param>
        /// <param name="widths">Ancho de cada columna.</param>
        /// <param name="rows">Filas (la primera es el encabezado).</param>
        /// <param name="fontSize">Tamaño de letra.</param>
        public TextGrid(Transform parent, string name, Vector2 topLeft, float rowHeight, float[] columnCentres, float[] widths, int rows, int fontSize)
        {
            _cells = new Text[rows, columnCentres.Length];
            _marks = new Image[rows];
            _top = topLeft.y;
            _rowHeight = rowHeight;
            _left = topLeft.x;
            _right = columnCentres[columnCentres.Length - 1] + widths[widths.Length - 1] * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                float y = RowY(r);
                UiKit.Panel(parent, name + "Linea" + r, new Vector2((_left + _right) * 0.5f, y + rowHeight * 0.5f), new Vector2(_right - _left, 1.5f),
                    new Color(UiTheme.Line.r, UiTheme.Line.g, UiTheme.Line.b, 0.6f)); // separador fino entre filas (tabla del diseño)
                _marks[r] = UiKit.Panel(parent, name + "Fila" + r, new Vector2((_left + _right) * 0.5f, y), new Vector2(_right - _left, rowHeight - 4f),
                    new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.18f));
                _marks[r].gameObject.SetActive(false);
                for (int c = 0; c < columnCentres.Length; c++)
                {
                    _cells[r, c] = UiKit.Label(parent, name + r + "_" + c, new Vector2(columnCentres[c], y), new Vector2(widths[c], rowHeight),
                        string.Empty, fontSize, r == 0 ? UiTheme.AccentText : UiTheme.Text,
                        c == 1 || c == 0 && widths[c] > 200f ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                }
            }
        }

        /// <summary>Altura del centro de una fila.</summary>
        public float RowY(int row) => _top - row * _rowHeight;

        /// <summary>Borra todos los textos y marcas.</summary>
        public void Clear()
        {
            for (int r = 0; r < _cells.GetLength(0); r++)
            {
                _marks[r].gameObject.SetActive(false);
                for (int c = 0; c < _cells.GetLength(1); c++)
                {
                    _cells[r, c].text = string.Empty;
                    _cells[r, c].color = r == 0 ? UiTheme.AccentText : UiTheme.Text;
                }
            }
        }

        /// <summary>Escribe una fila.</summary>
        public void SetRow(int row, params string[] values)
        {
            for (int c = 0; c < values.Length && c < _cells.GetLength(1); c++)
            {
                _cells[row, c].text = values[c];
            }
        }

        /// <summary>Cambia el color de una celda.</summary>
        public void SetColor(int row, int column, Color color) => _cells[row, column].color = color;

        /// <summary>Marca o desmarca una fila.</summary>
        public void Highlight(int row, bool on) => _marks[row].gameObject.SetActive(on);

        /// <summary>Fila tocada, o -1.</summary>
        public int RowAt(Vector2 p)
        {
            if (p.x < _left || p.x > _right)
            {
                return -1;
            }

            int row = Mathf.RoundToInt((_top - p.y) / _rowHeight);
            return row >= 0 && row < _cells.GetLength(0) && Mathf.Abs(RowY(row) - p.y) <= _rowHeight * 0.5f ? row : -1;
        }
    }
}
