using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Fondo vivo de los menús: un 3 contra 3 entre la IA en la cancha cerrada (pocos jugadores, liviano para teléfonos de
    /// gama baja) que se ve detrás de las pantallas semitransparentes. Se detiene al empezar un partido.
    /// </summary>
    public sealed class MenuBackdrop
    {
        private const float HalfSeconds = 90f;
        private const int AiDifficulty = 2;

        private MatchEngine _engine;
        private MatchView _view;
        private MatchSnapshot _previous = new MatchSnapshot();
        private MatchSnapshot _current = new MatchSnapshot();
        private float _accumulator;
        private ulong _seed = 1;

        /// <summary>Hay un partido de fondo en marcha.</summary>
        public bool Running => _view != null;

        /// <summary>Empieza (o sigue) el partido de fondo.</summary>
        public void Start()
        {
            if (_view != null)
            {
                return;
            }

            try
            {
                TeamDefinition[] teams = TeamCatalog.Teams;
                TeamDefinition a = teams[(int)(_seed % (ulong)teams.Length)];
                TeamDefinition b = teams[(int)((_seed + 1) % (ulong)teams.Length)];
                MatchConfig config = BalanceConfig.Create(true);
                config.Rules.HalfDuration = HalfSeconds;
                var rng = new SeededRandom(_seed++);
                MatchTeamSetup home = TeamBuilder.Build(a, config, rng.Fork("a"), SquadSelection.Auto(a, Formations.Indoor3, 0));
                MatchTeamSetup away = TeamBuilder.Build(b, config, rng.Fork("b"), SquadSelection.Auto(b, Formations.Indoor3, 0));
                _engine = new MatchEngine(config, rng.Fork("engine"), home, away, AiDifficulty, AiDifficulty);
                _view = new MatchView(_engine.Pitch, KitColors.From(a.Home), KitColors.From(b.Away), _engine.PlayersPerTeam, config.TickRate);
                _previous.CopyFrom(_engine);
                _current.CopyFrom(_engine);
                _accumulator = 0f;
            }
            catch (System.Exception e)
            {
                // El fondo es decorativo: si algo falla, los menús siguen funcionando sobre un color liso.
                Debug.LogWarning("Football: no se pudo crear el fondo del menú (" + e.Message + ")");
                Stop();
            }
        }

        /// <summary>Detiene y libera el partido de fondo.</summary>
        public void Stop()
        {
            _view?.Destroy();
            _view = null;
            _engine = null;
        }

        /// <summary>Avanza el partido de fondo y lo dibuja.</summary>
        public void Update(float deltaTime)
        {
            if (_view == null || _engine == null)
            {
                return;
            }

            if (_engine.IsFinished)
            {
                Stop();
                Start();
                return;
            }

            float tick = 1f / _engine.Config.TickRate;
            _accumulator += Mathf.Min(deltaTime, 0.1f);
            while (_accumulator >= tick && !_engine.IsFinished)
            {
                MatchSnapshot swap = _previous;
                _previous = _current;
                _current = swap;
                _engine.Tick();
                _current.CopyFrom(_engine);
                _accumulator -= tick;
            }

            _view.Render(_previous, _current, Mathf.Clamp01(_accumulator / tick), -1, -1, deltaTime);
        }
    }
}
