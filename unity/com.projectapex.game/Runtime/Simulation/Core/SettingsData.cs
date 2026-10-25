using System;

namespace Apex.Core
{
    public enum ControlPreset
    {
        Keyboard = 0,
        TouchButtons = 1,
        Tilt = 2,
    }

    /// <summary>
    /// Persistent player settings. Serialised by the engine layer (JsonUtility);
    /// versioned so old files migrate instead of breaking.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public ControlPreset controlPreset = ControlPreset.Keyboard;
        public bool tractionControl = true;
        public bool abs = true;
        public bool steeringAssist = true;
        public bool autoGearbox = true;
        /// <summary>Pit assist: the car drives itself through the pit lane and stops in its box.</summary>
        public bool pitAssist = true;
        public bool reducedMotion;
        /// <summary>0 cockpit, 1 T-cam (default, preferred in playtests), 2 chase, 3 far chase.</summary>
        public int cameraMode = 1;
        public bool showTelemetry;
        public float masterVolume = 0.8f;
        public float tiltMaxAngleDeg = 35f;
        public float tiltDeadZoneDeg = 2f;
        public float tiltSensitivity = 1f;
        public float tiltSmoothing = 0.05f;
        /// <summary>Safety switch in case a device reports mirrored sensor axes.</summary>
        public bool invertTilt;
        public float touchButtonScale = 1f;
        public float touchButtonOpacity = 0.35f;
        public bool leftHanded;
        public bool speedInMph;
        public float hudScale = 1f;
        public string bestLapsJson = "";

        /// <summary>Upgrades older versions and clamps out-of-range values. Returns true if changed.</summary>
        public bool MigrateAndValidate()
        {
            bool changed = false;
            if (version < 1)
            {
                // v0 had no tilt settings; defaults above already apply.
                version = 1;
                changed = true;
            }
            changed |= ClampField(ref masterVolume, 0f, 1f);
            changed |= ClampField(ref tiltMaxAngleDeg, 15f, 70f);
            changed |= ClampField(ref tiltDeadZoneDeg, 0f, 8f);
            changed |= ClampField(ref tiltSensitivity, 0.5f, 2f);
            changed |= ClampField(ref tiltSmoothing, 0f, 0.3f);
            changed |= ClampField(ref touchButtonScale, 0.7f, 1.5f);
            changed |= ClampField(ref touchButtonOpacity, 0.1f, 1f);
            changed |= ClampField(ref hudScale, 0.75f, 1.25f);
            if (!Enum.IsDefined(typeof(ControlPreset), controlPreset))
            {
                controlPreset = ControlPreset.Keyboard;
                changed = true;
            }
            return changed;
        }

        static bool ClampField(ref float v, float min, float max)
        {
            if (float.IsNaN(v)) { v = min; return true; }
            float c = ApexMath.Clamp(v, min, max);
            if (c == v) return false;
            v = c;
            return true;
        }
    }
}
