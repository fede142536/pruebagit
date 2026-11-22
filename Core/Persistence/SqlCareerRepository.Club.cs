using System.Collections.Generic;
using System.Data.Common;
using Football.Core.Career;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;

namespace Football.Core.Persistence
{
    // Tablas de la migración 10: DT, datos de clubes, estadios y obras, socios, historial de entradas, fichajes y medidor.
    public sealed partial class SqlCareerRepository
    {
        private void SaveClubManagement(CareerData data, DbTransaction tx)
        {
            ManagerProfile m = data.Manager ?? new ManagerProfile();
            SqlHelper.Execute(Connection,
                @"INSERT INTO manager (id, first_name, last_name, nationality, avatar, outfit, reputation, popularity, sacked)
                  VALUES (1, @first, @last, @nat, @avatar, @outfit, @rep, @pop, @sacked)", tx,
                ("@first", m.FirstName ?? string.Empty), ("@last", m.LastName ?? string.Empty), ("@nat", m.Nationality ?? string.Empty),
                ("@avatar", m.AvatarIndex), ("@outfit", (int)m.Outfit), ("@rep", (double)m.Reputation), ("@pop", (double)m.Popularity),
                ("@sacked", m.Sacked ? 1 : 0));
            foreach (KeyValuePair<int, ClubInfo> c in data.ClubInfos)
            {
                SqlHelper.Execute(Connection,
                    "INSERT INTO club_info (club_id, founded, stadium_name, club_value, tier, history) VALUES (@id, @f, @s, @v, @t, @h)", tx,
                    ("@id", c.Key), ("@f", c.Value.Founded), ("@s", c.Value.Stadium ?? string.Empty), ("@v", c.Value.ClubValue),
                    ("@t", c.Value.Tier ?? string.Empty), ("@h", c.Value.History ?? string.Empty));
            }

            foreach (KeyValuePair<int, StadiumState> s in data.Stadiums)
            {
                StadiumState st = s.Value;
                SqlHelper.Execute(Connection,
                    @"INSERT INTO club_stadiums (club_id, name, capacity, parking_spaces, parking_level, equipment_level, services_level, pitch)
                      VALUES (@id, @n, @c, @p, @pl, @el, @sl, @pitch)", tx,
                    ("@id", s.Key), ("@n", st.Name ?? string.Empty), ("@c", st.Capacity), ("@p", st.ParkingSpaces), ("@pl", st.ParkingLevel),
                    ("@el", st.EquipmentLevel), ("@sl", st.ServicesLevel), ("@pitch", (int)st.Pitch));
                foreach (RemodelWork w in st.Works)
                {
                    SqlHelper.Execute(Connection,
                        @"INSERT INTO stadium_works (club_id, category, tier, total_weeks, remaining_weeks, cost)
                          VALUES (@id, @cat, @tier, @total, @left, @cost)", tx,
                        ("@id", s.Key), ("@cat", (int)w.Category), ("@tier", w.Tier), ("@total", w.TotalWeeks), ("@left", w.RemainingWeeks), ("@cost", w.Cost));
                }
            }

            SupporterState sup = data.Supporters ?? new SupporterState();
            SqlHelper.Execute(Connection,
                @"INSERT INTO supporters (id, relation, unrest, critical_streak, pending_fixture_id, pending_price, accepted, ignored)
                  VALUES (1, @r, @u, @streak, @fixture, @price, @acc, @ign)", tx,
                ("@r", (double)sup.Relation), ("@u", (double)sup.Unrest), ("@streak", sup.CriticalStreak), ("@fixture", sup.PendingFixtureId),
                ("@price", sup.PendingSuggestedPrice), ("@acc", sup.RequestsAccepted), ("@ign", sup.RequestsIgnored));
            foreach (TicketRecord t in data.TicketHistory)
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO ticket_history (fixture_id, date, price, min_ideal, max_ideal, attendance, revenue)
                      VALUES (@f, @d, @p, @min, @max, @a, @rev)", tx,
                    ("@f", t.FixtureId), ("@d", DateFormat.ToText(t.Date)), ("@p", t.Price), ("@min", t.MinIdeal), ("@max", t.MaxIdeal),
                    ("@a", (double)t.Attendance), ("@rev", t.Revenue));
            }

