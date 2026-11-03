using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// The pit crew's movements during a stop (docs/PIT_LANE.md). Pure timing and positions, so the stop is tested
    /// without Unity and plays exactly as long as the race logic's service (PitState.serviceTotal):
    /// <list type="number">
    /// <item>Waiting: the crew stands in front of its garage.</item>
    /// <item>Ready: when its car is in the lane and close, everyone runs to a spot just outside the box.</item>
    /// <item>Service (car stopped, t = 0 … T): jacks in and up, guns on, old wheels off, new wheels on, guns tighten,
    /// jacks down, release.</item>
    /// <item>Return: after the car has gone, back to the garage.</item>
    /// </list>
    /// Positions are in the box frame: the car's frame where it stops (x = right of the driving direction, z = driving
    /// direction), origin at the box centre, metres. <c>garageSide</c> is +1 when the garage lies to the car's right,
    /// −1 to its left (the pit side of the circuit).
    /// </summary>
    public static class PitCrewChoreography
    {
        public enum Role
        {
            GunFL, OffFL, OnFL, GunFR, OffFR, OnFR, GunRL, OffRL, OnRL, GunRR, OffRR, OnRR,
            FrontJack, RearJack, StabiliserLeft, StabiliserRight, Release,
        }

        public static readonly Role[] Roles = (Role[])Enum.GetValues(typeof(Role));
        public const int CrewSize = 17;

        public enum Phase { Waiting, Ready, Service, Return }

        /// <summary>Car geometry the crew works around (VehicleSetup: axles, tracks, nose and tail).</summary>
        public struct Car
        {
            public float frontAxle, rearAxle, halfTrackFront, halfTrackRear, nose, tail;
            public static Car Default => new Car { frontAxle = 1.98f, rearAxle = -1.62f, halfTrackFront = 0.81f, halfTrackRear = 0.78f, nose = 3.3f, tail = -2.4f };
        }

        public struct Pose
        {
            /// <summary>Feet position in the box frame (y = 0 on the ground).</summary>
            public Vector3 position;
            /// <summary>Facing in the box frame (rad, 0 = +z, clockwise).</summary>
            public float yaw;
            /// <summary>0 standing … 1 kneeling (gun men at the wheel).</summary>
            public float kneel;
            /// <summary>A wheel in the hands (off man with the old one, on man with the new one).</summary>
            public bool carriesWheel;
            /// <summary>Holds a wheel gun / a jack / the release sign.</summary>
            public bool tool;
        }

        /// <summary>Time shares of the service (fractions of T).</summary>
        public const float JacksIn = 0.08f, Lifted = 0.14f, OffStart = 0.16f, OffEnd = 0.36f, OnStart = 0.38f, OnEnd = 0.62f,
            TightenEnd = 0.78f, JacksDown = 0.86f;
        /// <summary>Seconds to run from the garage to the ready spots, and back after the stop.</summary>
        public const float RunTime = 1.6f, ReturnTime = 2.4f;
        /// <summary>Distance from the garage front to the box centre (m): the crew waits there.</summary>
        public const float GarageX = 4.2f;

        static int Wheel(Role r) => r <= Role.OnRR ? (int)r / 3 : -1;           // 0 FL, 1 FR, 2 RL, 3 RR
        static int Job(Role r) => r <= Role.OnRR ? (int)r % 3 : -1;             // 0 gun, 1 off, 2 on

        /// <summary>Hub of a wheel in the box frame (car pointing +z, left wheels at −x).</summary>
        public static Vector3 Hub(Car car, int wheel)
        {
            bool front = wheel < 2, left = wheel % 2 == 0;
            float half = front ? car.halfTrackFront : car.halfTrackRear;
            return new Vector3(left ? -half : half, 0.35f, front ? car.frontAxle : car.rearAxle);
        }

        /// <summary>Where a role works during the service.</summary>
        public static Vector3 WorkSpot(Car car, Role r)
        {
            int w = Wheel(r);
            if (w >= 0)
            {
                var hub = Hub(car, w);
                float side = MathF.Sign(hub.X);
                switch (Job(r))
                {
                    case 0: return new Vector3(hub.X + side * 0.55f, 0f, hub.Z);
                    case 1: return new Vector3(hub.X + side * 1.15f, 0f, hub.Z - 0.55f);
                    default: return new Vector3(hub.X + side * 1.15f, 0f, hub.Z + 0.55f);
                }
            }
            switch (r)
            {
                case Role.FrontJack: return new Vector3(0f, 0f, car.nose + 0.7f);
                case Role.RearJack: return new Vector3(0f, 0f, car.tail - 0.7f);
                case Role.StabiliserLeft: return new Vector3(-1.25f, 0f, 0.3f);
                case Role.StabiliserRight: return new Vector3(1.25f, 0f, 0.3f);
                default: return new Vector3(0f, 0f, car.nose + 1.6f);     // release: ahead, in the driver's view (shifted to the garage side in PoseOf)
            }
        }

        /// <summary>Spot in front of the garage while waiting: two rows along the garage front.</summary>
        public static Vector3 WaitSpot(Role r, int garageSide)
        {
            int i = (int)r;
            float z = -6f + (i % 9) * 1.5f;
            float x = garageSide * (GarageX - 0.8f - (i / 9) * 1.1f);
            return new Vector3(x, 0f, z);
        }

        /// <summary>Ready spot: the work spot, 0.4 m further from the car (out of the arriving car's path).</summary>
        public static Vector3 ReadySpot(Car car, Role r)
        {
            var w = WorkSpot(car, r);
            if (r == Role.FrontJack) return w + new Vector3(0f, 0f, 0.8f);
            if (r == Role.RearJack) return w + new Vector3(0f, 0f, -0.6f);
            var outward = new Vector3(MathF.Sign(w.X == 0f ? 1f : w.X), 0f, 0f);
            return w + outward * 0.4f;   // 0.4 m: the far-side crew stays clear of the fast lane (PitLaneLayout)
        }

        static float Smooth(float t) { t = ApexMath.Clamp01(t); return t * t * (3f - 2f * t); }

        static float FacingTo(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return MathF.Atan2(d.X, d.Z);
        }

        /// <summary>
        /// Pose of a role. <paramref name="phase"/> and <paramref name="t"/>: Ready – seconds since the call to the
        /// box; Service – seconds since the car stopped (of <paramref name="serviceTotal"/>); Return – seconds since
        /// the release. <paramref name="changeTyres"/> false: the wheel crews stand by (repair only).
        /// </summary>
        public static Pose PoseOf(Role r, Phase phase, float t, float serviceTotal, bool changeTyres, Car car, int garageSide)
        {
            var wait = WaitSpot(r, garageSide);
            var ready = ReadySpot(car, r);
            var work = WorkSpot(car, r);
            if (r == Role.Release)
            {
                work.X = garageSide * 1.6f;
                ready = work + new Vector3(garageSide * 0.6f, 0f, 0f);
            }
            // The rear jack waits beside the box (the car rolls in from behind) and steps in behind it once it stops.
            if (r == Role.RearJack) ready = new Vector3(garageSide * 1.7f, 0f, car.tail - 0.9f);
            var carCentre = new Vector3(0f, 0f, 0.3f);
            switch (phase)
            {
                case Phase.Waiting:
                    return new Pose { position = wait, yaw = garageSide > 0 ? -MathF.PI * 0.5f : MathF.PI * 0.5f };   // facing the lane
                case Phase.Ready:
                {
                    float k = Smooth(t / RunTime);
                    var p = Vector3.Lerp(wait, ready, k);
                    return new Pose { position = p, yaw = k < 0.95f ? FacingTo(wait, ready) : FacingTo(ready, carCentre), tool = true, carriesWheel = changeTyres && Job(r) == 2 };
                }
                case Phase.Return:
                {
                    float k = Smooth(t / ReturnTime);
                    var p = Vector3.Lerp(ready, wait, k);
                    // The off man carries the old wheel back into the garage.
                    return new Pose { position = p, yaw = FacingTo(ready, wait), tool = true, carriesWheel = changeTyres && Job(r) == 1 };
                }
            }
            // Service.
            float T = MathF.Max(serviceTotal, 0.5f);
            float u = ApexMath.Clamp01(t / T);
            var pose = new Pose { tool = true, yaw = FacingTo(work, carCentre) };
            int wheel = Wheel(r);
            if (r == Role.FrontJack || r == Role.RearJack)
            {
                // In under the nose/tail, hold while lifted, out again after the jacks drop.
                float inK = Smooth(u / JacksIn), outK = Smooth((u - JacksDown) / (1f - JacksDown));
                pose.position = Vector3.Lerp(Vector3.Lerp(ready, work, inK), ready, outK);
                pose.yaw = r == Role.FrontJack ? MathF.PI : 0f;
                return pose;
            }
            if (wheel < 0)
            {
                pose.position = Vector3.Lerp(ready, work, Smooth(u / JacksIn));
                if (r == Role.Release) pose.yaw = FacingTo(work, new Vector3(0f, 0f, car.nose));
                return pose;
            }
            if (!changeTyres) { pose.position = ready; return pose; }
            var hub = Hub(car, wheel);
            var hubFloor = new Vector3(hub.X + MathF.Sign(hub.X) * 0.45f, 0f, hub.Z);
            switch (Job(r))
            {
                case 0:   // gun: in, kneel, off/on the nut twice, out after tightening
                    pose.position = Vector3.Lerp(Vector3.Lerp(ready, work, Smooth(u / OffStart)), ready, Smooth((u - TightenEnd) / 0.08f));
                    pose.kneel = u < TightenEnd + 0.04f ? Smooth(u / OffStart) : 1f - Smooth((u - TightenEnd - 0.04f) / 0.06f);
                    return pose;
                case 1:   // off: in to the hub, pull the old wheel, carry it out and set it down
                {
                    float inK = Smooth(u / OffStart);
                    float outK = Smooth((u - OffStart) / (OffEnd - OffStart));
                    pose.position = u < OffStart ? Vector3.Lerp(ready, hubFloor, inK) : Vector3.Lerp(hubFloor, work, outK);
                    pose.carriesWheel = u >= OffStart && u < OffEnd + 0.02f;
                    return pose;
                }
                default:  // on: waits with the new wheel, fits it once the old one is off, steps back
                {
                    float inK = Smooth((u - OnStart) / ((OnEnd - OnStart) * 0.6f));
                    float backK = Smooth((u - OnEnd) / 0.08f);
                    pose.position = Vector3.Lerp(Vector3.Lerp(ready, hubFloor, inK), ready, backK);
                    pose.carriesWheel = u < OnEnd;
                    return pose;
                }
            }
        }

        /// <summary>The car's wheel is off the hub (draw it in the off/on man's hands instead).</summary>
        public static bool WheelOffHub(float t, float serviceTotal, bool changeTyres)
        {
            if (!changeTyres) return false;
            float u = t / MathF.Max(serviceTotal, 0.5f);
            return u >= OffStart && u < OnEnd;
        }

        /// <summary>Jack lift of the car body (m) during the service.</summary>
        public static float Lift(float t, float serviceTotal)
        {
            float u = t / MathF.Max(serviceTotal, 0.5f);
            if (u <= JacksIn || u >= 1f) return 0f;
            return 0.06f * Smooth((u - JacksIn) / (Lifted - JacksIn)) * (1f - Smooth((u - JacksDown) / (1f - JacksDown)));
        }

        /// <summary>Box light above the box: red while the car must stay, green on release.</summary>
        public static bool ReleaseGreen(float t, float serviceTotal) => t >= serviceTotal - 0.15f;
    }
}
