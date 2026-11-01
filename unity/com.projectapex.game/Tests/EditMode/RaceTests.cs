using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.AI;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using Apex.Weather;
using NUnit.Framework;

namespace Apex.Tests
{
    public class RaceTests
    {
        const float Dt = 1f / 120f;

        static RaceDirector NewDirector(SessionType type, int laps, float length = 1000f)
        {
            var rules = new RaceRules { type = type, laps = laps, sessionSeconds = 60f };
            return new RaceDirector(rules, length, new[] { 0f, length / 3f, 2f * length / 3f }, 7);
        }

        [Test]
        public void StartLightsGoOutWithinRandomWindow()
        {
            var d = NewDirector(SessionType.Race, 3);
            d.AddEntry(0, "A", "AAA", "T", false, 0);
            int maxLights = 0;
            double outTime = -1;
            d.LightsChanged += n => maxLights = Math.Max(maxLights, n);
            d.LightsOut += () => outTime = d.Now;
            d.Start(0);
            for (double t = 0; t < 10 && outTime < 0; t += Dt) d.Tick(t);
            Assert.That(maxLights, Is.EqualTo(5));
            Assert.That(outTime, Is.InRange(5.2, 8.0), "5 s of lights + 0.2-3 s hold");
            Assert.That(d.CurrentPhase, Is.EqualTo(RaceDirector.Phase.Racing));
        }

        [Test]
        public void FalseStartIsPenalised()
        {
            var d = NewDirector(SessionType.Race, 3);
            var e = d.AddEntry(0, "A", "AAA", "T", false, 0);
            string reason = null;
            d.PenaltyIssued += (en, s, r) => reason = r;
            d.Start(0);
            d.UpdateEntry(e, 990f, 0.5);
            d.Tick(0.5);
            d.UpdateEntry(e, 992f, 1.5); // rolled 2 m before lights out
            d.Tick(1.5);
            Assert.That(e.falseStart, Is.True);
            Assert.That(e.penalties, Is.EqualTo(5.0));
            Assert.That(reason, Is.EqualTo("Frühstart"));
        }

        /// <summary>Simulated cars at constant speeds from a grid behind the line.</summary>
        static void RunConstantSpeeds(RaceDirector d, float[] speeds, double maxTime)
        {
            var dist = new double[speeds.Length];
            for (int i = 0; i < speeds.Length; i++) dist[i] = -10 - 8 * i;
            d.Start(0);
            double t = 0;
            while (t < maxTime && d.CurrentPhase != RaceDirector.Phase.Finished)
            {
                t += Dt;
                for (int i = 0; i < speeds.Length; i++)
                {
                    if (d.CurrentPhase == RaceDirector.Phase.Racing) dist[i] += speeds[i] * Dt;
                    float lap = (float)(((dist[i] % d.trackLength) + d.trackLength) % d.trackLength);
                    d.UpdateEntry(d.entries[i], lap, t);
                }
                d.Tick(t);
            }
        }

        [Test]
        public void RaceOrderGapsFinishAndPoints()
        {
            var d = NewDirector(SessionType.Race, 3);
            for (int i = 0; i < 3; i++) d.AddEntry(i, "Car" + i, "C" + i, "T", i == 0, i);
            // Car 2 is fastest and overtakes; car 0 slowest.
            RunConstantSpeeds(d, new[] { 48f, 50f, 55f }, 200);
            Assert.That(d.CurrentPhase, Is.EqualTo(RaceDirector.Phase.Finished));
            var s = d.Standings;
            Assert.That(s[0].id, Is.EqualTo(2));
            Assert.That(s[1].id, Is.EqualTo(1));
            Assert.That(s[2].id, Is.EqualTo(0));
            foreach (var e in s) Assert.That(e.finished, Is.True);
            double gap = d.GapToLeader(s[1]);
            Assert.That(gap, Is.GreaterThan(0.0));
            Assert.That(d.PointsFor(s[0]), Is.EqualTo(25 + 1), "winner + fastest lap");
            Assert.That(d.PointsFor(s[1]), Is.EqualTo(18));
        }

