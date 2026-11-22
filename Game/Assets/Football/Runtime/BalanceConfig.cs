using System;
using Football.Core.MatchCore;
using UnityEngine;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>
    /// Carga el balance del partido desde <c>Resources/match.json</c> (o <c>match_indoor.json</c> para la cancha cerrada),
    /// copias de <c>Data/balance</c>, para que los ajustes vivan en datos. Si el archivo falta o no es válido, se usan los
    /// valores por defecto del núcleo.
    /// </summary>
    public static class BalanceConfig
    {
        private const int ExpectedDifficulties = 5;
        private static readonly string[] ResourceNames = { "match", "match_indoor" };
        private static readonly string[] Json = new string[2];
        private static readonly bool[] Loaded = new bool[2];

        /// <summary>Motivo por el que no se usó el archivo, o null.</summary>
        public static string LoadError { get; private set; }

        /// <summary>Crea una configuración nueva (independiente en cada partido) a partir del archivo de balance.</summary>
        /// <param name="indoor">Cancha cerrada 3 contra 3.</param>
        public static MatchConfig Create(bool indoor = false)
        {
            int k = indoor ? 1 : 0;
            if (!Loaded[k])
            {
                Loaded[k] = true;
                TextAsset asset = Resources.Load<TextAsset>(ResourceNames[k]);
                Json[k] = asset != null ? asset.text : null;
                if (Json[k] == null)
                {
                    LoadError = "no se encontró Resources/" + ResourceNames[k] + ".json; se usan los valores por defecto";
                }
            }

            MatchConfig config = Defaults(indoor);
            string json = Json[k];
            if (json == null)
            {
                return config;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(json, config);
                if (config.TickRate > 0 && config.Difficulties != null && config.Difficulties.Length == ExpectedDifficulties
                    && config.Styles != null && config.Styles.Length > 0)
                {
                    return config;
                }

                LoadError = "Resources/" + ResourceNames[k] + ".json incompleto; se usan los valores por defecto";
            }
            catch (Exception e)
            {
                LoadError = e.Message;
                Debug.LogException(e);
            }

            Json[k] = null;
            return Defaults(indoor);
        }

        private static MatchConfig Defaults(bool indoor) => indoor ? MatchConfig.CreateIndoor() : new MatchConfig();
    }
}
