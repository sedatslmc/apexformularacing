using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Vehicle
{
    /// <summary>
    /// Planar (3-DOF) test rig that drives a <see cref="VehicleSimulation"/> on flat
    /// ground without a game engine: body-frame velocities, yaw, quasi-static
    /// longitudinal/lateral load transfer and aero load. Used by unit tests and
    /// tuning tools to measure acceleration, braking and cornering. It is not the
    /// in-game rigid body (that is the engine's physics), so results are indicative.
    /// Conventions match Unity: x right/east, z forward/north, yaw positive = clockwise.
    /// </summary>
    public sealed class FlatGroundRig
    {
        public readonly VehicleSimulation sim;
        public Vector2 positionXZ;
        /// <summary>Heading (rad) measured clockwise from +z (north).</summary>
        public float heading;
        /// <summary>Forward velocity (m/s).</summary>
        public float u;
        /// <summary>Rightward velocity (m/s).</summary>
        public float v;
        /// <summary>Yaw rate (rad/s), positive = turning right.</summary>
        public float yawRate;
        public float time;
        /// <summary>Lateral load-transfer share carried by the front axle.</summary>
        public float frontRollShare = 0.55f;

        float ax, ay;

        public FlatGroundRig(VehicleSimulation sim)
        {
            this.sim = sim;
        }

        public Vector3 Position => new Vector3(positionXZ.X, 0f, positionXZ.Y);
        public Vector3 Forward => new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading));
        public float Speed => MathF.Sqrt(u * u + v * v);

        public void Place(Vector3 position, Vector3 forward)
        {
            positionXZ = new Vector2(position.X, position.Z);
            heading = MathF.Atan2(forward.X, forward.Z);
            u = v = yawRate = ax = ay = 0f;
            sim.Reset();
        }

        /// <summary>Sets a rolling start: forward speed, free-rolling wheels, gear, synced engine.</summary>
        public void SetSpeed(float forwardSpeed, int gear)
        {
            u = forwardSpeed;
            foreach (var w in sim.wheels) w.omega = forwardSpeed / w.radius;
            sim.gearbox.gear = gear;
            sim.SyncEngineToWheels();
        }

        public void Step(float dt)
        {
            var s = sim.setup;
            sim.PreStep(dt, u, Speed);
            float m = s.mass;
            float g = 9.81f;
            float longTransfer = m * ax * s.cogHeight / s.wheelbase;
            for (int i = 0; i < 4; i++)
            {
                var w = sim.wheels[i];
                float axleStatic = m * g * (w.front ? s.weightFront : 1f - s.weightFront) * 0.5f;
                // Aero load from the previous step (ride-height dependent ground effect).
                float aero = (w.front ? sim.DownforceFront : sim.DownforceRear) * 0.5f;
                float lt = (w.front ? -longTransfer : longTransfer) * 0.5f;
                float track = w.front ? s.trackFront : s.trackRear;
                float share = w.front ? frontRollShare : 1f - frontRollShare;
                // ay > 0 (turning right) loads the left (outside) wheels.
                float latT = m * ay * s.cogHeight / track * share;
                w.loadOverride = MathF.Max(0f, axleStatic + aero + lt + (w.left ? latT : -latT));
                w.contact = true;
                // Quasi-static ride height: the spring carries the wheel load.
                w.compression = w.loadOverride / w.spring;
                float px = w.mount.X, pz = w.mount.Z;
                float ui = u - yawRate * px;
                float vi = v + yawRate * pz;
                float cs = MathF.Cos(w.steer), sn = MathF.Sin(w.steer);
                w.longitudinalVelocity = ui * cs + vi * sn;
                w.lateralVelocity = vi * cs - ui * sn;
            }
            sim.Step(dt);
            float fu = 0f, fv = 0f, mz = 0f;
            for (int i = 0; i < 4; i++)
            {
                var w = sim.wheels[i];
                float cs = MathF.Cos(w.steer), sn = MathF.Sin(w.steer);
                float wu = w.fx * cs - w.fy * sn;
                float wv = w.fx * sn + w.fy * cs;
                fu += wu;
                fv += wv;
                mz += w.mount.Z * wv - w.mount.X * wu;
            }
            float speed = Speed;
            if (speed > 0.01f)
            {
                fu -= sim.Drag * u / speed;
                fv -= sim.Drag * v / speed;
            }
            ax = fu / m;
            ay = fv / m;
            u += (ax + v * yawRate) * dt;
            v += (ay - u * yawRate) * dt;
            yawRate += mz / s.inertiaYaw * dt;
            heading += yawRate * dt;
            var fwd = new Vector2(MathF.Sin(heading), MathF.Cos(heading));
            var right = new Vector2(MathF.Cos(heading), -MathF.Sin(heading));
            positionXZ += (fwd * u + right * v) * dt;
            time += dt;
        }
    }
}
