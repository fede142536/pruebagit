using Football.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Escudo procedural de un club, armado con paneles de la interfaz: silueta de escudo con borde, diseño de la camiseta
    /// titular (bastones, franja, banda, V o mitades) en sus colores y las siglas del club. No reproduce ningún escudo real
    /// (D-12): solo usa los colores y la abreviatura de <c>teams.json</c>. Se crea una vez y se recolorea con <see cref="Set"/>.
    /// </summary>
    public sealed class ClubCrest
    {
        private const int Bars = 3;
        private const float TopShare = 0.62f;     // parte recta del escudo (el resto es la punta)
        private const float BorderShare = 0.06f;

        private readonly RectTransform _root;
        private readonly Image _borderTop, _borderPoint, _top, _point, _band, _sash, _half;
        private readonly Image[] _bars = new Image[Bars];
        private readonly Image[] _chevron = new Image[2];
        private readonly Text _initials;
        private readonly RawImage _real;

        /// <summary>Crea el escudo centrado en un punto.</summary>
        /// <param name="parent">Padre (coordenadas desde su esquina inferior izquierda).</param>
        /// <param name="center">Centro.</param>
        /// <param name="size">Alto del escudo (el ancho es el 82 %).</param>
        public ClubCrest(Transform parent, Vector2 center, float size)
        {
            var go = new GameObject("Escudo", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            float width = size * 0.82f;
            _root.sizeDelta = new Vector2(width, size);
            _root.anchoredPosition = center;

            float topHeight = size * TopShare;
            float border = size * BorderShare;
            Vector2 topCenter = new Vector2(width * 0.5f, size - topHeight * 0.5f);
            float pointSide = width / Mathf.Sqrt(2f);
            Vector2 pointCenter = new Vector2(width * 0.5f, size - topHeight);
            _borderTop = UiKit.Panel(_root, "Borde", topCenter, new Vector2(width, topHeight), Color.black);
            _borderPoint = UiKit.Panel(_root, "BordePunta", pointCenter, new Vector2(pointSide, pointSide), Color.black);
            _borderPoint.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _point = UiKit.Panel(_root, "Punta", pointCenter, new Vector2(pointSide - border * 1.6f, pointSide - border * 1.6f), Color.white);
            _point.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _top = UiKit.Panel(_root, "Campo", topCenter - new Vector2(0f, border * 0.5f), new Vector2(width - border * 2f, topHeight - border), Color.white);
            _top.gameObject.AddComponent<RectMask2D>(); // el diseño no se sale del escudo
            Transform field = _top.transform;
            Vector2 fieldSize = ((RectTransform)field).sizeDelta;
            for (int i = 0; i < Bars; i++)
            {
                _bars[i] = UiKit.Panel(field, "Baston" + i, new Vector2(fieldSize.x * (i + 0.5f) / Bars + fieldSize.x / (Bars * 4f), fieldSize.y * 0.5f),
                    new Vector2(fieldSize.x / (Bars * 2f), fieldSize.y), Color.black);
            }

            _band = UiKit.Panel(field, "Franja", new Vector2(fieldSize.x * 0.5f, fieldSize.y * 0.45f), new Vector2(fieldSize.x, fieldSize.y * 0.28f), Color.black);
            _sash = UiKit.Panel(field, "Banda", fieldSize * 0.5f, new Vector2(fieldSize.x * 0.26f, fieldSize.y * 2f), Color.black);
            _sash.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            _half = UiKit.Panel(field, "Mitad", new Vector2(fieldSize.x * 0.75f, fieldSize.y * 0.5f), new Vector2(fieldSize.x * 0.5f, fieldSize.y), Color.black);
            for (int i = 0; i < _chevron.Length; i++)
            {
                int side = i == 0 ? -1 : 1;
                _chevron[i] = UiKit.Panel(field, "V" + i, new Vector2(fieldSize.x * (0.5f + side * 0.2f), fieldSize.y * 0.6f),
                    new Vector2(fieldSize.x * 0.16f, fieldSize.y * 0.9f), Color.black);
                _chevron[i].transform.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
            }

            _initials = UiKit.Label(_root, "Siglas", new Vector2(width * 0.5f, size * 0.36f), new Vector2(width, size * 0.3f), string.Empty,
                Mathf.Max(10, Mathf.RoundToInt(size * 0.2f)), Color.white);
            _initials.fontStyle = FontStyle.Bold;

            // Escudo real (si el usuario puso la imagen): cuadrado, centrado y por encima del procedural.
            var realObject = new GameObject("EscudoReal", typeof(RectTransform));
            realObject.transform.SetParent(_root, false);
            var realRect = (RectTransform)realObject.transform;
            realRect.anchorMin = realRect.anchorMax = Vector2.zero;
            realRect.pivot = new Vector2(0.5f, 0.5f);
            realRect.sizeDelta = new Vector2(size, size);
            realRect.anchoredPosition = new Vector2(width * 0.5f, size * 0.5f);
            _real = realObject.AddComponent<RawImage>();
            _real.raycastTarget = false;
            _real.gameObject.SetActive(false);
        }

        /// <summary>Raíz del escudo (para moverlo u ocultarlo).</summary>
        public RectTransform Root => _root;

        /// <summary>Pinta el escudo con los colores y las siglas de un club.</summary>
        /// <param name="team">Club.</param>
        public void Set(TeamDefinition team)
        {
            Texture real = CrestLibrary.Find(team);
            _real.texture = real;
            _real.gameObject.SetActive(real != null);
            bool procedural = real == null;
            _borderTop.gameObject.SetActive(procedural);
            _borderPoint.gameObject.SetActive(procedural);
            _point.gameObject.SetActive(procedural);
            _top.gameObject.SetActive(procedural);
            _initials.gameObject.SetActive(procedural);
            if (!procedural)
            {
                return;
            }

            KitColors kit = KitColors.From(team.Home);
            Color dark = Color.Lerp(kit.Secondary, Color.black, 0.55f);
            _borderTop.color = dark;
            _borderPoint.color = dark;
            _top.color = kit.Shirt;
            _point.color = kit.Pattern == KitPattern.Solid ? kit.Secondary : kit.Shirt;
            for (int i = 0; i < Bars; i++)
            {
                _bars[i].color = kit.Secondary;
                _bars[i].gameObject.SetActive(kit.Pattern == KitPattern.Stripes);
            }

            _band.color = kit.Secondary;
            _band.gameObject.SetActive(kit.Pattern == KitPattern.Band || kit.Pattern == KitPattern.Solid);
            _sash.color = kit.Secondary;
            _sash.gameObject.SetActive(kit.Pattern == KitPattern.Sash);
            _half.color = kit.Secondary;
            _half.gameObject.SetActive(kit.Pattern == KitPattern.Halves);
            foreach (Image v in _chevron)
            {
                v.color = kit.Secondary;
                v.gameObject.SetActive(kit.Pattern == KitPattern.Chevron);
            }

            _initials.text = Initials(team);
            // Siglas legibles: blancas sobre fondos oscuros, oscuras sobre los claros.
            _initials.color = kit.Shirt.grayscale > 0.6f ? dark : Color.white;
        }

        /// <summary>Muestra u oculta el escudo.</summary>
        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>Siglas del club: su abreviatura del archivo, o las iniciales de su nombre (máximo 3 letras).</summary>
        public static string Initials(TeamDefinition team)
        {
            if (!string.IsNullOrEmpty(team.ShortName) && team.ShortName.Length <= 4)
            {
                return team.ShortName.ToUpperInvariant();
            }

            string[] words = (team.Name ?? "?").Split(' ');
            string result = string.Empty;
            foreach (string w in words)
            {
                if (w.Length > 2 && char.IsLetter(w[0]) && result.Length < 3)
                {
                    result += char.ToUpperInvariant(w[0]);
                }
            }

            return result.Length > 0 ? result : "?";
        }
    }
}
