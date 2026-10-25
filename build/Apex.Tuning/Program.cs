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
    for (int i = 0; i < 120 * 300 && timer.completedLaps.Count < 2; i++)
    {
        var c = ap.Drive(rig.Position, rig.Forward, rig.u, rig.sim.setup.wheelbase, rig.sim.setup.maxSteerAngleDeg * MathF.PI / 180f);
        rig.sim.throttle = c.throttle; rig.sim.brake = c.brake; rig.sim.steer = c.steer;
        rig.Step(Dt);
        var p = g.Project(rig.Position, hint);
        hint = p.index;
        float lapD = g.Wrap(p.distance - def.startLineDistance);
        timer.Update(lapD, rig.time);
        float beyond = Math.Abs(p.lateral) - g.Widths[p.index] * 0.5f;
        if ((trace && i % 60 == 0) || beyond > 1.5f && i % 30 == 0)
            Console.WriteLine($"t={rig.time:F1} d={lapD:F0} lat={p.lateral:F1} v={rig.u * 3.6f:F0} tgt={ap.TargetSpeed(p.distance) * 3.6f:F0} thr={c.throttle:F2} brk={c.brake:F2} st={c.steer:F2} yaw={rig.yawRate:F2} beta={MathF.Atan2(rig.v, rig.u) * 57.3f:F1}");
    }
    foreach (var l in timer.completedLaps) Console.WriteLine($"lap {l.lap}: {l.time:F3} valid={l.valid}");
}
