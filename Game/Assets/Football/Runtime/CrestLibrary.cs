using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Football.Core.Data;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Escudos reales de los clubes, si el usuario los puso en su proyecto (uso personal): imágenes PNG con fondo transparente en cualquier carpeta <c>Resources/Crests</c>
    /// (la recomendada es <c>Assets/MisEscudos/Resources/Crests</c>, que <c>ACTUALIZAR_JUEGO.bat</c> no toca), con el nombre del club en minúsculas, sin tildes y con guiones bajos
    /// (<c>boca_juniors.png</c>). Si un club no tiene imagen, se usa el escudo procedural de <see cref="ClubCrest"/>. El repositorio no trae ningún escudo (D-12, D-66).
    /// </summary>
    public static class CrestLibrary
    {
        private const string Folder = "Crests/";
        private const char Separator = '_';

        private static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();

        /// <summary>Nombre de archivo (sin extensión) del escudo de un club: minúsculas, sin tildes y con guiones bajos en lugar de espacios y signos.</summary>
        /// <param name="teamName">Nombre completo del club.</param>
        public static string SlugOf(string teamName)
        {
            string decomposed = (teamName ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                bool alnum = c >= 'a' && c <= 'z' || c >= '0' && c <= '9';
                if (alnum)
                {
                    sb.Append(c);
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != Separator)
                {
                    sb.Append(Separator);
                }
            }

            return sb.ToString().Trim(Separator);
        }

        /// <summary>Imagen del escudo real del club, o null si no hay (se busca una sola vez y se guarda).</summary>
        /// <param name="team">Club.</param>
        public static Texture Find(TeamDefinition team)
        {
            string slug = SlugOf(team.Name);
            if (Cache.TryGetValue(slug, out Texture cached))
            {
                return cached;
            }

            Texture found = Resources.Load<Texture2D>(Folder + slug);
            if (found == null)
            {
                Sprite sprite = Resources.Load<Sprite>(Folder + slug); // si el proyecto importa las imágenes como Sprite
                found = sprite != null ? sprite.texture : null;
            }

            Cache[slug] = found;
            return found;
        }
    }
}
