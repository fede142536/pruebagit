using System;
using System.Text;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Panel de depuración: FPS, tiempo de fotograma (media y p99), recolecciones de basura, asignaciones aproximadas,
    /// memoria y latencia estimada de los botones. Sirve para medir el rendimiento en el dispositivo real.
    /// </summary>
    public sealed class DebugPanel
    {
        private const int FrameWindow = 300;
        private const int LatencyWindow = 64;
        private const float RefreshSeconds = 0.5f;
        private const float DisplayLatencyMs = 16f;

        private readonly GameObject _root;
        private readonly Text _text;
        private readonly float[] _frames = new float[FrameWindow];
        private readonly float[] _scratch = new float[FrameWindow];
        private readonly float[] _latencies = new float[LatencyWindow];
        private readonly float[] _latencyScratch = new float[LatencyWindow];
        private readonly StringBuilder _builder = new StringBuilder(256);

        private int _frameCount;
        private int _frameIndex;
        private int _latencyCount;
        private int _latencyIndex;
        private float _refreshTimer;
        private float _fpsTime;
        private int _fpsFrames;
        private float _fps;
        private long _lastMono;
        private long _allocatedBytes;
        private ProfilerRecorder _gcRecorder; // «GC Allocated In Frame»: lo que de verdad se asignó en cada fotograma (exacto, no una diferencia de memoria)
        private bool _gcRecorderStarted;
        private float _allocWindow;
        private float _allocPerSecond;
        private int _gcStart;
        private int _ticksTotal;
        private int _framesTotal;

        /// <summary>El panel está visible.</summary>
        public bool Visible { get; private set; }

        /// <summary>Crea el panel dentro del lienzo dado.</summary>
        public DebugPanel(Transform canvas)
        {
            Image background = UiKit.Panel(canvas, "Depuracion", new Vector2(250f, 560f), new Vector2(480f, 330f), new Color(0f, 0f, 0f, 0.6f));
            _root = background.gameObject;
            _text = UiKit.Label(background.transform, "Texto", new Vector2(240f, 165f), new Vector2(460f, 310f), string.Empty, 24, Color.white,
                TextAnchor.UpperLeft);
            SetVisible(false);
        }

        /// <summary>Muestra u oculta el panel.</summary>
        public void SetVisible(bool visible)
        {
            Visible = visible;
            _root.SetActive(visible);
        }

        /// <summary>Pone las medidas a cero (al empezar un partido).</summary>
        public void Reset()
        {
            _frameCount = 0;
            _frameIndex = 0;
            _latencyCount = 0;
            _latencyIndex = 0;
            _gcStart = GC.CollectionCount(0);
            _lastMono = Profiler.GetMonoUsedSizeLong();
            _allocatedBytes = 0;
            _allocWindow = 0f;
            _allocPerSecond = 0f;
            _ticksTotal = 0;
            _framesTotal = 0;
        }

        /// <summary>Registra un fotograma.</summary>
        /// <param name="deltaTime">Duración del fotograma (s).</param>
        /// <param name="ticks">Pasos de simulación ejecutados en él.</param>
        public void Frame(float deltaTime, int ticks)
        {
            _frames[_frameIndex] = deltaTime;
            _frameIndex = (_frameIndex + 1) % FrameWindow;
            if (_frameCount < FrameWindow)
            {
                _frameCount++;
            }

            _ticksTotal += ticks;
            _framesTotal++;

            if (!_gcRecorderStarted)
            {
                _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                _gcRecorderStarted = true;
            }

            long mono = Profiler.GetMonoUsedSizeLong();
            if (_gcRecorder.Valid)
            {
                _allocatedBytes += _gcRecorder.LastValue; // asignaciones reales del fotograma
            }
            else if (mono > _lastMono)
            {
                _allocatedBytes += mono - _lastMono; // sin el contador de Unity: diferencia de memoria (aproximada)
            }

            _lastMono = mono;
            _allocWindow += deltaTime;
            _fpsTime += deltaTime;
            _fpsFrames++;

            _refreshTimer += deltaTime;
            if (_refreshTimer >= RefreshSeconds)
            {
                _refreshTimer = 0f;
                _fps = _fpsTime > 0f ? _fpsFrames / _fpsTime : 0f;
                _fpsTime = 0f;
                _fpsFrames = 0;
                _allocPerSecond = _allocWindow > 0f ? _allocatedBytes / 1024f / _allocWindow : 0f;
                _allocatedBytes = 0;
                _allocWindow = 0f;
                if (Visible)
                {
                    Refresh();
                }
            }
        }

        /// <summary>Registra el tiempo entre la pulsación y la ejecución de la acción dentro del juego (s).</summary>
        public void RecordLatency(float seconds)
        {
            _latencies[_latencyIndex] = seconds;
            _latencyIndex = (_latencyIndex + 1) % LatencyWindow;
            if (_latencyCount < LatencyWindow)
            {
                _latencyCount++;
            }
        }

        /// <summary>FPS medios del último medio segundo.</summary>
        public float Fps => _fps;

        /// <summary>Tiempo de fotograma medio (ms) de la ventana.</summary>
        public float AverageFrameMs => Percentile(_frames, _scratch, _frameCount, -1f) * 1000f;

        /// <summary>Tiempo de fotograma del percentil 99 (ms) de la ventana.</summary>
        public float P99FrameMs => Percentile(_frames, _scratch, _frameCount, 0.99f) * 1000f;

        /// <summary>Recolecciones de basura (generación 0) desde que empezó el partido.</summary>
        public int GcCollections => GC.CollectionCount(0) - _gcStart;

        /// <summary>Asignaciones gestionadas aproximadas (KB/s) en el último medio segundo.</summary>
        public float AllocKbPerSecond => _allocPerSecond;

        /// <summary>Latencia estimada p95 (ms): proceso interno + un fotograma de muestreo + refresco de pantalla.</summary>
        public float EstimatedLatencyP95Ms
        {
            get
            {
                float inner = Percentile(_latencies, _latencyScratch, _latencyCount, 0.95f) * 1000f;
                return inner + AverageFrameMs + DisplayLatencyMs;
            }
        }

        private static float Percentile(float[] values, float[] scratch, int count, float p)
        {
            if (count == 0)
            {
                return 0f;
            }

            Array.Copy(values, scratch, count);
            Array.Sort(scratch, 0, count);
            if (p < 0f)
            {
                float sum = 0f;
                for (int i = 0; i < count; i++)
                {
                    sum += scratch[i];
                }

                return sum / count;
            }

            int index = Mathf.Clamp(Mathf.CeilToInt(p * count) - 1, 0, count - 1);
            return scratch[index];
        }

        private void Refresh()
        {
            _builder.Length = 0;
            _builder.Append("FPS ").Append(_fps.ToString("F0"));
            _builder.Append("   fotograma ").Append(AverageFrameMs.ToString("F1")).Append(" ms (p99 ").Append(P99FrameMs.ToString("F1")).Append(")\n");
            _builder.Append("GC (gen0): ").Append(GcCollections).Append("   asign. ~").Append(_allocPerSecond.ToString("F1")).Append(" KB/s\n");
            _builder.Append("Memoria: ").Append((Profiler.GetTotalAllocatedMemoryLong() / 1048576L).ToString()).Append(" MB (mono ")
                .Append((Profiler.GetMonoUsedSizeLong() / 1048576L).ToString()).Append(" MB)\n");
            _builder.Append("Latencia est. p95: ").Append(EstimatedLatencyP95Ms.ToString("F0")).Append(" ms\n");
            _builder.Append("Pasos/fotograma: ").Append(_framesTotal > 0 ? ((float)_ticksTotal / _framesTotal).ToString("F2") : "0").Append('\n');
            _builder.Append(SystemInfo.deviceModel).Append("  RAM ").Append(SystemInfo.systemMemorySize).Append(" MB");
            _text.text = _builder.ToString();
        }
    }
}