        [Test]
        public void LiveGapMatchesTimeDifference()
        {
            var d = NewDirector(SessionType.Race, 10);
            d.AddEntry(0, "A", "A", "T", false, 0);
            d.AddEntry(1, "B", "B", "T", false, 1);
            // Same speed, B starts 8 m behind: gap = 8 m / 50 m/s = 0.16 s.
            RunConstantSpeeds(d, new[] { 50f, 50f }, 30);
            Assert.That(d.GapToLeader(d.Standings[1]), Is.EqualTo(0.16).Within(0.03));
        }

        [Test]
        public void QualifyingOrdersByBestLap()
        {
            var d = NewDirector(SessionType.Qualifying, 0);
            d.AddEntry(0, "A", "A", "T", false, 0);
            d.AddEntry(1, "B", "B", "T", false, 1);
            RunConstantSpeeds(d, new[] { 40f, 45f }, 120);
            Assert.That(d.Standings[0].id, Is.EqualTo(1));
            Assert.That(d.CurrentPhase, Is.EqualTo(RaceDirector.Phase.Finished), "session ends after time + last lap");
        }

        [Test]
        public void TrackLimitWarningsThenPenalty()
        {
            var d = NewDirector(SessionType.Race, 3);
            var e = d.AddEntry(0, "A", "A", "T", false, 0);
            d.Start(0);
            for (double t = 0; t < 9; t += Dt) d.Tick(t);
            for (int i = 0; i < 3; i++) d.TrackLimitViolation(e);
            Assert.That(e.penalties, Is.EqualTo(0));
            d.TrackLimitViolation(e);
            Assert.That(e.penalties, Is.EqualTo(5));
        }

        [Test]
        public void RacingLineCutsCornersInsideTrack()
        {
            var g = TestData.Geometry("bosphorus_gp");
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.5f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            for (int i = 0; i < g.Count; i++)
                Assert.That(Math.Abs(line.offsets[i]), Is.LessThanOrEqualTo(g.Widths[i] * 0.5f), $"inside track at {i}");
            Assert.That(line.LineLength(), Is.LessThan(g.Length - 20f), "line is shorter than the centreline");
            float maxLineK = 0f, maxCentreK = 0f, sumLine = 0f, sumCentre = 0f;
            for (int i = 0; i < g.Count; i++)
            {
                maxLineK = Math.Max(maxLineK, Math.Abs(line.curvature[i]));
                maxCentreK = Math.Max(maxCentreK, Math.Abs(g.Curvatures[i]));
                sumLine += line.curvature[i] * line.curvature[i];
                sumCentre += g.Curvatures[i] * g.Curvatures[i];
            }
            TestContext.WriteLine($"max k line {maxLineK:F4} centre {maxCentreK:F4}; sum k² line {sumLine:F3} centre {sumCentre:F3}");
            Assert.That(sumLine, Is.LessThan(sumCentre * 0.8f), "overall curvature (and thus speed limits) clearly reduced");
            Assert.That(maxLineK, Is.LessThan(maxCentreK * 1.05f), "no corner gets tighter than the centreline");
        }

        [Test]
        public void TireHeatsWithSlipAndCoolsWhenIdle()
        {
            var t = new TireState();
            t.Fit(TireCompound.Medium, 60f);
            float cold = t.TemperatureFactor();
            for (int i = 0; i < 120 * 30; i++) t.Update(Dt, 20000f, 3500f, 50f, 34f, 0f);
            Assert.That(t.temperature, Is.GreaterThan(85f));
            Assert.That(t.TemperatureFactor(), Is.GreaterThan(cold));
            float hot = t.temperature;
            for (int i = 0; i < 120 * 60; i++) t.Update(Dt, 0f, 0f, 0f, 34f, 0f);
            Assert.That(t.temperature, Is.LessThan(hot - 20f));
        }

