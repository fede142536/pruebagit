using UnityEngine;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>
    /// Reproduce las animaciones de fútbol (<see cref="AnimClips"/>) sobre los 20 huesos de un jugador. Elige el clip según lo que hace
    /// (reposo, caminar, trotar, correr, esprintar, patear, pasar, tirar, primer toque, cabezazo, barrida, tropiezo, saque de banda, estirada, achique,
    /// balón en las manos) y mezcla con fundido cada cambio, también desde y hacia la animación procedural (que sigue sirviendo para lo que no tiene clip:
    /// entrada de pie y festejo). Sin asignaciones por fotograma.
    /// </summary>
    public sealed class ClipPlayer
    {
        private const float BlendSeconds = 0.14f;
        private const float WalkBelow = 2.0f, JogBelow = 3.5f, RunBelow = 5.4f, Hysteresis = 0.35f;
        private const float WalkMinRate = 0.55f, WalkMaxRate = 1.25f, RunMinRate = 0.8f, RunMaxRate = 1.6f;
        private const float RunningShotSpeed = 3f;       // por encima, el tiro sale en carrera
        private const float HighDive = 1.5f;

        private enum Hold { None, Throw, Rush, Ball, Dance }

        private readonly Transform[] _bones;
        private readonly Transform _pelvis;
        private readonly Vector3 _pelvisBind;
        private readonly bool _keeper;
        private readonly Quaternion[] _from = new Quaternion[SkinnedBodyGeometry.BoneCount];
        private readonly Quaternion[] _last = new Quaternion[SkinnedBodyGeometry.BoneCount];
        private Vector3 _fromPelvis, _lastPelvis;
        private bool _hasLast;
        private float _blend = 1f;
        private AnimClip _clip;
        private float _time;
        private float _rate = 1f;
        private int _priority;
        private Hold _hold;
        private PlayerAction _lastAction;
        private AnimClip _idle, _keeperIdle, _walk, _jog, _run, _sprint, _kick, _shoot, _shootRun, _pass, _control, _header, _slide, _keeperKick;
        private AnimClip _diveRight, _diveLeft, _rush, _holdBall, _stumble, _throw, _throwHold;
        private AnimClip[] _dances;
        private static int _danceCounter;           // reparte los bailes de a uno por festejo (solo visual)
        private const float CelebrateStandSpeed = 1.2f; // por debajo, ya llegó y baila; por encima, todavía corre
        private int _band = -1; // 0 reposo, 1 caminata, 2 trote, 3 carrera, 4 esprint (con histéresis)

        /// <summary>Crea el reproductor para un jugador.</summary>
        /// <param name="bones">Los 20 huesos del jugador (<see cref="BodyBone"/>).</param>
        /// <param name="keeper">Es arquero.</param>
        public ClipPlayer(Transform[] bones, bool keeper)
        {
            _bones = bones;
            _pelvis = bones[(int)BodyBone.Pelvis];
            _pelvisBind = _pelvis.localPosition;
            _keeper = keeper;
            _idle = AnimClips.Find("idle");
            _keeperIdle = AnimClips.Find("keeper_idle") ?? _idle;
            _walk = AnimClips.Find("walk");
            _jog = AnimClips.Find("jog");
            _run = AnimClips.Find("run");
            _sprint = AnimClips.Find("sprint");
            _kick = AnimClips.Find("kick");
            _keeperKick = AnimClips.Find("keeper_kick");
            _shoot = AnimClips.Find("shoot");
            _shootRun = AnimClips.Find("shoot_run") ?? _shoot;
            _pass = AnimClips.Find("pass");
            _control = AnimClips.Find("control");
            _header = AnimClips.Find("header");
            _slide = AnimClips.Find("slide");
            _diveRight = AnimClips.Find("dive_right");
            _diveLeft = AnimClips.Find("dive_left");
            _rush = AnimClips.Find("rush");
            _holdBall = AnimClips.Find("hold");
            _stumble = AnimClips.Find("stumble");
            _throw = AnimClips.Find("throw");
            _throwHold = AnimClips.Find("throw_hold");
            var dances = new System.Collections.Generic.List<AnimClip>();
            foreach (string name in new[] { "celebrate_gangnam", "celebrate_robot", "celebrate_wave", "celebrate_victory", "celebrate_fist", "celebrate_clap" })
            {
                AnimClip dance = AnimClips.Find(name);
                if (dance != null)
                {
                    dances.Add(dance);
                }
            }

            _dances = dances.ToArray();
        }

        /// <summary>
        /// Avanza un fotograma y deja puestos los huesos: con un clip, lo que dice la animación; sin clip, lo que dejó la animación procedural,
        /// pero mezclado con la pose anterior si hubo un cambio. Se llama al final de <see cref="PlayerModel.Pose"/>.
        /// </summary>
        public void Update(float deltaTime, PlayerAction action, float speed, bool diveRight, float diveHeight, bool holdingThrow, bool holdingBall)
        {
            if (_idle == null || deltaTime <= 0f)
            {
                return; // sin animaciones: queda la procedural
            }

            Choose(action, speed, diveRight, diveHeight, holdingThrow, holdingBall);
            if (_clip != null)
            {
                Advance(deltaTime, speed);
                Apply();
            }

            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + deltaTime / BlendSeconds);
                float u = _blend * _blend * (3f - 2f * _blend);
                for (int i = 0; i < _bones.Length; i++)
                {
                    _bones[i].localRotation = Quaternion.Slerp(_from[i], _bones[i].localRotation, u);
                }

                _pelvis.localPosition = Vector3.Lerp(_fromPelvis, _pelvis.localPosition, u);
            }

            for (int i = 0; i < _bones.Length; i++)
            {
                _last[i] = _bones[i].localRotation;
            }

            _lastPelvis = _pelvis.localPosition;
            _hasLast = true;
        }

        private void Choose(PlayerAction action, float speed, bool diveRight, float diveHeight, bool holdingThrow, bool holdingBall)
        {
            bool edge = action != _lastAction;
            _lastAction = action;

            // Una pose sostenida sigue mientras siga vigente su estado.
            if (_clip != null && _clip.Hold && _time >= _clip.Length && !HoldStillValid(action, holdingThrow, holdingBall))
            {
                StartClip(null, 1f, 0, Hold.None);
            }

            if (_clip != null && !_clip.Loop && !_clip.Hold && _time >= _clip.Length)
            {
                StartClip(null, 1f, 0, Hold.None); // terminó el gesto
            }

            // Gestos que arrancan al cambiar la acción (el de mayor prioridad gana).
            if (edge)
            {
                AnimClip gesture = null;
                int priority = 0;
                float rate = 1f;
                switch (action)
                {
                    case PlayerAction.Dive: gesture = diveRight ? _diveRight : _diveLeft; priority = 3; break;
                    case PlayerAction.Slide: gesture = _slide; priority = 2; break;
                    case PlayerAction.Stumble: gesture = _stumble; priority = 2; break;
                    // El arquero saca con su patada de arquero (saque de arco, despeje); los demás, la suya.
                    case PlayerAction.Kick: gesture = _keeper && _keeperKick != null ? _keeperKick : _kick; priority = 1; break;
                    case PlayerAction.Pass: gesture = _keeper && _keeperKick != null ? _keeperKick : _pass; priority = 1; break;
                    case PlayerAction.Shoot: gesture = _keeper && _keeperKick != null ? _keeperKick : speed > RunningShotSpeed ? _shootRun : _shoot; priority = 1; break;
                    case PlayerAction.Control: gesture = _control; priority = 1; break;
                    case PlayerAction.Header: gesture = _header; priority = 1; break;
                    case PlayerAction.ThrowIn: gesture = _throw; priority = 1; break;
                }

                if (gesture != null && (priority >= _priority || _clip == null || _clip.Loop || _clip.Hold))
                {
                    StartClip(gesture, gesture.Rate * rate, priority, Hold.None);
                    return;
                }
            }

            if (_clip != null && !_clip.Loop && !_clip.Hold)
            {
                return; // un gesto en curso se termina
            }

            // Estados sostenidos.
            if (holdingThrow && _throwHold != null)
            {
                KeepOrStart(_throwHold, Hold.Throw);
                return;
            }

            if (holdingBall && _holdBall != null)
            {
                KeepOrStart(_holdBall, Hold.Ball);
                return;
            }

            if (action == PlayerAction.Rush && _rush != null)
            {
                KeepOrStart(_rush, Hold.Rush);
                return;
            }

            if (action == PlayerAction.Tackle)
            {
                StartClip(null, 1f, 0, Hold.None); // sin clip: queda la procedural
                return;
            }

            if (action == PlayerAction.Celebrate && speed <= CelebrateStandSpeed && _dances.Length > 0)
            {
                if (_hold != Hold.Dance)
                {
                    AnimClip dance = _dances[_danceCounter++ % _dances.Length]; // llegó: baila (todavía corriendo, sigue la carrera)
                    StartClip(dance, dance.Rate, 0, Hold.Dance);
                }

                return;
            }

            // Desplazamiento: reposo, caminata, trote, carrera o esprint según la velocidad (con histéresis).
            int band = BandFor(speed);
            AnimClip loco = band == 0 ? (_keeper ? _keeperIdle : _idle) : band == 1 ? _walk : band == 2 ? _jog : band == 3 ? _run : _sprint;
            if (loco == null)
            {
                loco = _idle;
            }

            if (_clip != loco || _hold != Hold.None)
            {
                StartClip(loco, 1f, 0, Hold.None);
            }
        }

        private bool HoldStillValid(PlayerAction action, bool holdingThrow, bool holdingBall)
        {
            switch (_hold)
            {
                case Hold.Throw: return holdingThrow;
                case Hold.Ball: return holdingBall;
                case Hold.Rush: return action == PlayerAction.Rush;
                case Hold.Dance: return action == PlayerAction.Celebrate;
                default: return false;
            }
        }

        private void KeepOrStart(AnimClip clip, Hold hold)
        {
            if (_clip != clip)
            {
                StartClip(clip, clip.Rate, 0, hold);
            }
        }

        private int BandFor(float speed)
        {
            int band = _band;
            float walk = WalkBelow, jog = JogBelow, run = RunBelow;
            if (band < 0)
            {
                band = speed < 0.35f ? 0 : speed < walk ? 1 : speed < jog ? 2 : speed < run ? 3 : 4;
            }
            else
            {
                // Se cambia de banda solo al pasar el límite con un margen, para que no parpadee entre dos clips.
                if (band == 0 && speed > 0.45f) band = 1;
                else if (band >= 1 && speed < 0.25f) band = 0;
                if (band == 1 && speed > walk + Hysteresis) band = 2;
                else if (band == 2 && speed < walk - Hysteresis) band = 1;
                else if (band == 2 && speed > jog + Hysteresis) band = 3;
                else if (band == 3 && speed < jog - Hysteresis) band = 2;
                else if (band == 3 && speed > run + Hysteresis) band = 4;
                else if (band == 4 && speed < run - Hysteresis) band = 3;
            }

            _band = band;
            return band;
        }

        private void StartClip(AnimClip clip, float rate, int priority, Hold hold)
        {
            if (_hasLast)
            {
                // Se mezcla desde la última pose que se vio.
                System.Array.Copy(_last, _from, _from.Length);
                _fromPelvis = _lastPelvis;
                _blend = 0f;
            }

            _clip = clip;
            _time = 0f;
            _rate = rate;
            _priority = priority;
            _hold = hold;
        }

        private void Advance(float deltaTime, float speed)
        {
            float rate = _rate;
            if (_clip.Loop && _clip.GroundSpeed > 0.5f)
            {
                // La cadencia de la zancada sigue a la velocidad del suelo, para que los pies no patinen.
                bool walking = _clip == _walk;
                rate = Mathf.Clamp(speed / _clip.GroundSpeed, walking ? WalkMinRate : RunMinRate, walking ? WalkMaxRate : RunMaxRate);
            }

            _time += deltaTime * rate;
            float length = _clip.Length;
            if (_clip.Loop)
            {
                _time = Mathf.Repeat(_time, length);
            }
            else if (_time > length)
            {
                _time = length;
            }
        }

        private void Apply()
        {
            float f = _time * AnimClips.Fps;
            int frames = _clip.Frames;
            int i0 = Mathf.Min((int)f, frames - 1);
            float t = Mathf.Clamp01(f - i0);
            int i1 = i0 + 1;
            if (i1 >= frames)
            {
                i1 = _clip.Loop ? 0 : frames - 1;
            }

            Quaternion[] rotations = _clip.Rotations;
            int a = i0 * AnimClips.BoneCount, b = i1 * AnimClips.BoneCount;
            _bones[0].localRotation = Quaternion.identity;
            _bones[0].localPosition = Vector3.zero;
            for (int k = 0; k < AnimClips.BoneCount; k++)
            {
                _bones[k + 1].localRotation = Quaternion.Slerp(rotations[a + k], rotations[b + k], t);
            }

            Vector3 p = Vector3.Lerp(_clip.Pelvis[i0], _clip.Pelvis[i1], t);
            _pelvis.localPosition = _pelvisBind + p; // el vaivén y la altura de la cadera, en metros respecto del reposo
        }
    }
}
