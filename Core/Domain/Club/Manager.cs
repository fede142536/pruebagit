namespace Football.Core.Domain
{
    /// <summary>Vestimenta del director técnico.</summary>
    public enum ManagerOutfit
    {
        /// <summary>Traje y corbata.</summary>
        Suit,
        /// <summary>Conjunto deportivo del club.</summary>
        Tracksuit
    }

    /// <summary>Director técnico que maneja el usuario en el modo carrera.</summary>
    public sealed class ManagerProfile
    {
        /// <summary>Popularidad inicial con la directiva.</summary>
        public const float StartingPopularity = 80f;

        /// <summary>Nombre.</summary>
        public string FirstName = string.Empty;
        /// <summary>Apellido.</summary>
        public string LastName = string.Empty;
        /// <summary>Nacionalidad (código de país de dos letras, por ejemplo <c>AR</c>).</summary>
        public string Nationality = "AR";
        /// <summary>Retrato/avatar elegido (0–5).</summary>
        public int AvatarIndex;
        /// <summary>Vestimenta del modelo 3D.</summary>
        public ManagerOutfit Outfit = ManagerOutfit.Suit;
        /// <summary>Reputación en el fútbol (0–100).</summary>
        public float Reputation = 50f;
        /// <summary>Popularidad con la directiva (0–100); por debajo del umbral crítico, lo pueden despedir.</summary>
        public float Popularity = StartingPopularity;
        /// <summary>La directiva lo despidió.</summary>
        public bool Sacked;

        /// <summary>Nombre completo.</summary>
        public string FullName => (FirstName + " " + LastName).Trim();
    }
}
