namespace Football.Core.Domain
{
    /// <summary>Línea del campo de una posición.</summary>
    public enum PitchLine
    {
        /// <summary>Arco.</summary>
        Goalkeeper,
        /// <summary>Defensa.</summary>
        Defence,
        /// <summary>Medio campo.</summary>
        Midfield,
        /// <summary>Ataque.</summary>
        Attack
    }

    /// <summary>Valoraciones simples de un jugador a partir de sus atributos (compartidas por gestión y simulador).</summary>
    public static class PlayerRatings
    {
        /// <summary>Línea de una posición.</summary>
        public static PitchLine LineOf(Position p)
        {
            switch (p)
            {
                case Position.Goalkeeper: return PitchLine.Goalkeeper;
                case Position.CenterBack:
                case Position.LeftBack:
                case Position.RightBack: return PitchLine.Defence;
                case Position.Striker:
                case Position.LeftWinger:
                case Position.RightWinger: return PitchLine.Attack;
                default: return PitchLine.Midfield;
            }
        }

        /// <summary>Valoración (1–100) en una línea: media de los atributos que importan en ella.</summary>
        /// <param name="a">Atributos.</param>
        /// <param name="line">Línea.</param>
        public static float InLine(AttributeSet a, PitchLine line)
        {
            switch (line)
            {
                case PitchLine.Goalkeeper:
                    return (a[AttributeId.Reflexes] + a[AttributeId.Handling] + a[AttributeId.GoalkeeperPositioning]) / 3f;
                case PitchLine.Defence:
                    return (a[AttributeId.Tackling] + a[AttributeId.Positioning] + a[AttributeId.Strength] + a[AttributeId.Heading] + a[AttributeId.Pace]) / 5f;
                case PitchLine.Attack:
                    return (a[AttributeId.Finishing] + a[AttributeId.Shooting] + a[AttributeId.Dribbling] + a[AttributeId.Pace] + a[AttributeId.Composure]) / 5f;
                default:
                    return (a[AttributeId.Passing] + a[AttributeId.Vision] + a[AttributeId.FirstTouch] + a[AttributeId.Decisions] + a[AttributeId.Dribbling]) / 5f;
            }
        }

        /// <summary>Valoración en su posición.</summary>
        public static float Overall(AttributeSet a, Position p) => InLine(a, LineOf(p));
    }
}
