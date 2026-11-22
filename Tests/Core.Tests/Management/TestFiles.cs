using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace Football.Core.Tests.Management
{
    /// <summary>Utilidades de las pruebas de gestión: archivos de balance del repositorio.</summary>
    internal static class TestFiles
    {
        /// <summary>Comprueba que un archivo de balance coincide con los valores por defecto de su clase.</summary>
        public static void AssertMatchesDefaults<T>(string fileName, string regenerate) where T : new()
        {
            string dir = AppContext.BaseDirectory, path = null;
            while (dir != null && path == null)
            {
                string candidate = Path.Combine(dir, "Data", "balance", fileName);
                path = File.Exists(candidate) ? candidate : null;
                dir = Path.GetDirectoryName(dir);
            }

            Assert.That(path, Is.Not.Null, "Data/balance/" + fileName + " debe existir");
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            T fromFile = JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
            Assert.That(JsonSerializer.Serialize(fromFile, options), Is.EqualTo(JsonSerializer.Serialize(new T(), options)), regenerate);
        }
    }
}
