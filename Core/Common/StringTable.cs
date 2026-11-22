using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Football.Core.Common
{
    /// <summary>
    /// Textos de interfaz en un objeto JSON plano (<c>{"clave": "texto", ...}</c>), como <c>Data/locale/ui.es.json</c>.
    /// Lector propio y mínimo: el núcleo no depende de bibliotecas de JSON y Unity no lee diccionarios con JsonUtility.
    /// </summary>
    public static class StringTable
    {
        /// <summary>Lee un objeto JSON plano de cadenas.</summary>
        /// <param name="json">Texto del archivo.</param>
        /// <returns>Diccionario clave → texto.</returns>
        /// <exception cref="FormatException">Si el texto no es un objeto plano de cadenas.</exception>
        public static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;
            SkipSpace(json, ref i);
            Expect(json, ref i, '{');
            SkipSpace(json, ref i);
            if (Peek(json, i) == '}')
            {
                return result;
            }

            while (true)
            {
                SkipSpace(json, ref i);
                string key = ReadString(json, ref i);
                SkipSpace(json, ref i);
                Expect(json, ref i, ':');
                SkipSpace(json, ref i);
                result[key] = ReadString(json, ref i);
                SkipSpace(json, ref i);
                char c = Peek(json, i);
                i++;
                if (c == '}')
                {
                    return result;
                }

                if (c != ',')
                {
                    throw new FormatException("Se esperaba ',' o '}' en la posición " + (i - 1));
                }
            }
        }

        /// <summary>Texto de una clave, o la propia clave entre corchetes si falta (así se nota en pantalla).</summary>
        /// <param name="table">Tabla.</param>
        /// <param name="key">Clave.</param>
        public static string Get(IReadOnlyDictionary<string, string> table, string key) =>
            table != null && table.TryGetValue(key, out string text) ? text : "[" + key + "]";

        private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (Peek(s, i) != c)
            {
                throw new FormatException("Se esperaba '" + c + "' en la posición " + i);
            }

            i++;
        }

        private static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                char e = Peek(s, i++);
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '/': sb.Append('/'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                        {
                            throw new FormatException("Escape \\u incompleto en la posición " + i);
                        }

                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default:
                        throw new FormatException("Escape desconocido en la posición " + (i - 1));
                }
            }

            throw new FormatException("Cadena sin cerrar");
        }
    }
}
