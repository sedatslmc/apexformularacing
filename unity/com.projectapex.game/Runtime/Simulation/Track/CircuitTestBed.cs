using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.AI;
using Apex.Core;
using Apex.Environment;
using Apex.Race;
using Apex.Vehicle;

namespace Apex.Tracks
{
    /// <summary>
    /// The circuit as the game builds it, without the engine: geometry, racing line, infrastructure
    /// plan (kerbs), the canonical <see cref="RoadSurface"/> incl. terrain, and its
    /// <see cref="CollisionWorld"/>. Drives the real vehicle model with the AI driver on the real
    /// surface (<see cref="SurfaceRig"/>) and records everything that points to a road defect.
    /// </summary>
    public sealed class CircuitTestBed
    {
        public const float Dt = 1f / 120f;

        public readonly CircuitDefinition def;
        public readonly TrackGeometry g;
        public readonly RacingLine line;
        public readonly TrackInfrastructurePlan plan;
        public readonly RoadSurface surface;
        public readonly TerrainField terrain;
        public readonly CollisionWorld world;

        public CircuitTestBed(CircuitDefinition definition, bool withTerrain = true)
        {
            def = definition;
            g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            surface = new RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            if (withTerrain)
            {
                terrain = new TerrainField(g, def);
                surface.BuildTerrain(terrain);
            }
            world = new CollisionWorld(surface.All);
        }

        public enum EventKind { Grounding, Airborne, VerticalSpike, OffRoadSurface, Stuck, LeftTrack, Spin }

        public struct DriveEvent
        {
            public EventKind kind;
            public float time, distance, lateral, speed;
            public Vector3 position;
            /// <summary>Penetration (m), airborne time (s), vertical acceleration (m/s²) … depending on the kind.</summary>
            public float value;
            public string part;
            public override string ToString() =>
                $"{kind} at {distance:F0} m (lat {lateral:F1}) ({position.X:F0}, {position.Y:F1}, {position.Z:F0}) {speed * 3.6f:F0} km/h value {value:F3} {part}";
        }