        [Test]
        public void CompoundsTradeGripForWearAndWetTiresWinInRain()
        {
            var soft = CompoundSpec.For(TireCompound.Soft);
            var hard = CompoundSpec.For(TireCompound.Hard);
            Assert.That(soft.muScale, Is.GreaterThan(hard.muScale));
            Assert.That(soft.wearRate, Is.GreaterThan(hard.wearRate));
            Assert.That(CompoundSpec.WetFactor(TireCompound.Medium, 0f), Is.GreaterThan(CompoundSpec.WetFactor(TireCompound.Intermediate, 0f)));
            Assert.That(CompoundSpec.WetFactor(TireCompound.Intermediate, 0.5f), Is.GreaterThan(CompoundSpec.WetFactor(TireCompound.Medium, 0.5f)));
            Assert.That(CompoundSpec.WetFactor(TireCompound.Wet, 1f), Is.GreaterThan(CompoundSpec.WetFactor(TireCompound.Intermediate, 1f)));
            var t = new TireState { wear = 0.95f };
            Assert.That(t.WearFactor(), Is.LessThan(0.85f), "worn tires lose grip");
        }

        [Test]
        public void RainWetsTrackAndItDriesAfterwards()
        {
            var w = new WeatherModel(WeatherState.HeavyRain, false, 3, 0f);
            for (int i = 0; i < 180; i++) w.Update(i, 1f);
            Assert.That(w.TrackWetness, Is.GreaterThan(0.6f));
            var dry = new WeatherModel(WeatherState.Clear, false, 3, 0.8f);
            for (int i = 0; i < 900; i++) dry.Update(i, 1f);
            Assert.That(dry.TrackWetness, Is.LessThan(0.2f));
        }

        [Test]
        public void DynamicWeatherIsReproducibleAndHasForecast()
        {
            var a = new WeatherModel(WeatherState.Clear, true, 42);
            var b = new WeatherModel(WeatherState.Clear, true, 42);
            Assert.That(a.Forecast.Count, Is.GreaterThanOrEqualTo(3));
            for (int i = 0; i < 3600; i++) { a.Update(i, 1f); b.Update(i, 1f); }
            Assert.That(a.State, Is.EqualTo(b.State));
            Assert.That(a.TrackWetness, Is.EqualTo(b.TrackWetness));
        }

        [Test]
        public void PitServiceTimeIsRealistic()
        {
            var crew = new PitCrew(5) { mistakeChance = 0f };
            for (int i = 0; i < 50; i++)
                Assert.That(crew.ServiceTime(true).time, Is.InRange(2.5f, 3.5f));
        }

