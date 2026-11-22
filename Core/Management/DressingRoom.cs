using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Vestuario: jerarquía (por edad, antigüedad, nivel y personalidad), química entre los que juegan juntos, promesas de
    /// minutos que se revisan cada mes y la influencia de los líderes en la moral del resto.
    /// </summary>
    public sealed class DressingRoom
    {
        private readonly ManagementConfig _config;
        private readonly EventBus _bus;
        private readonly Dictionary<(int, int), float> _minutesTogether = new Dictionary<(int, int), float>();

        /// <summary>Crea el vestuario.</summary>
        public DressingRoom(ManagementConfig config, EventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <summary>Peso de un jugador en el vestuario (más alto = más influyente).</summary>
        public static float Influence(Player p, DateTime today)
        {
            float age = Math.Min(p.AgeOn(today), 34) - 18;
            float years = (float)Math.Max(0, (today - p.JoinedOn).TotalDays / 365.0);
            float level = PlayerRatings.Overall(p.Attributes, p.Position) - 50f;
            float trait = p.Personality == PersonalityTrait.Leader ? 15f : p.Personality == PersonalityTrait.Professional ? 4f : 0f;
            return age + 2f * Math.Min(years, 8f) + 0.4f * level + trait;
        }

        /// <summary>Jerarquía: 1 líder, 2 muy influyentes, 3 influyentes; el resto, sin peso especial.</summary>
        public static Dictionary<int, DressingRoomTier> Hierarchy(IEnumerable<Player> squad, DateTime today)
        {
            var ranked = squad.OrderByDescending(p => Influence(p, today)).ThenBy(p => p.Id).ToList();
            var tiers = new Dictionary<int, DressingRoomTier>();
            for (int i = 0; i < ranked.Count; i++)
            {
                tiers[ranked[i].Id] = i == 0 ? DressingRoomTier.TeamLeader : i < 3 ? DressingRoomTier.HighlyInfluential
                    : i < 6 ? DressingRoomTier.Influential : DressingRoomTier.Other;
            }

            return tiers;
        }

        /// <summary>Anota los minutos que jugaron juntos los del informe (para la química).</summary>
        public void RecordMatch(TeamReport team)
        {
            var players = team.Players;
            for (int i = 0; i < players.Count; i++)
            {
                for (int j = i + 1; j < players.Count; j++)
                {
                    float together = Math.Min(players[i].Minutes, players[j].Minutes);
                    var key = Key(players[i].PlayerId, players[j].PlayerId);
                    _minutesTogether[key] = (_minutesTogether.TryGetValue(key, out float m) ? m : 0f) + together;
                }
            }
        }

        /// <summary>Química de un once (0 = nunca jugaron juntos, 1 = muy habituales).</summary>
        public float Chemistry(IReadOnlyList<int> lineup)
        {
            float total = 0f;
            int pairs = 0;
            for (int i = 0; i < lineup.Count; i++)
            {
                for (int j = i + 1; j < lineup.Count; j++)
                {
                    float m = _minutesTogether.TryGetValue(Key(lineup[i], lineup[j]), out float v) ? v : 0f;
                    total += Math.Min(1f, m / _config.ChemistryFullMinutes);
                    pairs++;
                }
            }

            return pairs > 0 ? total / pairs : 0f;
        }

        /// <summary>Atributos efectivos extra que da la química de un once (fracción).</summary>
        public float ChemistryBonus(IReadOnlyList<int> lineup) => _config.ChemistryBonus * Chemistry(lineup);

        /// <summary>
        /// Revisión mensual: promesas de minutos incumplidas (bajan la moral y se publican) e influencia de los líderes,
        /// que arrastran la moral del resto hacia la suya.
        /// </summary>
        public void MonthlyReview(int clubId, IReadOnlyList<Player> squad, IDictionary<int, PlayerCondition> conditions, DateTime today)
        {
            ManagementConfig c = _config;
            // Primero la influencia de los líderes y después el enojo por las promesas incumplidas (que debe notarse).
            Dictionary<int, DressingRoomTier> tiers = Hierarchy(squad, today);
            var leaders = squad.Where(p => tiers[p.Id] >= DressingRoomTier.HighlyInfluential && conditions.ContainsKey(p.Id)).ToList();
            if (leaders.Count > 0)
            {
                float leaderMorale = leaders.Average(p => conditions[p.Id].Morale);
                foreach (Player p in squad)
                {
                    if (tiers[p.Id] < DressingRoomTier.HighlyInfluential && conditions.TryGetValue(p.Id, out PlayerCondition cond))
                    {
                        cond.Morale += c.LeaderMoralePull * (leaderMorale - cond.Morale);
                    }
                }
            }

            foreach (Player p in squad)
            {
                if (!conditions.TryGetValue(p.Id, out PlayerCondition cond) || cond.Promise == PlayingTimePromise.None
                    || cond.TeamMatchesSincePromise < c.PromiseGraceMatches)
                {
                    continue;
                }

                float share = cond.MinutesSincePromise / (cond.TeamMatchesSincePromise * 90f);
                if (share < c.PromiseShare[(int)cond.Promise])
                {
                    float hit = p.Personality == PersonalityTrait.Ambitious ? c.BrokenPromiseMorale * 1.5f : c.BrokenPromiseMorale;
                    cond.Morale = Math.Max(0f, cond.Morale - hit);
                    _bus.Publish(new PromiseBroken { PlayerId = p.Id, ClubId = clubId });
                }
            }
        }

        private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
    }
}
