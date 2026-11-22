using Football.Core.Domain;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Adorno de una bandera, además de las franjas.</summary>
    public enum FlagEmblem
    {
        /// <summary>Solo franjas.</summary>
        None,
        /// <summary>Sol en el centro.</summary>
        CenterSun,
        /// <summary>Sol arriba a la izquierda.</summary>
        CornerSun,
        /// <summary>Rombo con círculo.</summary>
        Diamond,
        /// <summary>Cantón con estrella arriba a la izquierda.</summary>
        Canton
    }

    /// <summary>Bandera simplificada (franjas iguales y un adorno), dibujada con paneles.</summary>
    public sealed class NationFlag
    {
        /// <summary>Código del país (el que guarda <see cref="ManagerProfile.Nationality"/>).</summary>
        public string Code;
        /// <summary>Franjas, de arriba abajo o de izquierda a derecha.</summary>
        public Color[] Stripes;
        /// <summary>Franjas verticales.</summary>
        public bool Vertical;
        /// <summary>Adorno.</summary>
        public FlagEmblem Emblem;
        /// <summary>Color del adorno.</summary>
        public Color EmblemColor;
        /// <summary>Color secundario del adorno (círculo del rombo o estrella del cantón).</summary>
        public Color EmblemInner;

        private static readonly Color Celeste = new Color(0.45f, 0.72f, 0.92f);
        private static readonly Color White = Color.white;
        private static readonly Color Sun = new Color(0.98f, 0.75f, 0.15f);
        private static readonly Color Blue = new Color(0.05f, 0.2f, 0.6f);
        private static readonly Color Red = new Color(0.82f, 0.1f, 0.15f);
        private static readonly Color Green = new Color(0.0f, 0.55f, 0.27f);
        private static readonly Color Yellow = new Color(1f, 0.82f, 0.1f);

        /// <summary>Naciones elegibles para el DT (la primera, Argentina, es la de fábrica).</summary>
        public static readonly NationFlag[] All =
        {
            new NationFlag { Code = "AR", Stripes = new[] { Celeste, White, Celeste }, Emblem = FlagEmblem.CenterSun, EmblemColor = Sun },
            new NationFlag { Code = "UY", Stripes = new[] { White, Blue, White, Blue, White }, Emblem = FlagEmblem.CornerSun, EmblemColor = Sun },
            new NationFlag { Code = "BR", Stripes = new[] { Green }, Emblem = FlagEmblem.Diamond, EmblemColor = Yellow, EmblemInner = Blue },
            new NationFlag { Code = "CL", Stripes = new[] { White, Red }, Emblem = FlagEmblem.Canton, EmblemColor = Blue, EmblemInner = White },
            new NationFlag { Code = "PY", Stripes = new[] { Red, White, Blue } },
            new NationFlag { Code = "CO", Stripes = new[] { Yellow, Yellow, Blue, Red } },
            new NationFlag { Code = "PE", Stripes = new[] { Red, White, Red }, Vertical = true },
            new NationFlag { Code = "ES", Stripes = new[] { Red, Yellow, Yellow, Red } },
            new NationFlag { Code = "IT", Stripes = new[] { Green, White, Red }, Vertical = true },
            new NationFlag { Code = "FR", Stripes = new[] { Blue, White, Red }, Vertical = true }
        };

        /// <summary>Índice de una nación por código (0, Argentina, si no está).</summary>
        public static int IndexOf(string code)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Code == code)
                {
                    return i;
                }
            }

            return 0;
        }
    }

    /// <summary>Dibujo de una bandera (se arma una vez con el máximo de franjas; después solo cambian colores).</summary>
    public sealed class FlagView
    {
        private const int MaxStripes = 5;
        private const float SunShare = 0.3f;
        private const float CantonShare = 0.5f;
        private const float DiamondShare = 0.62f;
        private readonly Image[] _stripes = new Image[MaxStripes];
        private readonly Image _canton, _diamond, _emblem;
        private readonly Vector2 _center, _size;
        private readonly Transform _root;

        /// <summary>Crea la bandera.</summary>
        /// <param name="parent">Padre.</param>
        /// <param name="center">Centro.</param>
        /// <param name="size">Tamaño.</param>
        public FlagView(Transform parent, Vector2 center, Vector2 size)
        {
            _center = center;
            _size = size;
            _root = CareerPage.CreateGroup(parent, "Bandera");
            UiKit.Panel(_root, "Borde", center, size + new Vector2(4f, 4f), new Color(0f, 0f, 0f, 0.5f));
            for (int i = 0; i < MaxStripes; i++)
            {
                _stripes[i] = UiKit.Panel(_root, "Franja" + i, center, size, Color.white);
            }

            _canton = UiKit.Panel(_root, "Canton", center, size, Color.white);
            _diamond = UiKit.Panel(_root, "Rombo", center, size, Color.white);
            _diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _emblem = UiKit.Panel(_root, "Adorno", center, size, Color.white, true);
        }

        /// <summary>Pinta una bandera.</summary>
        public void Set(NationFlag flag)
        {
            int n = flag.Stripes.Length;
            for (int i = 0; i < MaxStripes; i++)
            {
                Image s = _stripes[i];
                s.gameObject.SetActive(i < n);
                if (i >= n)
                {
                    continue;
                }

                s.color = flag.Stripes[i];
                Vector2 size = flag.Vertical ? new Vector2(_size.x / n, _size.y) : new Vector2(_size.x, _size.y / n);
                Vector2 offset = flag.Vertical
                    ? new Vector2(-_size.x * 0.5f + size.x * (i + 0.5f), 0f)
                    : new Vector2(0f, _size.y * 0.5f - size.y * (i + 0.5f));
                Place(s, _center + offset, size);
            }

            float h = _size.y;
            _canton.gameObject.SetActive(flag.Emblem == FlagEmblem.Canton);
            _diamond.gameObject.SetActive(flag.Emblem == FlagEmblem.Diamond);
            _emblem.gameObject.SetActive(flag.Emblem != FlagEmblem.None);
            switch (flag.Emblem)
            {
                case FlagEmblem.CenterSun:
                    _emblem.color = flag.EmblemColor;
                    Place(_emblem, _center, new Vector2(h * SunShare, h * SunShare));
                    break;
                case FlagEmblem.CornerSun:
                    _emblem.color = flag.EmblemColor;
                    Place(_emblem, _center + new Vector2(-_size.x * 0.32f, h * 0.25f), new Vector2(h * SunShare, h * SunShare));
                    break;
                case FlagEmblem.Diamond:
                    float d = h * DiamondShare;
                    _diamond.color = flag.EmblemColor;
                    Place(_diamond, _center, new Vector2(d, d));
                    _emblem.color = flag.EmblemInner;
                    Place(_emblem, _center, new Vector2(h * 0.42f, h * 0.42f));
                    break;
                case FlagEmblem.Canton:
                    Vector2 c = _center + new Vector2(-_size.x * 0.5f + h * CantonShare * 0.5f, h * 0.25f);
                    _canton.color = flag.EmblemColor;
                    Place(_canton, c, new Vector2(h * CantonShare, h * CantonShare));
                    _emblem.color = flag.EmblemInner;
                    Place(_emblem, c, new Vector2(h * 0.2f, h * 0.2f));
                    break;
            }
        }

        private static void Place(Image image, Vector2 center, Vector2 size)
        {
            image.rectTransform.anchoredPosition = center;
            image.rectTransform.sizeDelta = size;
        }
    }

    /// <summary>Aspecto del DT según el avatar elegido (piel, pelo, peinado y barba): lo comparten el ícono y la figura 3D.</summary>
    public static class ManagerLooks
    {
        /// <summary>Cantidad de avatares.</summary>
        public const int Count = 6;
        private static readonly int[] Skin = { 1, 3, 0, 4, 2, 5 };
        private static readonly int[] Hair = { 0, 1, 2, 0, 3, 1 };
        private static readonly int[] Style = { 0, 1, 2, 3, 4, 0 };
        private static readonly bool[] Beard = { false, true, false, true, false, true };

        /// <summary>Color de piel del avatar.</summary>
        public static Color SkinOf(int avatar) => MatchView.SkinTones[Skin[Wrap(avatar)] % MatchView.SkinTones.Length];

        /// <summary>Color de pelo del avatar.</summary>
        public static Color HairOf(int avatar) => MatchView.HairColors[Hair[Wrap(avatar)] % MatchView.HairColors.Length];

        /// <summary>El avatar lleva barba.</summary>
        public static bool BeardOf(int avatar) => Beard[Wrap(avatar)];

        /// <summary>Ropa 3D del DT: traje oscuro con camisa blanca, o buzo con los colores del club (o del juego si no hay club).</summary>
        /// <param name="avatar">Avatar.</param>
        /// <param name="outfit">Traje o buzo.</param>
        /// <param name="club">Colores del club, o null.</param>
        public static PlayerLook Look(int avatar, ManagerOutfit outfit, KitColors? club)
        {
            bool suit = outfit == ManagerOutfit.Suit;
            Color charcoal = new Color(0.14f, 0.15f, 0.19f);
            Color top = suit ? charcoal : club?.Shirt ?? UiTheme.AccentDark;
            Color trim = suit ? Color.white : club?.Secondary ?? Color.white;
            Color legs = suit ? charcoal : club?.Shorts ?? new Color(0.1f, 0.12f, 0.16f);
            return new PlayerLook
            {
                Shirt = top, Secondary = trim, Pattern = KitPattern.Solid, Shorts = legs, Socks = legs,
                Skin = SkinOf(avatar), Hair = HairOf(avatar), HairStyle = Style[Wrap(avatar)] % MatchView.HairStyles, Beard = BeardOf(avatar),
                Boots = suit ? new Color(0.06f, 0.05f, 0.05f) : Color.white, Trousers = true, LongSleeves = true
            };
        }

        private static int Wrap(int avatar) => ((avatar % Count) + Count) % Count;
    }

    /// <summary>Ícono redondo del DT: fondo, cara, pelo y barba.</summary>
    public sealed class AvatarView
    {
        private readonly Image _ring, _face, _hair, _beard;

        /// <summary>Crea el ícono.</summary>
        public AvatarView(Transform parent, Vector2 center, float size)
        {
            _ring = UiKit.Panel(parent, "AvatarAro", center, new Vector2(size, size), UiTheme.Button, true);
            UiKit.Panel(parent, "AvatarFondo", center, new Vector2(size * 0.9f, size * 0.9f), new Color(0.2f, 0.26f, 0.34f), true);
            UiKit.Panel(parent, "AvatarCuerpo", center + new Vector2(0f, -size * 0.36f), new Vector2(size * 0.62f, size * 0.24f), new Color(0.14f, 0.15f, 0.19f));
            _face = UiKit.Panel(parent, "AvatarCara", center + new Vector2(0f, size * 0.04f), new Vector2(size * 0.44f, size * 0.52f), Color.white, true);
            _hair = UiKit.Panel(parent, "AvatarPelo", center + new Vector2(0f, size * 0.26f), new Vector2(size * 0.48f, size * 0.16f), Color.black);
            _beard = UiKit.Panel(parent, "AvatarBarba", center + new Vector2(0f, -size * 0.14f), new Vector2(size * 0.3f, size * 0.12f), Color.black);
        }

        /// <summary>Pinta el avatar.</summary>
        /// <param name="avatar">Índice.</param>
        /// <param name="selected">Marcado (aro de color).</param>
        public void Set(int avatar, bool selected)
        {
            _face.color = ManagerLooks.SkinOf(avatar);
            _hair.color = ManagerLooks.HairOf(avatar);
            _beard.color = ManagerLooks.HairOf(avatar);
            _beard.gameObject.SetActive(ManagerLooks.BeardOf(avatar));
            _ring.color = selected ? UiTheme.Accent : UiTheme.Button;
        }
    }
}
