using System;
using System.Collections.Generic;
using System.IO;
using Football.Core.Data;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Equipaciones titulares editadas por el usuario (pantalla de camisetas), guardadas en <c>kits.json</c> junto a las demás partidas. Al leer los equipos se aplican
    /// sobre la camiseta CASA de cada club (por nombre); la original queda guardada en memoria para poder restaurarla. Si el archivo falta o está roto, se usan las del archivo de equipos.
    /// </summary>
    public static class KitStore
    {
        private const string FileName = "kits.json";

        [Serializable]
        private sealed class Entry
        {
            public string team;
            public KitDefinition home;
        }

        [Serializable]
        private sealed class Overrides
        {
            public List<Entry> entries = new List<Entry>();
        }

        private static readonly Dictionary<string, KitDefinition> Originals = new Dictionary<string, KitDefinition>();
        private static List<TeamDefinition> _teams = new List<TeamDefinition>();
        private static Overrides _data = new Overrides();

        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Lee las equipaciones guardadas y las aplica a los equipos recién cargados (guarda antes las originales).</summary>
        /// <param name="teams">Equipos válidos del archivo de datos.</param>
        public static void ApplyTo(List<TeamDefinition> teams)
        {
            _teams = teams;
            Originals.Clear();
            foreach (TeamDefinition t in teams)
            {
                Originals[t.Name] = Copy(t.Home);
            }

            _data = Read();
            foreach (Entry e in _data.entries)
            {
                TeamDefinition team = teams.Find(t => t.Name == e.team);
                if (team != null && e.home != null)
                {
                    Assign(team.Home, e.home);
                }
            }
        }

        /// <summary>Si el club tiene una equipación editada.</summary>
        /// <param name="teamName">Nombre del club.</param>
        public static bool IsEdited(string teamName) => _data.entries.Exists(e => e.team == teamName);

        /// <summary>Guarda una nueva camiseta CASA para el club y la aplica ya mismo.</summary>
        /// <param name="team">Club.</param>
        /// <param name="home">Equipación nueva.</param>
        public static void Save(TeamDefinition team, KitDefinition home)
        {
            _data.entries.RemoveAll(e => e.team == team.Name);
            _data.entries.Add(new Entry { team = team.Name, home = Copy(home) });
            Assign(team.Home, home);
            Write();
        }

        /// <summary>Devuelve al club su camiseta CASA original del archivo de equipos.</summary>
        /// <param name="team">Club.</param>
        public static void Reset(TeamDefinition team)
        {
            _data.entries.RemoveAll(e => e.team == team.Name);
            if (Originals.TryGetValue(team.Name, out KitDefinition original))
            {
                Assign(team.Home, original);
            }

            Write();
        }

        private static KitDefinition Copy(KitDefinition k) => new KitDefinition { Shirt = k.Shirt, Secondary = k.Secondary, Pattern = k.Pattern, Shorts = k.Shorts, Socks = k.Socks };

        private static void Assign(KitDefinition target, KitDefinition from)
        {
            target.Shirt = from.Shirt;
            target.Secondary = from.Secondary;
            target.Pattern = from.Pattern;
            target.Shorts = from.Shorts;
            target.Socks = from.Socks;
        }

        private static Overrides Read()
        {
            try
            {
                if (File.Exists(Path))
                {
                    return JsonUtility.FromJson<Overrides>(File.ReadAllText(Path)) ?? new Overrides();
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("Football: no se pudo leer kits.json: " + e.Message);
            }

            return new Overrides();
        }

        private static void Write()
        {
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("Football: no se pudo guardar kits.json: " + e.Message);
            }
        }
    }
}
