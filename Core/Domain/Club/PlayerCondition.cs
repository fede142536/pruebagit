namespace Football.Core.Domain
{
    /// <summary>Promesa de minutos hecha al jugador al firmar (estilo Football Manager).</summary>
    public enum PlayingTimePromise
    {
        /// <summary>Sin promesa.</summary>
        None,
        /// <summary>Figura: juega casi siempre.</summary>
        Star,
        /// <summary>Titular habitual.</summary>
        Regular,
        /// <summary>Rotación.</summary>
        Rotation,
        /// <summary>Suplente.</summary>
        Backup,
        /// <summary>Juvenil con proyección.</summary>
        Youngster
    }

    /// <summary>
    /// Estado de un jugador en la carrera, que cambia día a día y partido a partido: forma, moral, cansancio, lesión,
    /// sanción, minutos y su promesa de minutos. La gestión lo convierte en atributos efectivos para el partido.
    /// </summary>
    public sealed class PlayerCondition
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Forma (1–10; 6 es normal): media móvil de sus notas.</summary>
        public float Form = 6f;
        /// <summary>Moral (0–100; 50 normal).</summary>
        public float Morale = 50f;
        /// <summary>Estado físico (0–1; 1 descansado).</summary>
        public float Fitness = 1f;
        /// <summary>Días de baja que le quedan por lesión.</summary>
        public int InjuryDays;
        /// <summary>Partidos de sanción que le quedan.</summary>
        public int SuspendedMatches;
        /// <summary>Amarillas acumuladas desde la última sanción.</summary>
        public int AccumulatedYellows;
        /// <summary>Minutos jugados en la temporada.</summary>
        public float SeasonMinutes;
        /// <summary>Partidos del equipo en la temporada desde que tiene la promesa (para medirla).</summary>
        public int TeamMatchesSincePromise;
        /// <summary>Minutos jugados desde que tiene la promesa.</summary>
        public float MinutesSincePromise;
        /// <summary>Promesa de minutos.</summary>
        public PlayingTimePromise Promise;
        /// <summary>Está en el equipo de reserva (no juega los partidos del primer equipo).</summary>
        public bool InReserve;
        /// <summary>Ya jugó algún partido en primera: solo así puede bajar a la reserva. Verdadero salvo para los canteranos que todavía no debutaron.</summary>
        public bool Debuted = true;
        /// <summary>Fracción de punto de atributo acumulada por el desarrollo (se gasta al llegar a 1).</summary>
        public float DevProgress;
        /// <summary>Media al empezar la temporada (0 si no se registró), para mostrar cuánto subió o bajó.</summary>
        public int SeasonStartRating;
        /// <summary>Anunció que esta es su última temporada: se retira al cerrarla.</summary>
        public bool RetiringAtEnd;
        /// <summary>Fracción de punto de atributo que lleva perdida por el declive de la edad.</summary>
        public float DeclineProgress;
        /// <summary>Minutos de la temporada ya contados por el desarrollo (para medir los de cada semana).</summary>
        public float DevMinutesMark;

        /// <summary>Puede jugar el próximo partido.</summary>
        public bool Available => InjuryDays <= 0 && SuspendedMatches <= 0;
    }

    /// <summary>Un jugador se lesionó (lo publica la gestión al aplicar un informe).</summary>
    public struct PlayerInjured
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Días de baja.</summary>
        public int Days;
    }

    /// <summary>Un jugador queda suspendido.</summary>
    public struct PlayerSuspended
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Partidos de sanción.</summary>
        public int Matches;
    }
}
