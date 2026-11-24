namespace Football.Game
{
    /// <summary>
    /// Canciones de los menús: dónde están y cuáles tiene encendidas la persona (una máscara de bits en <see cref="GameSettings.MenuSongs"/>).
    /// Los títulos salen de <c>ui_es.json</c>/<c>ui_en.json</c> (<c>music.0</c>, <c>music.1</c>…).
    /// </summary>
    public static class MenuMusic
    {
        /// <summary>Ruta de cada canción en <c>Resources</c>.</summary>
        public static readonly string[] Paths =
        {
            "Audio/music_menu", "Audio/music_magalenha", "Audio/music_chega_de_saudade", "Audio/music_insensatez",
            "Audio/music_garota_de_ipanema", "Audio/music_desafinado", "Audio/music_wave"
        };

        /// <summary>Cantidad de canciones.</summary>
        public static int Count => Paths.Length;

        /// <summary>Máscara con todas las canciones encendidas.</summary>
        public static int All => (1 << Paths.Length) - 1;

        /// <summary>Verdadero si la canción está encendida en la máscara.</summary>
        public static bool IsOn(int mask, int song) => (mask & (1 << song)) != 0;

        /// <summary>Enciende o apaga una canción.</summary>
        public static int Toggle(int mask, int song) => mask ^ (1 << song);

        /// <summary>Cuántas canciones hay encendidas.</summary>
        public static int OnCount(int mask)
        {
            int n = 0;
            for (int i = 0; i < Paths.Length; i++)
            {
                n += IsOn(mask, i) ? 1 : 0;
            }

            return n;
        }

        /// <summary>Con alguna encendida las apaga todas; con todas apagadas, las enciende todas.</summary>
        public static int ToggleAll(int mask) => OnCount(mask) > 0 ? 0 : All;
    }
}
