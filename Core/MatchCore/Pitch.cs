using System;
using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>
    /// Geometría del campo. El equipo que ataca hacia +X tiene <c>AttackDirection = +1</c> y defiende la portería
    /// del extremo -X; un "extremo" se identifica por su signo (<c>endSign</c>): +1 es la portería en +X.
    /// </summary>
    public sealed class Pitch
    {
        /// <summary>Largo del campo.</summary>
        public readonly float Length;
        /// <summary>Ancho del campo.</summary>
        public readonly float Width;
        /// <summary>Mitad del largo.</summary>
        public readonly float HalfLength;
        /// <summary>Mitad del ancho.</summary>
        public readonly float HalfWidth;
        /// <summary>Mitad de la distancia entre postes.</summary>
        public readonly float GoalHalfWidth;
        /// <summary>Altura del larguero.</summary>
        public readonly float GoalHeight;
        /// <summary>Datos de origen.</summary>
        public readonly PitchConfig Config;

        /// <summary>Crea la geometría a partir de la configuración.</summary>
        /// <param name="config">Dimensiones del campo.</param>
        public Pitch(PitchConfig config)
        {
            Config = config;
            Length = config.Length;
            Width = config.Width;
            HalfLength = config.Length * 0.5f;
            HalfWidth = config.Width * 0.5f;
            GoalHalfWidth = config.GoalWidth * 0.5f;
            GoalHeight = config.GoalHeight;
        }

        /// <summary>Centro de la línea de gol de un extremo.</summary>
        /// <param name="endSign">+1 o -1.</param>
        public Vec2 GoalCenter(float endSign) => new Vec2(endSign * HalfLength, 0f);

        /// <summary>Punto de penalti de un extremo.</summary>
        /// <param name="endSign">+1 o -1.</param>
        public Vec2 PenaltySpot(float endSign) => new Vec2(endSign * (HalfLength - Config.PenaltySpotDistance), 0f);

        /// <summary>Avance de una coordenada X hacia la portería rival: 0 en la línea propia, 1 en la rival.</summary>
        /// <param name="attackDirection">Dirección de ataque del equipo (+1 o -1).</param>
        /// <param name="x">Coordenada X.</param>
        public float Progress(float attackDirection, float x) => (attackDirection * x + HalfLength) / Length;

        /// <summary>Indica si un punto está dentro del área grande de un extremo (línea incluida).</summary>
        /// <param name="endSign">Extremo cuya área se comprueba.</param>
        /// <param name="p">Punto.</param>
        public bool InPenaltyArea(float endSign, Vec2 p) =>
            MathF.Abs(p.Y) <= Config.PenaltyAreaHalfWidth && endSign * p.X >= HalfLength - Config.PenaltyAreaDepth;

        /// <summary>Acota un punto al campo ampliado con el margen indicado.</summary>
        /// <param name="p">Punto.</param>
        /// <param name="margin">Margen fuera de las líneas.</param>
        public Vec2 Clamp(Vec2 p, float margin) => new Vec2(
            MathUtil.Clamp(p.X, -HalfLength - margin, HalfLength + margin),
            MathUtil.Clamp(p.Y, -HalfWidth - margin, HalfWidth + margin));
    }
}
