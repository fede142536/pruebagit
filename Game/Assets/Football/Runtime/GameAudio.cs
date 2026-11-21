using Football.Core.Common;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Sonido del partido generado por código al arrancar (sin archivos de audio con licencia): golpeo de balón, silbato,
    /// palo, red y una hinchada de fondo cuyo volumen sube cuando el balón se acerca a un arco y estalla en los goles.
    /// Los clips se crean una sola vez; durante el partido solo se reproducen.
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

        private readonly AudioSource _crowd;
        private readonly AudioSource _effects;
        private readonly AudioClip _kick, _whistle, _longWhistle, _post, _net, _cheer;
        private float _crowdTarget = CrowdBase;

        /// <summary>Crea las fuentes de sonido bajo el objeto indicado y genera los clips.</summary>
        public GameAudio(GameObject owner)
        {
            var rng = new SeededRandom(NoiseSeed);
            _crowd = owner.AddComponent<AudioSource>();
            _crowd.loop = true;
            _crowd.playOnAwake = false;
            _crowd.volume = 0f;
            _crowd.clip = SynthCrowd(rng);
            _effects = owner.AddComponent<AudioSource>();
            _effects.playOnAwake = false;

            _kick = SynthKick(rng);
            _whistle = SynthWhistle(0.35f, "Silbato");
            _longWhistle = SynthWhistle(1.1f, "SilbatoLargo");
            _post = SynthPost();
            _net = SynthNet(rng);
            _cheer = SynthCheer(rng);
        }

        /// <summary>Empieza el ambiente de la hinchada (al empezar un partido).</summary>
        public void StartCrowd()
        {
            _crowdTarget = CrowdBase;
            if (!_crowd.isPlaying)
            {
                _crowd.Play();
            }
        }

        /// <summary>Detiene la hinchada (al salir del partido).</summary>
        public void StopCrowd()
        {
            _crowd.Stop();
        }

        /// <summary>Volumen de la hinchada según lo cerca que está el balón de un arco (0 en el medio, 1 en la línea).</summary>
        public void UpdateCrowd(float closenessToGoal, float deltaTime)
        {
            float target = CrowdBase + CrowdNearGoal * Mathf.Clamp01((closenessToGoal - 0.55f) / 0.45f);
            _crowdTarget = Mathf.Max(target, _crowdTarget - deltaTime * 0.3f); // tras un gol, el estruendo baja despacio
            _crowd.volume = Mathf.Lerp(_crowd.volume, _crowdTarget, 1f - Mathf.Exp(-CrowdFade * deltaTime));
        }

        /// <summary>Golpeo del balón; el volumen depende de la fuerza.</summary>
        public void Kick(float speedChange)
        {
            if (speedChange >= KickMinSpeed)
            {
                _effects.PlayOneShot(_kick, Mathf.Clamp01(0.25f + speedChange / KickFullSpeed));
            }
        }

        /// <summary>Silbato corto (saque, falta, fuera de juego).</summary>
        public void Whistle() => _effects.PlayOneShot(_whistle, 0.6f);

        /// <summary>Silbato largo (fin de un tiempo).</summary>
        public void LongWhistle() => _effects.PlayOneShot(_longWhistle, 0.7f);

        /// <summary>Balón al palo: golpe metálico y lamento de la hinchada.</summary>
        public void Post()
        {
            _effects.PlayOneShot(_post, 0.8f);
            _crowdTarget = Mathf.Max(_crowdTarget, CrowdBase + CrowdNearGoal);
        }

        /// <summary>Gol: red y estallido de la hinchada.</summary>
        public void Goal()
        {
            _effects.PlayOneShot(_net, 0.7f);
            _effects.PlayOneShot(_cheer, 1f);
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
