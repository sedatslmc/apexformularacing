using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Vehicle
{
    /// <summary>
    /// Offline 6-DOF drive rig on the circuit's real collider triangles (<see cref="CollisionWorld"/>).
    /// The wheel and force code mirrors the in-game <c>VehicleController</c> step for step (ray-cast
    /// suspension from the mounts along the body's down axis, contact velocities in the steered
    /// wheel frame, spring/tire forces at the same points, aero at the axles, drag), integrated at
    /// the game's 120 Hz. In addition the car's physical colliders from <c>CarBuilder</c> (<see cref="BodyCollider"/>,
    /// four 0.28 m wheel spheres) are checked against the surface every step:
    /// a touch is recorded as a grounding event and resolved with a contact impulse (no
    /// restitution, friction 0.3 as the car's physics material). It is not PhysX, so it shows
    /// geometry problems and their size, not bit-exact engine behaviour.
    /// </summary>
    public sealed class SurfaceRig
    {
        public const float Gravity = 9.81f;
        public const float WheelSphereRadius = 0.28f;
        /// <summary>PhysX default contact offset: contacts are generated this far before touching.</summary>
        public const float ContactOffset = 0.01f;

        public readonly VehicleSimulation sim;
        public readonly CollisionWorld world;
        public Vector3 position;
        public Quaternion rotation = Quaternion.Identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float time;

        readonly VehicleSetup s;
        readonly Vector3 inertia;
        readonly Vector3[] contactPoints = new Vector3[4];
        readonly Vector3[] wheelForward = new Vector3[4];
        readonly Vector3[] wheelRight = new Vector3[4];
        readonly List<Vector3> bodyPoints = new List<Vector3>();
        readonly List<(Vector3 centre, float radius)> spheres = new List<(Vector3, float)>();

        // --- Telemetry of the last step ---------------------------------------------------------
        /// <summary>Wheels with ground contact.</summary>
        public int WheelContacts { get; private set; }
        /// <summary>Body collider points touching the surface (grounding).</summary>
        public int BodyContacts { get; private set; }
        /// <summary>Deepest body penetration (m, positive = into the surface).</summary>
        public float MaxPenetration { get; private set; }
        /// <summary>Normal impulse delivered by body contacts this step (N·s).</summary>
        public float BodyImpulse { get; private set; }
        /// <summary>Lowest clearance of the body box bottom above the surface (m).</summary>
        public float MinClearance { get; private set; }
        /// <summary>Surface part under the lowest body point.</summary>
        public string ClearancePart { get; private set; }
        /// <summary>Highest suspension compression of the four wheels (m).</summary>
        public float MaxCompression { get; private set; }
        /// <summary>Wheel hit surfaces of the last step.</summary>
        public readonly string[] wheelParts = new string[4];

        public SurfaceRig(VehicleSimulation sim, CollisionWorld world)
        {
            this.sim = sim;
            this.world = world;
            s = sim.setup;
            inertia = new Vector3(s.inertiaPitch, s.inertiaYaw, s.inertiaRoll);
            // Body collider bottoms (BodyCollider, as CarBuilder) and the wheel spheres at the wheel centres.
            bodyPoints.AddRange(new BodyCollider(s).BottomPoints());
            for (int i = 0; i < 4; i++)
            {
                var m = s.MountPosition(i, out _);
                float r = i < 2 ? s.wheelRadiusFront : s.wheelRadiusRear;
                spheres.Add((new Vector3(m.X, r - s.cogHeight, m.Z), WheelSphereRadius));
            }
        }

        public Vector3 Up => Vector3.Transform(Vector3.UnitY, rotation);
        public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, rotation);
        public Vector3 Right => Vector3.Transform(Vector3.UnitX, rotation);
        public float ForwardSpeed => Vector3.Dot(velocity, Forward);
        public Vector3 World(Vector3 local) => position + Vector3.Transform(local, rotation);

        /// <summary>
        /// Places the car like the game's grid spawn: centre of mass at the static ride height above
        /// the surface under (x, z), level with the surface along `forward`, at rest.
        /// </summary>
        public void Place(Vector3 groundPoint, Vector3 forward, float extraHeight = 0f)
        {
            var f = Vector3.Normalize(new Vector3(forward.X, forward.Y, forward.Z));
            var up = Vector3.UnitY;
            var right = Vector3.Normalize(Vector3.Cross(up, f));
            up = Vector3.Cross(f, right);
            rotation = Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                right.X, right.Y, right.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                f.X, f.Y, f.Z, 0f,
                0f, 0f, 0f, 1f));
            // As RaceController: centre of mass straight above the road point (world up).
            position = groundPoint + Vector3.UnitY * (s.cogHeight + extraHeight);
            velocity = angularVelocity = Vector3.Zero;
            sim.Reset();
        }

        public void SetSpeed(float forwardSpeed, int gear)
        {
            velocity = Forward * forwardSpeed;
            foreach (var w in sim.wheels) w.omega = forwardSpeed / w.radius;
            sim.gearbox.gear = gear;
            sim.SyncEngineToWheels();
        }

        Vector3 PointVelocity(Vector3 p) => velocity + Vector3.Cross(angularVelocity, p - position);

        Vector3 InvInertiaWorld(Vector3 v)
        {
            var inv = Quaternion.Inverse(rotation);
            var local = Vector3.Transform(v, inv);
            local = new Vector3(local.X / inertia.X, local.Y / inertia.Y, local.Z / inertia.Z);
            return Vector3.Transform(local, rotation);
        }

        Vector3 InertiaWorld(Vector3 v)
        {
            var local = Vector3.Transform(v, Quaternion.Inverse(rotation));
            local = new Vector3(local.X * inertia.X, local.Y * inertia.Y, local.Z * inertia.Z);
            return Vector3.Transform(local, rotation);
        }

        public void Step(float dt)
        {
            var up = Up;
            var fwd = Forward;
            sim.PreStep(dt, Vector3.Dot(velocity, fwd), velocity.Length());
            int contacts = 0;
            MaxCompression = 0f;
            for (int i = 0; i < 4; i++)
            {
                var w = sim.wheels[i];
                var origin = World(w.mount);
                float reach = s.suspensionTravel + w.radius;
                if (world.Raycast(origin, -up, reach, out var hit))
                {
                    contacts++;
                    w.contact = true;
                    w.compression = s.suspensionTravel - (hit.distance - w.radius);
                    w.normalAlignment = Vector3.Dot(hit.normal, up);
                    w.surface = hit.surface;
                    wheelParts[i] = hit.part;
                    contactPoints[i] = hit.point;
                    var nrm = hit.normal;
                    var wf = Vector3.Transform(fwd, Quaternion.CreateFromAxisAngle(up, w.steer));
                    wf = Vector3.Normalize(wf - nrm * Vector3.Dot(wf, nrm));
                    var wr = Vector3.Normalize(Vector3.Cross(nrm, wf));
                    wheelForward[i] = wf;
                    wheelRight[i] = wr;
                    var pv = PointVelocity(hit.point);
                    w.longitudinalVelocity = Vector3.Dot(pv, wf);
                    w.lateralVelocity = Vector3.Dot(pv, wr);
                    MaxCompression = MathF.Max(MaxCompression, w.compression);
                }
                else
                {
                    w.contact = false;
                    w.compression = 0f;
                    wheelParts[i] = null;
                }
            }
            WheelContacts = contacts;
            sim.Step(dt);

            var force = new Vector3(0f, -Gravity * s.mass, 0f);
            var torque = Vector3.Zero;
            void AddAt(Vector3 f, Vector3 at)
            {
                force += f;
                torque += Vector3.Cross(at - position, f);
            }
            for (int i = 0; i < 4; i++)
            {
                var w = sim.wheels[i];
                if (!w.contact) continue;
                AddAt(up * w.load, World(w.mount));
                AddAt(wheelForward[i] * w.fx + wheelRight[i] * w.fy, contactPoints[i]);
            }
            AddAt(-up * sim.DownforceFront, World(new Vector3(0f, 0f, s.FrontAxleZ)));
            AddAt(-up * sim.DownforceRear, World(new Vector3(0f, 0f, s.RearAxleZ)));
            if (velocity.LengthSquared() > 0.01f) force += -Vector3.Normalize(velocity) * sim.Drag;

            // Integrate velocities (semi-implicit Euler), Unity angular damping 0.05.
            velocity += force / s.mass * dt;
            var gyro = Vector3.Cross(angularVelocity, InertiaWorld(angularVelocity));
            angularVelocity += InvInertiaWorld(torque - gyro) * dt;
            angularVelocity *= 1f / (1f + dt * 0.05f);
            if (angularVelocity.Length() > 30f) angularVelocity = Vector3.Normalize(angularVelocity) * 30f;

            ResolveBodyContacts(dt);

            position += velocity * dt;
            var wq = new Quaternion(angularVelocity * (0.5f * dt), 0f);
            rotation = Quaternion.Normalize(rotation + wq * rotation);
            time += dt;
        }

        /// <summary>Body box bottom and wheel spheres against the surface: grounding detection and contact impulses.</summary>
        void ResolveBodyContacts(float dt)
        {
            BodyContacts = 0;
            MaxPenetration = float.NegativeInfinity;
            BodyImpulse = 0f;
            MinClearance = float.PositiveInfinity;
            ClearancePart = null;
            var pts = new List<(Vector3 p, Vector3 n, float pen)>();
            foreach (var lp in bodyPoints)
            {
                var p = World(lp);
                if (!world.SurfaceBelow(p.X, p.Z, p.Y + 0.6f, p.Y - 0.6f, out var hit)) continue;
                float gap = p.Y - hit.point.Y;
                if (gap < MinClearance) { MinClearance = gap; ClearancePart = hit.part; }
                if (gap < ContactOffset) pts.Add((p, hit.normal, -gap));
            }
            foreach (var (c, r) in spheres)
            {
                var p = World(c);
                if (!world.SurfaceBelow(p.X, p.Z, p.Y + 0.6f, p.Y - 0.9f, out var hit)) continue;
                float gap = p.Y - r - hit.point.Y;
                if (gap < ContactOffset) pts.Add((p - new Vector3(0f, r, 0f), hit.normal, -gap));
            }
            foreach (var c in pts) MaxPenetration = MathF.Max(MaxPenetration, c.pen);
            BodyContacts = pts.Count;
            if (pts.Count == 0) return;
            // Sequential impulses, no restitution, friction 0.3, depenetration at most 10 m/s (PhysX default).
            for (int iter = 0; iter < 6; iter++)
                foreach (var (p, nrm, pen) in pts)
                {
                    var r = p - position;
                    var vrel = PointVelocity(p);
                    float vn = Vector3.Dot(vrel, nrm);
                    // Speculative contact (PhysX): with a gap the body may still approach by the gap this step;
                    // when penetrating it is pushed out at up to 10 m/s.
                    float target = pen > 0f ? MathF.Min(10f, pen * 0.2f / dt) : pen / dt;
                    if (vn >= target) continue;
                    var rn = Vector3.Cross(r, nrm);
                    float k = 1f / s.mass + Vector3.Dot(nrm, Vector3.Cross(InvInertiaWorld(rn), r));
                    float j = (target - vn) / k;
                    var impulse = nrm * j;
                    var vt = vrel - nrm * vn;
                    float vtl = vt.Length();
                    if (vtl > 1e-4f)
                    {
                        var tdir = vt / vtl;
                        float kt = 1f / s.mass + Vector3.Dot(tdir, Vector3.Cross(InvInertiaWorld(Vector3.Cross(r, tdir)), r));
                        float jt = MathF.Min(vtl / kt, 0.3f * j);
                        impulse -= tdir * jt;
                    }
                    velocity += impulse / s.mass;
                    angularVelocity += InvInertiaWorld(Vector3.Cross(r, impulse));
                    BodyImpulse += j;
                }
        }
    }
}
