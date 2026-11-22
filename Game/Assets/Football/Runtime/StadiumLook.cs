using Football.Core.Management;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Cómo se ve el estadio de un partido: cuánta gente hay (la ocupación que calcula el núcleo), cuántos anillos de
    /// tribuna tiene según su capacidad y si hay una obra de tribunas en curso (andamios). Lo usan el estadio 3D y la vista
    /// isométrica de la pantalla de remodelación, así los dos muestran lo mismo.
    /// </summary>
    public struct StadiumLook
    {
        /// <summary>Anillos de tribuna como máximo.</summary>
        public const int MaxTiers = 3;
        /// <summary>Asientos que representa cada anillo.</summary>
        public const int SeatsPerTier = 20000;

        /// <summary>Ocupación (0–1).</summary>
        public float Attendance;
        /// <summary>Anillos de tribuna (1–3).</summary>
        public int Tiers;
        /// <summary>Hay una ampliación de tribunas en obra.</summary>
        public bool StandsWork;

        /// <summary>Estadio lleno con todas las tribunas (amistosos y partidos fuera de la carrera).</summary>
        public static StadiumLook Full => new StadiumLook { Attendance = 1f, Tiers = MaxTiers };

        /// <summary>Anillos según la capacidad.</summary>
        public static int TiersFor(int capacity) => Mathf.Clamp(Mathf.CeilToInt(capacity / (float)SeatsPerTier), 1, MaxTiers);

        /// <summary>Aspecto de un estadio de la carrera con la ocupación esperada.</summary>
        public static StadiumLook From(StadiumState stadium, float attendance) => new StadiumLook
        {
            Attendance = Mathf.Clamp01(attendance),
            Tiers = TiersFor(stadium.Capacity),
            StandsWork = stadium.ActiveWork(RemodelCategory.Stands) != null
        };
    }
}
