using Football.Core.Domain;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Textos cortos comunes de los jugadores: abreviatura de puesto, apellido y flecha de forma.</summary>
    public static class PlayerLabels
    {
        private const float FormNeutral = 6f;
        private const float FormStep = 0.6f;     // cada 0,6 de nota de diferencia, la flecha gira 45°
        private const float MaxArrowDegrees = 90f;

        /// <summary>Abreviatura del puesto (POR, DFC, LI, LD, MCD, MC, MCO, MI, MD, EI, ED, DC).</summary>
        public static string Code(Position p)
        {
            switch (p)
            {
                case Position.Goalkeeper: return "POR";
                case Position.LeftBack: return "LI";
                case Position.RightBack: return "LD";
                case Position.CenterBack: return "DFC";
                case Position.DefensiveMidfielder: return "MCD";
                case Position.CentralMidfielder: return "MC";
                case Position.AttackingMidfielder: return "MCO";
                case Position.LeftMidfielder: return "MI";
                case Position.RightMidfielder: return "MD";
                case Position.LeftWinger: return "EI";
                case Position.RightWinger: return "ED";
                default: return "DC";
            }
        }

        /// <summary>Apellido para mostrar (la última palabra del nombre).</summary>
        public static string Surname(string name)
        {
            string full = (name ?? string.Empty).Trim();
            int space = full.LastIndexOf(' ');
            return space >= 0 ? full.Substring(space + 1) : full;
        }

        /// <summary>Giro de la flecha de forma: 90° hacia arriba muy en forma, 0° normal, −90° hacia abajo sin forma.</summary>
        public static float FormArrowDegrees(float form) =>
            Mathf.Clamp(Mathf.Round((form - FormNeutral) / FormStep) * 45f, -MaxArrowDegrees, MaxArrowDegrees);

        /// <summary>Color de la flecha de forma (verde en forma, amarillo normal, rojo sin forma).</summary>
        public static Color FormColor(float form) =>
            form >= FormNeutral + FormStep ? new Color(0.35f, 0.9f, 0.4f) : form <= FormNeutral - FormStep ? new Color(0.95f, 0.35f, 0.3f) : new Color(1f, 0.82f, 0.25f);
    }
}
