using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Football.Core.Domain;
using Football.Core.MatchCore;
using Football.Core.Persistence;

namespace Football.Core.Career
{
    /// <summary>
    /// Archivo de partida del modo carrera en formato binario propio: guarda exactamente lo mismo que
    /// <see cref="SqlCareerRepository"/> pero sin depender de un proveedor de SQLite (Unity en Android no trae uno). Se
    /// escribe con <see cref="AtomicSave.WriteFile"/>: archivo temporal, reemplazo atómico y respaldos rotativos.
    /// </summary>
    public static partial class CareerFile
    {
        private const uint Magic = 0x50545243; // "PTRC"
        private const int Version = 4; // 2: estadísticas de temporada; 3: DT, estadio y obras, socios, entradas, fichajes y medidor; 4: alineación elegida

        /// <summary>Guarda la partida en un archivo, sin riesgo de dejarlo a medias.</summary>
        /// <param name="path">Ruta del archivo.</param>
        /// <param name="data">Estado (llamar antes a <see cref="CareerSession.SyncData"/>).</param>
        /// <param name="backups">Respaldos a conservar.</param>
        public static void Save(string path, CareerData data, int backups = 3)
        {
            AtomicSave.WriteFile(path, temp =>
            {
                using (FileStream stream = File.Create(temp))
                {
                    Write(stream, data);
                }
            }, backups);
        }

        /// <summary>Lee una partida de un archivo (null si no existe).</summary>
        /// <param name="path">Ruta.</param>
        /// <exception cref="InvalidDataException">Si el archivo no es una partida o es de una versión desconocida.</exception>
        public static CareerData Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using (FileStream stream = File.OpenRead(path))
            {
                return Read(stream);
            }
        }

