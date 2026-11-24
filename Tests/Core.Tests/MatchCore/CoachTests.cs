using System;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Director técnico en vivo: doble dibujo, gritos, charla del entretiempo y pelota parada.</summary>
    public sealed class CoachTests
    {
        [Test]
        public void DefensiveFormation_IsUsedOnlyWithoutTheBall()
        {
            MatchEngine e = MatchEngineTests.NewEngine(1); // local en 4-4-2
            MatchPlayer winger = e.Teams[0].Players.First(p => p.Role == Position.LeftMidfielder);
            Vec2 attackBefore = e.TestAnchor(winger, true);
            Vec2 defendBefore = e.TestAnchor(winger, false);
            Assert.That(e.SetDefensiveFormation(0, Formations.F352), Is.True);
            Assert.That(e.TestAnchor(winger, true), Is.EqualTo(attackBefore), "con balón no cambia");
            Assert.That(Vec2.Distance(e.TestAnchor(winger, false), defendBefore), Is.GreaterThan(1f), "sin balón usa el otro dibujo");
            Assert.That(e.SetDefensiveFormation(0, Formations.Indoor3), Is.False, "debe tener los mismos puestos");
        }

        [TestCase(TouchlineShout.ShortPassing)]
        [TestCase(TouchlineShout.WasteTime)]
        [TestCase(TouchlineShout.FindStriker)]
        [TestCase(TouchlineShout.CounterPress)]
        [TestCase(TouchlineShout.TakeOn)]
        public void Shout_ChangesTheStyle_AndWearsOff(TouchlineShout shout)
        {
            MatchEngine e = MatchEngineTests.NewEngine(2);
            TeamState t = e.Teams[0];
            float directness = t.Style.Directness, press = t.Style.PressIntensity, risk = t.Style.Risk;
            e.Shout(0, shout);
            bool changed = t.Style.Directness != directness || t.Style.PressIntensity != press || t.Style.Risk != risk;
            Assert.That(changed, Is.True);
            if (shout == TouchlineShout.FindStriker)
            {
                Assert.That(t.Style.Directness, Is.GreaterThan(directness));
            }

            if (shout == TouchlineShout.ShortPassing)
            {
                Assert.That(t.Style.Directness, Is.LessThan(directness));
            }

            int ticks = (int)((e.Config.Coach.ShoutSeconds + 2f) * e.Config.TickRate);
            for (int i = 0; i < ticks && !e.IsFinished; i++)
            {
                e.Tick();
            }

            Assert.That(t.Shout, Is.EqualTo(TouchlineShout.None));
            Assert.That(t.Style.Directness, Is.EqualTo(directness).Within(1e-5), "pasado su tiempo, el estilo vuelve");
        }

        [Test]
        public void WasteTime_SlowsDownOwnRestarts()
        {
            float normal = RestartWait(TouchlineShout.None), slow = RestartWait(TouchlineShout.WasteTime);
            // Ahora el ejecutante camina hasta la línea (0,6 m) y se da vuelta antes de sacar: el retraso del DT pesa menos sobre el total, pero se nota.
            Assert.That(slow, Is.GreaterThan(normal + 20f), $"normal {normal} pasos, perdiendo tiempo {slow}");
        }

        private static float RestartWait(TouchlineShout shout)
        {
            MatchEngine e = MatchEngineTests.NewEngine(3);
            e.TestBeginOpenPlay();
            e.Shout(0, shout);
            e.TestStartSetPiece(RestartKind.ThrowIn, 0, new Vec2(0f, e.Pitch.HalfWidth));
            int ticks = 0;
            while (e.Phase == MatchPhase.SetPiece && ticks < 2000)
            {
                e.Tick();
                ticks++;
            }

            return ticks;
        }

        [TestCase(TeamTalk.Calm, 1, TalkReaction.Positive)]
        [TestCase(TeamTalk.Calm, -1, TalkReaction.Negative)]
        [TestCase(TeamTalk.Passionate, -2, TalkReaction.Positive)]
        [TestCase(TeamTalk.Passionate, 2, TalkReaction.Neutral)]
        [TestCase(TeamTalk.Demanding, 0, TalkReaction.Positive)]
        [TestCase(TeamTalk.Demanding, -3, TalkReaction.Negative)]
        public void TeamTalk_ReactionDependsOnTheScore(TeamTalk talk, int difference, TalkReaction expected)
        {
            MatchEngine e = MatchEngineTests.NewEngine(4);
            e.SetHuman(0, true);
            Assert.That(e.GiveTeamTalk(0, talk, out _), Is.False, "solo en el entretiempo");
            e.Teams[0].Stats.Goals = Math.Max(0, difference);
            e.Teams[1].Stats.Goals = Math.Max(0, -difference);
            e.TestEndHalf();
            Assert.That(e.GiveTeamTalk(0, talk, out TalkReaction reaction), Is.True);
            Assert.That(reaction, Is.EqualTo(expected));
            Assert.That(e.GiveTeamTalk(0, talk, out _), Is.False, "una sola vez");
            MatchPlayer p = e.Teams[0].Players[5];
            if (expected == TalkReaction.Positive)
            {
                Assert.That(e.TestTalkError(p), Is.LessThan(1f), "más concentrados: menos error");
            }
            else if (expected == TalkReaction.Negative)
            {
                Assert.That(e.TestTalkError(p), Is.GreaterThan(1f), "nerviosos: más error");
            }
        }

        [Test]
        public void AiTeam_GivesItsOwnTalkAtHalfTime()
        {
            MatchEngine e = MatchEngineTests.NewEngine(5);
            e.Teams[1].Stats.Goals = 0;
            e.Teams[0].Stats.Goals = 2; // el visitante (IA) pierde: charla apasionada, efecto positivo
            e.TestEndHalf();
            Assert.That(e.Teams[1].TalkGiven, Is.True);
            Assert.That(e.Teams[1].TalkBoost, Is.GreaterThan(0f));
        }

        /// <summary>Lugar medio de los atacantes (sin el sacador ni el arquero) tras preparar un córner.</summary>
        [Test]
        public void SkipWait_PutsTheCornerTakerOnTheBallRightAway()
        {
            MatchEngine slow = CornerSetUp(CornerRoutine.Mixed, SetPieceMarking.Zonal, out _);
            MatchEngine fast = CornerSetUp(CornerRoutine.Mixed, SetPieceMarking.Zonal, out _);
            fast.Teams[0].Input = new MatchInput { SkipWait = true };
            int Ticks(MatchEngine e)
            {
                int n = 0;
                while (e.Phase == MatchPhase.SetPiece && n < 60 * 30)
                {
                    e.Tick();
                    n++;
                }

                return n;
            }

            int waited = Ticks(slow), skipped = Ticks(fast);
            Assert.That(skipped, Is.LessThan(waited), "tocar la pantalla acorta la espera");
            Assert.That(skipped, Is.LessThan(30), "casi de inmediato");
        }

        private static MatchEngine CornerSetUp(CornerRoutine routine, SetPieceMarking marking, out float postSide)
        {
            MatchEngine e = MatchEngineTests.NewEngine(6);
            e.TestBeginOpenPlay();
            e.SetSetPieces(0, routine, SetPieceMarking.Zonal);
            e.SetSetPieces(1, CornerRoutine.Mixed, marking);
            float dir = e.Teams[0].AttackDirection;
            postSide = 1f;
            e.TestStartSetPiece(RestartKind.Corner, 0, new Vec2(dir * e.Pitch.HalfLength, e.Pitch.HalfWidth));
            for (int i = 0; i < 150; i++)
            {
                e.Tick(); // se acomodan (la espera del córner es más larga que esto)
            }

            return e;
        }

        [Test]
        public void NearPostRoutine_LoadsTheNearPost()
        {
            double Mean(CornerRoutine routine)
            {
                MatchEngine e = CornerSetUp(routine, SetPieceMarking.Zonal, out float side);
                var attackers = e.Teams[0].Players.Skip(2).Where(p => p != e.Taker).ToArray();
                return attackers.Average(p => p.Target.Y * side);
            }

            Assert.That(Mean(CornerRoutine.NearPost), Is.GreaterThan(Mean(CornerRoutine.Mixed) + 2.0), "cargados hacia el primer palo");
        }

        [Test]
        public void ManMarking_PutsDefendersNextToTheirRivals()
        {
            double Gap(SetPieceMarking marking)
            {
                MatchEngine e = CornerSetUp(CornerRoutine.Mixed, marking, out _);
                double total = 0;
                int n = 0;
                for (int s = 2; s < e.Teams[1].Players.Length; s++)
                {
                    MatchPlayer rival = e.Teams[0].Players[s];
                    if (rival == e.Taker)
                    {
                        continue;
                    }

                    total += Vec2.Distance(e.Teams[1].Players[s].Target, rival.Pos);
                    n++;
                }

                return total / n;
            }

            Assert.That(Gap(SetPieceMarking.ManToMan), Is.LessThan(Gap(SetPieceMarking.Zonal) * 0.6), "pegados a su marca");
        }

        [Test]
        public void ShortCorner_IsPlayedShort()
        {
            MatchEngine e = CornerSetUp(CornerRoutine.Short, SetPieceMarking.Zonal, out _);
            Vec2 cornerSpot = e.CurrentRestart.Position;
            int guard = 0;
            while (e.Phase == MatchPhase.SetPiece && guard++ < 2000)
            {
                e.Tick();
            }

            for (int i = 0; i < 90 && e.Owner == null; i++)
            {
                e.Tick();
            }

            Assert.That(e.Owner, Is.Not.Null);
            Assert.That(e.Owner.Team, Is.EqualTo(0));
            Assert.That(Vec2.Distance(e.Owner.Pos, cornerSpot), Is.LessThan(20f), "la recibe un compañero cerca del banderín (un centro al área viaja más de 30 m)");
        }
    }
}
