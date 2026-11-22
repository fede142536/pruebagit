using System.Collections.Generic;
using System.Globalization;
using Football.Core.Domain;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Ficha del jugador superpuesta a cualquier pantalla (alineación, pausa, carrera): a la izquierda una carta con la media,
    /// el puesto, una silueta, pierna hábil, altura, edad y los 6 índices (RIT, TIR, PAS, REG, DEF, FÍS); al centro los
    /// atributos más destacados con barras; a la derecha un mini campo con los puestos compatibles, el estado físico y la
    /// moral, y las estadísticas acumuladas. Se cierra con cualquier toque. Los textos salen de <c>ui_es/ui_en</c>.
    /// </summary>
    public sealed class PlayerCardPanel
    {
        private const int TopRows = 6;
        private const float BarWidth = 300f;
        private static readonly Color CardGold = new Color(0.78f, 0.66f, 0.32f, 1f);
        private static readonly Color CardDark = new Color(0.07f, 0.08f, 0.11f, 0.97f);

        private readonly Canvas _canvas;
        private readonly Text _ovr, _pos, _name, _body;
        private readonly Text[] _faceValue = new Text[6];
        private readonly Text[] _attrName = new Text[TopRows], _attrValue = new Text[TopRows];
        private readonly Image[] _attrBar = new Image[TopRows];
        private readonly Dictionary<Position, Image> _spots = new Dictionary<Position, Image>();
        private readonly Image _fitness, _morale;
        private readonly Text _fitnessText, _moraleText, _stats;

        /// <summary>La ficha está abierta.</summary>
        public bool Visible => _canvas.gameObject.activeSelf;

        /// <summary>Crea la ficha (oculta) en su propio lienzo, por encima de la pantalla.</summary>
        /// <param name="parent">Padre.</param>
        public PlayerCardPanel(Transform parent)
        {
            _canvas = UiKit.CreateCanvas("FichaJugador", parent, 40);
            Transform root = UiKit.CreateArea(_canvas);
            UiKit.Panel(root, "Velo", UiKit.Reference * 0.5f, UiKit.FullScreenSize, new Color(0f, 0f, 0f, 0.72f));
            UiKit.Panel(root, "Fondo", new Vector2(960f, 540f), new Vector2(1640f, 760f), CardDark);

            // Carta.
            UiKit.Panel(root, "Carta", new Vector2(390f, 540f), new Vector2(420f, 640f), CardGold);
            UiKit.Panel(root, "CartaInterior", new Vector2(390f, 540f), new Vector2(396f, 616f), new Color(0.16f, 0.14f, 0.09f, 1f));
            _ovr = UiKit.Label(root, "Media", new Vector2(260f, 790f), new Vector2(160f, 90f), string.Empty, 80, CardGold);
            _ovr.fontStyle = FontStyle.Bold;
            _pos = UiKit.Label(root, "Puesto", new Vector2(260f, 725f), new Vector2(160f, 50f), string.Empty, 34, Color.white);
            UiKit.Panel(root, "Hombros", new Vector2(450f, 690f), new Vector2(190f, 110f), new Color(0.3f, 0.27f, 0.2f), true);
            UiKit.Panel(root, "Cabeza", new Vector2(450f, 775f), new Vector2(90f, 90f), new Color(0.3f, 0.27f, 0.2f), true);
            _name = UiKit.Label(root, "Nombre", new Vector2(390f, 610f), new Vector2(400f, 56f), string.Empty, 36, Color.white);
            _body = UiKit.Label(root, "Fisico", new Vector2(390f, 560f), new Vector2(400f, 40f), string.Empty, 22, UiTheme.TextDim);
            string[] faceKeys = { "card.pac", "card.sho", "card.pas", "card.dri", "card.def", "card.phy" };
            for (int i = 0; i < 6; i++)
            {
                float x = i < 3 ? 300f : 480f, y = 480f - (i % 3) * 62f;
                _faceValue[i] = UiKit.Label(root, "Indice" + i, new Vector2(x - 40f, y), new Vector2(70f, 50f), string.Empty, 34, Color.white);
                _faceValue[i].fontStyle = FontStyle.Bold;
                UiKit.Label(root, "IndiceNombre" + i, new Vector2(x + 40f, y), new Vector2(90f, 50f), Locale.Get(faceKeys[i]), 28, CardGold);
            }

            // Atributos destacados.
            UiKit.Label(root, "AtributosTitulo", new Vector2(900f, 840f), new Vector2(460f, 50f), Locale.Get("card.attributes"), 30, UiTheme.Accent);
            for (int i = 0; i < TopRows; i++)
            {
                float y = 770f - i * 74f;
                _attrName[i] = UiKit.Label(root, "Atributo" + i, new Vector2(840f, y + 16f), new Vector2(340f, 34f), string.Empty, 24, UiTheme.Text, TextAnchor.MiddleLeft);
                _attrValue[i] = UiKit.Label(root, "Valor" + i, new Vector2(1060f, y + 16f), new Vector2(70f, 34f), string.Empty, 26, Color.white);
                UiKit.Panel(root, "BarraFondo" + i, new Vector2(670f + BarWidth * 0.5f, y - 12f), new Vector2(BarWidth, 10f), UiTheme.Button);
                _attrBar[i] = UiKit.Panel(root, "Barra" + i, new Vector2(670f, y - 12f), new Vector2(BarWidth, 10f), UiTheme.Accent);
                ((RectTransform)_attrBar[i].transform).pivot = new Vector2(0f, 0.5f);
            }

            // Mini campo con los puestos compatibles.
            UiKit.Label(root, "PuestosTitulo", new Vector2(1460f, 840f), new Vector2(460f, 50f), Locale.Get("card.positions"), 30, UiTheme.Accent);
            UiKit.Panel(root, "Campo", new Vector2(1360f, 640f), new Vector2(220f, 320f), new Color(0.12f, 0.4f, 0.2f, 1f));
            UiKit.Panel(root, "MitadCampo", new Vector2(1360f, 640f), new Vector2(220f, 3f), new Color(1f, 1f, 1f, 0.5f));
            foreach (Position p in System.Enum.GetValues(typeof(Position)))
            {
                Vector2 at = new Vector2(1360f, 640f) + SpotOf(p);
                _spots[p] = UiKit.Panel(root, "Puesto" + p, at, new Vector2(30f, 30f), Color.white, true);
            }

            // Estado y estadísticas.
            UiKit.Label(root, "Estado", new Vector2(1640f, 790f), new Vector2(300f, 40f), Locale.Get("card.condition"), 24, UiTheme.TextDim);
            _fitness = Bar(root, new Vector2(1640f, 740f), out _fitnessText, "card.fitness");
            _morale = Bar(root, new Vector2(1640f, 660f), out _moraleText, "card.morale");
            UiKit.Label(root, "EstadisticasTitulo", new Vector2(1500f, 420f), new Vector2(560f, 50f), Locale.Get("card.stats"), 30, UiTheme.Accent);
            _stats = UiKit.Label(root, "Estadisticas", new Vector2(1500f, 330f), new Vector2(560f, 140f), string.Empty, 28, UiTheme.Text);
            UiKit.Label(root, "Cerrar", new Vector2(960f, 190f), new Vector2(800f, 40f), Locale.Get("card.close"), 22, UiTheme.TextMuted);
            _canvas.gameObject.SetActive(false);
        }

        private static Image Bar(Transform root, Vector2 center, out Text label, string key)
        {
            label = UiKit.Label(root, key, center + new Vector2(0f, 26f), new Vector2(300f, 30f), Locale.Get(key), 22, UiTheme.Text);
            UiKit.Panel(root, key + "Fondo", center, new Vector2(260f, 14f), UiTheme.Button);
            Image bar = UiKit.Panel(root, key + "Barra", center - new Vector2(130f, 0f), new Vector2(260f, 14f), UiTheme.Accent);
            ((RectTransform)bar.transform).pivot = new Vector2(0f, 0.5f);
            return bar;
        }

        /// <summary>Muestra la ficha de un jugador.</summary>
        public void Show(PlayerCardInfo card)
        {
            _ovr.text = card.Overall.ToString(CultureInfo.InvariantCulture);
            _pos.text = PlayerLabels.Code(card.Position);
            _name.text = (card.Number > 0 ? card.Number + "  " : string.Empty) + PlayerLabels.Surname(card.Name).ToUpperInvariant();
            _body.text = Locale.Get("card.foot." + (int)card.Foot) + "  ·  " + card.HeightCm + " cm  ·  " + card.Age + " " + Locale.Get("card.years");
            FaceStats f = card.Face;
            int[] values = { f.Pace, f.Shooting, f.Passing, f.Dribbling, f.Defending, f.Physical };
            for (int i = 0; i < 6; i++)
            {
                _faceValue[i].text = values[i].ToString(CultureInfo.InvariantCulture);
                _faceValue[i].color = Grade(values[i]);
            }

            for (int i = 0; i < TopRows; i++)
            {
                bool has = i < card.TopAttributes.Count;
                _attrName[i].text = has ? Locale.Get("attr." + card.TopAttributes[i].Key) : string.Empty;
                int v = has ? card.TopAttributes[i].Value : 0;
                _attrValue[i].text = has ? v.ToString(CultureInfo.InvariantCulture) : string.Empty;
                _attrBar[i].rectTransform.sizeDelta = new Vector2(BarWidth * v / 100f, 10f);
                _attrBar[i].color = Grade(v);
            }

            foreach (KeyValuePair<Position, Image> spot in _spots)
            {
                int rank = card.Compatible.IndexOf(spot.Key);
                spot.Value.color = rank == 0 ? UiTheme.Gold : rank > 0 ? UiTheme.Accent : new Color(1f, 1f, 1f, 0.18f);
            }

            SetBar(_fitness, _fitnessText, "card.fitness", card.Fitness, 1f);
            SetBar(_morale, _moraleText, "card.morale", card.Morale, 100f);
            CardStats s = card.Stats;
            _stats.text = s == null ? Locale.Get("card.no_stats")
                : string.Format(CultureInfo.InvariantCulture, Locale.Get("card.stats_line"), s.Matches, s.Goals, s.Assists,
                    s.Rated > 0 ? s.AverageRating.ToString("0.0", CultureInfo.InvariantCulture) : "-");
            _canvas.gameObject.SetActive(true);
        }

        /// <summary>Cierra la ficha.</summary>
        public void Hide() => _canvas.gameObject.SetActive(false);

        private static void SetBar(Image bar, Text label, string key, float value, float max)
        {
            bool known = value >= 0f;
            bar.rectTransform.sizeDelta = new Vector2(known ? 260f * Mathf.Clamp01(value / max) : 0f, 14f);
            bar.color = Grade(known ? 100f * value / max : 0f);
            label.text = Locale.Get(key) + (known ? "  " + Mathf.RoundToInt(100f * value / max) + "%" : "  -");
        }

        private static Color Grade(float v) =>
            v >= 80f ? new Color(0.35f, 0.9f, 0.45f) : v >= 70f ? new Color(0.75f, 0.9f, 0.35f) : v >= 60f ? new Color(1f, 0.82f, 0.25f) : new Color(0.95f, 0.45f, 0.3f);

        /// <summary>Lugar de cada puesto en el mini campo (el arco propio abajo).</summary>
        private static Vector2 SpotOf(Position p)
        {
            switch (p)
            {
                case Position.Goalkeeper: return new Vector2(0f, -140f);
                case Position.CenterBack: return new Vector2(0f, -90f);
                case Position.LeftBack: return new Vector2(-80f, -80f);
                case Position.RightBack: return new Vector2(80f, -80f);
                case Position.DefensiveMidfielder: return new Vector2(0f, -35f);
                case Position.CentralMidfielder: return new Vector2(0f, 10f);
                case Position.AttackingMidfielder: return new Vector2(0f, 60f);
                case Position.LeftMidfielder: return new Vector2(-80f, 10f);
                case Position.RightMidfielder: return new Vector2(80f, 10f);
                case Position.LeftWinger: return new Vector2(-80f, 90f);
                case Position.RightWinger: return new Vector2(80f, 90f);
                default: return new Vector2(0f, 120f);
            }
        }
    }

    /// <summary>
    /// Detecta un «mantener presionado»: el mismo dedo quieto (menos de unos píxeles) durante un instante. Sin objetos nuevos.
    /// </summary>
    public sealed class HoldDetector
    {
        private const float HoldSeconds = 0.45f;
        private const float MaxDrift = 24f;
        private int _id = -1;
        private Vector2 _start;
        private float _time;
        private bool _fired;

        /// <summary>Actualiza con los toques del fotograma; devuelve verdadero (una vez) cuando se cumple el tiempo.</summary>
        /// <param name="pointers">Entrada.</param>
        /// <param name="deltaTime">Tiempo del fotograma.</param>
        /// <param name="at">Lugar del toque (área de diseño).</param>
        public bool Update(PointerInput pointers, float deltaTime, out Vector2 at)
        {
            at = default;
            bool seen = false;
            for (int i = 0; i < pointers.Count; i++)
            {
                PointerState p = pointers.Pointers[i];
                Vector2 area = UiKit.ToArea(p.Position);
                if (p.Phase == PointerPhase.Began)
                {
                    _id = p.Id;
                    _start = area;
                    _time = 0f;
                    _fired = false;
                    seen = true;
                }
                else if (p.Id == _id && p.Phase != PointerPhase.Ended)
                {
                    seen = true;
                    if ((area - _start).sqrMagnitude > MaxDrift * MaxDrift)
                    {
                        _fired = true; // se movió: no es un «mantener»
                    }
                }
            }

            if (!seen)
            {
                _id = -1;
                return false;
            }

            _time += deltaTime;
            if (_fired || _time < HoldSeconds)
            {
                return false;
            }

            _fired = true;
            at = _start;
            return true;
        }
    }
}
