using System.Collections.Generic;
using Football.Core.Common;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Textos de la interfaz en español o inglés, leídos de <c>Resources/ui_es.json</c> y <c>ui_en.json</c> (copias de
    /// <c>Data/locale/</c> que hace <c>Tools/build-core.sh</c>). El idioma sale del sistema: inglés si el teléfono está en
    /// inglés, español en cualquier otro caso.
    /// </summary>
    public static class Locale
    {
        private const string Prefix = "ui_";
        private const string Fallback = "es";
        private static Dictionary<string, string> _table;

        /// <summary>Texto de una clave (si falta, la clave entre corchetes, para que se note).</summary>
        /// <param name="key">Clave, por ejemplo <c>menu.play</c>.</param>
        public static string Get(string key)
        {
            if (_table == null)
            {
                _table = Load(Application.systemLanguage == SystemLanguage.English ? "en" : Fallback) ?? Load(Fallback)
                         ?? new Dictionary<string, string>();
            }

            return StringTable.Get(_table, key);
        }

        private static Dictionary<string, string> Load(string language)
        {
            try
            {
                TextAsset text = Resources.Load<TextAsset>(Prefix + language);
                return text != null ? StringTable.Parse(text.text) : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Football: no se pudieron leer los textos '" + language + "' (" + e.Message + ")");
                return null;
            }
        }
    }
}
