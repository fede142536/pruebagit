using System;
using Football.Core.MatchCore;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Carga el balance del partido desde <c>Resources/match.json</c> (copia de <c>Data/balance/match.json</c>), para que los
    /// ajustes vivan en datos. Si el archivo falta o no es válido, se usan los valores por defecto del núcleo.
    /// </summary>
    public static class BalanceConfig
    {
        private const string ResourceName = "match";
        private const int ExpectedDifficulties = 5;
        private static string _json;
        private static bool _loaded;

        /// <summary>Motivo por el que no se usó el archivo, o null.</summary>
        public static string LoadError { get; private set; }

        /// <summary>Crea una configuración nueva (independiente en cada partido) a partir del archivo de balance.</summary>
        public static MatchConfig Create()
        {
            if (!_loaded)
            {
                _loaded = true;
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                _json = asset != null ? asset.text : null;
                if (_json == null)
                {
                    LoadError = "no se encontró Resources/match.json; se usan los valores por defecto";
                }
            }

            var config = new MatchConfig();
            if (_json == null)
            {
                return config;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(_json, config);
                if (config.TickRate > 0 && config.Difficulties != null && config.Difficulties.Length == ExpectedDifficulties
                    && config.Styles != null && config.Styles.Length > 0)
                {
                    return config;
                }

                LoadError = "Resources/match.json incompleto; se usan los valores por defecto";
            }
            catch (Exception e)
            {
                LoadError = e.Message;
                Debug.LogException(e);
            }

            _json = null;
            return new MatchConfig();
        }
    }
}
