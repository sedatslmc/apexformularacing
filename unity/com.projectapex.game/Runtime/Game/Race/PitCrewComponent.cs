using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Pit crews and pit stop animation (docs/PIT_LANE.md). One crew of 17 per box with a car in the session, in the
    /// team colour; the movements come from Apex.Race.PitCrewChoreography and follow the race logic's stop
    /// (PitState: called in, stopped for serviceTotal, released). Drawn instanced (people, wheels, tools); the car's
    /// own wheels are hidden while they are off the hub, and a lamp above each box turns from red to green at the
    /// release. On mobile only the crews of boxes with a car on its way in, in the box or just released are drawn.
    /// </summary>
    public sealed class PitCrewComponent : MonoBehaviour
    {
        public RaceController race;
        /// <summary>Environment quality (0 = mobile).</summary>
        public int quality = 1;
        /// <summary>Distance before the box (m) at which the crew comes out.</summary>
        public const float CallDistance = 160f;
        public const float DrawDistance = 300f;

        sealed class Box
        {
            public int index, team;
            public Vector3 position;
            public Quaternion rotation;
            public Material shirt;
            public RaceCar car;
            public PitCrewChoreography.Phase phase;
            public float t;
            public bool changeTyres;
            public float service;
            public Vector3 framePos;
            public Quaternion frameRot;
            public Renderer lamp;
            public bool lampGreen;
            public readonly Matrix4x4[] people = new Matrix4x4[PitCrewChoreography.CrewSize];
        }

        readonly List<Box> boxes = new List<Box>();
        Mesh person, tyre, tool;
        Material skin, tyreMat, toolMat, red, green;
        readonly Matrix4x4[] tyres = new Matrix4x4[64];
        readonly Matrix4x4[] tools = new Matrix4x4[64];
        int garageSide;
        PitCrewChoreography.Car carShape = PitCrewChoreography.Car.Default;

        public void Build(CircuitInstance ci)
        {
            var pit = ci.Pit;
            if (pit == null) return;
            garageSide = pit.side;
            var s = new Apex.Vehicle.VehicleSetup();
            carShape = new PitCrewChoreography.Car
            {
                frontAxle = s.FrontAxleZ, rearAxle = s.RearAxleZ, halfTrackFront = s.trackFront * 0.5f, halfTrackRear = s.trackRear * 0.5f,
                nose = s.FrontAxleZ + 1.3f, tail = s.RearAxleZ - 0.8f,
            };
            // Meshes: a person (shirt + skin like the spectators), a wheel, a tool block (wheel gun, jack handle, sign).
            var shirtMb = new MeshBuilder();
            var skinMb = new MeshBuilder();
            EnvironmentGeometry.SpectatorMesh(shirtMb, skinMb);
            person = EnvironmentBuilder.Combine(new Dictionary<string, MeshBuilder> { ["CrewShirt"] = shirtMb, [EnvironmentGeometry.Skin] = skinMb }, "PitCrew", out var mats);
            skin = mats[1];
            skin.enableInstancing = true;
            var wheelMb = new MeshBuilder();
            wheelMb.Tube(new Vector3(-0.19f, 0f, 0f), new Vector3(0.19f, 0f, 0f), 0.36f, 14, true);
            tyre = wheelMb.ToMesh("PitWheel");
            tyreMat = new Material(EnvironmentMaterials.Solid(new Color(0.05f, 0.05f, 0.055f))) { name = "PitWheel", enableInstancing = true };
            var toolMb = new MeshBuilder();
            toolMb.Box(Vector3.zero, new Vector3(0.12f, 0.12f, 0.32f));
            tool = toolMb.ToMesh("PitTool");
            toolMat = new Material(EnvironmentMaterials.Solid(new Color(0.15f, 0.16f, 0.18f))) { name = "PitTool", enableInstancing = true };
            red = new Material(EnvironmentMaterials.Solid(new Color(0.9f, 0.08f, 0.06f))) { name = "PitLampRed" };
            green = new Material(EnvironmentMaterials.Solid(new Color(0.1f, 0.95f, 0.3f))) { name = "PitLampGreen" };
            foreach (var m in new[] { red, green })
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", m.GetColor("_BaseColor") * 2.5f); }

            var g = ci.Geometry;
            for (int b = 0; b < 20; b++)
            {
                float d = g.Wrap(pit.BoxDistance(b));
                var pose = ci.PoseAt(d, ci.PitLaneLateral(d), 0f);
                int team = Mathf.Min(b / 2, Roster.Teams.Length - 1);
                var tc = Roster.Teams[team];
                var box = new Box
                {
                    index = b, team = team, position = pose.position, rotation = pose.rotation,
                    shirt = new Material(EnvironmentMaterials.Solid(new Color(tc.r, tc.g, tc.b))) { name = "CrewShirt" + team, enableInstancing = true },
                };
                box.framePos = box.position; box.frameRot = box.rotation;
                // Release lamp on a post at the garage end of the box, facing the driver.
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(post.GetComponent<Collider>());
                post.name = "PitLampPost" + b;
                post.transform.SetParent(transform, false);
                var basePos = box.position + box.rotation * new Vector3(garageSide * (PitCrewChoreography.GarageX + 0.4f), 0f, carShape.nose + 2.2f);
                post.transform.SetPositionAndRotation(basePos + Vector3.up * 1.1f, Quaternion.identity);
                post.transform.localScale = new Vector3(0.08f, 1.1f, 0.08f);
                post.GetComponent<Renderer>().sharedMaterial = toolMat;
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(lamp.GetComponent<Collider>());
                lamp.name = "PitLamp" + b;
                lamp.transform.SetParent(transform, false);
                lamp.transform.SetPositionAndRotation(basePos + Vector3.up * 2.3f, box.rotation);
                lamp.transform.localScale = new Vector3(0.35f, 0.35f, 0.15f);
                box.lamp = lamp.GetComponent<Renderer>();
                box.lamp.sharedMaterial = red;
                box.lamp.shadowCastingMode = ShadowCastingMode.Off;
                boxes.Add(box);
            }
        }

        void Update()
        {
            if (race == null || boxes.Count == 0) return;
            float dt = Time.deltaTime;
            var cam = Camera.main;
            var camPos = cam != null ? cam.transform.position : Vector3.zero;
            var g = race.Circuit.Geometry;
            var pit = race.Circuit.Pit;
            foreach (var box in boxes) box.car = null;
            foreach (var rc in race.Cars)
                if (!rc.removed && rc.boxIndex >= 0 && rc.boxIndex < boxes.Count)
                {
                    var bx = boxes[rc.boxIndex];
                    // Two cars on one box (teammates): the one with a stop under way.
                    if (bx.car == null || rc.pit.stage != PitState.Stage.None) bx.car = rc;
                }
            foreach (var box in boxes)
            {
                if (box.car == null) continue;
                Advance(box, g, pit, dt);
                bool active = box.phase != PitCrewChoreography.Phase.Waiting;
                if (quality <= 0 && !active) continue;
                if ((box.position - camPos).sqrMagnitude > DrawDistance * DrawDistance) continue;
                Draw(box);
            }
        }

        void Advance(Box box, Apex.Tracks.TrackGeometry g, Apex.Tracks.CircuitDefinition.PitLane pit, float dt)
        {
            var rc = box.car;
            var ps = rc.pit;
            var prev = box.phase;
            float toBox = ApexMath.PosMod(g.Wrap(pit.BoxDistance(box.index)) - rc.raw, g.Length);
            switch (ps.stage)
            {
                case PitState.Stage.Stopped:
                    box.phase = PitCrewChoreography.Phase.Service;
                    box.t = ps.serviceTotal - ps.serviceRemaining;
                    box.service = ps.serviceTotal;
                    box.changeTyres = ps.changeTires;
                    break;
                case PitState.Stage.InLane:
                case PitState.Stage.Requested:
                    if (ps.stage == PitState.Stage.InLane || toBox < CallDistance)
                    {
                        if (prev != PitCrewChoreography.Phase.Ready) box.t = 0f;
                        box.phase = PitCrewChoreography.Phase.Ready;
                        box.changeTyres = ps.changeTires;
                        box.t += dt;
                    }
                    break;
                default:
                    if (prev == PitCrewChoreography.Phase.Service) { box.phase = PitCrewChoreography.Phase.Return; box.t = 0f; }
                    if (box.phase == PitCrewChoreography.Phase.Return)
                    {
                        box.t += dt;
                        if (box.t > PitCrewChoreography.ReturnTime + 0.5f) box.phase = PitCrewChoreography.Phase.Waiting;
                    }
                    else if (box.phase == PitCrewChoreography.Phase.Ready) box.phase = PitCrewChoreography.Phase.Waiting;
                    break;
            }
            // Work around the car where it actually stopped (up to a few metres off the box centre).
            Vector3 targetPos = box.position;
            Quaternion targetRot = box.rotation;
            if (box.phase == PitCrewChoreography.Phase.Service && rc.car != null)
            {
                var ct = rc.car.transform;
                targetPos = new Vector3(ct.position.x, box.position.y, ct.position.z);
                var fwd = Vector3.ProjectOnPlane(ct.forward, Vector3.up);
                if (fwd.sqrMagnitude > 1e-4f) targetRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }
            float k = 1f - Mathf.Exp(-10f * dt);
            box.framePos = Vector3.Lerp(box.framePos, targetPos, k);
            box.frameRot = Quaternion.Slerp(box.frameRot, targetRot, k);
            // The car's wheels while they are off the hub, and the release lamp.
            var vis = rc.car != null ? rc.car.GetComponent<CarVisuals>() : null;
            bool off = box.phase == PitCrewChoreography.Phase.Service && PitCrewChoreography.WheelOffHub(box.t, box.service, box.changeTyres);
            if (vis != null) for (int w = 0; w < 4; w++) vis.SetWheelHidden(w, off);
            bool greenNow = box.phase == PitCrewChoreography.Phase.Service ? PitCrewChoreography.ReleaseGreen(box.t, box.service) : box.phase != PitCrewChoreography.Phase.Ready;
            if (greenNow != box.lampGreen) { box.lampGreen = greenNow; box.lamp.sharedMaterial = greenNow ? green : red; }
        }

        void Draw(Box box)
        {
            int nTyre = 0, nTool = 0;
            for (int i = 0; i < PitCrewChoreography.CrewSize; i++)
            {
                var role = PitCrewChoreography.Roles[i];
                var p = PitCrewChoreography.PoseOf(role, box.phase, box.t, box.service, box.changeTyres, carShape, garageSide);
                var local = new Vector3(p.position.X, 0f, p.position.Z);
                var world = box.framePos + box.frameRot * local;
                var rot = box.frameRot * Quaternion.Euler(0f, p.yaw * Mathf.Rad2Deg, 0f);
                // Kneeling: lower and shorter (the figure folds at the knees).
                float height = Mathf.Lerp(1.78f, 1.15f, p.kneel);
                box.people[i] = Matrix4x4.TRS(world, rot, new Vector3(1.78f, height, 1.78f));
                var hands = world + rot * new Vector3(0f, height * 0.55f, 0.32f);
                if (p.carriesWheel && nTyre < tyres.Length)
                    tyres[nTyre++] = Matrix4x4.TRS(hands + Vector3.up * 0.05f, rot * Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                if (p.tool && nTool < tools.Length)
                {
                    bool jack = role == PitCrewChoreography.Role.FrontJack || role == PitCrewChoreography.Role.RearJack;
                    var scale = jack ? new Vector3(0.5f, 0.5f, 5f) : Vector3.one;
                    tools[nTool++] = Matrix4x4.TRS(jack ? world + rot * new Vector3(0f, 0.45f, 0.6f) : hands, rot, scale);
                }
            }
            // Old wheels set down beside the car until the crew carries them back (service, after "off").
            if (box.phase == PitCrewChoreography.Phase.Service && box.changeTyres && box.t / Mathf.Max(box.service, 0.5f) >= PitCrewChoreography.OffEnd)
                for (int w = 0; w < 4 && nTyre < tyres.Length; w++)
                {
                    var hub = PitCrewChoreography.Hub(carShape, w);
                    var spot = new Vector3(hub.X + Mathf.Sign(hub.X) * 1.6f, 0.36f, hub.Z - 0.9f);
                    tyres[nTyre++] = Matrix4x4.TRS(box.framePos + box.frameRot * spot, box.frameRot * Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                }
            Graphics.DrawMeshInstanced(person, 0, box.shirt, box.people, PitCrewChoreography.CrewSize, null, ShadowCastingMode.On, true);
            Graphics.DrawMeshInstanced(person, 1, skin, box.people, PitCrewChoreography.CrewSize, null, ShadowCastingMode.Off, true);
            if (nTyre > 0) Graphics.DrawMeshInstanced(tyre, 0, tyreMat, tyres, nTyre, null, ShadowCastingMode.On, true);
            if (nTool > 0) Graphics.DrawMeshInstanced(tool, 0, toolMat, tools, nTool, null, ShadowCastingMode.Off, true);
        }
    }
}
