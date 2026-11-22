using System;
using System.Collections.Generic;
using System.IO;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;

namespace Football.Core.Career
{
    // Versión 3 del archivo de partida: DT, datos de clubes, estadios y obras, socios, entradas, fichajes y medidor.
    public static partial class CareerFile
    {
        private static void WriteClubManagement(BinaryWriter w, CareerData d)
        {
            ManagerProfile m = d.Manager ?? new ManagerProfile();
            w.Write(m.FirstName ?? string.Empty);
            w.Write(m.LastName ?? string.Empty);
            w.Write(m.Nationality ?? string.Empty);
            w.Write(m.AvatarIndex);
            w.Write((int)m.Outfit);
            w.Write(m.Reputation);
            w.Write(m.Popularity);
            w.Write(m.Sacked);
            w.Write((int)d.TicketLevel);
            w.Write(d.NextPriceOverride);
            w.Write(d.SeasonStartCash);

            w.Write(d.ClubInfos.Count);
            foreach (KeyValuePair<int, ClubInfo> c in d.ClubInfos)
            {
                w.Write(c.Key);
                w.Write(c.Value.Founded);
                w.Write(c.Value.Stadium ?? string.Empty);
                w.Write(c.Value.ClubValue);
                w.Write(c.Value.Tier ?? string.Empty);
                w.Write(c.Value.History ?? string.Empty);
            }

            w.Write(d.Stadiums.Count);
            foreach (KeyValuePair<int, StadiumState> s in d.Stadiums)
            {
                StadiumState st = s.Value;
                w.Write(s.Key);
                w.Write(st.Name ?? string.Empty);
                w.Write(st.Capacity);
                w.Write(st.ParkingSpaces);
                w.Write(st.ParkingLevel);
                w.Write(st.EquipmentLevel);
                w.Write(st.ServicesLevel);
                w.Write((int)st.Pitch);
                w.Write(st.Works.Count);
                foreach (RemodelWork work in st.Works)
                {
                    w.Write((int)work.Category);
                    w.Write(work.Tier);
                    w.Write(work.TotalWeeks);
                    w.Write(work.RemainingWeeks);
                    w.Write(work.Cost);
                }
            }

            SupporterState sup = d.Supporters ?? new SupporterState();
            w.Write(sup.Relation);
            w.Write(sup.Unrest);
            w.Write(sup.CriticalStreak);
            w.Write(sup.PendingFixtureId);
            w.Write(sup.PendingSuggestedPrice);
            w.Write(sup.RequestsAccepted);
            w.Write(sup.RequestsIgnored);

            w.Write(d.TicketHistory.Count);
            foreach (TicketRecord t in d.TicketHistory)
            {
                w.Write(t.FixtureId);
                w.Write(t.Date.Ticks);
                w.Write(t.Price);
                w.Write(t.MinIdeal);
                w.Write(t.MaxIdeal);
                w.Write(t.Attendance);
                w.Write(t.Revenue);
            }

            w.Write(d.Signings.Count);
            foreach (SigningRecord g in d.Signings)
            {
                w.Write(g.PlayerId);
                w.Write(g.Name ?? string.Empty);
                w.Write(g.Fee);
                w.Write(g.Date.Ticks);
                w.Write(g.Matches);
                w.Write(g.RatingSum);
                w.Write(g.Contributions);
                w.Write(g.TeamMatches);
            }

            w.Write(d.ManagementHistory.Count);
            foreach (ManagementHistoryEntry h in d.ManagementHistory)
            {
                w.Write(h.Date.Ticks);
                w.Write(h.Supporters);
                w.Write(h.Squad);
                w.Write(h.Budget);
                w.Write(h.Total);
            }
        }

        private static void ReadClubManagement(BinaryReader r, CareerData d)
        {
            d.Manager = new ManagerProfile
            {
                FirstName = r.ReadString(), LastName = r.ReadString(), Nationality = r.ReadString(), AvatarIndex = r.ReadInt32(),
                Outfit = (ManagerOutfit)r.ReadInt32(), Reputation = r.ReadSingle(), Popularity = r.ReadSingle(), Sacked = r.ReadBoolean()
            };
            d.TicketLevel = (TicketLevel)r.ReadInt32();
            d.NextPriceOverride = r.ReadInt64();
            d.SeasonStartCash = r.ReadInt64();

            int infos = r.ReadInt32();
            for (int i = 0; i < infos; i++)
            {
                d.ClubInfos[r.ReadInt32()] = new ClubInfo
                {
                    Founded = r.ReadInt32(), Stadium = r.ReadString(), ClubValue = r.ReadInt64(), Tier = r.ReadString(), History = r.ReadString()
                };
            }

            int stadiums = r.ReadInt32();
            for (int i = 0; i < stadiums; i++)
            {
                int club = r.ReadInt32();
                var st = new StadiumState
                {
                    Name = r.ReadString(), Capacity = r.ReadInt32(), ParkingSpaces = r.ReadInt32(), ParkingLevel = r.ReadInt32(),
                    EquipmentLevel = r.ReadInt32(), ServicesLevel = r.ReadInt32(), Pitch = (PitchCondition)r.ReadInt32()
                };
                int works = r.ReadInt32();
                for (int k = 0; k < works; k++)
                {
                    st.Works.Add(new RemodelWork
                    {
                        Category = (RemodelCategory)r.ReadInt32(), Tier = r.ReadInt32(), TotalWeeks = r.ReadInt32(), RemainingWeeks = r.ReadInt32(),
                        Cost = r.ReadInt64()
                    });
                }

                d.Stadiums[club] = st;
            }

            d.Supporters = new SupporterState
            {
                Relation = r.ReadSingle(), Unrest = r.ReadSingle(), CriticalStreak = r.ReadInt32(), PendingFixtureId = r.ReadInt32(),
                PendingSuggestedPrice = r.ReadInt64(), RequestsAccepted = r.ReadInt32(), RequestsIgnored = r.ReadInt32()
            };

            int tickets = r.ReadInt32();
            for (int i = 0; i < tickets; i++)
            {
                d.TicketHistory.Add(new TicketRecord
                {
                    FixtureId = r.ReadInt32(), Date = new DateTime(r.ReadInt64()), Price = r.ReadInt64(), MinIdeal = r.ReadInt64(), MaxIdeal = r.ReadInt64(),
                    Attendance = r.ReadSingle(), Revenue = r.ReadInt64()
                });
            }

            int signings = r.ReadInt32();
            for (int i = 0; i < signings; i++)
            {
                d.Signings.Add(new SigningRecord
                {
                    PlayerId = r.ReadInt32(), Name = r.ReadString(), Fee = r.ReadInt64(), Date = new DateTime(r.ReadInt64()), Matches = r.ReadInt32(),
                    RatingSum = r.ReadSingle(), Contributions = r.ReadInt32(), TeamMatches = r.ReadInt32()
                });
            }

            int history = r.ReadInt32();
            for (int i = 0; i < history; i++)
            {
                d.ManagementHistory.Add(new ManagementHistoryEntry
                {
                    Date = new DateTime(r.ReadInt64()), Supporters = r.ReadSingle(), Squad = r.ReadSingle(), Budget = r.ReadSingle(), Total = r.ReadSingle()
                });
            }
        }
    }
}
