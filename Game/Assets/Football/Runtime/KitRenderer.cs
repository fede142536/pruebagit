using UnityEngine;

namespace Football.Game
{
    /// <summary>Prenda de la equipación a la que se aplica un <see cref="KitData"/>.</summary>
    public enum KitPart
    {
        /// <summary>Camiseta (usa el patrón de la equipación).</summary>
        Shirt,
        /// <summary>Short: color sólido y una única línea lateral fina en el color de detalle.</summary>
        Shorts,
        /// <summary>Medias: color sólido y dos franjas horizontales finas arriba, en el color de detalle.</summary>
        Socks
    }

    /// <summary>
    /// Controlador del material de la equipación (shader <c>Football/Kit</c>): pinta una prenda con los colores de un <see cref="KitData"/> sin crear materiales por jugador. Un solo
    /// <see cref="Material"/> compartido y una sola textura máscara; cada <see cref="Renderer"/> recibe sus colores, su celda de máscara y la grilla por un
    /// <see cref="MaterialPropertyBlock"/> reutilizado (sin asignaciones por llamada).
    /// <para>
    /// Convención de UV de las mallas de prendas: cada prenda ocupa el cuadrado 0–1. En la camiseta, la mitad izquierda (u &lt; 0,5) es el frente y la derecha la espalda,
    /// cada una con el ancho completo (mangas incluidas) en 0–1; en el short y las medias, el cuadrado entero es la prenda, con v hacia arriba. Una malla con otro desarrollo de UV
    /// necesita una máscara pintada para él (se puede pasar al constructor).
    /// </para>
    /// </summary>
    public sealed class KitRenderer
    {
        /// <summary>Celdas del atlas de máscaras: seis camisetas, el short y las medias.</summary>
        public const int CellCount = 8;
        /// <summary>Celda del short.</summary>
        public const int ShortsCell = 6;
        /// <summary>Celda de las medias.</summary>
        public const int SocksCell = 7;

        private const string ShaderName = "FootballKit";
        private const int CellSize = 256;

        private static readonly int PrimaryId = Shader.PropertyToID("_PrimaryColor");
        private static readonly int SecondaryId = Shader.PropertyToID("_SecondaryColor");
        private static readonly int TrimId = Shader.PropertyToID("_TrimColor");
        private static readonly int CellId = Shader.PropertyToID("_MaskCell");
        private static readonly int GridId = Shader.PropertyToID("_GridEnabled");
        private static readonly int MaskId = Shader.PropertyToID("_MaskAtlas");
        private static readonly int CellCountId = Shader.PropertyToID("_CellCount");

        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Material _material;

        /// <summary>Crea el controlador con el material compartido y la máscara indicados.</summary>
        /// <param name="material">Material con el shader <c>Football/Kit</c>.</param>
        /// <param name="maskAtlas">Atlas de máscaras (RGB, <see cref="CellCount"/> celdas horizontales, Point); null usa el pintado por código (<see cref="BuildMaskAtlas"/>).</param>
        public KitRenderer(Material material, Texture2D maskAtlas = null)
        {
            _material = material;
            _material.SetTexture(MaskId, maskAtlas != null ? maskAtlas : BuildMaskAtlas());
            _material.SetFloat(CellCountId, CellCount);
        }

        /// <summary>Material compartido con el que se dibujan todas las prendas (asignarlo al <c>sharedMaterial</c> de cada renderer).</summary>
        public Material Material => _material;

        /// <summary>
        /// Crea el controlador con el shader del juego (<c>Resources/FootballKit</c>) y la máscara pintada por código.
        /// </summary>
        /// <returns>El controlador, o null si el shader no está o no es compatible (el llamador conserva el material plano de siempre).</returns>
        public static KitRenderer CreateDefault()
        {
            Shader shader = Resources.Load<Shader>(ShaderName);
            if (shader == null || !shader.isSupported)
            {
                return null;
            }

            return new KitRenderer(new Material(shader) { name = "Equipación (compartido)" });
        }

        /// <summary>Número de celda de la máscara de una prenda y patrón.</summary>
        /// <param name="part">Prenda.</param>
        /// <param name="pattern">Patrón de la camiseta.</param>
        public static int CellOf(KitPart part, KitPatternId pattern) => part == KitPart.Shirt ? (int)pattern : part == KitPart.Shorts ? ShortsCell : SocksCell;

        /// <summary>
        /// Pinta una prenda con la equipación: tintas, celda de máscara y grilla. No crea materiales ni asigna memoria.
        /// </summary>
        /// <param name="target">Renderer de la prenda (con el material compartido).</param>
        /// <param name="kit">Equipación.</param>
        /// <param name="part">Prenda que dibuja este renderer.</param>
        /// <param name="technicalGrid">Dibuja la grilla técnica (solo vista previa del editor y del selector; en cancha va apagada).</param>
        public void Apply(Renderer target, KitData kit, KitPart part, bool technicalGrid = false)
        {
            target.GetPropertyBlock(_block);
            Color primary = part == KitPart.Shirt ? kit.Primary : part == KitPart.Shorts ? kit.Shorts : kit.Socks;
            _block.SetColor(PrimaryId, primary);
            _block.SetColor(SecondaryId, kit.Secondary);
            _block.SetColor(TrimId, kit.Trim);
            _block.SetFloat(CellId, CellOf(part, kit.Pattern));
            _block.SetFloat(GridId, technicalGrid && part == KitPart.Shirt && kit.Pattern == KitPatternId.PlanoTecnico ? 1f : 0f);
            target.SetPropertyBlock(_block);
        }

        // ------------------------------------------------------------------ máscaras pintadas por código

