using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    public class LapTimerTests
    {
        const float Length = 1000f;
        double dist;
        double time;

        void Start(LapTimer t, double d)
        {
            dist = d;
            time = 0;
            t.Update((float)ApexMath.PosMod((float)d, Length), 0);
        }

        void Drive(LapTimer t, double toDist, double speed, double dt = 1.0 / 120.0)
        {
            while (dist < toDist)
            {
                dist += speed * dt;
                time += dt;
                t.Update((float)(dist % Length), time);
            }
        }

        [Test]
        public void LapTimeIsInterpolatedBelowOneTick()
        {
            var t = new LapTimer(Length, new[] { 0f, 300f, 700f });
            var laps = new List<double>();
            t.LapCompleted += (n, lt, v, pb) => laps.Add(lt);
            Start(t, Length - 10f);
            Drive(t, 2 * Length + 5, 50);
            Assert.That(laps.Count, Is.EqualTo(1));
            Assert.That(laps[0], Is.EqualTo(20.0).Within(1e-4));
            Assert.That(t.completedLaps[0].sectors[0], Is.EqualTo(6.0).Within(1e-4));
        }

        [Test]
        public void ShortcutDoesNotCount()
        {
            var t = new LapTimer(Length, new[] { 0f, 300f, 700f });
            int completed = 0;
            t.LapCompleted += (n, lt, v, pb) => completed++;
            Start(t, Length - 5f);
            Drive(t, Length + 100, 50);
            // Drive backwards in small steps to just before the line (a shortcut).
            for (int k = 0; k < 51; k++)
            {
                time += 0.1;
                t.Update(ApexMath.PosMod(Length + 100f - k * 2f - 2f, Length), time);
            }
            dist = Length - 4f;
            Drive(t, Length + 10, 50);
            Assert.That(completed, Is.EqualTo(0));
        }

        [Test]
        public void ReversingOverLineStartsNothing()
        {
            var t = new LapTimer(Length);
            int started = 0;
            t.LapStarted += n => started++;
            t.Update(5f, 0);
            t.Update(Length - 5f, 0.5);
            t.Update(Length - 10f, 1.0);
            Assert.That(started, Is.EqualTo(0));
        }

        [Test]
        public void InvalidLapIsNotBest()
        {
            var t = new LapTimer(Length);
            Start(t, Length - 1f);
            Drive(t, Length + 500, 50);
            t.Invalidate("track limits");
            Drive(t, 2 * Length + 1, 50);
            Assert.That(t.LastLapTime, Is.GreaterThan(0));
            Assert.That(t.LastLapValid, Is.False);
            Assert.That(t.BestLapTime, Is.EqualTo(0));
            Drive(t, 3 * Length + 1, 50);
            Assert.That(t.BestLapTime, Is.EqualTo(20.0).Within(1e-3));
        }

        [Test]
        public void LiveDeltaAgainstBest()
        {
            var t = new LapTimer(Length);
            Start(t, Length - 1f);
            Drive(t, 2 * Length + 1, 50);
            Assert.That(double.IsNaN(t.LiveDelta(500f)), Is.False);
            Drive(t, 2 * Length + 500, 55.555);
            Assert.That(t.LiveDelta(500f), Is.EqualTo(-1.0).Within(0.05));
        }

        [Test]
        public void TeleportResetsLap()
        {
            var t = new LapTimer(Length);
            Start(t, Length - 1f);
            Drive(t, Length + 200, 50);
            Assert.That(t.LapActive, Is.True);
            t.Update(800f, time + 0.01);
            Assert.That(t.LapActive, Is.False);
        }
    }
}
