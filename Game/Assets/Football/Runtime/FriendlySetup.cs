using UnityEngine;
using Football.Core.Data;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>Elecciones para un partido amistoso: equipos, camisetas y convocatorias de cada lado.</summary>
    public sealed class FriendlySetup
    {
        private readonly GameSettings _settings;
        private readonly SquadSelection[] _squads = new SquadSelection[2];

        /// <summary>Crea el estado a partir de los últimos ajustes guardados.</summary>
        public FriendlySetup(GameSettings settings)
        {
            _settings = settings;
            _squads[0] = SquadStore.Load(settings.HomeTeam);
            _squads[1] = SquadStore.Load(settings.Opponent);
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

            _squads[side] = SquadStore.Load(next);
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
            SquadStore.Save(Team(side), _squads[side]);
        }

        /// <summary>Vuelve a la convocatoria automática de un lado.</summary>
        public void ResetSquad(int side)
        {
            TeamDefinition team = Definition(side);
            _squads[side] = SquadSelection.Auto(team, TeamBuilder.ParseFormation(team.Formation));
            SaveSquad(side);
        }
    }

    /// <summary>Guarda en el dispositivo la convocatoria elegida para cada club.</summary>
    public static class SquadStore
    {
        private const string Prefix = "football.squad.";

        /// <summary>Convocatoria guardada del club, o la automática si no hay o ya no es válida (por ejemplo, si cambió la plantilla).</summary>
        public static SquadSelection Load(int teamIndex)
        {
            TeamDefinition team = TeamCatalog.Teams[teamIndex];
            SquadSelection saved = SquadSelection.Deserialize(team, PlayerPrefs.GetString(Prefix + team.Name, string.Empty));
            return saved ?? SquadSelection.Auto(team, TeamBuilder.ParseFormation(team.Formation));
        }

        /// <summary>Guarda la convocatoria del club.</summary>
        public static void Save(int teamIndex, SquadSelection selection)
        {
            PlayerPrefs.SetString(Prefix + TeamCatalog.Teams[teamIndex].Name, selection.Serialize());
            PlayerPrefs.Save();
        }
    }
}
