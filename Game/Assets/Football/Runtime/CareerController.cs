using System;
using System.Globalization;
using System.IO;
using Football.Core.Career;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;
using Football.Core.MatchSim;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Une la pantalla de carrera con el núcleo: crea o carga la partida (<see cref="CareerFile"/> en la carpeta de datos del
    /// teléfono), guarda sola después de cada partido, y prepara el partido 3D del día con los equipos que arma
    /// <see cref="CareerSession"/>. El usuario siempre juega como el lado 0 del motor; si en el calendario es visitante, el
    /// informe se da vuelta antes de cargarlo. Sin lógica de juego: solo traslada pedidos y resultados.
    /// </summary>
    public sealed class CareerController
    {
        private const string CareerFileName = "carrera.potrero";
        private const string TournamentFileName = "torneo.potrero";
        private static readonly string[] ConfigNames = { "career", "finance", "management", "matchsim" };

        private readonly CareerScreen _screen;
        private bool _tournament;
        private bool _userIsHome;

        /// <summary>Partida en curso (null en la elección de club).</summary>
        public CareerSession Session { get; private set; }

        /// <summary>Se pidió jugar el partido de hoy en 3D.</summary>
        public bool PlayRequested { get; private set; }

        /// <summary>Se pidió volver al menú principal.</summary>
        public bool ExitRequested { get; private set; }

        /// <summary>Crea el controlador.</summary>
        /// <param name="screen">Pantalla de carrera.</param>
        public CareerController(CareerScreen screen)
        {
            _screen = screen;
        }

        /// <summary>Abre el modo (elección de club, con la opción de continuar si hay partida guardada).</summary>
        /// <param name="tournament">Modo Torneo.</param>
        /// <param name="favorite">Club preseleccionado.</param>
        public void Open(bool tournament, int favorite)
        {
            _tournament = tournament;
            Session = null;
            _screen.Configure(Configs().Career);
            _screen.ShowStart(tournament, favorite, File.Exists(SavePath));
        }

        /// <summary>Vuelve a la pantalla de la partida (después del partido 3D).</summary>
        public void Resume()
        {
            if (Session.Data.Manager.Sacked)
            {
                Sack();
                return;
            }

            _screen.ShowSession(Session);
        }

        /// <summary>
        /// Aspecto del estadio del partido de hoy: la ocupación que calcula el núcleo con el precio y la importancia, los
        /// anillos según la capacidad y los andamios si hay una ampliación en obra.
        /// </summary>
        public StadiumLook MatchStadium()
        {
            Fixture f = Session?.PendingUserFixture;
            if (f == null || !Session.Data.Stadiums.TryGetValue(f.HomeClubId, out StadiumState stadium))
            {
                return StadiumLook.Full;
            }

            return StadiumLook.From(stadium, Session.ExpectedAttendance(f), Session.ForecastOf(f).Weather); // el mismo cielo que anuncia el pronóstico
        }

        /// <summary>Oculta la pantalla.</summary>
        public void Hide() => _screen.Hide();

        /// <summary>Procesa la pantalla y sus pedidos.</summary>
        public void Update()
        {
            PlayRequested = false;
            ExitRequested = false;
            _screen.Update();
            switch (_screen.Action)
            {
                case CareerAction.NewGame:
                    StartNew(_screen.ChosenTeam, _screen.Manager, _screen.PrimeraSize, _screen.Vintage);
                    break;
                case CareerAction.Sacked:
                    Sack();
                    break;
                case CareerAction.LoadGame:
                    Load();
                    break;
                case CareerAction.Play:
                    PlayRequested = Session?.PendingUserFixture != null;
                    break;
                case CareerAction.Save:
                    Save(true);
                    break;
                case CareerAction.Exit:
                    if (Session != null)
                    {
                        Save(false);
                    }

                    ExitRequested = true;
                    break;
            }
        }

        // ------------------------------------------------------------------ partido 3D

        /// <summary>El usuario juega de local el partido preparado con <see cref="PrepareMatch"/>.</summary>
        public bool UserIsHome => _userIsHome;

        /// <summary>
        /// Arma los dos equipos del partido de hoy para el motor 3D: el del usuario primero. Devuelve también las definiciones
        /// del archivo de equipos (nombres y camisetas).
        /// </summary>

        public void PrepareMatch(out MatchTeamSetup user, out MatchTeamSetup rival, out TeamDefinition userTeam, out TeamDefinition rivalTeam)
        {
            Session.PrepareUserMatch(out MatchTeamSetup home, out MatchTeamSetup away);
            Fixture f = Session.PendingUserFixture;
            _userIsHome = f.HomeClubId == Session.Data.UserClubId;
            user = _userIsHome ? home : away;
            rival = _userIsHome ? away : home;
            userTeam = Definition(Session.Data.UserClubId);
            rivalTeam = Definition(_userIsHome ? f.AwayClubId : f.HomeClubId);
        }

        /// <summary>Carga el resultado del partido jugado en 3D y guarda.</summary>
        /// <param name="report">Informe del motor (lado 0 = usuario).</param>
        public void CompleteMatch(MatchReport report)
        {
            Session.CompleteUserMatch(_userIsHome ? report : Swapped(report));
            Save(false);
        }

        /// <summary>El usuario abandonó el partido: el resultado se simula para que la temporada siga.</summary>
        public void AbandonMatch()
        {
            if (Session?.PendingUserFixture != null)
            {
                Session.SimulateUserMatch();
                _screen.SetStatus(Locale.Get("career.abandoned"));
                Save(false);
            }
        }

        private static MatchReport Swapped(MatchReport report)
        {
            TeamReport first = report.Teams[0];
            report.Teams[0] = report.Teams[1];
            report.Teams[1] = first;
            for (int i = 0; i < report.Shots.Count; i++)
            {
                ShotRecord s = report.Shots[i];
                s.Team = 1 - s.Team;
                report.Shots[i] = s;
            }

            return report;
        }

        private TeamDefinition Definition(int clubId)
        {
            string name = Session.Data.Club(clubId).Name;
            TeamDefinition found = Array.Find(TeamCatalog.AllTeams, t => t.Name == name);
            return found ?? new TeamDefinition { Name = name, ShortName = Session.Data.Club(clubId).ShortName };
        }

        // ------------------------------------------------------------------ partida

        private string SavePath => Path.Combine(Application.persistentDataPath, _tournament ? TournamentFileName : CareerFileName);

        /// <summary>La directiva echó al DT: la carrera termina (se borra la partida) y se vuelve a crear un DT.</summary>
        private void Sack()
        {
            string club = Session.Data.Club(Session.Data.UserClubId).Name;
            try
            {
                File.Delete(SavePath);
            }
            catch (IOException e)
            {
                Debug.LogWarning("Football: no se pudo borrar la partida (" + e.Message + ")");
            }

            Session = null;
            _screen.ShowStart(_tournament, 0, false);
            _screen.SetStatus(string.Format(CultureInfo.InvariantCulture, Locale.Get("career.sacked_end"), club));
        }

        private void StartNew(int teamIndex, ManagerProfile manager, int primeraSize, bool vintage)
        {
            try
            {
                vintage = vintage && !_tournament;
                TeamDatabase db = vintage ? TeamCatalog.VintageDatabase : TeamCatalog.Database;
                int seed = (int)(DateTime.Now.Ticks & int.MaxValue);
                Session = CareerSession.NewGame(db, TeamCatalog.AllTeams[teamIndex].Name, DateTime.Now.Year, seed, Configs(), _tournament,
                    _tournament ? null : manager, _tournament ? 0 : primeraSize);
                _screen.ShowSession(Session);
                Save(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _screen.SetStatus(e.Message);
            }
        }

        private void Load()
        {
            try
            {
                CareerData data = CareerFile.Load(SavePath);
                if (data == null)
                {
                    return;
                }

                Session = new CareerSession(data, Configs());
                _screen.ShowSession(Session);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _screen.SetStatus(string.Format(CultureInfo.InvariantCulture, Locale.Get("career.save_failed"), e.Message));
            }
        }

        private void Save(bool announce)
        {
            if (Session == null)
            {
                return;
            }

            try
            {
                Session.SyncData();
                CareerFile.Save(SavePath, Session.Data);
                if (announce)
                {
                    _screen.SetStatus(Locale.Get("career.saved"));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _screen.SetStatus(string.Format(CultureInfo.InvariantCulture, Locale.Get("career.save_failed"), e.Message));
            }
        }

        /// <summary>Parámetros de la carrera leídos de <c>Resources</c> (copias de <c>Data/balance</c>); si falta uno, los de fábrica.</summary>
        private static CareerConfigs Configs()
        {
            var configs = new CareerConfigs { Match = BalanceConfig.Create() };
            configs.Report = configs.Match.Report;
            object[] targets = { configs.Career, configs.Finance, configs.Management, configs.Sim };
            for (int i = 0; i < ConfigNames.Length; i++)
            {
                TextAsset text = Resources.Load<TextAsset>(ConfigNames[i]);
                if (text == null)
                {
                    continue;
                }

                try
                {
                    JsonUtility.FromJsonOverwrite(text.text, targets[i]);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Football: no se pudo leer " + ConfigNames[i] + ".json (" + e.Message + "); se usan los valores de fábrica");
                }
            }

            return configs;
        }
    }
}
