using System;
using System.IO;
using System.Numerics;
using Apex.AI;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;

// Usage: dotnet run -- launch | lap <track_id> [trace]
const float Dt = 1f / 120f;
string mode = args.Length > 0 ? args[0] : "launch";

FlatGroundRig NewRig(bool tc)
{
    var sim = new VehicleSimulation(new VehicleSetup()) { assistTraction = tc, assistSteering = false };
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
            if (i % 30 == 0)
                Console.WriteLine($"tc={tc} t={rig.time:F2} v={rig.u * 3.6f:F0} g={rig.sim.gearbox.gear} rpm={rig.sim.EngineRpm:F0} sr={rig.sim.wheels[2].slipRatio:F2} cut={rig.sim.TcCut:F2} Fz_r={rig.sim.wheels[2].load:F0}");
            if (t100 < 0 && rig.u * 3.6f >= 100) t100 = rig.time;
        }
        Console.WriteLine($"tc={tc}: 0-100 {t100:F2}s");
    }
}
else if (mode == "brake")
{
    var rig = NewRig(true);
    rig.u = 200f / 3.6f;
    foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
    rig.sim.gearbox.gear = 6;
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
        rig.sim.gearbox.gear = g;
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
        rig.sim.gearbox.gear = 3;
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
