using System;
using System.Collections.Generic;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Acciones del partido que se pueden asignar a una tecla.</summary>
    public enum GameAction
    {
        /// <summary>Mover arriba.</summary>
        Up,
        /// <summary>Mover abajo.</summary>
        Down,
        /// <summary>Mover a la izquierda.</summary>
        Left,
        /// <summary>Mover a la derecha.</summary>
        Right,
        /// <summary>Esprint (mantener).</summary>
        Sprint,
        /// <summary>Pase (toque: automático; mantener: potencia).</summary>
        Pass,
        /// <summary>Tiro (mantener y soltar).</summary>
        Shoot,
        /// <summary>Pase filtrado.</summary>
        Through,
        /// <summary>Centro.</summary>
        Cross,
        /// <summary>Despejar.</summary>
        Clear,
        /// <summary>Barrida.</summary>
        Slide,
        /// <summary>Presionar (mantener).</summary>
        Press,
        /// <summary>Cambiar de jugador.</summary>
        Switch,
        /// <summary>Controlar al arquero.</summary>
        Keeper,
        /// <summary>Pausa.</summary>
        Pause,
        /// <summary>Segunda marca (mantener): un compañero sale a presionar al poseedor. Va al final para no mover las teclas guardadas.</summary>
        SecondPress
    }

    /// <summary>
    /// Teclas asignadas a cada acción, guardadas en el dispositivo. Se guardan como <see cref="KeyCode"/> y, si el proyecto
    /// usa el sistema de entrada nuevo, se traducen una sola vez a su equivalente (sin coste por fotograma).
    /// </summary>
    public static class KeyBindings
    {
        private const string Prefix = "football.key.";

        /// <summary>Cantidad de acciones.</summary>
        public static readonly int Count = Enum.GetValues(typeof(GameAction)).Length;

        /// <summary>Nombre en español de cada acción, en el orden de <see cref="GameAction"/>.</summary>
        public static readonly string[] Labels =
        {
            "ARRIBA", "ABAJO", "IZQUIERDA", "DERECHA", "ESPRINT", "PASE", "TIRO", "PASE FILTRADO", "CENTRO", "DESPEJAR",
            "BARRER", "PRESIONAR", "CAMBIAR JUGADOR", "ARQUERO", "PAUSA", "SEGUNDA MARCA"
        };

        private static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftShift, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.I, KeyCode.R,
            KeyCode.Q, KeyCode.E, KeyCode.Tab, KeyCode.G, KeyCode.Escape, KeyCode.F
        };

        private static readonly KeyCode[] Current = new KeyCode[Defaults.Length];
        private static bool _loaded;

        /// <summary>Se incrementa cada vez que cambia una tecla (para refrescar cachés).</summary>
        public static int Version { get; private set; }

        /// <summary>Tecla asignada a una acción.</summary>
        public static KeyCode Get(GameAction action)
        {
            EnsureLoaded();
            return Current[(int)action];
        }

        /// <summary>
        /// Asigna una tecla a una acción. Si otra acción la usaba, intercambian teclas (nunca quedan dos acciones con la misma).
        /// </summary>
        public static void Set(GameAction action, KeyCode key)
        {
            EnsureLoaded();
            int index = (int)action;
            for (int i = 0; i < Current.Length; i++)
            {
                if (i != index && Current[i] == key)
                {
                    Current[i] = Current[index];
                    Save(i);
                }
            }

            Current[index] = key;
            Save(index);
            PlayerPrefs.Save();
            Version++;
        }

        /// <summary>Vuelve a las teclas por defecto.</summary>
        public static void ResetAll()
        {
            EnsureLoaded();
            for (int i = 0; i < Current.Length; i++)
            {
                Current[i] = Defaults[i];
                Save(i);
            }

            PlayerPrefs.Save();
            Version++;
        }

        /// <summary>Nombre legible de una tecla.</summary>
        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift: return "SHIFT IZQ.";
                case KeyCode.RightShift: return "SHIFT DER.";
                case KeyCode.LeftControl: return "CTRL IZQ.";
                case KeyCode.RightControl: return "CTRL DER.";
                case KeyCode.Space: return "ESPACIO";
                case KeyCode.Return: return "ENTER";
                case KeyCode.Escape: return "ESC";
                case KeyCode.UpArrow: return "FLECHA ARRIBA";
                case KeyCode.DownArrow: return "FLECHA ABAJO";
                case KeyCode.LeftArrow: return "FLECHA IZQ.";
                case KeyCode.RightArrow: return "FLECHA DER.";
                default:
                    string name = key.ToString();
                    return name.StartsWith("Alpha", StringComparison.Ordinal) ? name.Substring(5) : name.ToUpperInvariant();
            }
        }

        /// <summary>Teclas que se pueden asignar (todas las del teclado; sin botones de ratón ni mandos).</summary>
        public static IReadOnlyList<KeyCode> Assignable
        {
            get
            {
                if (_assignable == null)
                {
                    var list = new List<KeyCode>();
                    foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                    {
                        string n = k.ToString();
                        if (k != KeyCode.None && !n.StartsWith("Mouse", StringComparison.Ordinal) && !n.StartsWith("Joystick", StringComparison.Ordinal))
                        {
                            list.Add(k);
                        }
                    }

                    _assignable = list.ToArray();
                }

                return _assignable;
            }
        }

        private static KeyCode[] _assignable;

        /// <summary>
        /// Nombre de la tecla en el sistema de entrada nuevo (difiere en algunas: Alpha1 → Digit1, Return → Enter, Control → Ctrl).
        /// </summary>
        public static string InputSystemName(KeyCode key)
        {
            string n = key.ToString();
            if (n.StartsWith("Alpha", StringComparison.Ordinal))
            {
                return "Digit" + n.Substring(5);
            }

            if (n.StartsWith("Keypad", StringComparison.Ordinal))
            {
                return "Numpad" + n.Substring(6);
            }

            switch (n)
            {
                case "Return": return "Enter";
                case "LeftControl": return "LeftCtrl";
                case "RightControl": return "RightCtrl";
                case "BackQuote": return "Backquote";
                case "LeftCommand": return "LeftMeta";
                case "RightCommand": return "RightMeta";
                default: return n;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            for (int i = 0; i < Defaults.Length; i++)
            {
                int stored = PlayerPrefs.GetInt(Prefix + i, (int)Defaults[i]);
                Current[i] = Enum.IsDefined(typeof(KeyCode), stored) ? (KeyCode)stored : Defaults[i];
            }
        }

        private static void Save(int index)
        {
            PlayerPrefs.SetInt(Prefix + index, (int)Current[index]);
        }
    }
}
