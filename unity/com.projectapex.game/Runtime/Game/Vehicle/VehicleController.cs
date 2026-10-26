using System.Collections.Generic;
using Apex.Core;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Bridges <see cref="VehicleSimulation"/> to a Unity Rigidbody. Each FixedUpdate:
    /// raycast suspension per wheel, contact velocities in the steered wheel frame,
    /// simulation step, then suspension, tire and aero forces are applied. The body
    /// origin is the centre of mass; x right, y up, z forward (same as the sim).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        public VehicleSetup setup = new VehicleSetup();
        [Tooltip("Layers the wheels can stand on.")]
        public LayerMask groundMask = ~0;

        public VehicleSimulation Sim { get; private set; }
        public Rigidbody Body { get; private set; }
        public Transform[] WheelVisuals { get; set; } = new Transform[4];
        /// <summary>Optional spinning child per wheel; when set, the pivot only steers
        /// (uprights and calipers stay put) and the spinner rotates.</summary>
        public Transform[] WheelSpinners { get; set; }
        public float ForwardSpeed => Sim?.ForwardSpeed ?? 0f;

        readonly RaycastHit[] hits = new RaycastHit[8];
        readonly Dictionary<Collider, SurfaceType> surfaceCache = new Dictionary<Collider, SurfaceType>();
        readonly Vector3[] contactPoints = new Vector3[4];
        readonly Vector3[] wheelForward = new Vector3[4];
        readonly Vector3[] wheelRight = new Vector3[4];

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Sim = new VehicleSimulation(setup);
            Body.mass = setup.mass;
            Body.centerOfMass = Vector3.zero;
            Body.inertiaTensor = new Vector3(setup.inertiaPitch, setup.inertiaYaw, setup.inertiaRoll);
            Body.inertiaTensorRotation = Quaternion.identity;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.maxAngularVelocity = 30f;
            RigidbodyCompat.SetDamping(Body, 0f, 0.05f);
        }

        /// <summary>Teleports the car and clears its dynamic state.</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            RigidbodyCompat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            Sim.Reset();
            Physics.SyncTransforms();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var t = transform;
            Vector3 up = t.up;
            Vector3 fwd = t.forward;
            Vector3 vel = RigidbodyCompat.GetVelocity(Body);
            Sim.PreStep(dt, Vector3.Dot(vel, fwd), vel.magnitude);

            for (int i = 0; i < 4; i++)
            {
                var w = Sim.wheels[i];
                Vector3 origin = t.TransformPoint(w.mount.ToUnity());
                float reach = setup.suspensionTravel + w.radius;
                if (Raycast(origin, -up, reach, out RaycastHit hit))
                {
                    w.contact = true;
                    w.compression = setup.suspensionTravel - (hit.distance - w.radius);
                    w.normalAlignment = Vector3.Dot(hit.normal, up);
                    w.surface = SurfaceOf(hit.collider);
                    contactPoints[i] = hit.point;
                    Vector3 n = hit.normal;
                    Vector3 wf = Quaternion.AngleAxis(w.steer * ApexMath.Rad2Deg, up) * fwd;
                    wf = Vector3.ProjectOnPlane(wf, n).normalized;
                    Vector3 wr = Vector3.Cross(n, wf).normalized;
                    wheelForward[i] = wf;
                    wheelRight[i] = wr;
                    Vector3 pv = Body.GetPointVelocity(hit.point);
                    w.longitudinalVelocity = Vector3.Dot(pv, wf);
                    w.lateralVelocity = Vector3.Dot(pv, wr);
                }
                else
                {
                    w.contact = false;
                    w.compression = 0f;
                }
            }

            Sim.Step(dt);

            for (int i = 0; i < 4; i++)
            {
                var w = Sim.wheels[i];
                if (!w.contact) continue;
                Body.AddForceAtPosition(up * w.load, t.TransformPoint(w.mount.ToUnity()));
                Body.AddForceAtPosition(wheelForward[i] * w.fx + wheelRight[i] * w.fy, contactPoints[i]);
            }
            Body.AddForceAtPosition(-up * Sim.DownforceFront, t.TransformPoint(new Vector3(0f, 0f, setup.FrontAxleZ)));
            Body.AddForceAtPosition(-up * Sim.DownforceRear, t.TransformPoint(new Vector3(0f, 0f, setup.RearAxleZ)));
            if (vel.sqrMagnitude > 0.01f) Body.AddForce(-vel.normalized * Sim.Drag);
        }

        /// <summary>A part was damaged (description for the HUD/radio).</summary>
        public event System.Action<string> Damaged;

        void OnCollisionEnter(Collision collision)
        {
            if (Sim == null || !Sim.damageEnabled) return;
            Vector3 impulse = collision.impulse;
            float deltaV = impulse.magnitude / Body.mass;
            if (deltaV < DamageState.Threshold) return;
            Vector3 dirLocal = transform.InverseTransformDirection(impulse.normalized);
            // Landing on the ground / bottoming out is not a crash.
            if (Mathf.Abs(dirLocal.y) > 0.7f) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Vector3 local = transform.InverseTransformPoint(point);
            string hit = Sim.damage.ApplyImpact(setup, local.ToNum(), deltaV, dirLocal.ToNum());
            if (hit != null) Damaged?.Invoke(hit);
        }

        bool Raycast(Vector3 origin, Vector3 dir, float reach, out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, reach, groundMask, QueryTriggerInteraction.Ignore);
            best = default;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].rigidbody == Body) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = hits[i];
                }
            }
            return bestDist < float.MaxValue;
        }

        SurfaceType SurfaceOf(Collider c)
        {
            if (surfaceCache.TryGetValue(c, out var s)) return s;
            var tag = c.GetComponent<TrackSurface>();
            s = tag != null ? tag.surface : SurfaceType.Asphalt;
            surfaceCache[c] = s;
            return s;
        }

        void Update()
        {
            if (Sim == null) return;
            for (int i = 0; i < 4; i++)
            {
                var vis = WheelVisuals[i];
                if (vis == null) continue;
                var w = Sim.wheels[i];
                // Surface texture (kerb ripples) lifts the wheel visibly as well.
                float springLen = w.contact ? setup.suspensionTravel - w.compression - w.roadOffset : setup.suspensionTravel;
                var m = w.mount.ToUnity();
                vis.localPosition = new Vector3(m.x, m.y - springLen, m.z);
                var steer = Quaternion.Euler(0f, w.steer * ApexMath.Rad2Deg, 0f);
                var spin = Quaternion.Euler(w.spinAngle * ApexMath.Rad2Deg, 0f, 0f);
                var spinner = WheelSpinners != null ? WheelSpinners[i] : null;
                if (spinner != null)
                {
                    vis.localRotation = steer;
                    spinner.localRotation = spin;
                }
                else vis.localRotation = steer * spin;
            }
        }
    }
}