        // Silueta plana de la camiseta, en coordenadas de media textura (0–1): torso, mangas, cuello y puños. Todo recto, sin curvas.
        private const float TorsoMin = 0.24f, TorsoMax = 0.76f;
        private const float BodyBottom = 0.04f, BodyTop = 0.96f;
        private const float SleeveBottom = 0.62f, SleeveTop = 0.94f;
        private const float CuffWidth = 0.07f;
        private const float NeckHalfWidth = 0.10f, FrontNeckDepth = 0.14f, BackNeckDepth = 0.04f;
        private const int StripeCount = 7;
        private const float DiagonalHalfWidth = 0.10f;
        private const float BandBottom = 0.52f, BandTop = 0.72f;
        private const float NumberBoxMinX = 0.34f, NumberBoxMaxX = 0.66f, NumberBoxMinY = 0.36f, NumberBoxMaxY = 0.74f;
        private const float HalfWidth = 0.5f;
        private const float ShortsLineWidth = 0.035f;
        private const float SocksStripeHeight = 0.035f, SocksStripeTop = 0.94f, SocksStripeGap = 0.065f;
        private const byte On = 255;

        private enum Ink { Outside, Base, Secondary, Trim }

        /// <summary>
        /// Pinta el atlas de máscaras: <see cref="CellCount"/> celdas de <c>256 × 256</c> con un solo canal encendido por píxel (R base, G secundario, B detalle) y bordes netos.
        /// Se crea sin mipmaps, lineal y con filtro Point para que los cortes queden sin suavizar.
        /// </summary>
        public static Texture2D BuildMaskAtlas()
        {
            int width = CellSize * CellCount;
            var pixels = new Color32[width * CellSize];
            for (int cell = 0; cell < CellCount; cell++)
            {
                for (int py = 0; py < CellSize; py++)
                {
                    for (int px = 0; px < CellSize; px++)
                    {
                        float u = (px + 0.5f) / CellSize, v = (py + 0.5f) / CellSize;
                        Ink ink = cell == ShortsCell ? PaintShorts(u) : cell == SocksCell ? PaintSocks(v) : PaintShirt((KitPatternId)cell, u, v);
                        pixels[py * width + cell * CellSize + px] = ink == Ink.Base ? new Color32(On, 0, 0, On) : ink == Ink.Secondary ? new Color32(0, On, 0, On)
                            : ink == Ink.Trim ? new Color32(0, 0, On, On) : new Color32(0, 0, 0, 0);
                    }
                }
            }

            var atlas = new Texture2D(width, CellSize, TextureFormat.RGBA32, false, true) { name = "Máscara de equipaciones", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            return atlas;
        }

        private static Ink PaintShorts(float u) => u < ShortsLineWidth || u > 1f - ShortsLineWidth ? Ink.Trim : Ink.Base;

        private static Ink PaintSocks(float v)
        {
            float first = SocksStripeTop - SocksStripeHeight;
            bool stripe = (v >= first && v <= SocksStripeTop) || (v >= first - SocksStripeGap && v <= first - SocksStripeGap + SocksStripeHeight);
            return stripe ? Ink.Trim : Ink.Base;
        }

        private static Ink PaintShirt(KitPatternId pattern, float u, float v)
        {
            bool back = u >= HalfWidth;
            float x = (back ? u - HalfWidth : u) / HalfWidth;
            bool torso = x >= TorsoMin && x <= TorsoMax && v >= BodyBottom && v <= BodyTop;
            bool sleeve = !(x >= TorsoMin && x <= TorsoMax) && v >= SleeveBottom && v <= SleeveTop;
            if (!torso && !sleeve)
            {
                return Ink.Outside;
            }

            bool contrastSleeves = pattern == KitPatternId.MangasContraste;
            float fromCentre = Mathf.Abs(x - HalfWidth);
            bool collar = fromCentre <= NeckHalfWidth && (back ? v >= BodyTop - BackNeckDepth : v >= BodyTop - FrontNeckDepth + FrontNeckDepth * fromCentre / NeckHalfWidth);
            bool cuff = sleeve && (x <= CuffWidth || x >= 1f - CuffWidth);
            if (collar || cuff)
            {
                return contrastSleeves ? Ink.Secondary : Ink.Trim;
            }

            if (back && InNumberBox(x, v) && pattern != KitPatternId.PlanoTecnico && pattern != KitPatternId.MangasContraste)
            {
                return Ink.Base; // recuadro liso, esquinas rectas, para que el dorsal siempre se lea
            }

            switch (pattern)
            {
                case KitPatternId.Bastones:
                    return torso && (int)((x - TorsoMin) / (TorsoMax - TorsoMin) * StripeCount) % 2 == 1 ? Ink.Secondary : Ink.Base;
                case KitPatternId.BandaDiagonal:
                    float t = (v - BodyBottom) / (BodyTop - BodyBottom);
                    return !back && torso && Mathf.Abs(x - (TorsoMin + (TorsoMax - TorsoMin) * t)) <= DiagonalHalfWidth ? Ink.Secondary : Ink.Base;
                case KitPatternId.FranjaHorizontal:
                    return torso && v >= BandBottom && v <= BandTop ? Ink.Secondary : Ink.Base;
                case KitPatternId.Mitades:
                    return (back ? 1f - x : x) < HalfWidth ? Ink.Base : Ink.Secondary;
                case KitPatternId.MangasContraste:
                    return sleeve ? Ink.Secondary : Ink.Base;
                default:
                    return Ink.Base;
            }
        }

        private static bool InNumberBox(float x, float v) => x >= NumberBoxMinX && x <= NumberBoxMaxX && v >= NumberBoxMinY && v <= NumberBoxMaxY;
    }
}