        public sealed class DriveResult
        {
            public readonly List<DriveEvent> events = new List<DriveEvent>();
            public readonly List<double> lapTimes = new List<double>();
            public float distanceDriven, maxSpeed, time;
            public float minClearance = float.PositiveInfinity;
            public float maxCompression;
            public int Count(EventKind k) { int n = 0; foreach (var e in events) if (e.kind == k) n++; return n; }
            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"driven {distanceDriven:F0} m in {time:F1} s, laps {lapTimes.Count}, vmax {maxSpeed * 3.6f:F0} km/h, min clearance {minClearance * 1000f:F0} mm, max compression {maxCompression * 1000f:F0} mm");
                foreach (EventKind k in Enum.GetValues(typeof(EventKind))) sb.Append($", {k} {Count(k)}");
                return sb.ToString();
            }
        }

        /// <summary>Ground point on the physical surface at a raw distance and lateral offset.</summary>
        public Vector3 GroundAt(float rawDistance, float lateral)
        {
            g.FrameAt(rawDistance, out var p, out _, out _, lateral);
            if (world.SurfaceBelow(p.X, p.Z, p.Y + 2f, p.Y - 2f, out var hit)) return hit.point;
            return p;
        }

        /// <summary>Grid slot (0 = pole) as RaceController places it: staggered two-wide, 8 m apart.</summary>
        public (float distance, float lateral) GridSlot(int slot) =>
            (g.Wrap(def.startLineDistance - (12f + slot * 8f)), slot % 2 == 0 ? -3f : 3f);

        /// <summary>Vehicle setup for the drives (default: the game's setup).</summary>
        public Func<VehicleSetup> setupFactory = () => new VehicleSetup();

        public SurfaceRig NewRig(out VehicleSimulation sim)
        {
            sim = new VehicleSimulation(setupFactory());
            return new SurfaceRig(sim, world);
        }

        /// <summary>
        /// Drives with the AI from a pose (as the game spawns: 5 cm above the road) for the given
        /// distance or until `maxTime`. `throttleOnly` = straight-line full throttle with the AI
        /// steering (acceleration tests). Grounding is a body-collider touch with an impulse of more
        /// than `groundingImpulse` N·s in a step.
        /// </summary>
        /// <summary>Optional per-step trace (raw distance, rig) for diagnostics.</summary>
        public Action<float, SurfaceRig> trace;

        public DriveResult Drive(float rawDistance, float lateral, float distance, float maxTime, float initialSpeed = 0f,
            bool throttleOnly = false, float pace = 0.95f, int laps = 0, float groundingImpulse = 5f,
            Func<float, float> lateralAt = null, float speedLimit = float.PositiveInfinity, int seed = 3,
            Action<VehicleSimulation> configure = null, bool digitalInput = false)
        {
            var rig = NewRig(out var sim);
            g.FrameAt(rawDistance, out _, out var f, out _, lateral);
            rig.Place(GroundAt(rawDistance, lateral), f, 0.05f);
            if (initialSpeed > 0f) rig.SetSpeed(initialSpeed, Math.Max(1, sim.gearbox.gear));
            var ai = new AIDriver(line, AIPersonality.Make(pace, 0f, 1f, 0.5f, 0.5f), seed) { forcedLateral = throttleOnly ? lateral : float.NaN };
            sim.speedLimit = speedLimit;
            configure?.Invoke(sim);
            // Keyboard/touch driver: the AI decides, but the car only gets digital keys through the game's ramps.
            var steerKey = new InputLogic.DigitalAxis();
            var thrKey = new InputLogic.DigitalAxis(InputLogic.InputTuning.ThrottleRate, InputLogic.InputTuning.ThrottleRate, InputLogic.InputTuning.ThrottleRate);
            var brkKey = new InputLogic.DigitalAxis(InputLogic.InputTuning.BrakeRate, InputLogic.InputTuning.BrakeRate, InputLogic.InputTuning.BrakeRate);
            var states = new List<CarState>();
            var timer = new LapTimer(g.Length, Array.ConvertAll(def.sectorFractions, x => x * g.Length));
            var res = new DriveResult();
            int hint = -1;
            float airborne = 0f, stuck = 0f, prevVy = 0f;
            float lastRaw = rawDistance;
            bool grounded = false, offRoad = false, spinning = false;
            for (int step = 0; step < (int)(maxTime / Dt); step++)
            {
                var pr = g.Project(rig.position, hint);
                hint = pr.index;
                float moved = ApexMath.PosMod(pr.distance - lastRaw + g.Length * 0.5f, g.Length) - g.Length * 0.5f;
                lastRaw = pr.distance;
                res.distanceDriven += moved;
                if (laps == 0 && res.distanceDriven >= distance) break;
                if (laps > 0 && res.lapTimes.Count >= laps) break;
                var st = new CarState { id = 0, distance = pr.distance, lateral = pr.lateral, speed = rig.ForwardSpeed, position = rig.position, forward = rig.Forward };
                states.Clear();
                states.Add(st);
                if (lateralAt != null) ai.forcedLateral = lateralAt(pr.distance);
                // As RaceController: the driver knows the conditions (grip of the tyres, wet track).
                float grip = 0f;
                foreach (var t in sim.tires) grip += t.GripMultiplier(sim.trackWetness) / 4f;
                ai.gripFactor = grip;
                ai.wet = sim.trackWetness > 0.25f;
                ai.maxSteerRad = sim.CurrentMaxLock();
                ai.frontSlipUsage = MathF.Max(MathF.Abs(sim.wheels[0].slipAngle), MathF.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
                var c = ai.Drive(st, states, Dt);
                if (digitalInput)
                {
                    float k = ApexMath.Clamp01(rig.ForwardSpeed / 60f);
                    steerKey.pressRate = InputLogic.InputTuning.SteerPressRate(k, sim.setup.legacy);
                    steerKey.releaseRate = InputLogic.InputTuning.SteerReleaseRate(k, sim.setup.legacy);
                    steerKey.reverseRate = InputLogic.InputTuning.SteerReverseRate;
                    float key = 0f;
                    if (MathF.Abs(c.steer) > 0.05f && (c.steer - steerKey.Value) * MathF.Sign(c.steer) > 0.03f) key = MathF.Sign(c.steer);
                    sim.steer = steerKey.Update(key, Dt);
                    sim.throttle = thrKey.Update(c.throttle > 0.5f ? 1f : 0f, Dt);
                    sim.brake = brkKey.Update(c.brake > 0.25f ? 1f : 0f, Dt);
                }
                else
                {
                    sim.throttle = throttleOnly ? 1f : c.throttle;
                    sim.brake = throttleOnly ? 0f : c.brake;
                    sim.steer = c.steer;
                }
                rig.Step(Dt);
                trace?.Invoke(pr.distance, rig);
                if (laps > 0) timer.Update(g.Wrap(pr.distance - def.startLineDistance), rig.time);
                if (laps > 0 && timer.completedLaps.Count > res.lapTimes.Count) res.lapTimes.Add(timer.completedLaps[timer.completedLaps.Count - 1].time);
                float v = rig.velocity.Length();
                res.maxSpeed = MathF.Max(res.maxSpeed, v);
                res.minClearance = MathF.Min(res.minClearance, rig.MinClearance);
                res.maxCompression = MathF.Max(res.maxCompression, rig.MaxCompression);
                DriveEvent Ev(EventKind k, float value, string part = null) => new DriveEvent
                {
                    kind = k, time = rig.time, distance = pr.distance, lateral = pr.lateral, speed = v, position = rig.position, value = value, part = part,
                };
                // Body collider on the surface with a real impulse (one event per touchdown).
                bool g0 = rig.BodyContacts > 0 && rig.BodyImpulse > groundingImpulse;
                if (g0 && !grounded) res.events.Add(Ev(EventKind.Grounding, rig.MaxPenetration, rig.ClearancePart));
                grounded = g0;
                // All four wheels off the ground for more than 50 ms.
                // (The first 0.5 s are the spawn drop from 5 cm, as in the game.)
                if (rig.WheelContacts == 0 && rig.time > 0.5f) airborne += Dt;
                else
                {
                    if (airborne > 0.05f) res.events.Add(Ev(EventKind.Airborne, airborne));
                    airborne = 0f;
                }
                // Vertical acceleration of the body beyond ±2.5 g (no smooth road produces that).
                float vy = rig.velocity.Y;
                float ay = (vy - prevVy) / Dt;
                prevVy = vy;
                if (rig.time > 0.5f && MathF.Abs(ay) > 2.5f * SurfaceRig.Gravity) res.events.Add(Ev(EventKind.VerticalSpike, ay));
                // A wheel on terrain or barrier means the road surface is missing or covered there.
                bool off = false; string offPart = null;
                for (int w = 0; w < 4; w++)
                    if (rig.wheelParts[w] == "Terrain" || rig.wheelParts[w] == "Barriers" || rig.wheelParts[w] == "PitWall") { off = true; offPart = rig.wheelParts[w]; }
                if (off && !offRoad) res.events.Add(Ev(EventKind.OffRoadSurface, 0f, offPart));
                offRoad = off;
                // Beyond the barrier = left the circuit.
                int side = pr.lateral < 0f ? -1 : 1;
                float wall = side < 0 ? surface.wallLeft[pr.index] : surface.wallRight[pr.index];
                if (MathF.Abs(pr.lateral) > wall + 0.3f)
                {
                    res.events.Add(Ev(EventKind.LeftTrack, pr.lateral));
                    break;
                }
                // Spin: body slip angle beyond 26° (as the keyboard lap tests count it).
                float beta = MathF.Abs(MathF.Atan2(Vector3.Dot(rig.velocity, rig.Right), MathF.Abs(rig.ForwardSpeed) + 0.1f));
                if (beta > 0.45f && !spinning) { res.events.Add(Ev(EventKind.Spin, beta * ApexMath.Rad2Deg)); spinning = true; }
                if (beta < 0.1f) spinning = false;
                if (v < 1f && rig.time > 3f) stuck += Dt; else stuck = 0f;
                if (stuck > 3f)
                {
                    res.events.Add(Ev(EventKind.Stuck, v));
                    break;
                }
            }
            res.time = rig.time;
            return res;
        }
    }
}
