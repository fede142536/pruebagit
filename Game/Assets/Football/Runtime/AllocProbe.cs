using System;
using System.Text;

namespace Football.Game
{
    /// <summary>
    /// Medidor de asignaciones de memoria por parte del cuadro (solo en el benchmark): dice cuántos bytes asigna cada tramo de
    /// <c>MatchScreen.UpdateUnsafe</c>, para encontrar de dónde salen los KB/s de basura. No asigna nada y cuesta casi nada.
    /// </summary>
    public static class AllocProbe
    {
        /// <summary>Tramos medidos del cuadro.</summary>
        public enum Part
        {
            /// <summary>Lectura de los toques y botones.</summary>
            Controls,
            /// <summary>Pasos del motor y copia de la instantánea.</summary>
            Engine,
            /// <summary>Eventos del partido, marcador, reloj y sonido.</summary>
            EventsAndHud,
            /// <summary>Vistas previas de pase y cambio de jugador.</summary>
            Previews,
            /// <summary>Dibujo del partido (jugadores, balón, cámara, estadio).</summary>
            ViewRender,
            /// <summary>Radar.</summary>
            Radar,
            /// <summary>Aro y barras del jugador.</summary>
            PlayerHud,
            /// <summary>Nombres sobre los jugadores.</summary>
            NameTags,
            /// <summary>Resto (avisos y panel de depuración).</summary>
            Rest
        }

        private static readonly long[] Totals = new long[Enum.GetValues(typeof(Part)).Length];
        private static long _last;
        private static float _seconds;
        private static bool _working;
        private static bool _useMono;
        private static bool _sourceChosen;

        /// <summary>
        /// Bytes asignados hasta ahora. <c>GC.GetAllocatedBytesForCurrentThread</c> devuelve 0 con el motor Mono de Unity (el Editor y las compilaciones Mono); en ese caso
        /// se usa el tamaño del montón de Mono (<c>Profiler.GetMonoUsedSizeLong</c>), que sube con cada asignación (si hubo una recolección en el medio, ese tramo no suma).
        /// </summary>
        private static long Allocated()
        {
            if (!_sourceChosen)
            {
                _sourceChosen = true;
                long first = GC.GetAllocatedBytesForCurrentThread();
                _useMono = first == 0 && GC.GetAllocatedBytesForCurrentThread() == 0;
            }

            return _useMono ? UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() : GC.GetAllocatedBytesForCurrentThread();
        }

        /// <summary>Se mide solo si es verdadero (benchmark).</summary>
        public static bool Enabled { get; set; }

        /// <summary>Pone los contadores a cero.</summary>
        public static void Reset()
        {
            Array.Clear(Totals, 0, Totals.Length);
            _seconds = 0f;
            _working = false;
        }

        /// <summary>Empieza a medir un cuadro.</summary>
        /// <param name="deltaTime">Duración del cuadro (s).</param>
        public static void Begin(float deltaTime)
        {
            if (!Enabled)
            {
                return;
            }

            _seconds += deltaTime;
            _last = Allocated();
            _working = true;
        }

        /// <summary>Anota lo asignado desde la marca anterior al tramo indicado.</summary>
        /// <param name="part">Tramo que acaba de terminar.</param>
        public static void Mark(Part part)
        {
            if (!_working)
            {
                return;
            }

            long now = Allocated();
            Totals[(int)part] += Math.Max(0L, now - _last); // si bajó, hubo una recolección: ese tramo no se cuenta
            _last = now;
        }

        /// <summary>Resumen en texto: KB por segundo de cada tramo, de mayor a menor.</summary>
        public static string Report()
        {
            if (_seconds <= 0f)
            {
                return "Asignaciones por parte: sin datos\n";
            }

            long sum = 0;
            for (int i = 0; i < Totals.Length; i++)
            {
                sum += Totals[i];
            }

            var sb = new StringBuilder();
            sb.Append(_useMono ? "Asignaciones por parte (KB/s, por tamaño del montón de Mono, total " : "Asignaciones por parte (KB/s, total ").Append((sum / 1024f / _seconds).ToString("F1")).Append("):\n");
            if (sum == 0)
            {
                sb.Append("  el medidor devolvió 0 (usar el Profiler)\n");
                return sb.ToString();
            }

            var order = new int[Totals.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) => Totals[b].CompareTo(Totals[a]));
            for (int k = 0; k < order.Length && Totals[order[k]] > 0; k++)
            {
                sb.Append("  ").Append(((Part)order[k]).ToString()).Append(": ").Append((Totals[order[k]] / 1024f / _seconds).ToString("F1")).Append('\n');
            }

            return sb.ToString();
        }
    }
}
