using System;
using System.IO;
using System.Linq;
using Football.Core.Common;
using NUnit.Framework;

namespace Football.Core.Tests
{
    /// <summary>Textos de interfaz (es/en): lector de JSON plano y archivos completos y copiados a Unity.</summary>
    public class StringTableTests
    {
        private static string Find(params string[] parts)
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        [Test]
        public void Parse_ReadsKeysAndEscapes()
        {
            var t = StringTable.Parse("{ \"a\": \"uno\\ndos\", \"b\" : \"comillas \\\" y \\u00e1\" }");
            Assert.That(t["a"], Is.EqualTo("uno\ndos"));
            Assert.That(t["b"], Is.EqualTo("comillas \" y á"));
            Assert.That(StringTable.Get(t, "falta"), Is.EqualTo("[falta]"));
            Assert.That(StringTable.Parse("{}"), Is.Empty);
        }

        [TestCase("[\"a\"]")]
        [TestCase("{\"a\": 1}")]
        [TestCase("{\"a\": \"sin cerrar}")]
        public void Parse_RejectsWhatIsNotAFlatStringObject(string json)
        {
            Assert.Throws<FormatException>(() => StringTable.Parse(json));
        }

        [TestCase("career.json")]
        [TestCase("finance.json")]
        [TestCase("management.json")]
        [TestCase("matchsim.json")]
        public void UnityCopyOfTheCareerBalance_IsUpToDate(string file)
        {
            string from = Find("Data", "balance", file);
            string to = Find("Game", "Assets", "Football", "Resources", file);
            Assert.That(to, Is.Not.Null, "falta la copia para Unity: ejecutar Tools/build-core.sh");
            Assert.That(File.ReadAllText(to), Is.EqualTo(File.ReadAllText(from)));
        }

        [Test]
        public void NewsInSpanishAndEnglish_HaveTheSameKeys_IncludingTheCareerOnes()
        {
            var es = StringTable.Parse(File.ReadAllText(Find("Data", "locale", "news.es.json")));
            var en = StringTable.Parse(File.ReadAllText(Find("Data", "locale", "news.en.json")));
            Assert.That(es.Keys, Is.EquivalentTo(en.Keys));
            foreach (string key in new[] { "news.result", "news.scout_report", "news.offer_received", "news.relegated", "news.user_relegated",
                         "news.qualified", "news.season_end", "news.champion" })
            {
                Assert.That(es.ContainsKey(key), Is.True, key);
            }
        }

        [Test]
        public void SpanishAndEnglish_HaveTheSameKeys_AndTheGameIsCalledPotrero()
        {
            var es = StringTable.Parse(File.ReadAllText(Find("Data", "locale", "ui.es.json")));
            var en = StringTable.Parse(File.ReadAllText(Find("Data", "locale", "ui.en.json")));
            Assert.That(es.Keys, Is.EquivalentTo(en.Keys));
            Assert.That(es["app.title"], Is.EqualTo("POTRERO"));
            Assert.That(es.Values.Concat(en.Values).All(v => !string.IsNullOrWhiteSpace(v)), Is.True);
        }

        [TestCase("ui.es.json", "ui_es.json")]
        [TestCase("ui.en.json", "ui_en.json")]
        [TestCase("news.es.json", "news_es.json")]
        [TestCase("news.en.json", "news_en.json")]
        public void UnityCopyOfTheTexts_IsUpToDate(string source, string copy)
        {
            string from = Find("Data", "locale", source);
            string to = Find("Game", "Assets", "Football", "Resources", copy);
            Assert.That(to, Is.Not.Null, "falta la copia para Unity: ejecutar Tools/build-core.sh");
            Assert.That(File.ReadAllText(to), Is.EqualTo(File.ReadAllText(from)));
        }
    }
}
