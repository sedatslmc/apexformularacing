using System;
using System.IO;
using System.Numerics;
using Apex.AI;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;

// Usage: dotnet run -- launch | brake | steer | slide | aero | validate | line | field | lap <track_id> [trace]
const float Dt = 1f / 120f;
string mode = args.Length > 0 ? args[0] : "launch";

FlatGroundRig NewRig(bool tc)
{
    var sim = new VehicleSimulation(new VehicleSetup()) { assistTraction = tc, assistSteering = false, speedSensitiveSteering = false };
    var rig = new FlatGroundRig(sim);
    rig.Place(Vector3.Zero, new Vector3(0, 0, 1));
    return rig;
}

if (mode == "launch")
{
    foreach (bool tc in new[] { true, false })
    {
        var rig = NewRig(tc);
        float t100 = -1;
        for (int i = 0; i < 120 * 8; i++)
        {
            rig.sim.throttle = 1f;
            rig.Step(Dt);
            if (i % 30 == 0 || (Environment.GetEnvironmentVariable("APEX_TRACE") != null && i < 120))
                Console.WriteLine($"tc={tc} t={rig.time:F2} v={rig.u * 3.6f:F0} g={rig.sim.gearbox.gear} rpm={rig.sim.EngineRpm:F0} sr={rig.sim.wheels[2].slipRatio:F2} cut={rig.sim.TcCut:F2} Fz_r={rig.sim.wheels[2].load:F0} Td={rig.sim.wheels[2].driveTorque:F0} tcA={rig.sim.TcActive}");
            if (t100 < 0 && rig.u * 3.6f >= 100) t100 = rig.time;
        }
        Console.WriteLine($"tc={tc}: 0-100 {t100:F2}s");
    }
}
else if (mode == "aero")
{
    // Steady-state ride height, ground-effect gain and downforce over speed.
    foreach (float kmh in new[] { 60f, 100f, 150f, 200f, 250f, 300f })
    {
        var rig = NewRig(true);
        rig.u = kmh / 3.6f;
        foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
        rig.sim.gearbox.gear = 8; rig.sim.SyncEngineToWheels();
        for (int i = 0; i < 240; i++) { rig.u = kmh / 3.6f; rig.sim.throttle = 0.3f; rig.Step(Dt); }
        var s = rig.sim;
        float nominal = 0.5f * s.setup.airDensity * rig.u * rig.u * s.setup.downforceArea;
        Console.WriteLine($"{kmh,4} km/h hF {s.RideHeightFront * 1000:F0} mm hR {s.RideHeightRear * 1000:F0} mm floor x{s.FloorGain:F2} DF {s.DownforceFront + s.DownforceRear:F0} N ({(s.DownforceFront + s.DownforceRear) / nominal:P0} of nominal) bal {s.AeroBalance:P1} drag {s.Drag:F0} N");
    }
}
else if (mode == "validate")
{
    // Handling validation report (Markdown). Flat-ground rig: indicative values, the
    // in-game rigid body adds pitch/roll dynamics and real kerb geometry.
    FlatGroundRig RigFor(string profile, float kmh, int gear, bool wet = false)
    {
        var sim = new VehicleSimulation(new VehicleSetup());
        if (profile == "Profi") { sim.tractionLevel = 1; sim.steeringAssist = 0f; }
        else if (profile == "Alles aus") { sim.tractionLevel = 0; sim.assistAbs = false; sim.steeringAssist = 0f; sim.speedSensitiveSteering = false; }
        else if (profile == "Einsteiger") { sim.tractionLevel = 3; sim.steeringAssist = 1f; }
        else { sim.tractionLevel = 2; sim.steeringAssist = 0.5f; }
        if (wet) { sim.trackWetness = 0.6f; sim.FitTires(TireCompound.Intermediate); }
        var r = new FlatGroundRig(sim);
        r.Place(Vector3.Zero, new Vector3(0, 0, 1));
        if (kmh > 0f)
        {
            r.u = kmh / 3.6f;
            foreach (var w in sim.wheels) w.omega = r.u / w.radius;
            sim.gearbox.gear = gear; sim.SyncEngineToWheels();
        }
        return r;
    }
    int GearFor(float kmh) { int gg = 1; while (gg < 8 && kmh / 3.6f / 0.36f * new Gearbox().RatioFor(gg) * 60f / 6.2832f > 10500f) gg++; return gg; }
    // Driver holding speed with measured part throttle (max 50 %, as at the limit).
    float Hold(FlatGroundRig r, float kmh) => Math.Clamp((kmh / 3.6f - r.u) * 0.3f + 0.1f, 0f, 0.5f);

    Console.WriteLine("## Kurven (Lenkstoss, konstantes Tempo)");
    Console.WriteLine();
    Console.WriteLine("| Szenario | Profil | Radwinkel | t63 Gierrate | Gierrate | Querbeschl. | Schräglauf v/h | max. Schwimmwinkel | Federweg außen v | Ergebnis |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
    foreach (var (name, kmh, wet) in new[] { ("Haarnadel 60 km/h", 60f, false), ("Mittelschnell 140 km/h", 140f, false), ("Schnell 240 km/h", 240f, false), ("Nass 140 km/h (Inter)", 140f, true) })
    {
        // Grip-limit lock = what the full steering assist would allow at this speed.
        var probe = RigFor("Einsteiger", kmh, GearFor(kmh), wet);
        probe.sim.PreStep(Dt, probe.u, probe.u);
        float gripLock = probe.sim.CurrentMaxLock();
        var balancedRig = RigFor("Ausgewogen", kmh, GearFor(kmh), wet);
        balancedRig.sim.PreStep(Dt, balancedRig.u, balancedRig.u);
        float balancedLock = balancedRig.sim.CurrentMaxLock();
        foreach (var (profile, lockRad, label) in new[] { ("Ausgewogen", balancedLock, "Vollanschlag Eingabe"), ("Profi", gripLock, "Vollanschlag Eingabe"), ("Profi", gripLock, "Vollanschlag + Vollgas"), ("Alles aus", balancedLock, "Überlenken + Vollgas"), ("Ausgewogen", balancedLock, "Vollanschlag + Vollgas") })
        {
            var r = RigFor(profile, kmh, GearFor(kmh), wet);
            float steerCmd = Math.Clamp(lockRad / (r.sim.setup.maxSteerAngleDeg * MathF.PI / 180f), 0f, 1f);
            if (profile != "Alles aus") steerCmd = 1f;
            var yaw = new System.Collections.Generic.List<float>();
            float maxBeta = 0f, maxComp = 0f; bool spun = false;
            float staticComp = r.sim.wheels[0].staticCompression;
            for (int i = 0; i < 120 * 3; i++)
            {
                r.sim.steer = steerCmd;
                r.sim.throttle = label.Contains("Vollgas") && i > 60 ? 1f : Hold(r, kmh);
                r.Step(Dt);
                yaw.Add(r.yawRate);
                float beta = MathF.Abs(MathF.Atan2(r.v, r.u));
                maxBeta = Math.Max(maxBeta, beta);
                if (beta > 0.3f) spun = true;
                maxComp = Math.Max(maxComp, r.sim.wheels[0].compression - staticComp);
            }
            float steady = 0f;
            for (int i = yaw.Count - 60; i < yaw.Count; i++) steady += yaw[i] / 60f;
            int t63 = yaw.FindIndex(y => y >= 0.63f * steady);
            float lat = r.u * steady / 9.81f;
            string t63s = spun ? "–" : $"{(t63 + 1) * Dt * 1000f:F0} ms";
            string res = spun ? "Dreher" : MathF.Abs(r.sim.wheels[0].slipAngle) > r.sim.tireFront.slipAnglePeak * 1.15f ? "stabil, schiebt (Untersteuern)" : "stabil";
            Console.WriteLine($"| {name} | {profile}, {label} | {MathF.Abs(r.sim.SteerAngle) * 57.3f:F1}° | {t63s} | {(spun ? "–" : $"{steady * 57.3f:F1} °/s")} | {(spun ? "–" : $"{lat:F2} g")} | {MathF.Abs(r.sim.wheels[0].slipAngle) * 57.3f:F1}° / {MathF.Abs(r.sim.wheels[2].slipAngle) * 57.3f:F1}° | {maxBeta * 57.3f:F1}° | {maxComp * 1000f:F1} mm | {res} |");
        }
    }

    Console.WriteLine();
    Console.WriteLine("## Beschleunigung (Ausgewogen, TC mittel)");
    Console.WriteLine();
    {
        var r = RigFor("Ausgewogen", 0f, 1);
        float t100 = -1, t200 = -1, peakSr = 0f;
        for (int i = 0; i < 120 * 40; i++)
        {
            r.sim.throttle = 1f;
            r.Step(Dt);
            if (t100 < 0 && r.u >= 100f / 3.6f) t100 = r.time;
            if (t200 < 0 && r.u >= 200f / 3.6f) t200 = r.time;
            if (r.u > 3f) peakSr = Math.Max(peakSr, r.sim.wheels[2].slipRatio);
        }
        Console.WriteLine($"0–100 km/h {t100:F2} s · 0–200 km/h {t200:F2} s · Höchstgeschwindigkeit {r.u * 3.6f:F0} km/h · max. Längsschlupf hinten {peakSr:F2} · Bodeneffekt-Gewinn bei Vmax ×{r.sim.FloorGain:F2}");
    }

    Console.WriteLine();
    Console.WriteLine("## Bremsen (Vollbremsung, Ausgewogen mit ABS / Alles aus ohne ABS)");
    Console.WriteLine();
    Console.WriteLine("| Start | Bedingung | Profil | Bremsweg | Zeit | max. Verzögerung | min. Schlupf v/h (>20 km/h) |");
    Console.WriteLine("|---|---|---|---|---|---|---|");
    foreach (float kmh in new[] { 100f, 200f, 300f })
        foreach (bool wet in new[] { false, true })
            foreach (string profile in new[] { "Ausgewogen", "Alles aus" })
            {
                var r = RigFor(profile, kmh, GearFor(kmh), wet);
                float dist = 0f, peak = 0f, srF = 0f, srR = 0f, prevU = r.u;
                for (int i = 0; i < 120 * 10 && r.u > 0.3f; i++)
                {
                    r.sim.brake = 1f;
                    r.Step(Dt);
                    dist += r.u * Dt;
                    peak = Math.Max(peak, (prevU - r.u) / Dt / 9.81f);
                    prevU = r.u;
                    if (r.u > 20f / 3.6f)
                    {
                        srF = Math.Min(srF, r.sim.wheels[0].slipRatio);
                        srR = Math.Min(srR, r.sim.wheels[2].slipRatio);
                    }
                }
                Console.WriteLine($"| {kmh:F0} km/h | {(wet ? "nass (Inter)" : "trocken")} | {profile} | {dist:F0} m | {r.time:F2} s | {peak:F1} g | {srF:F2} / {srR:F2} |");
            }

    Console.WriteLine();
    Console.WriteLine("## Kerb (linke Räder auf Kerb, 150 km/h geradeaus)");
    Console.WriteLine();
    {
        var r = RigFor("Ausgewogen", 150f, GearFor(150f));
        float minL = float.MaxValue, maxL = 0f, maxYaw = 0f, maxOff = 0f;
        for (int i = 0; i < 120 * 2; i++)
        {
            r.sim.wheels[0].surface = SurfaceType.Kerb;
            r.sim.wheels[2].surface = SurfaceType.Kerb;
            r.sim.throttle = Hold(r, 150f);
            r.Step(Dt);
            minL = Math.Min(minL, r.sim.wheels[0].load);
            maxL = Math.Max(maxL, r.sim.wheels[0].load);
            maxOff = Math.Max(maxOff, r.sim.wheels[0].roadOffset);
            maxYaw = Math.Max(maxYaw, Math.Abs(r.yawRate));
        }
        Console.WriteLine($"Radlast vorne links {minL:F0}–{maxL:F0} N · Kerb-Anregung bis {maxOff * 1000f:F1} mm · max. Gierstörung {maxYaw * 57.3f:F2} °/s · Querversatz nach 2 s {r.positionXZ.X:F2} m");
    }

    Console.WriteLine();
    Console.WriteLine("## Rechenzeit Simulation");
    Console.WriteLine();
    {
        var r = RigFor("Ausgewogen", 150f, 5);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int steps = 120 * 60;
        for (int i = 0; i < steps; i++) { r.sim.steer = MathF.Sin(i * 0.01f) * 0.3f; r.sim.throttle = 0.6f; r.Step(Dt); }
        sw.Stop();
        double us = sw.Elapsed.TotalMilliseconds * 1000.0 / steps;
        Console.WriteLine($"{us:F1} µs pro Fahrzeug-Schritt (Desktop-CPU, ohne Unity) → 20 Autos × 120 Hz ≈ {us * 20 * 120 / 1000.0:F1} ms CPU pro Sekunde");
    }
}
else if (mode == "probe")
{
    float kmh = float.Parse(args[1]); float lockDeg = float.Parse(args[2]); float rate = float.Parse(args[3]);
    var sim = new VehicleSimulation(new VehicleSetup()); sim.tractionLevel = 0; sim.steeringAssist = 0f;
    sim.setup.steerRateDeg = rate;
    var r = new FlatGroundRig(sim); r.Place(Vector3.Zero, new Vector3(0, 0, 1));
    r.u = kmh / 3.6f; foreach (var w in sim.wheels) w.omega = r.u / w.radius; sim.gearbox.gear = args.Length > 5 ? int.Parse(args[5]) : 5; sim.SyncEngineToWheels();
    for (int i = 0; i < 240; i++)
    {
        sim.steer = lockDeg / sim.setup.maxSteerAngleDeg;
        sim.throttle = args.Length > 4 ? float.Parse(args[4]) : Math.Clamp((kmh / 3.6f - r.u) * 0.5f + 0.3f, 0f, 1f);
        r.Step(Dt);
        if (i % (args.Length > 6 ? int.Parse(args[6]) : 6) == 0) Console.WriteLine($"t={r.time:F2} lock={sim.SteerAngle * 57.3f:F1} yaw={r.yawRate * 57.3f:F1} beta={MathF.Atan2(r.v, r.u) * 57.3f:F1} aF={sim.wheels[0].slipAngle * 57.3f:F1}/{sim.wheels[1].slipAngle * 57.3f:F1} aR={sim.wheels[2].slipAngle * 57.3f:F1}/{sim.wheels[3].slipAngle * 57.3f:F1} FyF={sim.wheels[0].fy + sim.wheels[1].fy:F0} FyR={sim.wheels[2].fy + sim.wheels[3].fy:F0} FzFL={sim.wheels[0].load:F0} FzFR={sim.wheels[1].load:F0} FzRL={sim.wheels[2].load:F0} FzRR={sim.wheels[3].load:F0} thr={sim.throttle:F2} srR={sim.wheels[2].slipRatio:F2}/{sim.wheels[3].slipRatio:F2} TdR={sim.wheels[2].driveTorque:F0}/{sim.wheels[3].driveTorque:F0} srF={sim.wheels[0].slipRatio:F3}/{sim.wheels[1].slipRatio:F3} u={r.u:F1}");
    }
}
else if (mode == "keyboard")
{
    // A "keyboard driver": the AI decides what it wants, but the car only receives
    // digital keys through the same ramps as InputRouter (tapping to modulate).
    string id = args.Length > 1 ? args[1] : "bosphorus_gp";
    float pace = args.Length > 2 ? float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0.95f;
    var dirk = Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks");
    var def = CircuitDefinition.FromJson(File.ReadAllText(Path.Combine(dirk, id + ".json")));
    var g = TrackGeometry.FromDefinition(def);
    var setup0 = new VehicleSetup();
    var line = new RacingLine(g, 1.45f, setup0.downforceArea * setup0.airDensity * 0.5f / setup0.mass);
    var profiles = new (string name, int tc, bool abs, float assist)[]
    {
        ("Einsteiger", 3, true, 1f), ("Ausgewogen", 2, true, 0.5f), ("Profi", 1, true, 0f), ("Profi ohne ABS", 1, false, 0f), ("Alles aus", 0, false, 0f),
    };
    var envProfiles = Environment.GetEnvironmentVariable("APEX_PROFILES");
    if (!string.IsNullOrEmpty(envProfiles))
        profiles = Array.ConvertAll(envProfiles.Split(';'), e =>
        {
            var f = e.Split(':');
            return (f[0], int.Parse(f[1]), f[2] == "1", float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture));
        });
    Console.WriteLine($"| Profil | Rundenzeit | Dreher | max. Schwimmwinkel | max. neben Strecke |");
    Console.WriteLine("|---|---|---|---|---|");
    foreach (var pr in profiles)
    {
        var sim = new VehicleSimulation(new VehicleSetup()) { tractionLevel = pr.tc, assistAbs = pr.abs, steeringAssist = pr.assist };
        if (Environment.GetEnvironmentVariable("APEX_WET") != null) { sim.trackWetness = 0.6f; sim.FitTires(TireCompound.Intermediate); }
        var csEnv = Environment.GetEnvironmentVariable("APEX_CS");
        if (csEnv != null) sim.setup.counterSteerGain = float.Parse(csEnv, System.Globalization.CultureInfo.InvariantCulture);
        var rig = new FlatGroundRig(sim);
        g.FrameAt(g.Wrap(def.startLineDistance - 15f), out var p0, out var f0, out _, 0f);
        rig.Place(p0, f0);
        var ai = new AIDriver(line, AIPersonality.Make(pace, 0.2f, 1f, 0.5f, 0.5f), 7);
        var steerAxis = new Apex.InputLogic.DigitalAxis(3.2f, 5f, 7f);
        var thrAxis = new Apex.InputLogic.DigitalAxis(7f, 7f, 7f);
        var brkAxis = new Apex.InputLogic.DigitalAxis(9f, 9f, 9f);
        var timer = new LapTimer(g.Length, Array.ConvertAll(def.sectorFractions, f => f * g.Length));
        var others = new System.Collections.Generic.List<CarState>();
        int hint = -1, spins = 0; bool spinning = false; float maxBeta = 0f, maxOff = 0f;
        for (int i = 0; i < 120 * 400 && timer.completedLaps.Count < 2; i++)
        {
            var proj = g.Project(rig.Position, hint); hint = proj.index;
            var st = new CarState { id = 0, distance = proj.distance, lateral = proj.lateral, speed = rig.u, position = rig.Position, forward = rig.Forward };
            others.Clear(); others.Add(st);
            ai.maxSteerRad = sim.CurrentMaxLock();
            float gsum = 0f; foreach (var tr in sim.tires) gsum += tr.GripMultiplier(sim.trackWetness);
            ai.gripFactor = gsum / 4f;
            ai.wet = sim.trackWetness > 0.2f;
            ai.frontSlipUsage = MathF.Max(Math.Abs(sim.wheels[0].slipAngle), Math.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
            var c = ai.Drive(st, others, Dt);
            float k = Math.Clamp(rig.u / 60f, 0f, 1f);
            steerAxis.pressRate = Apex.InputLogic.InputTuning.SteerPressRate(k);
            steerAxis.releaseRate = Apex.InputLogic.InputTuning.SteerReleaseRate(k);
            steerAxis.reverseRate = Apex.InputLogic.InputTuning.SteerReverseRate;
            // Tap the key while the wheel is short of the wanted angle, release when past it.
            float key = 0f;
            if (Math.Abs(c.steer) > 0.05f && (c.steer - steerAxis.Value) * MathF.Sign(c.steer) > 0.03f) key = MathF.Sign(c.steer);
            sim.steer = steerAxis.Update(key, Dt);
            sim.throttle = thrAxis.Update(c.throttle > 0.5f ? 1f : 0f, Dt);
            sim.brake = brkAxis.Update(c.brake > 0.25f ? 1f : 0f, Dt);
            rig.Step(Dt);
            timer.Update(g.Wrap(proj.distance - def.startLineDistance), rig.time);
            float beta = MathF.Abs(MathF.Atan2(rig.v, MathF.Abs(rig.u) + 0.1f));
            maxBeta = Math.Max(maxBeta, beta);
            if (beta > 0.45f && !spinning) { spins++; spinning = true; }
            if (Environment.GetEnvironmentVariable("APEX_TRACE") != null && beta > 0.12f && i % 6 == 0)
                Console.WriteLine($"   t={rig.time:F2} d={g.Wrap(proj.distance - def.startLineDistance):F0} v={rig.u * 3.6f:F0} g={sim.gearbox.gear} beta={MathF.Atan2(rig.v, rig.u) * 57.3f:F0} st={sim.steer:F2} lock={sim.SteerAngle * 57.3f:F1} cs={sim.CounterSteer * 57.3f:F1} thr={sim.throttle:F2} brk={sim.brake:F2} aF={sim.wheels[0].slipAngle * 57.3f:F0} aR={sim.wheels[2].slipAngle * 57.3f:F0} srR={sim.wheels[2].slipRatio:F2} srF={sim.wheels[0].slipRatio:F2} yaw={rig.yawRate * 57.3f:F0} ai={c.steer:F2}");
            if (beta < 0.1f) spinning = false;
            maxOff = Math.Max(maxOff, Math.Abs(proj.lateral) - g.WidthAt(proj.distance) * 0.5f);
        }
        string lap = timer.completedLaps.Count > 1 ? $"{timer.completedLaps[1].time:F2} s" : "–";
        Console.WriteLine($"| {pr.name} | {lap} | {spins} | {maxBeta * 57.3f:F0}° | {Math.Max(0f, maxOff):F1} m |");
    }
}
else if (mode == "brake")
{
    var rig = NewRig(true);
    rig.u = 200f / 3.6f;
    foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
    rig.sim.gearbox.gear = 6; rig.sim.SyncEngineToWheels();
    for (int i = 0; i < 120 * 4 && rig.u > 0.5f; i++)
    {
        rig.sim.brake = 1f;
        rig.Step(Dt);
        var ws = rig.sim.wheels;
        if (i % 12 == 0 || ws[0].slipRatio < -0.6f || ws[2].slipRatio < -0.6f)
            Console.WriteLine($"t={rig.time:F2} v={rig.u * 3.6f:F0} g={rig.sim.gearbox.gear} rpm={rig.sim.EngineRpm:F0} srF={ws[0].slipRatio:F2} srR={ws[2].slipRatio:F2} TbF={ws[0].brakeTorque:F0} TbR={ws[2].brakeTorque:F0} TdR={ws[2].driveTorque:F0} FzF={ws[0].load:F0} FzR={ws[2].load:F0} abs={rig.sim.AbsActive}");
    }
}
else if (mode == "steer")
{
    // Keyboard-style full-lock step steer at constant speed (digital ramp like InputRouter).
    foreach (float kmh in new[] { 20f, 40f, 70f, 100f, 150f, 200f, 250f })
    {
        var rig = NewRig(true);
        rig.sim.assistSteering = true;
        rig.u = kmh / 3.6f;
        foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
        int g = 1; while (g < 8 && rig.u / 0.36f * rig.sim.gearbox.RatioFor(g) * 60f / 6.2832f > 11000f) g++;
        rig.sim.gearbox.gear = g; rig.sim.SyncEngineToWheels();
        var axis = new Apex.InputLogic.DigitalAxis(3.2f, 5f, 7f);
        axis.pressRate = 3.4f + (1.7f - 3.4f) * Math.Clamp(kmh / 3.6f / 60f, 0f, 1f);
        float maxBeta = 0, maxRear = 0, maxFront = 0, yawEnd = 0, latEnd = 0, maxLock = 0; bool spun = false;
        for (int i = 0; i < 120 * 3; i++)
        {
            float target = kmh / 3.6f;
            rig.sim.throttle = Math.Clamp((target - rig.u) * 0.4f + 0.25f, 0f, 1f);
            rig.sim.brake = 0f;
            rig.sim.steer = axis.Update(1f, Dt);
            rig.Step(Dt);
            float beta = MathF.Atan2(rig.v, rig.u);
            maxBeta = Math.Max(maxBeta, Math.Abs(beta));
            maxRear = Math.Max(maxRear, Math.Abs(rig.sim.wheels[2].slipAngle));
            maxFront = Math.Max(maxFront, Math.Abs(rig.sim.wheels[0].slipAngle));
            maxLock = Math.Max(maxLock, Math.Abs(rig.sim.SteerAngle));
            if (Math.Abs(beta) > 0.25f) spun = true;
            yawEnd = rig.yawRate; latEnd = rig.u * rig.yawRate / 9.81f;
        }
        float radius = Math.Abs(rig.u / Math.Max(Math.Abs(yawEnd), 1e-3f));
        Console.WriteLine($"{kmh,4} km/h lock {maxLock * 57.3f,5:F1}° maxBeta {maxBeta * 57.3f,5:F1}° rearSA {maxRear * 57.3f,5:F1}° frontSA {maxFront * 57.3f,5:F1}° lat {latEnd,5:F2} g radius {radius,6:F1} m v_end {rig.u * 3.6f,4:F0} {(spun ? "SPIN" : "")}");
    }
}
else if (mode == "slide")
{
    // Power oversteer: steady right turn at 90 km/h, then full throttle with TC off.
    foreach (var (assist, tc) in new[] { (false, false), (true, false), (false, true), (true, true) })
    {
        var rig = NewRig(tc);
        rig.sim.assistSteering = assist;
        rig.u = 25f;
        foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
        rig.sim.gearbox.gear = 3; rig.sim.SyncEngineToWheels();
        float maxBeta = 0f; bool spun = false;
        for (int i = 0; i < 120 * 4; i++)
        {
            bool power = i > 120;
            rig.sim.steer = assist ? 0.55f : 0.45f;
            rig.sim.throttle = power ? 1f : Math.Clamp((25f - rig.u) * 0.4f + 0.25f, 0f, 1f);
            rig.Step(Dt);
            float beta = MathF.Atan2(rig.v, rig.u);
            maxBeta = Math.Max(maxBeta, Math.Abs(beta));
            if (Math.Abs(beta) > 0.35f) spun = true;
        }
        Console.WriteLine($"assist={assist} tc={tc}: max sideslip {maxBeta * 57.3f:F1}°, counter-steer {rig.sim.CounterSteer * 57.3f:F1}° {(spun ? "SPIN" : "caught")}");
    }
}
else if (mode == "line")
{
    string id = args.Length > 1 ? args[1] : "bosphorus_gp";
    var dir = Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks");
    var def = CircuitDefinition.FromJson(File.ReadAllText(Path.Combine(dir, id + ".json")));
    var g = TrackGeometry.FromDefinition(def);
    var setup = new VehicleSetup();
    var line = new RacingLine(g, 1.5f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
    var idx = new int[g.Count];
    for (int i = 0; i < g.Count; i++) idx[i] = i;
    Array.Sort(idx, (a, b) => Math.Abs(line.curvature[b]).CompareTo(Math.Abs(line.curvature[a])));
    for (int k = 0; k < 8; k++)
    {
        int i = idx[k];
        Console.WriteLine($"i={i} d={g.Distances[i]:F0} lineK={line.curvature[i]:F4} centreK={g.Curvatures[i]:F4} off={line.offsets[i]:F2} w/2={g.Widths[i] * 0.5f:F1} offPrev={line.offsets[(i - 3 + g.Count) % g.Count]:F2} offNext={line.offsets[(i + 3) % g.Count]:F2}");
    }
    Console.WriteLine($"line {line.LineLength():F0} m vs centre {g.Length:F0} m");
    if (args.Length > 3)
        for (float dd = float.Parse(args[2]); dd < float.Parse(args[3]); dd += 10f)
            Console.WriteLine($"  d={dd:F0} off={line.OffsetAt(dd):F1} w/2={g.WidthAt(dd) * 0.5f:F1} lineK={line.curvature[g.IndexAt(dd)]:F4} centreK={g.CurvatureAt(dd):F4} v={line.SpeedAt(dd) * 3.6f:F0}");
}
else if (mode == "field")
{
    string id = args.Length > 1 ? args[1] : "test_loop";
    var dirp = Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks");
    var def = CircuitDefinition.FromJson(File.ReadAllText(Path.Combine(dirp, id + ".json")));
    var g = TrackGeometry.FromDefinition(def);
    var setup = new VehicleSetup();
    var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
    int n = int.Parse(Environment.GetEnvironmentVariable("APEX_N") ?? "6");
    var rigs = new FlatGroundRig[n]; var ais = new AIDriver[n]; var hints = new int[n];
    var states = new System.Collections.Generic.List<CarState>();
    for (int i = 0; i < n; i++)
    {
        var sim = new VehicleSimulation(new VehicleSetup()) { assistSteering = true };
        rigs[i] = new FlatGroundRig(sim);
        g.FrameAt(g.Wrap(def.startLineDistance - 12f - i * 8f), out var p, out var f, out _, i % 2 == 0 ? -3f : 3f);
        rigs[i].Place(p, f);
        ais[i] = new AIDriver(line, AIPersonality.Make(0.86f + 0.015f * (n - i), 0.6f, 0.8f, 0.5f, 0.5f), 100 + i);
        hints[i] = -1;
    }
    var reported = new bool[n];
    var maxB = new float[n];
    for (int step = 0; step < 120 * 150; step++)
    {
        states.Clear();
        for (int i = 0; i < n; i++)
        {
            var pr = g.Project(rigs[i].Position, hints[i]); hints[i] = pr.index;
            states.Add(new CarState { id = i, distance = pr.distance, lateral = pr.lateral, speed = rigs[i].u, position = rigs[i].Position, forward = rigs[i].Forward });
        }
        for (int i = 0; i < n; i++)
        {
            float gsum = 0f; foreach (var tr in rigs[i].sim.tires) gsum += tr.GripMultiplier(0f);
            ais[i].gripFactor = gsum / 4f;
            ais[i].maxSteerRad = rigs[i].sim.CurrentMaxLock();
            ais[i].frontSlipUsage = MathF.Max(Math.Abs(rigs[i].sim.wheels[0].slipAngle), Math.Abs(rigs[i].sim.wheels[1].slipAngle)) / rigs[i].sim.tireFront.slipAnglePeak;
            var c = ais[i].Drive(states[i], states, Dt);
            rigs[i].sim.throttle = c.throttle; rigs[i].sim.brake = c.brake; rigs[i].sim.steer = c.steer;
            rigs[i].Step(Dt);
        }
        for (int i = 0; i < n; i++)
        {
            var st = states[i];
            float beyond = Math.Abs(st.lateral) - g.WidthAt(st.distance) * 0.5f;
            maxB[i] = Math.Max(maxB[i], beyond);
            bool traceCar = args.Length > 2 && i == int.Parse(args[2]) && step / 120f > float.Parse(args[3]) && step / 120f < float.Parse(args[4]) && step % 15 == 0;
            if (traceCar)
                Console.WriteLine($"  t={step / 120f:F2} d={st.distance:F0} lat={st.lateral:F1} tgt={ais[i].TargetLateral:F1} v={st.speed * 3.6f:F0} want={ais[i].TargetSpeed(st.distance) * 3.6f:F0} str={rigs[i].sim.steer:F2} saF={rigs[i].sim.wheels[0].slipAngle * 57.3f:F1} saR={rigs[i].sim.wheels[2].slipAngle * 57.3f:F1} k={g.CurvatureAt(st.distance):F4} lineOff={ais[i].line.OffsetAt(st.distance):F1} grip={ais[i].gripFactor:F2} {ais[i].Intent}");
            if ((beyond > 1.5f && !reported[i]) || (step % 1200 == 0 && i == 0))
            {
                if (beyond > 1.5f) reported[i] = true;
                Console.WriteLine($"t={step / 120f:F1} car{i} d={st.distance:F0} lat={st.lateral:F1} v={st.speed * 3.6f:F0} intent={ais[i].Intent} tgtLat={ais[i].TargetLateral:F1} beta={MathF.Atan2(rigs[i].v, rigs[i].u) * 57.3f:F0} beyond={beyond:F1}");
            }
        }
    }
    Console.WriteLine("max beyond edge per car: " + string.Join(" ", Array.ConvertAll(maxB, b => b.ToString("F2"))));
}
else if (mode == "lap")
{
    string id = args.Length > 1 ? args[1] : "test_loop";
    var dir = Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks");
    var def = CircuitDefinition.FromJson(File.ReadAllText(Path.Combine(dir, id + ".json")));
    var g = TrackGeometry.FromDefinition(def);
    var rig = NewRig(true);
    g.FrameAt(g.Wrap(def.startLineDistance - 15f), out var pos, out var fwd, out _, -3f);
    rig.Place(pos, fwd);
    var ap = new Autopilot(g, rig.sim.setup);
    var timer = new LapTimer(g.Length, Array.ConvertAll(def.sectorFractions, f => f * g.Length));
    int hint = -1;
    bool trace = args.Length > 2;
    var pSum = new double[4]; long pN = 0;
    for (int i = 0; i < 120 * 300 && timer.completedLaps.Count < 2; i++)
    {
        var c = ap.Drive(rig.Position, rig.Forward, rig.u, rig.sim.setup.wheelbase, rig.sim.setup.maxSteerAngleDeg * MathF.PI / 180f);
        rig.sim.throttle = c.throttle; rig.sim.brake = c.brake; rig.sim.steer = c.steer;
        rig.Step(Dt);
        var p = g.Project(rig.Position, hint);
        hint = p.index;
        float lapD = g.Wrap(p.distance - def.startLineDistance);
        timer.Update(lapD, rig.time);
        for (int k = 0; k < 4; k++) pSum[k] += rig.sim.tires[k].LastSlipPower; pN++;
        float beyond = Math.Abs(p.lateral) - g.Widths[p.index] * 0.5f;
        if ((trace && i % 60 == 0) || beyond > 1.5f && i % 30 == 0)
            Console.WriteLine($"t={rig.time:F1} d={lapD:F0} lat={p.lateral:F1} v={rig.u * 3.6f:F0} tgt={ap.TargetSpeed(p.distance) * 3.6f:F0} thr={c.throttle:F2} brk={c.brake:F2} st={c.steer:F2} yaw={rig.yawRate:F2} beta={MathF.Atan2(rig.v, rig.u) * 57.3f:F1}");
    }
    foreach (var l in timer.completedLaps) Console.WriteLine($"lap {l.lap}: {l.time:F3} valid={l.valid}");
    Console.WriteLine($"mean slip power kW: FL {pSum[0] / pN / 1000:F1} FR {pSum[1] / pN / 1000:F1} RL {pSum[2] / pN / 1000:F1} RR {pSum[3] / pN / 1000:F1}");
    var tt = rig.sim.tires;
    Console.WriteLine($"tire temps FL {tt[0].temperature:F0} FR {tt[1].temperature:F0} RL {tt[2].temperature:F0} RR {tt[3].temperature:F0} wear {rig.sim.AverageTireWear() * 100f:F2}%");
}
