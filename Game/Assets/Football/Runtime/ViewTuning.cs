using System;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Ajustes de la cámara del partido (se leen de <c>Resources/view_tuning.json</c>).</summary>
    [Serializable]
    public sealed class CameraTuning
    {
        /// <summary>Altura de la cámara de transmisión (m).</summary>
        public float Height = 13f;
        /// <summary>Distancia horizontal de la cámara al centro del campo (m).</summary>
        public float Distance = 34f;
        /// <summary>Campo de visión vertical (grados).</summary>
        public float Fov = 40f;
        /// <summary>Cuánto sigue al balón a lo largo (0 nada, 1 del todo).</summary>
        public float FollowX = 0.8f;
        /// <summary>Cuánto sigue al balón a lo ancho (alto: se ven las dos líneas laterales).</summary>
        public float FollowZ = 0.85f;
        /// <summary>Rapidez con la que la cámara alcanza su objetivo (1/s).</summary>
        public float FollowSpeed = 3f;
        /// <summary>Distancia al fondo donde la cámara deja de avanzar (m, cancha grande).</summary>
        public float EdgeMargin = 18.5f;
        /// <summary>Alejamiento con el balón largo o en el medio (× distancia).</summary>
        public float ZoomOut = 1.15f;
        /// <summary>Acercamiento en el último tercio (× distancia).</summary>
        public float ZoomIn = 0.88f;
        /// <summary>Velocidad del balón a partir de la cual se considera balón largo (m/s).</summary>
        public float ZoomLongBallSpeed = 15f;
        /// <summary>Rapidez del cambio de zoom (1/s).</summary>
        public float ZoomSpeed = 1.5f;
        /// <summary>Segundos que la cámara se adelanta hacia donde corre el poseedor.</summary>
        public float LookAheadSeconds = 0.7f;
        /// <summary>Adelanto máximo (m).</summary>
        public float LookAheadMax = 6f;
        /// <summary>Zoom en los saques (× distancia).</summary>
        public float SetPieceZoom = 1.25f;
        /// <summary>Adelanto de la cámara en los saques (m).</summary>
        public float SetPieceLookAhead = 12f;
        /// <summary>Zoom al enfocar a un jugador (festejo).</summary>
        public float FocusZoom = 0.55f;
        /// <summary>Rapidez del paso suave de un modo de cámara a otro (1/s).</summary>
        public float TransitionSpeed = 4f;
        /// <summary>Táctica panorámica: altura (m).</summary>
        public float TacticalHeight = 40f;
        /// <summary>Táctica panorámica: distancia horizontal (m).</summary>
        public float TacticalDistance = 20f;
        /// <summary>Lateral baja: altura (m).</summary>
        public float LowHeight = 5.5f;
        /// <summary>Lateral baja: distancia horizontal al balón (m).</summary>
        public float LowDistance = 17f;
        /// <summary>Profesional: altura detrás del jugador controlado (m).</summary>
        public float ProHeight = 4.2f;
        /// <summary>Profesional: distancia detrás del jugador controlado (m).</summary>
        public float ProBehind = 8.5f;
        /// <summary>Profesional: cuánto por delante del jugador mira (m).</summary>
        public float ProLookAhead = 9f;
        /// <summary>Profesional: rapidez con la que la cámara gira detrás del jugador (1/s).</summary>
        public float ProTurnSpeed = 2.2f;
        /// <summary>Repetición: altura de la cámara a ras del césped detrás del arco (m).</summary>
        public float GoalCamHeight = 0.35f;
        /// <summary>Repetición: distancia de esa cámara por detrás de la línea de gol (m).</summary>
        public float GoalCamBehind = 3.2f;
    }

    /// <summary>Ajustes de los controles táctiles (se leen de <c>Resources/view_tuning.json</c>).</summary>
    [Serializable]
    public sealed class ControlsTuning
    {
        /// <summary>Radio del joystick (unidades de la interfaz).</summary>
        public float JoystickRadius = 130f;
        /// <summary>Zonas muertas que ofrece Ajustes (fracción del radio); el ajuste elige una.</summary>
        public float[] DeadZones = { 0.06f, 0.12f, 0.2f };
        /// <summary>Segundos para cargar el tiro al máximo.</summary>
        public float ShootChargeSeconds = 0.8f;
        /// <summary>Segundos para cargar el pase al máximo.</summary>
        public float PassChargeSeconds = 0.7f;
        /// <summary>Un toque de pase más corto que esto es un pase automático (s).</summary>
        public float PassTapSeconds = 0.2f;
        /// <summary>Potencia mínima del tiro (fracción).</summary>
        public float ShootMinPower = 0.35f;
        /// <summary>Escala de los botones grandes.</summary>
        public float BigScale = 1.2f;
        /// <summary>Distancia del joystick fijo a los bordes (unidades).</summary>
        public float FixedJoystickMargin = 250f;
        /// <summary>Margen del botón de pausa (unidades).</summary>
        public float PauseMargin = 75f;
    }

    /// <summary>Ajustes de la interfaz del partido (se leen de <c>Resources/view_tuning.json</c>).</summary>
    [Serializable]
    public sealed class HudTuning
    {
        /// <summary>Separación del marcador respecto de la esquina superior izquierda (unidades).</summary>
        public float ScoreboardMargin = 20f;
    }

    /// <summary>
    /// Valores de ajuste de la capa Unity (cámara, controles, interfaz) en datos, no en el código. Si el archivo falta o
    /// está roto, se usan los valores por defecto de estas clases (que coinciden con el archivo entregado).
    /// </summary>
    [Serializable]
    public sealed class ViewTuning
    {
        private const string ResourceName = "view_tuning";
        private static ViewTuning _current;

        /// <summary>Cámara.</summary>
        public CameraTuning Camera = new CameraTuning();
        /// <summary>Controles táctiles.</summary>
        public ControlsTuning Controls = new ControlsTuning();
        /// <summary>Interfaz del partido.</summary>
        public HudTuning Hud = new HudTuning();

        /// <summary>Ajustes en uso (se cargan la primera vez).</summary>
        public static ViewTuning Current => _current ?? (_current = Load());

        private static ViewTuning Load()
        {
            var tuning = new ViewTuning();
            try
            {
                TextAsset text = Resources.Load<TextAsset>(ResourceName);
                if (text != null)
                {
                    JsonUtility.FromJsonOverwrite(text.text, tuning);
                }
                else
                {
                    Debug.LogWarning("Falta Resources/" + ResourceName + ".json; se usan los valores por defecto.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("No se pudo leer " + ResourceName + ".json (" + e.Message + "); se usan los valores por defecto.");
                tuning = new ViewTuning();
            }

            if (tuning.Controls.DeadZones == null || tuning.Controls.DeadZones.Length == 0)
            {
                tuning.Controls.DeadZones = new ControlsTuning().DeadZones;
            }

            return tuning;
        }
    }
}
