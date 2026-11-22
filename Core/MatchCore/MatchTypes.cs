using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Fase en la que se encuentra el partido.</summary>
    public enum MatchPhase
    {
        /// <summary>Juego en marcha.</summary>
        Playing,
        /// <summary>Balón parado esperando a que se ejecute un saque.</summary>
        SetPiece,
        /// <summary>Pausa tras un gol.</summary>
        GoalPause,
        /// <summary>Partido terminado.</summary>
        Finished
    }

    /// <summary>Tipo de saque tras una interrupción.</summary>
    public enum RestartKind
    {
        /// <summary>Saque inicial.</summary>
        KickOff,
        /// <summary>Saque de banda.</summary>
        ThrowIn,
        /// <summary>Saque de puerta.</summary>
        GoalKick,
        /// <summary>Saque de esquina.</summary>
        Corner,
        /// <summary>Tiro libre.</summary>
        FreeKick,
        /// <summary>Penalti.</summary>
        Penalty
    }

    /// <summary>Saque pendiente: quién lo ejecuta y desde dónde.</summary>
    public struct Restart
    {
        /// <summary>Tipo de saque.</summary>
        public RestartKind Kind;
        /// <summary>Equipo que saca.</summary>
        public int Team;
        /// <summary>Posición del balón.</summary>
        public Vec2 Position;

        /// <summary>Crea un saque.</summary>
        public Restart(RestartKind kind, int team, Vec2 position)
        {
            Kind = kind;
            Team = team;
            Position = position;
        }
    }

    /// <summary>Tipo de evento del partido.</summary>
    public enum MatchEventType
    {
        /// <summary>Saque inicial.</summary>
        KickOff,
        /// <summary>Gol.</summary>
        Goal,
        /// <summary>Tiro a portería (incluye los desviados).</summary>
        Shot,
        /// <summary>Parada del portero.</summary>
        Save,
        /// <summary>Balón al poste o al larguero.</summary>
        Woodwork,
        /// <summary>Falta.</summary>
        Foul,
        /// <summary>Tarjeta amarilla.</summary>
        YellowCard,
        /// <summary>Tarjeta roja.</summary>
        RedCard,
        /// <summary>Fuera de juego.</summary>
        Offside,
        /// <summary>Saque de banda.</summary>
        ThrowIn,
        /// <summary>Saque de puerta.</summary>
        GoalKick,
        /// <summary>Saque de esquina.</summary>
        Corner,
        /// <summary>Penalti concedido.</summary>
        Penalty,
        /// <summary>Tiro libre concedido.</summary>
        FreeKick,
        /// <summary>Fin de la primera parte.</summary>
        HalfTime,
        /// <summary>Fin del partido.</summary>
        FullTime,
        /// <summary>Un equipo cambió su mentalidad o su presión.</summary>
        TacticsChanged,
        /// <summary>Cambio de jugador (PlayerIndex = puesto del que entra; el nombre del que sale queda en el equipo).</summary>
        Substitution,
        /// <summary>Un jugador se lesionó (lesión leve: puede seguir, más lento, hasta que lo cambien).</summary>
        Injury
    }

    /// <summary>Evento registrado durante el partido.</summary>
    public struct MatchEvent
    {
        /// <summary>Tipo de evento.</summary>
        public MatchEventType Type;
        /// <summary>Equipo protagonista (0 local, 1 visitante).</summary>
        public int Team;
        /// <summary>Índice global del jugador protagonista (0..21), o -1.</summary>
        public int PlayerIndex;
        /// <summary>Minuto de partido (0..90).</summary>
        public float Minute;
        /// <summary>Posición X del evento.</summary>
        public float X;
        /// <summary>Posición Y del evento.</summary>
        public float Y;
    }

    /// <summary>Registro de eventos con capacidad reservada (no genera basura en régimen normal).</summary>
    public sealed class MatchEventLog
    {
        private MatchEvent[] _items = new MatchEvent[512];

        /// <summary>Número de eventos registrados.</summary>
        public int Count { get; private set; }

        /// <summary>Evento por posición.</summary>
        /// <param name="index">Índice, de 0 a <see cref="Count"/> - 1.</param>
        public MatchEvent this[int index] => _items[index];

        /// <summary>Añade un evento.</summary>
        public void Add(MatchEvent e)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, _items.Length * 2);
            }

            _items[Count++] = e;
        }

        /// <summary>Cuenta los eventos de un tipo y equipo.</summary>
        /// <param name="type">Tipo.</param>
        /// <param name="team">Equipo, o -1 para ambos.</param>
        public int CountOf(MatchEventType type, int team = -1)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_items[i].Type == type && (team < 0 || _items[i].Team == team))
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>Acciones del jugador humano en un paso de simulación. Los botones son impulsos de un solo paso.</summary>
    public struct MatchInput
    {
        /// <summary>Dirección deseada en coordenadas del campo (longitud ≤ 1). La capa de presentación la convierte desde la cámara.</summary>
        public Vec2 Move;
        /// <summary>Esprint mantenido.</summary>
        public bool Sprint;
        /// <summary>Pase corto.</summary>
        public bool Pass;
        /// <summary>Pase filtrado al espacio.</summary>
        public bool Through;
        /// <summary>Centro o pase por alto.</summary>
        public bool Cross;
        /// <summary>Disparo.</summary>
        public bool Shoot;
        /// <summary>Potencia del disparo de 0 a 1.</summary>
        public float ShootPower;
        /// <summary>Potencia del pase de 0 a 1 (0 = la calcula la asistencia para que llegue al compañero).</summary>
        public float PassPower;
        /// <summary>Potencia del centro o pase largo de 0 a 1 (en los saques decide la distancia; 0 = la de por defecto).</summary>
        public float CrossPower;
        /// <summary>Entrada o presión al poseedor.</summary>
        public bool Tackle;
        /// <summary>Cambiar de jugador.</summary>
        public bool Switch;
        /// <summary>Barrida (impulso): más alcance que la entrada de pie, más riesgo de falta y el jugador queda en el suelo.</summary>
        public bool Slide;
        /// <summary>Despejar el balón (impulso, con el balón).</summary>
        public bool Clear;
        /// <summary>Presionar (mantenido): el jugador persigue al poseedor y entra de pie, sin barrer, al alcanzarlo.</summary>
        public bool Press;
        /// <summary>Tomar o soltar el control del arquero (impulso).</summary>
        public bool Keeper;
        /// <summary>Segunda marca (mantenido): sin balón, el compañero más cercano al poseedor rival sale a presionarlo.</summary>
        public bool SecondPress;
    }

    /// <summary>Acción visible de un jugador, para elegir animación.</summary>
    public enum PlayerAction : byte
    {
        /// <summary>Quieto.</summary>
        Idle,
        /// <summary>Trotando.</summary>
        Run,
        /// <summary>A máxima velocidad.</summary>
        Sprint,
        /// <summary>Acaba de patear.</summary>
        Kick,
        /// <summary>Entrando al balón.</summary>
        Tackle,
        /// <summary>Portero lanzándose.</summary>
        Dive,
        /// <summary>Trastabilla tras un choque con un rival más fuerte.</summary>
        Stumble,
        /// <summary>Festejando un gol.</summary>
        Celebrate,
        /// <summary>Pase raso (apertura de cadera, golpe con el interior).</summary>
        Pass,
        /// <summary>Tiro (armado amplio y acompañamiento de la pierna).</summary>
        Shoot,
        /// <summary>Barrida: deslizamiento rasante con una pierna extendida.</summary>
        Slide,
        /// <summary>Saque de banda con las dos manos por detrás de la cabeza.</summary>
        ThrowIn,
        /// <summary>Arquero que sale a achicar en el mano a mano.</summary>
        Rush
    }

}
