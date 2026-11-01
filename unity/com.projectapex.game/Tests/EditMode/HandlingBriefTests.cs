using System;
using Apex.InputLogic;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>
    /// Tests A–D of the handling brief 2026-11 (docs/HANDLING_KEYBOARD.md), driven through the real keyboard chain
    /// (KeyboardSteering ramp and curve → front-slip limiter → 6-DOF pad), profile Ausgewogen, keyboard Ausgewogen.
    /// Numbers in the comments: measured 2026-11-01 (before → after the overhaul).
    /// </summary>
    public class HandlingBriefTests
    {
        static KeyboardHandlingProbe Probe(int assist = 1, KeyboardSteering.Profile keys = KeyboardSteering.Profile.Balanced) =>
            new KeyboardHandlingProbe(null, assist, keys);

        [Test]
        public void A_SlowHairpinTurnsInWithoutPushing()
        {
            // Key held at 60 km/h: front tyres at their peak instead of 2.1 × past it; 1.47 → ~1.85 g, radius 19 → 15 m.
            var r = Probe().Hold(60f);
            TestContext.WriteLine(KeyboardHandlingProbe.Format("hold", r));
            Assert.That(r.spun, Is.False);
            Assert.That(r.frontSlipEnd, Is.InRange(0.85f, 1.1f), "front at its grip peak, not scrubbing");
            Assert.That(r.lateralG, Is.GreaterThan(1.75f));
            Assert.That(r.radius, Is.LessThan(17f));
            Assert.That(r.yawOvershootPct, Is.LessThan(10f), "turns in without a lurch");
        }

        [Test]
        [TestCase(200f)]
        [TestCase(300f)]
        public void B_FastCornerKeepsTheRearCalm(float kmh)
        {
            var p = Probe();
            var hold = p.Hold(kmh);
            var tap = p.Tap(kmh);
            TestContext.WriteLine(KeyboardHandlingProbe.Format("hold", hold));
            TestContext.WriteLine(KeyboardHandlingProbe.Format("tap", tap));
            Assert.That(hold.spun, Is.False);
            Assert.That(hold.rearSlipMax, Is.LessThan(1.0f), "the rear stays below its peak with the key held");
            Assert.That(hold.frontSlipMax, Is.LessThan(1.15f), "no overdriven front (was 1.3-1.9)");
            Assert.That(tap.maxBetaDeg, Is.LessThan(3f), "a small key tap is a small correction");
            // Lift-off in the corner: no snap.
            var lift = p.LiftOff(kmh);
            Assert.That(lift.spun, Is.False);
            Assert.That(lift.rearSlipMax, Is.LessThan(1.0f));
        }

        [Test]
        public void B_TheLimitIsStillThereForGrossErrors()
        {
            // Without any assist and with the previous keyboard ramp, full lock at 250 km/h still overloads the tyres:
            // grip is generous, not glued.
            var r = new KeyboardHandlingProbe(null, 2, KeyboardSteering.Profile.Classic).Hold(250f);
            Assert.That(r.frontSlipMax, Is.GreaterThan(1.1f), "past the peak when the driver overdoes it");
        }

        [Test]
        public void C_KeyboardRampIsSmoothDirectAndReturnsToCentre()
        {
            foreach (KeyboardSteering.Profile prof in new[] { KeyboardSteering.Profile.Direct, KeyboardSteering.Profile.Balanced, KeyboardSteering.Profile.Smooth })
            {
                var k = new KeyboardSteering { profile = prof };
                const float dt = 1f / 120f;
                // Short tap (60 ms): a small correction.
                float tap = 0f;
                for (int i = 0; i < 7; i++) tap = k.Update(1f, 30f, dt);
                Assert.That(tap, Is.InRange(0.08f, 0.6f), $"{prof}: tap");
                // Holding: full input within 0.35 s at 60 km/h and within 0.6 s at 300 km/h (smooth, not slow).
                k.Reset();
                float t = 0f;
                while (k.Update(1f, 60f / 3.6f, dt) < 0.999f) t += dt;
                Assert.That(t, Is.LessThan(0.35f), $"{prof}: full input at 60 km/h");
                k.Reset();
                float t300 = 0f;
                while (k.Update(1f, 300f / 3.6f, dt) < 0.999f) t300 += dt;
                Assert.That(t300, Is.LessThan(0.6f).And.GreaterThan(t), $"{prof}: more progressive at speed");
                // Release: back to the centre within 0.25 s.
                float r = 0f;
                while (k.Update(0f, 120f / 3.6f, dt) > 0.001f) r += dt;
                Assert.That(r, Is.LessThan(0.25f), $"{prof}: return to centre");
                // Reversal left → right: crosses the centre, no step.
                for (int i = 0; i < 60; i++) k.Update(-1f, 120f / 3.6f, dt);
                float prev = k.Value, maxStep = 0f;
                for (int i = 0; i < 60; i++) { float v = k.Update(1f, 120f / 3.6f, dt); maxStep = Math.Max(maxStep, Math.Abs(v - prev)); prev = v; }
                Assert.That(prev, Is.GreaterThan(0.9f), $"{prof}: reaches the other side");
                Assert.That(maxStep, Is.LessThan(0.13f), $"{prof}: no jump through the centre");
            }
            // Smoothing setting adds a filter, sensitivity speeds the ramp up.
            var a = new KeyboardSteering { sensitivity = 1.5f };
            var b = new KeyboardSteering { sensitivity = 0.5f };
            Assert.That(a.Update(1f, 20f, 0.05f), Is.GreaterThan(b.Update(1f, 20f, 0.05f)));
            var sm = new KeyboardSteering { smoothing = 1f };
            var raw = new KeyboardSteering();
            Assert.That(sm.Update(1f, 20f, 1f / 120f), Is.LessThan(raw.Update(1f, 20f, 1f / 120f)));
        }

        [Test]
        [TestCase(120f)]
        [TestCase(200f)]
        [TestCase(300f)]
        public void C_QuickLeftRightChangesDoNotSwingTheCarRound(float kmh)
        {
            var r = Probe().Slalom(kmh);
            TestContext.WriteLine(KeyboardHandlingProbe.Format("slalom", r));
            Assert.That(r.spun, Is.False);
            Assert.That(r.rearSlipMax, Is.LessThan(1.05f), "was 0.94-1.03 before, up to 4.9 (spin) with the limiter alone");
            Assert.That(r.maxBetaDeg, Is.LessThan(6f));
        }

        [Test]
        [TestCase(150f)]
        [TestCase(250f)]
        [TestCase(300f)]
        public void D_BrakingWhileTurningInStaysStable(float kmh)
        {
            // Full brake and the key held into a corner – the classic entry oversteer case (rear 1.16-1.18 before).
            var r = Probe().TrailBrake(kmh);
            TestContext.WriteLine(KeyboardHandlingProbe.Format("trailbrake", r));
            Assert.That(r.spun, Is.False);
            Assert.That(r.rearSlipMax, Is.LessThan(1.15f));
            Assert.That(r.maxBetaDeg, Is.LessThan(6.5f));
        }

        [Test]
        public void D_FullBrakingWithAndWithoutAbsStaysStraight()
        {
            foreach (bool abs in new[] { true, false })
            {
                var hb = new HandlingBenchmark(null, 1) { configure = s => s.assistAbs = abs };
                var b = hb.Brake(300f, 80f);
                TestContext.WriteLine($"ABS {(abs ? "on" : "off")}: 300->80 km/h in {b.distance:F0} m, mean {b.meanG:F2} g");
                Assert.That(b.distance, Is.LessThan(abs ? 95f : 130f));
                var t = new KeyboardHandlingProbe(null, 1).Run(300f, 2.5f, _ => 0f, (sim, _) => { sim.assistAbs = abs; sim.throttle = 0f; sim.brake = 1f; });
                Assert.That(t.maxBetaDeg, Is.LessThan(2f), $"ABS {(abs ? "on" : "off")}: straight under full braking");
            }
        }
    }
}