            foreach (SigningRecord g in data.Signings)
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO signings (player_id, name, fee, date, matches, rating_sum, contributions, team_matches)
                      VALUES (@p, @n, @fee, @d, @m, @r, @c, @t)", tx,
                    ("@p", g.PlayerId), ("@n", g.Name ?? string.Empty), ("@fee", g.Fee), ("@d", DateFormat.ToText(g.Date)), ("@m", g.Matches),
                    ("@r", (double)g.RatingSum), ("@c", g.Contributions), ("@t", g.TeamMatches));
            }

            foreach (ManagementHistoryEntry h in data.ManagementHistory)
            {
                SqlHelper.Execute(Connection, "INSERT INTO management_history (date, supporters, squad, budget, total) VALUES (@d, @s, @q, @b, @t)", tx,
                    ("@d", DateFormat.ToText(h.Date)), ("@s", (double)h.Supporters), ("@q", (double)h.Squad), ("@b", (double)h.Budget), ("@t", (double)h.Total));
            }
        }

        private void LoadClubManagement(CareerData data)
        {
            Read("SELECT first_name, last_name, nationality, avatar, outfit, reputation, popularity, sacked FROM manager", r => data.Manager = new ManagerProfile
            {
                FirstName = r.GetString(0), LastName = r.GetString(1), Nationality = r.GetString(2), AvatarIndex = r.GetInt32(3),
                Outfit = (ManagerOutfit)r.GetInt32(4), Reputation = (float)r.GetDouble(5), Popularity = (float)r.GetDouble(6), Sacked = r.GetInt32(7) != 0
            });
            Read("SELECT club_id, founded, stadium_name, club_value, tier, history FROM club_info", r => data.ClubInfos[r.GetInt32(0)] = new ClubInfo
            {
                Founded = r.GetInt32(1), Stadium = r.GetString(2), ClubValue = r.GetInt64(3), Tier = r.GetString(4), History = r.GetString(5)
            });
            Read("SELECT club_id, name, capacity, parking_spaces, parking_level, equipment_level, services_level, pitch FROM club_stadiums",
                r => data.Stadiums[r.GetInt32(0)] = new StadiumState
                {
                    Name = r.GetString(1), Capacity = r.GetInt32(2), ParkingSpaces = r.GetInt32(3), ParkingLevel = r.GetInt32(4),
                    EquipmentLevel = r.GetInt32(5), ServicesLevel = r.GetInt32(6), Pitch = (PitchCondition)r.GetInt32(7)
                });
            Read("SELECT club_id, category, tier, total_weeks, remaining_weeks, cost FROM stadium_works ORDER BY club_id, category",
                r => data.Stadiums[r.GetInt32(0)].Works.Add(new RemodelWork
                {
                    Category = (RemodelCategory)r.GetInt32(1), Tier = r.GetInt32(2), TotalWeeks = r.GetInt32(3), RemainingWeeks = r.GetInt32(4), Cost = r.GetInt64(5)
                }));
            Read("SELECT relation, unrest, critical_streak, pending_fixture_id, pending_price, accepted, ignored FROM supporters", r => data.Supporters = new SupporterState
            {
                Relation = (float)r.GetDouble(0), Unrest = (float)r.GetDouble(1), CriticalStreak = r.GetInt32(2), PendingFixtureId = r.GetInt32(3),
                PendingSuggestedPrice = r.GetInt64(4), RequestsAccepted = r.GetInt32(5), RequestsIgnored = r.GetInt32(6)
            });
            Read("SELECT fixture_id, date, price, min_ideal, max_ideal, attendance, revenue FROM ticket_history ORDER BY date, fixture_id",
                r => data.TicketHistory.Add(new TicketRecord
                {
                    FixtureId = r.GetInt32(0), Date = DateFormat.Parse(r.GetString(1)), Price = r.GetInt64(2), MinIdeal = r.GetInt64(3), MaxIdeal = r.GetInt64(4),
                    Attendance = (float)r.GetDouble(5), Revenue = r.GetInt64(6)
                }));
            Read("SELECT player_id, name, fee, date, matches, rating_sum, contributions, team_matches FROM signings ORDER BY id", r => data.Signings.Add(new SigningRecord
            {
                PlayerId = r.GetInt32(0), Name = r.GetString(1), Fee = r.GetInt64(2), Date = DateFormat.Parse(r.GetString(3)), Matches = r.GetInt32(4),
                RatingSum = (float)r.GetDouble(5), Contributions = r.GetInt32(6), TeamMatches = r.GetInt32(7)
            }));
            Read("SELECT date, supporters, squad, budget, total FROM management_history ORDER BY id", r => data.ManagementHistory.Add(new ManagementHistoryEntry
            {
                Date = DateFormat.Parse(r.GetString(0)), Supporters = (float)r.GetDouble(1), Squad = (float)r.GetDouble(2), Budget = (float)r.GetDouble(3),
                Total = (float)r.GetDouble(4)
            }));
        }
    }
}
