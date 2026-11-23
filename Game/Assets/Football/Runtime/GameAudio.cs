using Football.Core.Common;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Sonido del juego. Efectos (golpeo, silbato, palo, red) sintetizados al arrancar. Hinchada, grito de gol, cánticos de Boca y música
    /// de los menús son archivos de <c>Resources/Audio</c> (registrados en <c>ASSETS_LICENSES.md</c>); si faltan, la hinchada vuelve a un
    /// murmullo sintético. Los clips se cargan una sola vez; durante el partido solo se reproducen.
    /// </summary>
    public sealed class GameAudio
    {
        private const int Rate = 22050;
        private const float CrowdBase = 0.16f;
        private const float CrowdNearGoal = 0.38f;
        private const float CrowdFade = 2f;
        private const float KickMinSpeed = 5f;      // m/s de cambio de velocidad para que suene un golpeo
        private const float KickFullSpeed = 28f;
        private const ulong NoiseSeed = 7;
        private const string AmbientPath = "Audio/crowd_ambient";
        private const string CheerPath = "Audio/crowd_cheer";
        private const string BocaPath = "Audio/boca_chants";
        private const string MusicPath = "Audio/music_menu";
        private const float CheerSeconds = 7f;         // lo que dura el grito de gol antes de apagarse
        private const float CheerFadeSeconds = 2f;
        private const float BocaMinSong = 18f;         // los cánticos suenan en tramos de esta duración, desde un punto al azar del archivo
        private const float BocaMaxSong = 34f;
        private const float BocaMinSilence = 12f;      // y se callan entre tramos
        private const float BocaMaxSilence = 40f;
        private const float BocaFadeSeconds = 2.5f;
        private const float BocaGain = 0.75f;
        private const float MusicGain = 0.6f;
        private const float MusicFadeSeconds = 1.2f;

        private readonly GameSettings _settings;
        private readonly AudioSource _crowd;
        private readonly AudioSource _effects;
        private readonly AudioSource _cheerSource, _bocaSource, _music;
        private readonly AudioClip[] _ambient;          // un archivo de hinchada (o el sintético si falta)
        private readonly AudioClip _cheerClip, _bocaClip, _musicClip;
        private readonly SeededRandom _presentationRng = new SeededRandom((ulong)System.Environment.TickCount); // solo presentación: no es azar de juego
        private bool _matchRunning, _bocaFans, _bocaSinging;
        private float _bocaTimer, _cheerTimer, _musicTarget, _musicLevel;
        private readonly AudioClip _kick, _whistle, _longWhistle, _post, _net, _cheer;
        private float _crowdTarget = CrowdBase;
        private float _crowdLevel;

        private float Fx => GameSettings.Volume(_settings.EffectsVolume);

        /// <summary>Crea las fuentes de sonido bajo el objeto indicado y genera los clips.</summary>
        /// <param name="owner">Objeto que lleva las fuentes de sonido.</param>
        /// <param name="settings">Ajustes (volumen del ambiente y de los efectos).</param>
        public GameAudio(GameObject owner, GameSettings settings)
        {
            _settings = settings;
            var rng = new SeededRandom(NoiseSeed);
            _crowd = owner.AddComponent<AudioSource>();
            _crowd.loop = true;
            _crowd.playOnAwake = false;
            _crowd.volume = 0f;
            AudioClip real = Resources.Load<AudioClip>(AmbientPath);
            _ambient = new[] { real != null ? real : SynthCrowd(rng) };
            _crowd.clip = _ambient[0];
            _cheerClip = Resources.Load<AudioClip>(CheerPath);
            _bocaClip = Resources.Load<AudioClip>(BocaPath);
            _musicClip = Resources.Load<AudioClip>(MusicPath);
            _cheerSource = NewSource(owner, false);
            _bocaSource = NewSource(owner, false);
            _music = NewSource(owner, true);
            _music.clip = _musicClip;
            _effects = owner.AddComponent<AudioSource>();
            _effects.playOnAwake = false;

            _kick = SynthKick(rng);
            _whistle = SynthWhistle(0.35f, "Silbato");
            _longWhistle = SynthWhistle(1.1f, "SilbatoLargo");
            _post = SynthPost();
            _net = SynthNet(rng);
            _cheer = SynthCheer(rng);
        }

        private static AudioSource NewSource(GameObject owner, bool loop)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
            return source;
        }

        /// <summary>Empieza el ambiente de la hinchada (al empezar un partido).</summary>
        /// <param name="bocaFans">Elige Boca Juniors: además suenan sus cánticos, a tramos y al azar.</param>
        public void StartCrowd(bool bocaFans = false)
        {
            _crowdTarget = CrowdBase;
            _matchRunning = true;
            _bocaFans = bocaFans && _bocaClip != null;
            _bocaSinging = false;
            _bocaTimer = _presentationRng.NextFloat() * BocaMinSilence; // el primer cántico no tarda
            if (!_crowd.isPlaying)
            {
                _crowd.time = _presentationRng.NextFloat() * Mathf.Max(0f, _crowd.clip.length - 1f);
                _crowd.Play();
            }
        }

        /// <summary>Detiene la hinchada, el grito y los cánticos (al salir del partido).</summary>
        public void StopCrowd()
        {
            _matchRunning = false;
            _crowd.Stop();
            _cheerSource.Stop();
            _bocaSource.Stop();
            _bocaSinging = false;
        }

        /// <summary>Música de los menús (suena en todo menos en el partido, pausa incluida). Entra y sale con fundido.</summary>
        /// <param name="on">Verdadero para que suene.</param>
        public void SetMenuMusic(bool on)
        {
            _musicTarget = on && _musicClip != null ? 1f : 0f;
            if (_musicTarget > 0f && !_music.isPlaying)
            {
                _music.Play();
            }
        }

        /// <summary>Avanza los fundidos de la música y los tramos de cánticos (cada cuadro, también en los menús).</summary>
        /// <param name="deltaTime">Tiempo real transcurrido.</param>
        public void Tick(float deltaTime)
        {
            float step = deltaTime / MusicFadeSeconds;
            _musicLevel = Mathf.MoveTowards(_musicLevel, _musicTarget, step);
            _music.volume = _musicLevel * MusicGain * GameSettings.Volume(_settings.MusicVolume);
            if (_musicLevel <= 0f && _musicTarget <= 0f && _music.isPlaying)
            {
                _music.Stop();
            }
        }

        private void UpdateCheerAndChants(float deltaTime)
        {
            float stadium = GameSettings.Volume(_settings.StadiumVolume);
            if (_cheerSource.isPlaying)
            {
                _cheerTimer -= deltaTime;
                _cheerSource.volume = stadium * Mathf.Clamp01(_cheerTimer / CheerFadeSeconds);
                if (_cheerTimer <= 0f)
                {
                    _cheerSource.Stop();
                }
            }

            if (!_bocaFans || !_matchRunning)
            {
                return;
            }

            _bocaTimer -= deltaTime;
            if (_bocaSinging)
            {
                float left = _bocaTimer;
                float fade = Mathf.Clamp01(Mathf.Min(left, _bocaSource.time) / BocaFadeSeconds);
                _bocaSource.volume = stadium * BocaGain * fade;
                if (left <= 0f || !_bocaSource.isPlaying)
                {
                    _bocaSource.Stop();
                    _bocaSinging = false;
                    _bocaTimer = Mathf.Lerp(BocaMinSilence, BocaMaxSilence, _presentationRng.NextFloat());
                }
            }
            else if (_bocaTimer <= 0f)
            {
                float length = Mathf.Lerp(BocaMinSong, BocaMaxSong, _presentationRng.NextFloat());
                float start = _presentationRng.NextFloat() * Mathf.Max(0f, _bocaClip.length - length);
                _bocaSource.clip = _bocaClip;
                _bocaSource.time = start;
                _bocaSource.volume = 0f;
                _bocaSource.Play();
                _bocaSinging = true;
                _bocaTimer = Mathf.Min(length, _bocaClip.length - start);
            }
        }

        /// <summary>Volumen de la hinchada según lo cerca que está el balón de un arco (0 en el medio, 1 en la línea).</summary>
        public void UpdateCrowd(float closenessToGoal, float deltaTime)
        {
            float target = CrowdBase + CrowdNearGoal * Mathf.Clamp01((closenessToGoal - 0.55f) / 0.45f);
            _crowdTarget = Mathf.Max(target, _crowdTarget - deltaTime * 0.3f); // tras un gol, el estruendo baja despacio
            _crowdLevel = Mathf.Lerp(_crowdLevel, _crowdTarget, 1f - Mathf.Exp(-CrowdFade * deltaTime));
            _crowd.volume = _crowdLevel * GameSettings.Volume(_settings.StadiumVolume);
            if (_matchRunning && !_crowd.isPlaying && !AudioListener.pause)
            {
                _crowd.time = _presentationRng.NextFloat() * Mathf.Max(0f, _crowd.clip.length - 1f); // el archivo se acabó: otro punto al azar
                _crowd.Play();
            }

            UpdateCheerAndChants(deltaTime);
        }

        /// <summary>Golpeo del balón; el volumen depende de la fuerza.</summary>
        public void Kick(float speedChange)
        {
            if (speedChange >= KickMinSpeed)
            {
                _effects.PlayOneShot(_kick, Mathf.Clamp01(0.25f + speedChange / KickFullSpeed) * Fx);
            }
        }

        /// <summary>Silbato corto (saque, falta, fuera de juego).</summary>
        public void Whistle() => _effects.PlayOneShot(_whistle, 0.6f * Fx);

        /// <summary>Silbato largo (fin de un tiempo).</summary>
        public void LongWhistle() => _effects.PlayOneShot(_longWhistle, 0.7f * Fx);

        /// <summary>Balón al palo: golpe metálico y lamento de la hinchada.</summary>
        public void Post()
        {
            _effects.PlayOneShot(_post, 0.8f * Fx);
            _crowdTarget = Mathf.Max(_crowdTarget, CrowdBase + CrowdNearGoal);
        }

        /// <summary>Gol: red y estallido de la hinchada.</summary>
        public void Goal()
        {
            _effects.PlayOneShot(_net, 0.7f * Fx);
            if (_cheerClip != null)
            {
                _cheerSource.clip = _cheerClip;
                _cheerSource.time = 0f;
                _cheerSource.volume = GameSettings.Volume(_settings.StadiumVolume);
                _cheerSource.Play();
                _cheerTimer = CheerSeconds;
            }
            else
            {
                _effects.PlayOneShot(_cheer, GameSettings.Volume(_settings.StadiumVolume));
            }

            _crowdTarget = 1f;
        }

        // ---- Síntesis de los sonidos ------------------------------------------------------------------------------

        private static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthKick(IRandom rng)
        {
            var d = new float[(int)(Rate * 0.14f)];
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = Mathf.Lerp(150f, 55f, t / 0.14f);
                phase += 2f * Mathf.PI * freq / Rate;
                float click = t < 0.006f ? (rng.NextFloat() * 2f - 1f) * (1f - t / 0.006f) : 0f;
                d[i] = (Mathf.Sin(phase) * Mathf.Exp(-t * 32f) + click * 0.6f) * 0.9f;
            }

            return Make("Golpeo", d);
        }

        private static AudioClip SynthWhistle(float seconds, string name)
        {
            var d = new float[(int)(Rate * seconds)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float envelope = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((seconds - t) / 0.05f);
                float trill = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 32f * t); // el "trino" de la bolita del silbato
                d[i] = Mathf.Sin(2f * Mathf.PI * 2750f * t) * trill * envelope * 0.5f;
            }

            return Make(name, d);
        }

        private static AudioClip SynthPost()
        {
            var d = new float[(int)(Rate * 0.7f)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 1870f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3130f * t) * 0.4f) * Mathf.Exp(-t * 7f) * 0.6f;
            }

            return Make("Palo", d);
        }

        private static AudioClip SynthNet(IRandom rng)
        {
            var d = new float[(int)(Rate * 0.45f)];
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                low += 0.18f * ((rng.NextFloat() * 2f - 1f) - low);
                d[i] = low * Mathf.Exp(-t * 9f) * 1.6f;
            }

            return Make("Red", d);
        }

        /// <summary>Murmullo de estadio: ruido filtrado, con el final mezclado con el principio para que el bucle no se note.</summary>
        private static AudioClip SynthCrowd(IRandom rng)
        {
            const float seconds = 4f;
            var d = new float[(int)(Rate * seconds)];
            float a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                a += 0.05f * (white - a);   // grave
                b += 0.25f * (white - b);   // voces
                float t = i / (float)Rate;
                float swell = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                d[i] = (a * 2.2f + b * 0.5f) * swell;
            }

            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                d[i] = d[i] * w + d[d.Length - fade + i] * (1f - w);
            }

            var loop = new float[d.Length - fade];
            System.Array.Copy(d, loop, loop.Length);
            return Make("Hinchada", loop);
        }

        private static AudioClip SynthCheer(IRandom rng)
        {
            const float seconds = 3f;
            var d = new float[(int)(Rate * seconds)];
            float b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                b += 0.3f * ((rng.NextFloat() * 2f - 1f) - b);
                float envelope = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-Mathf.Max(0f, t - 0.8f) * 1.2f);
                d[i] = b * envelope * 1.4f;
            }

            return Make("Grito", d);
        }
    }
}
