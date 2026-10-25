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
