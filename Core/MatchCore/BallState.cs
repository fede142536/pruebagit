using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Estado físico del balón. Origen en el centro del campo, Z hacia arriba.</summary>
    public sealed class BallState
    {
        /// <summary>Posición del centro del balón (Z = altura sobre el césped).</summary>
        public Vec3 Pos;

        /// <summary>Velocidad (m/s).</summary>
        public Vec3 Vel;

        /// <summary>Giro alrededor del eje vertical (rad/s); positivo curva hacia +Y si el balón viaja hacia +X.</summary>
        public float Spin;

        /// <summary>Coloca el balón en reposo.</summary>
        /// <param name="position">Posición sobre el césped.</param>
        public void PlaceAt(Vec2 position)
        {
            Pos = new Vec3(position.X, position.Y, 0f);
            Vel = Vec3.Zero;
            Spin = 0f;
        }
    }
}