        /// <summary>Escribe la partida en un flujo.</summary>
        public static void Write(Stream stream, CareerData d)
        {
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(d.Seed);
                w.Write(d.Season);
                w.Write(d.Today.Ticks);
                w.Write(d.UserClubId);
                w.Write(d.TournamentOnly);
                UserTactics t = d.Tactics ?? new UserTactics();
                w.Write(t.Formation ?? string.Empty);
                w.Write(t.DefensiveFormation ?? string.Empty);
                w.Write(t.Style ?? string.Empty);
                w.Write((int)t.FullbackDuty);
                w.Write((int)t.MidfieldDuty);
                w.Write((int)t.StrikerDuty);
                w.Write(t.Lineup ?? string.Empty);

                w.Write(d.Clubs.Count);
                foreach (Club c in d.Clubs)
                {
                    w.Write(c.Id);
                    w.Write(c.Name ?? string.Empty);
                    w.Write(c.ShortName ?? string.Empty);
                    w.Write(c.Country ?? string.Empty);
                    w.Write(c.PrimaryColor ?? string.Empty);
                    w.Write(c.SecondaryColor ?? string.Empty);
                    WriteInt(w, c.StadiumId);
                    w.Write(c.TransferBudget);
                    w.Write(c.WageBudget);
                    ClubFinances f = d.Finances[c.Id];
                    w.Write(f.Cash);
                    w.Write(f.TransferBudget);
                    w.Write(f.WeeklyWageBudget);
                    w.Write(f.Members);
                    w.Write(f.TicketPrice);
                    w.Write(f.Reputation);
                    w.Write(f.Ledger.Count);
                    foreach (LedgerEntry e in f.Ledger)
                    {
                        w.Write(e.Date.Ticks);
                        w.Write((int)e.Category);
                        w.Write(e.Amount);
                    }

                    foreach (Facility facility in d.Facilities[c.Id].All)
                    {
                        w.Write(facility.Level);
                        w.Write(facility.WeeksToFinish);
                    }
                }

                w.Write(d.Players.Count);
                foreach (Player p in d.Players.Values)
                {
                    w.Write(p.Id);
                    WriteInt(w, p.ClubId);
                    w.Write(p.FirstName ?? string.Empty);
                    w.Write(p.LastName ?? string.Empty);
                    w.Write(p.BirthDate.Ticks);
                    w.Write((int)p.Position);
                    w.Write(p.Potential);
                    w.Write((int)p.Personality);
                    w.Write(p.JoinedOn.Ticks);
                    foreach (AttributeId a in AttributeSet.All)
                    {
                        w.Write(p.Attributes[a]);
                    }

                    w.Write(p.Contract != null);
                    if (p.Contract != null)
                    {
                        w.Write(p.Contract.WeeklyWage);
                        w.Write(p.Contract.ExpiresOn.Ticks);
                        w.Write(p.Contract.ReleaseClause);
                        w.Write((int)p.Contract.Promise);
                    }

                    PlayerCondition k = d.Conditions.TryGetValue(p.Id, out PlayerCondition found) ? found : new PlayerCondition { PlayerId = p.Id };
                    w.Write(k.Form);
                    w.Write(k.Morale);
                    w.Write(k.Fitness);
                    w.Write(k.InjuryDays);
                    w.Write(k.SuspendedMatches);
                    w.Write(k.AccumulatedYellows);
                    w.Write(k.SeasonMinutes);
                    w.Write((int)k.Promise);
                    w.Write(k.TeamMatchesSincePromise);
                    w.Write(k.MinutesSincePromise);
                }

                w.Write(d.Staff.Count);
                foreach (StaffMember s in d.Staff)
                {
                    w.Write(s.Id);
                    WriteInt(w, s.ClubId);
                    w.Write(s.Name ?? string.Empty);
                    w.Write((int)s.Role);
                    w.Write(s.Ability);
                    w.Write(s.WeeklyWage);
                    WriteInt(w, s.FormerPlayerId);
                }

                w.Write(d.Competitions.Count);
                foreach (CompetitionInfo c in d.Competitions)
                {
                    w.Write(c.Id);
                    w.Write(c.Name ?? string.Empty);
                    w.Write(c.Season);
                    w.Write((int)c.Kind);
                }

                w.Write(d.Fixtures.Count);
                foreach (Fixture f in d.Fixtures)
                {
                    w.Write(f.Id);
                    w.Write(f.CompetitionId);
                    w.Write(f.Round);
                    w.Write(f.Group);
                    w.Write(f.Date.Ticks);
                    w.Write(f.HomeClubId);
                    w.Write(f.AwayClubId);
                    WriteInt(w, f.HomeGoals);
                    WriteInt(w, f.AwayGoals);
                    WriteInt(w, f.HomePenalties);
                    WriteInt(w, f.AwayPenalties);
                }

                w.Write(d.Rivalries.Count);
                foreach (ClubRivalry r in d.Rivalries)
                {
                    w.Write(r.ClubA);
                    w.Write(r.ClubB);
                    w.Write((int)r.Level);
                    w.Write(r.Name ?? string.Empty);
                }

                w.Write(d.News.Count);
                foreach (NewsItem n in d.News)
                {
                    w.Write(n.Date.Ticks);
                    w.Write(n.ClubId);
                    w.Write(n.Key ?? string.Empty);
                    string[] args = n.Args ?? Array.Empty<string>();
                    w.Write(args.Length);
                    foreach (string a in args)
                    {
                        w.Write(a ?? string.Empty);
                    }

                    w.Write(n.Read);
                }

                w.Write(d.Scouting.Count);
                foreach ((int clubId, int playerId, float knowledge) in d.Scouting)
                {
                    w.Write(clubId);
                    w.Write(playerId);
                    w.Write(knowledge);
                }

                w.Write(d.Records.Count);
                foreach (RecordEntry r in d.Records)
                {
                    w.Write(r.Key ?? string.Empty);
                    w.Write(r.Value);
                    w.Write(r.HolderId);
                    w.Write(r.Season);
                }

                w.Write(d.CareerGoals.Count);
                foreach (KeyValuePair<int, int> g in d.CareerGoals)
                {
                    w.Write(g.Key);
                    w.Write(g.Value);
                }

                w.Write(d.HallOfFame.Count);
                foreach (int id in d.HallOfFame)
                {
                    w.Write(id);
                }

                w.Write(d.SeasonStats.Count);
                foreach (KeyValuePair<int, CardStats> st in d.SeasonStats)
                {
                    w.Write(st.Key);
                    w.Write(st.Value.Matches);
                    w.Write(st.Value.Goals);
                    w.Write(st.Value.Assists);
                    w.Write(st.Value.RatingSum);
                    w.Write(st.Value.Rated);
                }

                WriteClubManagement(w, d);
            }
        }

        /// <summary>Lee una partida de un flujo.</summary>
        /// <exception cref="InvalidDataException">Si no es una partida o es de una versión desconocida.</exception>
        public static CareerData Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (r.ReadUInt32() != Magic)
                {
                    throw new InvalidDataException("El archivo no es una partida de POTRERO.");
                }

                int version = r.ReadInt32();
                if (version < 1 || version > Version)
                {
                    throw new InvalidDataException("Versión de partida desconocida: " + version);
                }

                var d = new CareerData
                {
                    Seed = r.ReadInt32(), Season = r.ReadInt32(), Today = new DateTime(r.ReadInt64()), UserClubId = r.ReadInt32(),
                    TournamentOnly = r.ReadBoolean(),
                    Tactics = new UserTactics
                    {
                        Formation = r.ReadString(), DefensiveFormation = r.ReadString(), Style = r.ReadString(),
                        FullbackDuty = (PlayerDuty)r.ReadInt32(), MidfieldDuty = (PlayerDuty)r.ReadInt32(), StrikerDuty = (PlayerDuty)r.ReadInt32()
                    }
                };
                if (version >= 4)
                {
                    d.Tactics.Lineup = r.ReadString();
                }

                int clubs = r.ReadInt32();
                for (int i = 0; i < clubs; i++)
                {
                    var c = new Club
                    {
                        Id = r.ReadInt32(), Name = r.ReadString(), ShortName = r.ReadString(), Country = r.ReadString(), PrimaryColor = r.ReadString(),
                        SecondaryColor = r.ReadString(), StadiumId = ReadInt(r), TransferBudget = r.ReadInt64(), WageBudget = r.ReadInt64()
                    };
                    d.Clubs.Add(c);
                    var f = new ClubFinances
                    {
                        ClubId = c.Id, Cash = r.ReadInt64(), TransferBudget = r.ReadInt64(), WeeklyWageBudget = r.ReadInt64(), Members = r.ReadInt32(),
                        TicketPrice = r.ReadInt64(), Reputation = r.ReadInt32()
                    };
                    int entries = r.ReadInt32();
                    for (int e = 0; e < entries; e++)
                    {
                        f.Ledger.Add(new LedgerEntry { Date = new DateTime(r.ReadInt64()), Category = (FinanceCategory)r.ReadInt32(), Amount = r.ReadInt64() });
                    }

                    d.Finances[c.Id] = f;
                    var facilities = new ClubFacilities { ClubId = c.Id };
                    foreach (Facility facility in facilities.All)
                    {
                        facility.Level = r.ReadInt32();
                        facility.WeeksToFinish = r.ReadInt32();
                    }

                    d.Facilities[c.Id] = facilities;
                }

                int players = r.ReadInt32();
                for (int i = 0; i < players; i++)
                {
                    var p = new Player
                    {
                        Id = r.ReadInt32(), ClubId = ReadInt(r), FirstName = r.ReadString(), LastName = r.ReadString(), BirthDate = new DateTime(r.ReadInt64()),
                        Position = (Position)r.ReadInt32(), Potential = r.ReadInt32(), Personality = (PersonalityTrait)r.ReadInt32(),
                        JoinedOn = new DateTime(r.ReadInt64())
                    };
                    foreach (AttributeId a in AttributeSet.All)
                    {
                        p.Attributes[a] = r.ReadInt32();
                    }

                    if (r.ReadBoolean())
                    {
                        p.Contract = new Contract
                        {
                            WeeklyWage = r.ReadInt64(), ExpiresOn = new DateTime(r.ReadInt64()), ReleaseClause = r.ReadInt64(),
                            Promise = (PlayingTimePromise)r.ReadInt32()
                        };
                    }

                    d.Players[p.Id] = p;
                    d.Conditions[p.Id] = new PlayerCondition
                    {
                        PlayerId = p.Id, Form = r.ReadSingle(), Morale = r.ReadSingle(), Fitness = r.ReadSingle(), InjuryDays = r.ReadInt32(),
                        SuspendedMatches = r.ReadInt32(), AccumulatedYellows = r.ReadInt32(), SeasonMinutes = r.ReadSingle(),
                        Promise = (PlayingTimePromise)r.ReadInt32(), TeamMatchesSincePromise = r.ReadInt32(), MinutesSincePromise = r.ReadSingle()
                    };
                }

                int staff = r.ReadInt32();
                for (int i = 0; i < staff; i++)
                {
                    d.Staff.Add(new StaffMember
                    {
                        Id = r.ReadInt32(), ClubId = ReadInt(r), Name = r.ReadString(), Role = (StaffRole)r.ReadInt32(), Ability = r.ReadInt32(),
                        WeeklyWage = r.ReadInt64(), FormerPlayerId = ReadInt(r)
                    });
                }

                int competitions = r.ReadInt32();
                for (int i = 0; i < competitions; i++)
                {
                    d.Competitions.Add(new CompetitionInfo { Id = r.ReadInt32(), Name = r.ReadString(), Season = r.ReadInt32(), Kind = (TournamentKind)r.ReadInt32() });
                }

                int fixtures = r.ReadInt32();
                for (int i = 0; i < fixtures; i++)
                {
                    d.Fixtures.Add(new Fixture
                    {
                        Id = r.ReadInt32(), CompetitionId = r.ReadInt32(), Round = r.ReadInt32(), Group = r.ReadInt32(), Date = new DateTime(r.ReadInt64()),
                        HomeClubId = r.ReadInt32(), AwayClubId = r.ReadInt32(), HomeGoals = ReadInt(r), AwayGoals = ReadInt(r),
                        HomePenalties = ReadInt(r), AwayPenalties = ReadInt(r)
                    });
                }

                int rivalries = r.ReadInt32();
                for (int i = 0; i < rivalries; i++)
                {
                    d.Rivalries.Add(new ClubRivalry { ClubA = r.ReadInt32(), ClubB = r.ReadInt32(), Level = (RivalryLevel)r.ReadInt32(), Name = r.ReadString() });
                }

                int news = r.ReadInt32();
                for (int i = 0; i < news; i++)
                {
                    var n = new NewsItem { Date = new DateTime(r.ReadInt64()), ClubId = r.ReadInt32(), Key = r.ReadString() };
                    n.Args = new string[r.ReadInt32()];
                    for (int a = 0; a < n.Args.Length; a++)
                    {
                        n.Args[a] = r.ReadString();
                    }

                    n.Read = r.ReadBoolean();
                    d.News.Add(n);
                }

                int scouting = r.ReadInt32();
                for (int i = 0; i < scouting; i++)
                {
                    d.Scouting.Add((r.ReadInt32(), r.ReadInt32(), r.ReadSingle()));
                }

                int records = r.ReadInt32();
                for (int i = 0; i < records; i++)
                {
                    d.Records.Add(new RecordEntry { Key = r.ReadString(), Value = r.ReadSingle(), HolderId = r.ReadInt32(), Season = r.ReadInt32() });
                }

                int goals = r.ReadInt32();
                for (int i = 0; i < goals; i++)
                {
                    d.CareerGoals[r.ReadInt32()] = r.ReadInt32();
                }

                int fame = r.ReadInt32();
                for (int i = 0; i < fame; i++)
                {
                    d.HallOfFame.Add(r.ReadInt32());
                }

                if (version >= 2)
                {
                    int stats = r.ReadInt32();
                    for (int i = 0; i < stats; i++)
                    {
                        d.SeasonStats[r.ReadInt32()] = new CardStats
                        {
                            Matches = r.ReadInt32(), Goals = r.ReadInt32(), Assists = r.ReadInt32(), RatingSum = r.ReadSingle(), Rated = r.ReadInt32()
                        };
                    }
                }

                if (version >= 3)
                {
                    ReadClubManagement(r, d);
                }

                return d;
            }
        }

        private static void WriteInt(BinaryWriter w, int? value)
        {
            w.Write(value.HasValue);
            if (value.HasValue)
            {
                w.Write(value.Value);
            }
        }

        private static int? ReadInt(BinaryReader r) => r.ReadBoolean() ? r.ReadInt32() : (int?)null;
    }
}
