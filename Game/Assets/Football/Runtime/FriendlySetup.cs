using UnityEngine;
using Football.Core.Data;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Elecciones para un partido amistoso: equipos, camisetas y convocatorias de cada lado.</summary>
    public sealed class FriendlySetup
    {
        private readonly GameSettings _settings;
        private readonly SquadSelection[] _squads = new SquadSelection[2];
        private bool _indoor;

        /// <summary>Crea el estado a partir de los últimos ajustes guardados.</summary>
        public FriendlySetup(GameSettings settings)
        {
            _settings = settings;
            ReloadSquads();
        }

        /// <summary>Modo cancha cerrada (3 contra 3): las alineaciones son de tres y se guardan aparte.</summary>
        public bool Indoor
        {
            get => _indoor;
            set
            {
                if (_indoor != value)
                {
                    _indoor = value;
                    ReloadSquads();
                }
            }
        }

        /// <summary>Formaciones disponibles en el modo actual.</summary>
        public Formation[] AvailableFormations => _indoor ? Formations.Indoor : Formations.All;

        private void ReloadSquads()
        {
            _squads[0] = SquadStore.Load(_settings.HomeTeam, _indoor);
            _squads[1] = SquadStore.Load(_settings.Opponent, _indoor);
        }

        /// <summary>Índice del equipo de un lado (0 el tuyo, 1 el rival).</summary>
        public int Team(int side) => side == 0 ? _settings.HomeTeam : _settings.Opponent;

        /// <summary>Camiseta elegida para un lado (0 titular, 1 alternativa).</summary>
        public int Kit(int side) => side == 0 ? _settings.HomeKit : _settings.AwayKit;

        /// <summary>Definición del equipo de un lado.</summary>
        public TeamDefinition Definition(int side) => TeamCatalog.Teams[Team(side)];

        /// <summary>Convocatoria de un lado.</summary>
        public SquadSelection Squad(int side) => _squads[side];

        /// <summary>Colores de la camiseta elegida para un lado.</summary>
        public KitColors KitOf(int side) =>
            Football.Game.KitColors.From(Kit(side) == 0 ? Definition(side).Home : Definition(side).Away);

        /// <summary>Las dos camisetas se parecen demasiado.</summary>
        public bool KitsClash => KitOf(0).Distance(KitOf(1)) < TeamCatalog.KitClashDistance;

        /// <summary>Pasa al equipo siguiente o anterior de un lado, saltando el del otro lado.</summary>
        public void CycleTeam(int side, int step)
        {
            int count = TeamCatalog.Teams.Length;
            int other = Team(1 - side);
            int next = Team(side);
            do
            {
                next = (next + step + count) % count;
            }
            while (next == other && count > 1);

            if (side == 0)
            {
                _settings.HomeTeam = next;
            }
            else
            {
                _settings.Opponent = next;
            }

            _squads[side] = SquadStore.Load(next, _indoor);
            _settings.Save();
        }

        /// <summary>Alterna entre camiseta titular y alternativa de un lado.</summary>
        public void ToggleKit(int side)
        {
            if (side == 0)
            {
                _settings.HomeKit = 1 - _settings.HomeKit;
            }
            else
            {
                _settings.AwayKit = 1 - _settings.AwayKit;
            }

            _settings.Save();
        }

        /// <summary>Guarda la convocatoria de un lado para la próxima vez.</summary>
        public void SaveSquad(int side)
        {
            SquadStore.Save(Team(side), _squads[side], _indoor);
        }

        /// <summary>Vuelve a la convocatoria automática de un lado.</summary>
        public void ResetSquad(int side)
        {
            _squads[side] = SquadStore.Auto(Definition(side), _indoor);
            SaveSquad(side);
        }
    }

    /// <summary>Guarda en el dispositivo la convocatoria elegida para cada club.</summary>
    public static class SquadStore
    {
        private const string Prefix = "football.squad.";
        private const string IndoorSuffix = ".potrero"; // las alineaciones 3v3 viejas (con arquero) no se reutilizan
        private const int IndoorBench = 3;

        /// <summary>
        /// Convocatoria guardada del club para el modo dado, o la automática si no hay o ya no es válida (por ejemplo, si
        /// cambió la plantilla o se guardó para el otro modo).
        /// </summary>
        public static SquadSelection Load(int teamIndex, bool indoor)
        {
            TeamDefinition team = TeamCatalog.Teams[teamIndex];
            SquadSelection saved = SquadSelection.Deserialize(team, PlayerPrefs.GetString(Key(team, indoor), string.Empty));
            bool rightSize = saved != null && (saved.Formation.Slots.Length == Formations.Indoor3.Slots.Length) == indoor;
            return rightSize ? saved : Auto(team, indoor);
        }

        /// <summary>Convocatoria automática del modo dado.</summary>
        public static SquadSelection Auto(TeamDefinition team, bool indoor) => indoor
            ? SquadSelection.Auto(team, Formations.Indoor3, IndoorBench)
            : SquadSelection.Auto(team, TeamBuilder.ParseFormation(team.Formation));

        /// <summary>Guarda la convocatoria del club para el modo dado.</summary>
        public static void Save(int teamIndex, SquadSelection selection, bool indoor)
        {
            TeamDefinition team = TeamCatalog.Teams[teamIndex];
            PlayerPrefs.SetString(Key(team, indoor), selection.Serialize(team));
            PlayerPrefs.Save();
        }

        private static string Key(TeamDefinition team, bool indoor) => Prefix + team.Name + (indoor ? IndoorSuffix : string.Empty);
    }
}
