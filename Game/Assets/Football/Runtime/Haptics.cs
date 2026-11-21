using UnityEngine;

namespace Football.Game
{
    /// <summary>Vibración corta del teléfono (goles, palos). En el ordenador no hace nada.</summary>
    public static class Haptics
    {
        /// <summary>Vibra una vez si la opción está activada.</summary>
        /// <param name="enabled">Ajuste del jugador.</param>
        public static void Pulse(bool enabled)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (enabled)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
