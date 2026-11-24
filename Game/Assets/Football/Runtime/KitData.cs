using System;
using Football.Core.Data;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Los seis patrones de camiseta del manual de POTRERO. El número es la celda de la textura máscara (<see cref="KitRenderer"/>).</summary>
    public enum KitPatternId
    {
        /// <summary>Base lisa con grilla técnica (la grilla solo se dibuja en la vista previa del editor), cuello en V y puños en acento.</summary>
        PlanoTecnico = 0,
        /// <summary>Siete bastones verticales de ancho uniforme; en la espalda, recuadro liso para el dorsal.</summary>
        Bastones = 1,
        /// <summary>Banda diagonal recta de hombro a cadera.</summary>
        BandaDiagonal = 2,
        /// <summary>Franja horizontal recta a la altura del pecho.</summary>
        FranjaHorizontal = 3,
        /// <summary>Mitades verticales exactas, 50 % y 50 %.</summary>
        Mitades = 4,
        /// <summary>Torso de un color; mangas, puños y cuello en V del secundario.</summary>
        MangasContraste = 5
    }

    /// <summary>Nombres de archivo de los patrones y conversión desde y hacia <see cref="KitPatternId"/>.</summary>
    public static class KitPatterns
    {
        /// <summary>Identificadores tal como se guardan en el JSON (en el mismo orden que <see cref="KitPatternId"/>).</summary>
        public static readonly string[] Names = { "PLANO_TECNICO", "BASTONES", "BANDA_DIAGONAL", "FRANJA_HORIZONTAL", "MITADES", "MANGAS_CONTRASTE" };

        /// <summary>Cantidad de patrones.</summary>
        public static int Count => Names.Length;

        /// <summary>Nombre de archivo de un patrón.</summary>
        /// <param name="id">Patrón.</param>
        public static string NameOf(KitPatternId id) => Names[(int)id];

        /// <summary>Lee un patrón por su nombre (sin distinguir mayúsculas); si no existe, <see cref="KitPatternId.PlanoTecnico"/>.</summary>
        /// <param name="name">Nombre guardado.</param>
        public static KitPatternId Parse(string name)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (string.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return (KitPatternId)i;
                }
            }

            return KitPatternId.PlanoTecnico;
        }
    }

    /// <summary>Paleta oficial del manual (valores exactos, <c>#RRGGBB</c>). Las equipaciones de los clubes pueden usar cualquier color; esta paleta es la del editor y los valores por defecto.</summary>
    public static class KitPalette
    {
        /// <summary>Fondo / negro noche.</summary>
        public const string NightBlack = "#0f1a17";
        /// <summary>Blanco hueso (texto y base clara).</summary>
        public const string Bone = "#e8efe9";
        /// <summary>Verde césped (acento principal).</summary>
        public const string Grass = "#3fb67a";
        /// <summary>Verde oscuro (selección activa).</summary>
        public const string DarkGreen = "#1a3a2d";
        /// <summary>Gris claro desaturado (alternativa clara).</summary>
        public const string LightGrey = "#c2d3c9";
        /// <summary>Ámbar.</summary>
        public const string Amber = "#e0b04a";
        /// <summary>Rojo coral.</summary>
        public const string Coral = "#e0685a";
        /// <summary>Texto oscuro sobre colores claros o de acento.</summary>
        public const string DarkText = "#0c1a14";
        /// <summary>Líneas finas y grilla.</summary>
        public const string Grid = "#2a3b34";
    }

    /// <summary>
    /// Equipación completa de un equipo, serializable en JSON: patrón y colores (<c>#RRGGBB</c>) de camiseta, short, medias y dorsal. Es solo datos; el dibujo lo hace
    /// <see cref="KitRenderer"/>. Los colores inválidos caen en la paleta del manual en lugar de romper el dibujo.
    /// </summary>
    [Serializable]
    public sealed class KitData
    {
        private const float LuminanceR = 0.2126f, LuminanceG = 0.7152f, LuminanceB = 0.0722f;
        private const float DarkTextBelowLuminance = 0.55f;

        /// <summary>Patrón de la camiseta (uno de <see cref="KitPatterns.Names"/>).</summary>
        public string patternId = "PLANO_TECNICO";
        /// <summary>Color primario (base de la camiseta).</summary>
        public string primaryColorHex = KitPalette.NightBlack;
        /// <summary>Color secundario (franjas, banda, mitad, mangas).</summary>
        public string secondaryColorHex = KitPalette.Bone;
        /// <summary>Color de detalle: cuello, puños, línea del short y franjas de las medias.</summary>
        public string trimColorHex = KitPalette.Grass;
        /// <summary>Color del short.</summary>
        public string shortsColorHex = KitPalette.NightBlack;
        /// <summary>Color de las medias.</summary>
        public string socksColorHex = KitPalette.NightBlack;
        /// <summary>Color plano del dorsal y del apellido (alto contraste con la camiseta).</summary>
        public string numberColorHex = KitPalette.Bone;

        /// <summary>Patrón de la camiseta.</summary>
        public KitPatternId Pattern => KitPatterns.Parse(patternId);

        /// <summary>Color primario.</summary>
        public Color Primary => Parse(primaryColorHex, KitPalette.NightBlack);

        /// <summary>Color secundario.</summary>
        public Color Secondary => Parse(secondaryColorHex, KitPalette.Bone);

        /// <summary>Color de detalle.</summary>
        public Color Trim => Parse(trimColorHex, KitPalette.Grass);

        /// <summary>Color del short.</summary>
        public Color Shorts => Parse(shortsColorHex, KitPalette.NightBlack);

        /// <summary>Color de las medias.</summary>
        public Color Socks => Parse(socksColorHex, KitPalette.NightBlack);

        /// <summary>Color del dorsal.</summary>
        public Color NumberColor => Parse(numberColorHex, KitPalette.Bone);

        /// <summary>Equipación por defecto: la del manual (plano técnico en negro noche con acento verde césped).</summary>
        public static KitData Default() => new KitData();

        /// <summary>Lee una equipación de JSON; si el texto está vacío o roto devuelve la de por defecto.</summary>
        /// <param name="json">Texto JSON.</param>
        public static KitData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Default();
            }

            try
            {
                return JsonUtility.FromJson<KitData>(json) ?? Default();
            }
            catch (ArgumentException)
            {
                return Default();
            }
        }

        /// <summary>Convierte la equipación a JSON.</summary>
        /// <param name="pretty">Con sangrías, para archivos editables a mano.</param>
        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        /// <summary>
        /// Convierte la equipación de los archivos de equipos (<c>teams.json</c>): rayas → bastones, franja → franja horizontal, banda → banda diagonal, mitades → mitades;
        /// la lisa y la V no tienen equivalente y quedan como plano técnico. El detalle toma el color secundario y el dorsal el que más contraste da.
        /// </summary>
        /// <param name="kit">Equipación del archivo de datos.</param>
        public static KitData FromDefinition(KitDefinition kit)
        {
            KitPatternId pattern;
            switch ((kit.Pattern ?? string.Empty).ToLowerInvariant())
            {
                case "stripes": pattern = KitPatternId.Bastones; break;
                case "band": pattern = KitPatternId.FranjaHorizontal; break;
                case "sash": pattern = KitPatternId.BandaDiagonal; break;
                case "halves": pattern = KitPatternId.Mitades; break;
                default: pattern = KitPatternId.PlanoTecnico; break;
            }

            Color shirt = Parse(kit.Shirt, KitPalette.Bone);
            return new KitData
            {
                patternId = KitPatterns.NameOf(pattern), primaryColorHex = kit.Shirt, secondaryColorHex = kit.Secondary, trimColorHex = kit.Secondary,
                shortsColorHex = kit.Shorts, socksColorHex = kit.Socks, numberColorHex = BestNumberColorHex(shirt)
            };
        }

        /// <summary>Color plano del dorsal con más contraste sobre una camiseta: texto oscuro del manual sobre las claras, blanco hueso sobre las oscuras.</summary>
        /// <param name="shirt">Color primario de la camiseta.</param>
        public static string BestNumberColorHex(Color shirt) =>
            LuminanceR * shirt.r + LuminanceG * shirt.g + LuminanceB * shirt.b > DarkTextBelowLuminance ? KitPalette.DarkText : KitPalette.Bone;

        private static Color Parse(string hex, string fallbackHex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c) && !ColorUtility.TryParseHtmlString(fallbackHex, out c))
            {
                c = Color.magenta; // la paleta del manual siempre es válida; el magenta solo delata un error de programación
            }

            return c;
        }
    }
}