        [Test]
        [TestCase("test_loop")]
        [TestCase("bosphorus_gp")]
        [TestCase("bosphorus_a")]
        public void AIFieldRacesCleanlyOnRig(string trackId)
        {
            // Six AI cars on independent flat rigs (no car-to-car contact physics here):
            // they must all complete 3 laps inside the track and keep a plausible order.
            var def = TestData.Circuit(trackId);
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var perf = CarPerformance.For(setup);   // as RaceController: the AI plans with the car's real grip
            var line = perf.Line(g);
            var rules = new RaceRules { type = SessionType.Race, laps = 3 };
            var sectors = Array.ConvertAll(def.sectorFractions, f => f * g.Length);
            var dir = new RaceDirector(rules, g.Length, sectors, 3);
            const int n = 6;
            var rigs = new FlatGroundRig[n];
            var ais = new AIDriver[n];
            var states = new List<CarState>();
            var hints = new int[n];
            for (int i = 0; i < n; i++)
            {
                var sim = new VehicleSimulation(new VehicleSetup()) { assistSteering = true };
                rigs[i] = new FlatGroundRig(sim);
                float back = 12f + i * 8f;
                g.FrameAt(g.Wrap(def.startLineDistance - back), out var p, out var f, out _, i % 2 == 0 ? -3f : 3f);
                rigs[i].Place(p, f);
                ais[i] = new AIDriver(line, AIPersonality.Make(0.86f + 0.015f * (n - i), 0.6f, 0.8f, 0.5f, 0.5f), 100 + i);
                dir.AddEntry(i, "AI" + i, "A" + i, "T", false, i);
                hints[i] = -1;
            }
            dir.Start(0);
            double t = 0;
            float maxBeyond = 0f;
            var history = new Queue<string>[n];
            for (int i = 0; i < n; i++) history[i] = new Queue<string>();
            while (dir.CurrentPhase != RaceDirector.Phase.Finished && t < 600)
            {
                t += Dt;
                states.Clear();
                for (int i = 0; i < n; i++)
                {
                    var pr = g.Project(rigs[i].Position, hints[i]);
                    hints[i] = pr.index;
                    states.Add(new CarState { id = i, distance = pr.distance, lateral = pr.lateral, speed = rigs[i].u, position = rigs[i].Position, forward = rigs[i].Forward });
                    if (dir.CurrentPhase == RaceDirector.Phase.Racing)
                    {
                        float b = Math.Abs(pr.lateral) - g.Widths[pr.index] * 0.5f;
                        if ((int)(t * 120) % 12 == 0)
                        {
                            history[i].Enqueue($"  t={t:F1} d={pr.distance:F0} lat={pr.lateral:F1} tgt={ais[i].TargetLateral:F1} v={rigs[i].u * 3.6f:F0} steer={rigs[i].sim.steer:F2} saF={rigs[i].sim.wheels[0].slipAngle * 57.3f:F1} saR={rigs[i].sim.wheels[2].slipAngle * 57.3f:F1} beta={MathF.Atan2(rigs[i].v, rigs[i].u) * 57.3f:F0} {ais[i].Intent}");
                            if (history[i].Count > 25) history[i].Dequeue();
                        }
                        if (b > maxBeyond && b > 3f && maxBeyond <= 3f)
                        {
                            TestContext.WriteLine($"first excursion: car {i} t={t:F1} race t={t - dir.RaceStartTime:F1} d={pr.distance:F0} lat={pr.lateral:F1} v={rigs[i].u * 3.6f:F0} intent={ais[i].Intent}");
                            foreach (var h in history[i]) TestContext.WriteLine(h);
                        }
                        maxBeyond = Math.Max(maxBeyond, b);
                    }
                }
                // Slipstream and dirty air between the cars (Slipstream), as RaceController.ApplyWakes does.
                var wp = new System.Numerics.Vector3[n]; var wf = new System.Numerics.Vector3[n]; var ws = new float[n];
                for (int i = 0; i < n; i++) { wp[i] = rigs[i].Position; wf[i] = rigs[i].Forward; ws[i] = rigs[i].u; }
                for (int i = 0; i < n; i++) rigs[i].sim.wake = Slipstream.For(i, wp, wf, ws);
                for (int i = 0; i < n; i++)
                {
                    var sim = rigs[i].sim;
                    if (dir.CarsMayMove)
                    {
                        sim.holdBrake = false;
                        float grip = 0f;
                        foreach (var tr in sim.tires) grip += tr.GripMultiplier(0f) / 4f;
                        ais[i].gripFactor = grip;
                        ais[i].maxSteerRad = sim.CurrentMaxLock();
                        ais[i].frontSlipUsage = Math.Max(Math.Abs(sim.wheels[0].slipAngle), Math.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
                        var c = ais[i].Drive(states[i], states, Dt);
                        sim.throttle = c.throttle; sim.brake = c.brake; sim.steer = c.steer;
                    }
                    sim.holdBrake = !dir.CarsMayMove;
                    rigs[i].Step(Dt);
                    dir.UpdateEntry(dir.entries[i], g.Wrap(states[i].distance - def.startLineDistance), t);
                }
                dir.Tick(t);
            }
            Assert.That(dir.CurrentPhase, Is.EqualTo(RaceDirector.Phase.Finished), "race finished");
            foreach (var e in dir.entries) Assert.That(e.LapsCompleted, Is.GreaterThanOrEqualTo(2));
            TestContext.WriteLine($"winner {dir.Standings[0].name} in {dir.Standings[0].finishTime:F1} s, max beyond edge {maxBeyond:F2} m");
            Assert.That(maxBeyond, Is.LessThan(3f), "nobody goes further than the kerb/run-off edge");
        }
    }
}
