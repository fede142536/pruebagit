using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Textura única y compartida de los decales de las camisetas (dorsal y escudo): un <c>Texture2D</c> R8 de 256×256 (64 KB) que se
    /// arma por código al arrancar, desde los píxeles de <see cref="PlayerAtlasData"/>, y se publica como la textura global
    /// <c>_FootballAtlas</c>. Al ser global, el material de los jugadores sigue siendo uno solo (SRP Batcher) y no hace falta
    /// <c>MaterialPropertyBlock</c>.
    /// </summary>
    public static class PlayerAtlas
    {
        private static readonly int AtlasId = Shader.PropertyToID("_FootballAtlas");
        private static Texture2D _texture;

        /// <summary>Crea la textura (si hace falta) y la publica para los shaders. Es seguro llamarla más de una vez.</summary>
        public static void Publish()
        {
            if (_texture == null)
            {
                _texture = new Texture2D(PlayerAtlasData.Size, PlayerAtlasData.Size, TextureFormat.R8, false)
                {
                    name = "FootballAtlas",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _texture.LoadRawTextureData(PlayerAtlasData.Build());
                _texture.Apply(false, true); // sin mipmaps y sin copia en la memoria del procesador
            }

            Shader.SetGlobalTexture(AtlasId, _texture);
        }
    }
}
