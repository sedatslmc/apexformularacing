using System;
using System.Numerics;
using Apex.Core;

namespace Apex.InputLogic
{
    public enum DeviceOrientation
    {
        Portrait,
        PortraitUpsideDown,
        LandscapeLeft,
        LandscapeRight,
    }

    /// <summary>
    /// Gyro/tilt steering: the phone is held in landscape like a steering wheel and
    /// rotated about the axis perpendicular to the screen. The roll angle comes from
    /// the gravity vector projected into screen space, measured relative to a
    /// calibrated neutral pose, so compass heading and how far the phone is tilted
    /// back do not matter.
    /// </summary>
    [Serializable]
    public sealed class TiltSteering
    {
        /// <summary>Phone rotation that gives full steering lock (deg).</summary>
        public float maxAngleDeg = 35f;
        public float deadZoneDeg = 2f;
        /// <summary>Multiplier on the normalised input before the curve.</summary>
        public float sensitivity = 1f;
        /// <summary>Response curve exponent; &gt;1 gives finer control near centre.</summary>
        public float curveExponent = 1.35f;
        /// <summary>Exponential smoothing time constant (s); 0 = none.</summary>
        public float smoothingTime = 0.05f;

        public float NeutralDeg { get; private set; }
        public bool IsCalibrated { get; private set; }
        public float Output { get; private set; }

        /// <summary>
        /// Screen-space roll (deg, positive = rotated clockwise = steer right) from a
        /// gravity vector in device coordinates (Unity convention: x right, y up in
        /// natural portrait, z out of the screen; upright portrait gravity = (0,-1,0)).
        /// </summary>
        public static float ScreenRollDeg(Vector3 gravityDevice, DeviceOrientation orientation)
        {
            float gx, gy;
            switch (orientation)
            {
                case DeviceOrientation.LandscapeLeft:
                    // Device rotated 90° CCW: screen right = -device y, screen up = device x.
                    gx = -gravityDevice.Y;
                    gy = gravityDevice.X;
                    break;
                case DeviceOrientation.LandscapeRight:
                    // Device rotated 90° CW: screen right = device y, screen up = -device x.
                    gx = gravityDevice.Y;
                    gy = -gravityDevice.X;
                    break;
                case DeviceOrientation.PortraitUpsideDown:
                    gx = -gravityDevice.X;
                    gy = -gravityDevice.Y;
                    break;
                default:
                    gx = gravityDevice.X;
                    gy = gravityDevice.Y;
                    break;
            }
            // Upright: screen gravity (0,-1) -> 0°. Rotating the phone clockwise by
            // theta turns screen-space gravity to (sin theta, -cos theta).
            return MathF.Atan2(gx, -gy) * ApexMath.Rad2Deg;
        }

        /// <summary>
        /// How reliable the roll reading is: 1 when the screen is vertical, 0 when the
        /// phone lies flat (gravity along the screen normal, roll undefined).
        /// </summary>
        public static float Reliability(Vector3 gravityDevice)
        {
            float len = gravityDevice.Length();
            if (len < 1e-4f) return 0f;
            float inPlane = MathF.Sqrt(gravityDevice.X * gravityDevice.X + gravityDevice.Y * gravityDevice.Y) / len;
            return ApexMath.Clamp01((inPlane - 0.2f) / 0.3f);
        }

        public void Calibrate(float rollDeg)
        {
            NeutralDeg = rollDeg;
            IsCalibrated = true;
            Output = 0f;
        }

        /// <summary>Maps a roll reading to steering in [-1, 1] (no smoothing).</summary>
        public float Map(float rollDeg)
        {
            float rel = ApexMath.WrapAngle((rollDeg - NeutralDeg) * ApexMath.Deg2Rad) * ApexMath.Rad2Deg;
            float mag = MathF.Abs(rel) - deadZoneDeg;
            if (mag <= 0f) return 0f;
            float range = MathF.Max(maxAngleDeg - deadZoneDeg, 1f);
            float x = ApexMath.Clamp01(mag / range * sensitivity);
            return MathF.Sign(rel) * MathF.Pow(x, curveExponent);
        }

        /// <summary>Mapped and smoothed steering; reliability fades input to centre when the phone is flat.</summary>
        public float Process(float rollDeg, float dt, float reliability = 1f)
        {
            float target = Map(rollDeg) * ApexMath.Clamp01(reliability);
            if (smoothingTime <= 0f) Output = target;
            else Output += (target - Output) * (1f - MathF.Exp(-dt / smoothingTime));
            return Output;
        }
    }
}
