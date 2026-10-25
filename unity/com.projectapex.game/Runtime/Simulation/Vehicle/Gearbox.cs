using System;

namespace Apex.Vehicle
{
    /// <summary>
    /// Sequential seamless-shift gearbox with optional automatic shifting.
    /// Gear 0 = neutral, -1 = reverse, 1..n = forward.
    /// </summary>
    [Serializable]
    public class Gearbox
    {
        public float[] ratios = { 6.8f, 5.55f, 4.53f, 3.70f, 3.02f, 2.47f, 2.01f, 1.66f };
        public float reverseRatio = 3.2f;
        public float finalDrive = 3.0f;
        public float efficiency = 0.92f;
        /// <summary>Torque interruption per shift (s).</summary>
        public float shiftTime = 0.05f;
        public float upshiftRpm = 12000f;
        public float downshiftRpm = 8000f;
        /// <summary>Minimum time between automatic shifts; prevents gear hunting.</summary>
        public float autoShiftInterval = 0.45f;

        [NonSerialized] public int gear = 1;
        [NonSerialized] float shiftTimer;
        [NonSerialized] float sinceShift = 10f;

        public int GearCount => ratios.Length;
        public bool IsShifting => shiftTimer > 0f;

        public float TotalRatio
        {
            get
            {
                if (gear == 0) return 0f;
                if (gear < 0) return -reverseRatio * finalDrive;
                return ratios[gear - 1] * finalDrive;
            }
        }

        public float RatioFor(int g) => g <= 0 ? 0f : ratios[g - 1] * finalDrive;

        public void Reset(int newGear = 1)
        {
            gear = newGear;
            shiftTimer = 0f;
            sinceShift = 10f;
        }

        public bool ShiftUp()
        {
            if (gear >= GearCount) return false;
            gear++;
            shiftTimer = shiftTime;
            sinceShift = 0f;
            return true;
        }

        public bool ShiftDown()
        {
            if (gear <= -1) return false;
            gear--;
            shiftTimer = shiftTime;
            sinceShift = 0f;
            return true;
        }

        public void Update(float dt)
        {
            shiftTimer = Math.Max(0f, shiftTimer - dt);
            sinceShift += dt;
        }

        /// <summary>
        /// Automatic shift decision. <paramref name="wheelRpm"/> must come from road
        /// speed, not a spinning wheel, or wheelspin causes premature upshifts.
        /// </summary>
        public bool AutoShift(float wheelRpm, float throttle, float brake, float forwardSpeed)
        {
            if (IsShifting || sinceShift < autoShiftInterval) return false;
            if (gear <= 0)
            {
                if (forwardSpeed > -1f && throttle > 0.1f)
                {
                    gear = 1;
                    return true;
                }
                return false;
            }
            float rpm = wheelRpm * RatioFor(gear);
            if (rpm > upshiftRpm && gear < GearCount && throttle > 0.05f) return ShiftUp();
            if (gear > 1)
            {
                float lowerRpm = wheelRpm * RatioFor(gear - 1);
                // Downshift earlier while braking so the engine is ready on exit.
                float threshold = downshiftRpm + (brake > 0.2f ? 1500f : 0f);
                if (rpm < threshold && lowerRpm < upshiftRpm - 1200f) return ShiftDown();
            }
            return false;
        }
    }
}
