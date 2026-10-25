using System;
using Apex.Core;

namespace Apex.Vehicle
{
    public enum TireCompound
    {
        Soft,
        Medium,
        Hard,
        Intermediate,
        Wet,
    }

    /// <summary>Per-compound characteristics (fictional series, tuned for gameplay).</summary>
    public readonly struct CompoundSpec
    {
        public readonly string name;
        public readonly string shortName;
        public readonly float muScale;
        public readonly float optimalTemp;
        public readonly float window;
        public readonly float wearRate;
        public readonly bool wetTire;

        public CompoundSpec(string name, string shortName, float muScale, float optimalTemp, float window, float wearRate, bool wetTire)
        {
            this.name = name;
            this.shortName = shortName;
            this.muScale = muScale;
            this.optimalTemp = optimalTemp;
            this.window = window;
            this.wearRate = wearRate;
            this.wetTire = wetTire;
        }

        public static CompoundSpec For(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return new CompoundSpec("Soft", "S", 1.04f, 95f, 25f, 1.6f, false);
                case TireCompound.Hard: return new CompoundSpec("Hart", "H", 0.965f, 105f, 35f, 0.65f, false);
                case TireCompound.Intermediate: return new CompoundSpec("Intermediate", "I", 1f, 65f, 30f, 1.2f, true);
                case TireCompound.Wet: return new CompoundSpec("Regen", "W", 1f, 50f, 30f, 1.0f, true);
                default: return new CompoundSpec("Medium", "M", 1f, 100f, 30f, 1f, false);
            }
        }

        /// <summary>Grip vs track wetness (0 dry .. 1 standing water), before temperature/wear.</summary>
        public static float WetFactor(TireCompound c, float wetness)
        {
            wetness = ApexMath.Clamp01(wetness);
            switch (c)
            {
                case TireCompound.Intermediate:
                    return wetness < 0.4f ? ApexMath.Lerp(0.86f, 0.83f, wetness / 0.4f) : ApexMath.Lerp(0.83f, 0.62f, (wetness - 0.4f) / 0.6f);
                case TireCompound.Wet:
                    return wetness < 0.4f ? ApexMath.Lerp(0.78f, 0.76f, wetness / 0.4f) : ApexMath.Lerp(0.76f, 0.72f, (wetness - 0.4f) / 0.6f);
                default:
                    return 1f - 0.58f * MathF.Pow(wetness, 0.8f);
            }
        }
    }

    /// <summary>
    /// Tire condition: single lumped temperature heated by carcass flexing (load ×
    /// sqrt(speed)) and slip power, cooled by airflow and water; wear driven by slip
    /// energy. Produces a grip multiplier from compound, temperature window, wear and
    /// track wetness. Calibrated so a clean lap settles near the window (~95-105 °C).
    /// </summary>
    public sealed class TireState
    {
        public TireCompound compound = TireCompound.Medium;
        /// <summary>Lumped tread temperature (°C).</summary>
        public float temperature = 92f;
        /// <summary>0 = new, 1 = worn out.</summary>
        public float wear;
        /// <summary>°C/s per N·sqrt(m/s) of load × sqrt(speed) (carcass hysteresis).</summary>
        public float rollingHeat = 1.22e-4f;
        /// <summary>°C per kJ of slip energy.</summary>
        public float heatRate = 0.03f;
        /// <summary>Cooling multiplier (wider rear tires shed more heat).</summary>
        public float coolingScale = 1f;
        /// <summary>Wear per kJ of slip energy for a medium tire (before multiplier).</summary>
        public float wearPerKJ = 2e-5f;
        /// <summary>Gameplay multiplier so strategy matters in short races.</summary>
        public float wearMultiplier = 1f;

        public CompoundSpec Spec => CompoundSpec.For(compound);

        /// <summary>Fits a fresh tire. Default start temperature models tire blankets
        /// (slightly below the window's centre).</summary>
        public void Fit(TireCompound c, float startTemp = float.NaN)
        {
            compound = c;
            temperature = float.IsNaN(startTemp) ? CompoundSpec.For(c).optimalTemp - 8f : startTemp;
            wear = 0f;
        }

        public float TemperatureFactor()
        {
            var s = Spec;
            float x = (temperature - s.optimalTemp) / s.window;
            return MathF.Max(0.72f, 1f - 0.25f * MathF.Min(1f, x * x));
        }

        public float WearFactor()
        {
            float f = 1f - 0.18f * MathF.Pow(wear, 1.5f);
            // Past ~90 % the tire falls off the "cliff".
            if (wear > 0.9f) f -= (wear - 0.9f) * 1.5f;
            return MathF.Max(0.5f, f);
        }

        public float GripMultiplier(float wetness) =>
            Spec.muScale * TemperatureFactor() * WearFactor() * CompoundSpec.WetFactor(compound, wetness);

        /// <param name="slipPowerW">Frictional power at the contact patch (W).</param>
        public float LastSlipPower { get; private set; }

        public void Update(float dt, float slipPowerW, float load, float speed, float trackTemp, float wetness)
        {
            LastSlipPower = slipPowerW;
            float kj = slipPowerW * dt / 1000f;
            float ambient = trackTemp - wetness * 15f;
            float cool = (0.02f + 0.0005f * speed + wetness * 0.05f) * coolingScale;
            temperature += rollingHeat * load * MathF.Sqrt(MathF.Max(speed, 0f)) * dt + heatRate * kj - cool * (temperature - ambient) * dt;
            wear = MathF.Min(1f, wear + Spec.wearRate * wearPerKJ * wearMultiplier * kj);
        }
    }
}
