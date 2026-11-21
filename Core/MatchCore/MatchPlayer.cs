using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Estado de un jugador durante el partido.</summary>
    public sealed class MatchPlayer
    {
        /// <summary>Índice global (0..21): equipo × 11 + puesto.</summary>
        public readonly int Index;
        /// <summary>Equipo (0 local, 1 visitante).</summary>
        public readonly int Team;
        /// <summary>Puesto en la formación (0 = portero).</summary>
        public readonly int Slot;
        /// <summary>Posición asignada.</summary>
        public readonly Position Role;
        /// <summary>Atributos efectivos.</summary>
        public readonly AttributeSet Attr;
        /// <summary>Nombre para mostrar.</summary>
        public readonly string Name;
        /// <summary>Dorsal.</summary>
        public readonly int Number;

        /// <summary>Posición en el campo.</summary>
        public Vec2 Pos;
        /// <summary>Orientación en radianes.</summary>
        public float Heading;
        /// <summary>Rapidez actual (m/s).</summary>
        public float Speed;
        /// <summary>Resistencia restante de 0 a 1.</summary>
        public float Stamina = 1f;

        /// <summary>Velocidad punta con resistencia plena (m/s).</summary>
        public readonly float TopSpeed;
        /// <summary>Aceleración (m/s²).</summary>
        public readonly float Acceleration;
        /// <summary>Multiplicador de la capacidad de giro.</summary>
        public readonly float TurnScale;
        /// <summary>Multiplicador del gasto de resistencia.</summary>
        public readonly float StaminaDrainScale;

        /// <summary>Punto al que el jugador quiere ir.</summary>
        public Vec2 Target;
        /// <summary>Fracción de la velocidad punta deseada: 0 quieto, <c>JogFraction</c> trote, 1 esprint.</summary>
        public float SpeedFactor;
        /// <summary>El jugador quiere disputar el balón al poseedor.</summary>
        public bool WantsTackle;
        /// <summary>El portero se lanza (velocidad lateral alta).</summary>
        public bool Diving;
        /// <summary>Rival al que marca.</summary>
        public MatchPlayer MarkTarget;
        /// <summary>Posición base que le corresponde según formación, balón y estilo del equipo.</summary>
        public Vec2 Anchor;

        /// <summary>Tiempo hasta la próxima decisión sin balón (s).</summary>
        public float DecisionTimer;
        /// <summary>Tiempo hasta la próxima decisión con balón (s).</summary>
        public float OwnerTimer;
        /// <summary>Tiempo hasta poder actuar con el balón (s).</summary>
        public float ActionTimer;
        /// <summary>Tiempo durante el que no puede recuperar el balón tras patearlo (s).</summary>
        public float KickTimer;
        /// <summary>Tiempo hasta poder volver a entrar al balón (s).</summary>
        public float TackleTimer;
        /// <summary>Tiempo que queda de la ventana de entrada pedida por el humano (s).</summary>
        public float TackleWindow;
        /// <summary>Tiempo que se muestra la acción actual (s).</summary>
        public float ActionDisplayTimer;
        /// <summary>Tiempo que queda de barrida (s); mientras dura, el jugador se desliza sin poder girar.</summary>
        public float SlideTimer;
        /// <summary>Tiempo que tarda en levantarse tras una barrida (s).</summary>
        public float RecoverTimer;
        /// <summary>La barrida en curso ya tocó (o falló) al rival.</summary>
        public bool SlideResolved;
        /// <summary>Tiempo que queda trastabillando tras un choque (s): corre más lento.</summary>
        public float StumbleTimer;
        /// <summary>Tiempo que queda del efecto de su último recorte con el balón (s).</summary>
        public float CutTimer;
        /// <summary>Orientación medida al inicio del intervalo de muestreo de recortes.</summary>
        public float HeadingSample;
        /// <summary>Tiempo hasta la próxima medición del giro (s).</summary>
        public float CutSampleTimer;

        /// <summary>Tarjetas amarillas recibidas.</summary>
        public int Yellows;
        /// <summary>Expulsado.</summary>
        public bool SentOff;
        /// <summary>Acción visible, para animar.</summary>
        public PlayerAction Action;

        /// <summary>Crea un jugador con sus valores derivados de los atributos.</summary>
        public MatchPlayer(int index, int team, int slot, PlayerMatchView view, PlayerConfig c)
        {
            Index = index;
            Team = team;
            Slot = slot;
            Role = view.Role;
            Attr = view.Attributes;
            Name = view.Name;
            Number = view.Number;

            TopSpeed = MathUtil.Lerp(c.MinTopSpeed, c.MaxTopSpeed, MathUtil.Skill01(Attr[AttributeId.Pace]));
            Acceleration = MathUtil.Lerp(c.MinAcceleration, c.MaxAcceleration, MathUtil.Skill01(Attr[AttributeId.Acceleration]));
            TurnScale = 0.7f + 0.6f * MathUtil.Skill01(Attr[AttributeId.Agility]);
            StaminaDrainScale = 1f + c.StaminaAttributeSpread - 2f * c.StaminaAttributeSpread * MathUtil.Skill01(Attr[AttributeId.Stamina]);
        }

        /// <summary>Es el portero.</summary>
        public bool IsKeeper => Role == Position.Goalkeeper;

        /// <summary>Velocidad vectorial.</summary>
        public Vec2 Velocity => Vec2.FromAngle(Heading) * Speed;

        /// <summary>Habilidad de un atributo en [0, 1].</summary>
        public float Skill(AttributeId id) => MathUtil.Skill01(Attr[id]);
    }
}
