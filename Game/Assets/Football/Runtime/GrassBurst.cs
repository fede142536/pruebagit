using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game
{
    /// <summary>
    /// Terrones de césped que saltan en una barrida. Piezas reutilizadas (sin crear objetos durante el partido) y
    /// movimiento determinista (sin azar): solo es decoración.
    /// </summary>
    public sealed class GrassBurst
    {
        private const int Pieces = 18;
        private const int PerBurst = 6;
        private const float Life = 0.6f;
        private const float Gravity = 9.81f;
        private const float Size = 0.07f;
        private const float GoldenAngle = 2.39996f;

        private readonly Transform[] _pieces = new Transform[Pieces];
        private readonly Vector3[] _velocity = new Vector3[Pieces];
        private readonly float[] _age = new float[Pieces];
        private int _next;
        private int _burstCount;

        /// <summary>Crea las piezas (ocultas).</summary>
        /// <param name="parent">Padre en la escena.</param>
        public GrassBurst(Transform parent)
        {
            Color[] tones = { new Color(0.12f, 0.45f, 0.16f), new Color(0.3f, 0.22f, 0.12f), new Color(0.16f, 0.52f, 0.2f) };
            for (int i = 0; i < Pieces; i++)
            {
                var go = new GameObject("Terron");
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(Size, Size * 0.5f, Size);
                go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.Get(tones[i % tones.Length]);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                _pieces[i] = go.transform;
                _age[i] = Life;
            }
        }

        /// <summary>Lanza terrones desde un punto, hacia donde se desliza el jugador.</summary>
        /// <param name="at">Punto de contacto (pies).</param>
        /// <param name="direction">Dirección de la barrida (horizontal).</param>
        public void Emit(Vector3 at, Vector3 direction)
        {
            _burstCount++;
            for (int k = 0; k < PerBurst; k++)
            {
                int i = _next;
                _next = (_next + 1) % Pieces;
                float angle = (k + _burstCount) * GoldenAngle;
                Vector3 side = new Vector3(-direction.z, 0f, direction.x) * Mathf.Sin(angle) * 1.2f;
                _velocity[i] = direction * (2f + 0.4f * k) + side + Vector3.up * (1.6f + 0.25f * (k % 3));
                _age[i] = 0f;
                _pieces[i].localPosition = at;
                _pieces[i].gameObject.SetActive(true);
            }
        }

        /// <summary>Avanza la animación.</summary>
        /// <param name="deltaTime">Tiempo del fotograma (s).</param>
        public void Update(float deltaTime)
        {
            for (int i = 0; i < Pieces; i++)
            {
                if (_age[i] >= Life)
                {
                    continue;
                }

                _age[i] += deltaTime;
                if (_age[i] >= Life)
                {
                    _pieces[i].gameObject.SetActive(false);
                    continue;
                }

                _velocity[i] += Vector3.down * (Gravity * deltaTime);
                Vector3 p = _pieces[i].localPosition + _velocity[i] * deltaTime;
                if (p.y < 0.02f)
                {
                    p.y = 0.02f;
                    _velocity[i] *= 0.3f;
                }

                _pieces[i].localPosition = p;
            }
        }
    }
}
