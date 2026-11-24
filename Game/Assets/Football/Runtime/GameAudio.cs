using Football.Core.Common;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Sonido del juego. Efectos (golpeo, silbato, palo, red) sintetizados al arrancar. Hinchada, grito de gol, temas de los partidos (La 12 con Boca de local) y música
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
        private static readonly string[] BocaPaths = { "Audio/boca_song_1", "Audio/boca_song_2", "Audio/boca_song_3", "Audio/boca_song_4" };
        private static readonly string[] SongPaths =
        {
            "Audio/songs/como_olvidar", "Audio/songs/para_no_olvidar", "Audio/songs/cuando_yo_muera", "Audio/songs/estas_para_ganar",
            "Audio/songs/no_me_arrepiento", "Audio/songs/mi_historia_entre_tus_dedos", "Audio/songs/bad_moon_rising",
            "Audio/songs/se_me_ha_perdido_un_corazon", "Audio/songs/y_dale_alegria", "Audio/songs/beso_a_beso",
            "Audio/songs/una_calle_nos_separa", "Audio/songs/el_negro_jose", "Audio/songs/dale_vieja_dale"
        };
        private static readonly string[] MenuPaths = { "Audio/music_menu", "Audio/music_magalenha" };
        private const float BocaOwnSongChance = 0.75f;  // con Boca de local, tres de cada cuatro temas son de La 12 y el resto sale del repertorio general
        private const float SongGain = 0.55f;           // los temas de estudio suenan más bajos que las grabaciones de la hinchada
        private const float CheerSeconds = 7f;         // lo que dura el grito de gol antes de apagarse
        private const float CheerFadeSeconds = 2f;
        private const float BocaFadeSeconds = 3f;       // los temas se encadenan con un fundido cruzado: nunca hay un hueco entre uno y otro
        private const float BocaGain = 0.8f;
        private const float ReactMinSeconds = 1.2f, ReactMaxSeconds = 2.4f;  // reacciones breves de la hinchada (ohh, protesta) tomadas del archivo del grito
        private const float ReactFadeSeconds = 0.35f;
        private const float ReactCooldown = 1.6f;                           // no se pisan unas con otras
        private const float ReactGain = 0.7f;
        private const float ProtestPitch = 0.82f;                           // la protesta suena más grave que el ¡ooh!
        private const float MusicGain = 0.6f;
        private const float RainGain = 0.45f;          // el golpeteo de la lluvia, bajo la hinchada
        private const float MusicFadeSeconds = 1.2f;

        private readonly GameSettings _settings;
        private readonly AudioSource _crowd;
        private readonly AudioSource _effects;
        private readonly AudioSource _cheerSource, _music;
        private AudioSource _rain;
        private AudioClip _rainClip;
        private bool _rainOn;
        private readonly GameObject _owner;
        private readonly AudioSource[] _bocaSources = new AudioSource[2];   // dos voces: una termina mientras la otra entra
        private readonly float[] _bocaLevel = new float[2];
        private readonly float[] _voiceGain = new float[2];
        private readonly AudioClip[] _voiceClip = new AudioClip[2];          // lo que carga cada voz: se descarga al terminar para no ocupar memoria
        private ResourceRequest _nextRequest;                                 // el próximo tema se carga en segundo plano mientras suena el actual
        private string _nextPath, _lastPath, _currentPath;
        private bool _nextIsBoca;
        private int _menuTrack;
        private AudioClip _menuClip;
        private readonly AudioClip[] _ambient;          // un archivo de hinchada (o el sintético si falta)
        private readonly AudioClip _cheerClip;
        private readonly SeededRandom _presentationRng = new SeededRandom((ulong)System.Environment.TickCount); // solo presentación: no es azar de juego
        private bool _matchRunning, _bocaFans, _bocaCrossing;
        private readonly AudioSource _react;
        private float _reactTimer, _reactLength, _reactIntensity, _reactCooldown;
        private int _bocaActive;
        private float _cheerTimer, _musicTarget, _musicLevel;
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
            _owner = owner;
            var rng = new SeededRandom(NoiseSeed);
            _crowd = owner.AddComponent<AudioSource>();
            _crowd.loop = true;
            _crowd.playOnAwake = false;
            _crowd.volume = 0f;
            AudioClip real = Resources.Load<AudioClip>(AmbientPath);
            _ambient = new[] { real != null ? real : SynthCrowd(rng) };
            _crowd.clip = _ambient[0];
            _cheerClip = Resources.Load<AudioClip>(CheerPath);
            _cheerSource = NewSource(owner, false);
            _bocaSources[0] = NewSource(owner, false);
            _bocaSources[1] = NewSource(owner, false);
            _react = NewSource(owner, false);
            _music = NewSource(owner, false);
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
        /// <param name="bocaFans">Boca Juniors de local: los temas de La 12 tienen prioridad (3 de cada 4). En todos los partidos suenan temas al azar, uno tras otro y sin silencios.</param>
        public void StartCrowd(bool bocaFans = false)
        {
            _crowdTarget = CrowdBase;
            _matchRunning = true;
            _bocaFans = bocaFans;
            _bocaCrossing = false;
            _bocaActive = 0;
            _bocaLevel[0] = _bocaLevel[1] = 0f;
            _lastPath = null;
            _nextRequest = null;
            string first = PickSong(_bocaFans);
            _voiceClip[0] = Resources.Load<AudioClip>(first); // el primero se carga ya (en la pantalla de carga); los demás, en segundo plano
            StartSong(0, first, _bocaFans);
            PrefetchNext();
            if (!_crowd.isPlaying)
            {
                _crowd.time = _presentationRng.NextFloat() * Mathf.Max(0f, _crowd.clip.length - 1f);
                _crowd.Play();
            }
        }

        /// <summary>Enciende o apaga el ruido de la lluvia (suena durante todo el partido, a un volumen bajo).</summary>
        /// <param name="on">Verdadero con lluvia.</param>
        public void SetRain(bool on)
        {
            _rainOn = on;
            if (on && _rain == null)
            {
                _rainClip = SynthRain(new SeededRandom(NoiseSeed + 1));
                _rain = NewSource(_owner, true);
                _rain.clip = _rainClip;
            }

            if (_rain == null)
            {
                return;
            }

            if (on && !_rain.isPlaying)
            {
                _rain.Play();
            }
            else if (!on)
            {
                _rain.Stop();
            }
        }

        /// <summary>Detiene la hinchada, el grito y los cánticos (al salir del partido).</summary>
        public void StopCrowd()
        {
            if (_rain != null)
            {
                _rain.Stop();
            }

            _matchRunning = false;
            _crowd.Stop();
            _cheerSource.Stop();
            _react.Stop();
            _bocaSources[0].Stop();
            _bocaSources[1].Stop();
            _bocaCrossing = false;
            _nextRequest = null;
            UnloadVoice(0);
            UnloadVoice(1);
        }

        /// <summary>Música de los menús (suena en todo menos en el partido, pausa incluida). Entra y sale con fundido.</summary>
        /// <param name="on">Verdadero para que suene.</param>
        public void SetMenuMusic(bool on)
        {
            _musicTarget = on ? 1f : 0f;
            if (_musicTarget > 0f && !_music.isPlaying)
            {
                PlayNextMenuTrack();
            }
        }

        private void PlayNextMenuTrack()
        {
            AudioClip previous = _menuClip;
            _menuTrack = _menuClip == null ? _presentationRng.NextInt(0, MenuPaths.Length) : (_menuTrack + 1) % MenuPaths.Length; // arranca por uno al azar y se alternan
            _menuClip = Resources.Load<AudioClip>(MenuPaths[_menuTrack]);
            if (previous != null && previous != _menuClip)
            {
                Resources.UnloadAsset(previous);
            }

            if (_menuClip != null)
            {
                _music.clip = _menuClip;
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
            else if (_musicTarget > 0f && !_music.isPlaying && !AudioListener.pause)
            {
                PlayNextMenuTrack(); // terminó el tema: sigue el otro
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

            _reactCooldown -= deltaTime;
            if (_react.isPlaying)
            {
                _reactTimer -= deltaTime;
                float fade = Mathf.Clamp01(Mathf.Min(_reactTimer, _reactLength - _reactTimer) / ReactFadeSeconds);
                _react.volume = stadium * ReactGain * _reactIntensity * fade;
                if (_reactTimer <= 0f)
                {
                    _react.Stop();
                }
            }

            UpdateBoca(deltaTime, stadium);
        }

        /// <summary>Elige el próximo tema al azar (sin repetir el anterior). Con Boca de local, <see cref="BocaOwnSongChance"/> de las veces es de La 12.</summary>
        private string PickSong(bool boca)
        {
            string[] source = boca && _presentationRng.NextFloat() < BocaOwnSongChance ? BocaPaths : SongPaths;
            string path = source[_presentationRng.NextInt(0, source.Length)];
            if (path == _lastPath)
            {
                path = source[(System.Array.IndexOf(source, path) + 1) % source.Length];
            }

            _lastPath = path;
            return path;
        }

        private void StartSong(int source, string path, bool boca)
        {
            AudioSource voice = _bocaSources[source];
            voice.clip = _voiceClip[source];
            _currentPath = path;
            _voiceGain[source] = System.Array.IndexOf(BocaPaths, path) >= 0 ? BocaGain : SongGain;
            _bocaLevel[source] = 0f;
            voice.volume = 0f;
            if (voice.clip != null)
            {
                voice.time = 0f;
                voice.Play();
            }
        }

        private void PrefetchNext()
        {
            _nextPath = PickSong(_bocaFans);
            _nextRequest = Resources.LoadAsync<AudioClip>(_nextPath);
        }

        private void UnloadVoice(int source)
        {
            AudioClip clip = _voiceClip[source];
            _voiceClip[source] = null;
            _bocaSources[source].clip = null;
            if (clip != null)
            {
                Resources.UnloadAsset(clip);
            }
        }

        /// <summary>
        /// Temas encadenados: cuando al que suena le quedan <see cref="BocaFadeSeconds"/> segundos, el siguiente (ya cargado en segundo plano) entra con un
        /// fundido cruzado y se descarga el anterior. No hay silencios entre temas ni durante el partido.
        /// </summary>
        private void UpdateBoca(float deltaTime, float stadium)
        {
            if (!_matchRunning)
            {
                return;
            }

            AudioSource active = _bocaSources[_bocaActive];
            int other = 1 - _bocaActive;
            bool ending = active.clip == null || !active.isPlaying || active.clip.length - active.time <= BocaFadeSeconds;
            if (!_bocaCrossing && ending && _nextRequest != null && _nextRequest.isDone)
            {
                _voiceClip[other] = _nextRequest.asset as AudioClip;
                string path = _nextPath;
                _nextRequest = null;
                if (_voiceClip[other] == null)
                {
                    PrefetchNext(); // el archivo no está: se intenta con otro
                    return;
                }

                StartSong(other, path, _bocaFans);
                _bocaCrossing = true;
            }

            float step = deltaTime / BocaFadeSeconds;
            _bocaLevel[_bocaActive] = Mathf.MoveTowards(_bocaLevel[_bocaActive], _bocaCrossing ? 0f : 1f, step);
            if (_bocaCrossing)
            {
                _bocaLevel[other] = Mathf.MoveTowards(_bocaLevel[other], 1f, step);
            }

            _bocaSources[0].volume = _bocaLevel[0] * stadium * _voiceGain[0];
            _bocaSources[1].volume = _bocaLevel[1] * stadium * _voiceGain[1];
            if (_bocaCrossing && _bocaLevel[_bocaActive] <= 0f)
            {
                active.Stop();
                UnloadVoice(_bocaActive);
                _bocaActive = other;
                _bocaCrossing = false;
                PrefetchNext();
            }
        }

        /// <summary>Volumen de la hinchada según lo cerca que está el balón de un arco (0 en el medio, 1 en la línea).</summary>
        public void UpdateCrowd(float closenessToGoal, float deltaTime)
        {
            float target = CrowdBase + CrowdNearGoal * Mathf.Clamp01((closenessToGoal - 0.55f) / 0.45f);
            _crowdTarget = Mathf.Max(target, _crowdTarget - deltaTime * 0.3f); // tras un gol, el estruendo baja despacio
            _crowdLevel = Mathf.Lerp(_crowdLevel, _crowdTarget, 1f - Mathf.Exp(-CrowdFade * deltaTime));
            _crowd.volume = _crowdLevel * GameSettings.Volume(_settings.StadiumVolume);
            if (_rain != null)
            {
                _rain.volume = _rainOn && _matchRunning ? RainGain * GameSettings.Volume(_settings.StadiumVolume) : 0f;
            }
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

        /// <summary>Reacción breve de la hinchada a una jugada (un ¡ooh! en un remate o una atajada, una protesta en una falta).</summary>
        /// <param name="intensity">Fuerza de 0 a 1.</param>
        /// <param name="protest">Verdadero para una protesta (más grave) y falso para un ¡ooh!.</param>
        public void React(float intensity, bool protest)
        {
            if (_cheerClip == null || !_matchRunning || _reactCooldown > 0f || _cheerSource.isPlaying)
            {
                return;
            }

            _reactLength = Mathf.Lerp(ReactMinSeconds, ReactMaxSeconds, _presentationRng.NextFloat());
            _react.clip = _cheerClip;
            _react.time = _presentationRng.NextFloat() * Mathf.Max(0f, _cheerClip.length - _reactLength);
            _react.pitch = protest ? ProtestPitch : 1f;
            _react.volume = 0f;
            _react.Play();
            _reactTimer = _reactLength;
            _reactIntensity = Mathf.Clamp01(intensity);
            _reactCooldown = ReactCooldown;
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

        private static AudioClip SynthRain(IRandom rng)
        {
            const float seconds = 3f;
            var d = new float[(int)(Rate * seconds)];
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                low += 0.12f * (white - low);
                d[i] = (white - low) * 0.5f; // pasa-altos: el siseo de las gotas
            }

            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                d[i] = d[i] * w + d[d.Length - fade + i] * (1f - w);
            }

            var loop = new float[d.Length - fade];
            System.Array.Copy(d, loop, loop.Length);
            return Make("Lluvia", loop);
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
