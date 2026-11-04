using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Pit crews and pit stop animation (docs/PIT_LANE.md). One crew of 14 per box with a car in the session (three per
    /// wheel, front and rear jack), own procedural models (PitCrewGeometry: overall in the team colour, helmet, gloves,
    /// boots; wheel, wheel gun, trolley jack). The movements come from Apex.Race.PitCrewChoreography, the limbs from the
    /// skeleton Apex.Race.PitCrewFigure (gait, kneeling, hands on the job), following the race logic's stop
    /// (PitState: called in, stopped for serviceTotal, released). Drawn instanced per body part; the car's
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
            public float clock;
        }

        readonly List<Box> boxes = new List<Box>();
        // Body part meshes (submeshes: overall, dark, helmet) and props (submeshes: dark, metal).
        readonly Mesh[] parts = new Mesh[PitCrewFigure.PartCount];
        readonly bool[][] partHasSlot = new bool[PitCrewFigure.PartCount][];
        Mesh wheel, gun, jackHead, jackHandle;
        Material dark, shell, propDark, propMetal, red, green;
        // Instance matrices per body part (one per person) and per prop.
        readonly Matrix4x4[][] partMatrices = new Matrix4x4[PitCrewFigure.PartCount][];
        readonly int[] partCount = new int[PitCrewFigure.PartCount];
        readonly Matrix4x4[] wheels = new Matrix4x4[32], guns = new Matrix4x4[8], heads = new Matrix4x4[4], handles = new Matrix4x4[4];
        readonly PitCrewFigure.Frame[] frames = new PitCrewFigure.Frame[PitCrewFigure.PartCount];
        int garageSide;
        // The player's own box, lit up while a stop is requested or under way (outline on the ground, arrow above).
        Transform ownMarker, ownArrow;
        // LED speed display after the limit line (speed of the car coming in, red when too fast) and the pit exit light.
        TextMesh speedDisplay;
        float speedDisplayAt;
        Renderer exitRed, exitGreen;
        Material lampOff;
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
            // Own crew models (placeholder art): ten body parts, wheel, gun and jack (PitCrewGeometry).
            int detail = quality > 0 ? 1 : 0;
            for (int p = 0; p < PitCrewFigure.PartCount; p++)
            {
                var slots = PitCrewGeometry.Part((PitCrewFigure.Part)p, detail);
                parts[p] = ToMesh("Crew" + (PitCrewFigure.Part)p, slots);
                partHasSlot[p] = new bool[slots.Length];
                for (int k = 0; k < slots.Length; k++) partHasSlot[p][k] = slots[k].TriangleCount > 0;
                partMatrices[p] = new Matrix4x4[PitCrewChoreography.CrewSize];
            }
            wheel = ToMesh("PitWheel", PitCrewGeometry.Wheel(detail));
            gun = ToMesh("PitGun", PitCrewGeometry.Gun());
            jackHead = ToMesh("PitJackHead", PitCrewGeometry.JackHead());
            jackHandle = ToMesh("PitJackHandle", PitCrewGeometry.JackHandle());
            dark = Instanced("CrewDark", new Color(0.08f, 0.085f, 0.1f));
            shell = Instanced("CrewHelmet", new Color(0.92f, 0.93f, 0.95f));
            propDark = Instanced("PitToolDark", new Color(0.06f, 0.06f, 0.065f));
            propMetal = Instanced("PitToolMetal", new Color(0.55f, 0.57f, 0.6f));
            red = new Material(EnvironmentMaterials.Solid(new Color(0.9f, 0.08f, 0.06f))) { name = "PitLampRed" };
            green = new Material(EnvironmentMaterials.Solid(new Color(0.1f, 0.95f, 0.3f))) { name = "PitLampGreen" };
            foreach (var m in new[] { red, green })
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", m.GetColor("_BaseColor") * 2.5f); }

            BuildOwnMarker();
            BuildLaneDisplays(ci);
            var g = ci.Geometry;
            for (int b = 0; b < pit.boxCount; b++)
            {
                float d = g.Wrap(pit.BoxDistance(b));
                var pose = ci.PoseAt(d, ci.PitLaneLateral(d), 0f);
                int team = pit.TeamOfBox(b, Roster.Teams.Length);
                var tc = Roster.Teams[team];
                var box = new Box
                {
                    index = b, team = team, position = pose.position, rotation = pose.rotation,
                    shirt = Instanced("CrewOverall" + team, new Color(tc.r, tc.g, tc.b)),
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
                post.GetComponent<Renderer>().sharedMaterial = propDark;
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

        static Material Instanced(string name, Color c) => new Material(EnvironmentMaterials.Solid(c)) { name = name, enableInstancing = true };

        /// <summary>One mesh with a submesh per material slot (empty slots stay as empty submeshes, so indices are fixed).</summary>
        static Mesh ToMesh(string name, MeshBuilder[] slots)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var mesh = new Mesh { name = name };
            var tris = new List<int>[slots.Length];
            for (int k = 0; k < slots.Length; k++)
            {
                int b = verts.Count;
                verts.AddRange(slots[k].Vertices);
                uvs.AddRange(slots[k].UVs);
                tris[k] = new List<int>(slots[k].Triangles.Count);
                foreach (int i in slots[k].Triangles) tris[k].Add(i + b);
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = slots.Length;
            for (int k = 0; k < slots.Length; k++) mesh.SetTriangles(tris[k], k);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildOwnMarker()
        {
            var cyan = new Material(EnvironmentMaterials.Solid(new Color(0f, 0.83f, 1f))) { name = "OwnBoxMarker" };
            cyan.EnableKeyword("_EMISSION");
            if (cyan.HasProperty("_EmissionColor")) cyan.SetColor("_EmissionColor", new Color(0f, 0.83f, 1f) * 2.2f);
            var frame = new MeshBuilder();
            const float hl = 4.0f, hw = 2.1f, lw = 0.18f, y = 0.025f;
            frame.Box(new Vector3(0f, y, hl), new Vector3(hw * 2f, 0.01f, lw));
            frame.Box(new Vector3(0f, y, -hl), new Vector3(hw * 2f, 0.01f, lw));
            frame.Box(new Vector3(hw, y, 0f), new Vector3(lw, 0.01f, hl * 2f));
            frame.Box(new Vector3(-hw, y, 0f), new Vector3(lw, 0.01f, hl * 2f));
            ownMarker = MakeObject(frame.ToMesh("OwnBoxFrame"), cyan, "OwnBox");
            var arrow = new MeshBuilder();
            arrow.Box(new Vector3(-0.32f, 0.35f, 0f), new Vector3(0.18f, 0.9f, 0.18f), Quaternion.Euler(0f, 0f, 35f));
            arrow.Box(new Vector3(0.32f, 0.35f, 0f), new Vector3(0.18f, 0.9f, 0.18f), Quaternion.Euler(0f, 0f, -35f));
            ownArrow = MakeObject(arrow.ToMesh("OwnBoxArrow"), cyan, "OwnBoxArrow");
            ownMarker.gameObject.SetActive(false);
            ownArrow.gameObject.SetActive(false);
        }

        Transform MakeObject(Mesh mesh, Material mat, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        void BuildLaneDisplays(CircuitInstance ci)
        {
            var pit = ci.Pit;
            var g = ci.Geometry;
            var plan = pit.PlanFor(g.Length);
            float deck = (PitLaneLayout.WallOuter + PitLaneLayout.DeckOuter) * 0.5f;
            lampOff = new Material(EnvironmentMaterials.Solid(new Color(0.06f, 0.06f, 0.07f))) { name = "PitLampOff" };
            // Speed display on a post on the deck, 25 m after the limit line, facing the cars coming in.
            speedDisplayAt = g.Wrap(pit.entry + PitLaneLayout.LimiterStart + 25f);
            var pose = ci.PoseAt(speedDisplayAt, pit.side * (g.WidthAt(speedDisplayAt) * 0.5f + deck), 0f);
            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(housing.GetComponent<Collider>());
            housing.name = "PitSpeedDisplay";
            housing.transform.SetParent(transform, false);
            housing.transform.SetPositionAndRotation(pose.position + Vector3.up * 2.4f, pose.rotation);
            housing.transform.localScale = new Vector3(1.3f, 0.7f, 0.12f);
            housing.GetComponent<Renderer>().sharedMaterial = lampOff;
            var font = CircuitBuilder.BuiltinFont();
            if (font != null)
            {
                var tgo = new GameObject("PitSpeedDigits");
                tgo.transform.SetParent(transform, false);
                // TextMesh reads from its −z side: turn it to face the cars coming in (−z of the lane frame).
                tgo.transform.SetPositionAndRotation(pose.position + Vector3.up * 2.4f - pose.rotation * Vector3.forward * 0.07f, pose.rotation);
                speedDisplay = tgo.AddComponent<TextMesh>();
                speedDisplay.font = font;
                speedDisplay.fontSize = 64;
                speedDisplay.characterSize = 0.08f;
                speedDisplay.anchor = TextAnchor.MiddleCenter;
                speedDisplay.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            // Pit exit light at the exit line: red until the cars may leave, green after.
            float dx = g.Wrap(pit.entry + plan.limitSpan);
            var ep = ci.PoseAt(dx, pit.side * (g.WidthAt(dx) * 0.5f + deck), 0f);
            Renderer Lamp(float y)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(l.GetComponent<Collider>());
                l.name = "PitExitLight";
                l.transform.SetParent(transform, false);
                l.transform.SetPositionAndRotation(ep.position + Vector3.up * y, ep.rotation);
                l.transform.localScale = new Vector3(0.4f, 0.4f, 0.15f);
                var r = l.GetComponent<Renderer>();
                r.sharedMaterial = lampOff;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return r;
            }
            exitRed = Lamp(2.9f);
            exitGreen = Lamp(2.4f);
        }

        void UpdateLaneDisplays()
        {
            if (exitRed != null)
            {
                bool open = race.Started && race.Director.CarsMayMove;
                exitRed.sharedMaterial = open ? lampOff : red;
                exitGreen.sharedMaterial = open ? green : lampOff;
            }
            if (speedDisplay == null) return;
            // The nearest car in the pit lane coming towards the display.
            var g = race.Circuit.Geometry;
            float best = float.MaxValue, speed = -1f;
            foreach (var rc in race.Cars)
            {
                if (rc.removed || !rc.pit.InPitLane) continue;
                float ahead = ApexMath.PosMod(speedDisplayAt - rc.raw, g.Length);
                if (ahead < 70f && ahead < best) { best = ahead; speed = rc.Sim.ForwardSpeed; }
            }
            float limit = race.Circuit.Pit.SpeedLimit;
            speedDisplay.text = speed < 0f ? "" : (speed * 3.6f).ToString("0");
            speedDisplay.color = speed > limit + PitSpeedMonitor.Tolerance ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.4f);
        }

        void UpdateOwnMarker()
        {
            var p = race.Player;
            bool show = p != null && !p.removed && p.boxIndex >= 0 && p.boxIndex < boxes.Count
                        && (p.pit.stage == PitState.Stage.Requested || (p.pit.stage == PitState.Stage.InLane && !p.pit.settling));
            ownMarker.gameObject.SetActive(show);
            ownArrow.gameObject.SetActive(show);
            if (!show) return;
            var bx = boxes[p.boxIndex];
            ownMarker.SetPositionAndRotation(bx.position, bx.rotation);
            float bob = Mathf.Sin(Time.time * 3f) * 0.15f;
            ownArrow.SetPositionAndRotation(bx.position + Vector3.up * (3.2f + bob), Quaternion.Euler(0f, Time.time * 60f, 0f));
        }

        void Update()
        {
            if (race == null || boxes.Count == 0) return;
            UpdateOwnMarker();
            UpdateLaneDisplays();
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
                Draw(box, dt);
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
            // Red while the car must stay (also when the release is held for traffic in the fast lane), green on release.
            bool greenNow = box.phase == PitCrewChoreography.Phase.Service ? PitCrewChoreography.ReleaseGreen(box.t, box.service) && !ps.releaseHeld : box.phase != PitCrewChoreography.Phase.Ready;
            if (greenNow != box.lampGreen) { box.lampGreen = greenNow; box.lamp.sharedMaterial = greenNow ? green : red; }
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>World matrix of a box-frame part frame (with an optional length along forward).</summary>
        static Matrix4x4 World(Box box, in PitCrewFigure.Frame f, float zScale = 1f)
        {
            var pos = box.framePos + box.frameRot * U(f.origin);
            var rot = box.frameRot * Quaternion.LookRotation(U(f.forward), U(f.up));
            return Matrix4x4.TRS(pos, rot, new Vector3(1f, 1f, zScale));
        }

        void Draw(Box box, float dt)
        {
            box.clock += dt;
            for (int p = 0; p < partCount.Length; p++) partCount[p] = 0;
            int nWheel = 0, nGun = 0, nJack = 0;
            for (int i = 0; i < PitCrewChoreography.CrewSize; i++)
            {
                var role = PitCrewChoreography.Roles[i];
                var pose = PitCrewChoreography.PoseOf(role, box.phase, box.t, box.service, box.changeTyres, carShape, garageSide);
                // Gait from the crew clock (keeps running through the phases), offset per person so they don't march in step.
                PitCrewFigure.Solve(pose, PitCrewFigure.ToFigure(pose.hands, pose.position, pose.yaw), box.clock + i * 0.137f, frames);
                for (int p = 0; p < PitCrewFigure.PartCount; p++)
                    partMatrices[p][partCount[p]++] = World(box, PitCrewFigure.ToBox(frames[p], pose.position, pose.yaw));
                var props = PitCrewFigure.PropsOf(role, pose, box.phase, box.t, box.service, carShape);
                if (props.wheel && nWheel < wheels.Length) wheels[nWheel++] = World(box, props.wheelFrame);
                if (props.gun && nGun < guns.Length) guns[nGun++] = World(box, props.gunFrame);
                if (props.jack && nJack < heads.Length)
                {
                    heads[nJack] = World(box, props.jackHead);
                    handles[nJack++] = World(box, props.jackHandle, props.handleLength);
                }
            }
            // Old wheels set down beside the car until the crew carries them back (service, after "off").
            if (box.phase == PitCrewChoreography.Phase.Service && box.changeTyres && box.t / Mathf.Max(box.service, 0.5f) >= PitCrewChoreography.OffEnd)
                for (int w = 0; w < 4 && nWheel < wheels.Length; w++)
                {
                    var hub = PitCrewChoreography.Hub(carShape, w);
                    var spot = new Vector3(hub.X + Mathf.Sign(hub.X) * 1.6f, 0.2f, hub.Z - 0.9f);
                    wheels[nWheel++] = Matrix4x4.TRS(box.framePos + box.frameRot * spot, box.frameRot * Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                }
            for (int p = 0; p < PitCrewFigure.PartCount; p++)
            {
                var has = partHasSlot[p];
                if (has[PitCrewGeometry.Overall]) Graphics.DrawMeshInstanced(parts[p], PitCrewGeometry.Overall, box.shirt, partMatrices[p], partCount[p], null, ShadowCastingMode.On, true);
                if (has[PitCrewGeometry.Dark]) Graphics.DrawMeshInstanced(parts[p], PitCrewGeometry.Dark, dark, partMatrices[p], partCount[p], null, ShadowCastingMode.On, true);
                if (has[PitCrewGeometry.Shell]) Graphics.DrawMeshInstanced(parts[p], PitCrewGeometry.Shell, shell, partMatrices[p], partCount[p], null, ShadowCastingMode.On, true);
            }
            DrawProp(wheel, wheels, nWheel);
            DrawProp(gun, guns, nGun);
            DrawProp(jackHead, heads, nJack);
            DrawProp(jackHandle, handles, nJack);
        }

        void DrawProp(Mesh mesh, Matrix4x4[] m, int n)
        {
            if (n <= 0) return;
            Graphics.DrawMeshInstanced(mesh, PitCrewGeometry.PropDark, propDark, m, n, null, ShadowCastingMode.On, true);
            Graphics.DrawMeshInstanced(mesh, PitCrewGeometry.PropMetal, propMetal, m, n, null, ShadowCastingMode.Off, true);
        }
    }
}
