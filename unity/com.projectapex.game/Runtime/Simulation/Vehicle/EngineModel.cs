using System;
using Apex.Core;

namespace Apex.Vehicle
{
    /// <summary>Fictional hybrid power unit: torque curve, limiter, engine braking.</summary>
    [Serializable]
    public class EngineModel
    {
        public float idleRpm = 4000f;
        public float maxRpm = 12500f;
        public float limiterRpm = 12300f;
        /// <summary>Peak crank torque (N·m), combined combustion + electric deployment.</summary>
        public float peakTorque = 600f;
        /// <summary>Normalised full-throttle torque sampled every 1000 rpm from 0 rpm.</summary>
        public float[] torqueCurve =
        {
            0.45f, 0.50f, 0.56f, 0.62f, 0.70f, 0.78f, 0.85f, 0.91f,
            0.96f, 0.99f, 1.00f, 0.99f, 0.95f, 0.88f,
        };
        /// <summary>Closed-throttle braking torque at max rpm (N·m).</summary>
        public float engineBrakeTorque = 110f;

        public float CurveAt(float rpm)
        {
            float x = ApexMath.Clamp(rpm / 1000f, 0f, torqueCurve.Length - 1);
            int i = Math.Min((int)x, torqueCurve.Length - 2);
            return ApexMath.Lerp(torqueCurve[i], torqueCurve[i + 1], x - i);
        }

        /// <summary>Net crank torque for throttle in [0,1], including engine braking and limiter cut.</summary>
        public float Torque(float rpm, float throttle)
        {
            float drive = peakTorque * CurveAt(rpm) * throttle;
            if (rpm >= limiterRpm) drive = 0f;
            float braking = engineBrakeTorque * ApexMath.Clamp01(rpm / maxRpm) * (1f - throttle);
            return drive - braking;
        }

        public float PowerKw(float rpm) => Torque(rpm, 1f) * rpm * ApexMath.Tau / 60f / 1000f;
    }
}
