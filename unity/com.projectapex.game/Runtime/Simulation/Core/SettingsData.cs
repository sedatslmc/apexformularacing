using System;

namespace Apex.Core
{
    public enum ControlPreset
    {
        Keyboard = 0,
        TouchButtons = 1,
        Tilt = 2,
    }

    /// <summary>Assist presets. Balanced is the default: realistic grip and inertia, assists
    /// that help without adding grip or steering the car onto the racing line.</summary>
    public enum DrivingProfile
    {
        Casual = 0,
        Balanced = 1,
        Expert = 2,
        Custom = 3,
    }

    /// <summary>
    /// Persistent player settings. Serialised by the engine layer (JsonUtility);
    /// versioned so old files migrate instead of breaking.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public ControlPreset controlPreset = ControlPreset.Keyboard;
        public DrivingProfile drivingProfile = DrivingProfile.Balanced;
        /// <summary>0 off, 1 low, 2 medium, 3 high.</summary>
        public int tractionLevel = 2;
        public bool abs = true;
        /// <summary>0 off, 1 light, 2 full.</summary>
        public int steeringAssistLevel = 1;
        public bool autoGearbox = true;
        /// <summary>Speed-sensitive steering: input filter limiting lock at speed (not a grip aid).</summary>
        public bool speedSensitiveSteering = true;
        /// <summary>Front brake bias (share of brake torque on the front axle).</summary>
        public float brakeBiasFront = 0.57f;
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
        /// <summary>Environment density: 0 = low (mobile), 1 = medium, 2 = high, 3 = extreme (PC).</summary>
        public int environmentDetail = 1;
        /// <summary>Camera motion blur at the tier's strength (High and above); off = no blur.</summary>
        public bool motionBlur = true;
        /// <summary>Main menu in front of the real Istanbul scene (PC; built behind the boot overlay).</summary>
        public bool menuBackdrop = true;
        /// <summary>URP post-processing (tonemapping, colour grade, bloom, FXAA); only offered with URP.</summary>
        public bool postProcessing = true;
        /// <summary>A/B comparison: the player's car uses the setup and input from before the October 2026
        /// handling overhaul (AI cars always use the current one). Applies from the next session.</summary>
        public bool legacyHandling;
        /// <summary>Road textures: the fetched 4K CC0 sets when present (false = the committed 1K sets). From the next session.</summary>
        public bool ultraTextures = true;
        /// <summary>Graphics tier (<see cref="GraphicsTier"/>); -1 = automatic for the device. Render settings apply live.</summary>
        public int graphicsTier = -1;
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
            if (version < 2)
            {
                // v1 stored on/off assists; v2 introduces profiles and assist levels.
                ApplyProfile(DrivingProfile.Balanced);
                version = 2;
                changed = true;
            }
            if (version < 3)
            {
                // v3: revised Expert preset and speed-sensitive steering; re-apply presets.
                if (drivingProfile != DrivingProfile.Custom) ApplyProfile(drivingProfile);
                speedSensitiveSteering = true;
                version = 3;
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
            changed |= ClampField(ref brakeBiasFront, 0.5f, 0.65f);
            changed |= ClampInt(ref tractionLevel, 0, 3);
            changed |= ClampInt(ref steeringAssistLevel, 0, 2);
            changed |= ClampInt(ref environmentDetail, 0, GraphicsPreset.EnvironmentLevels - 1);
            changed |= ClampInt(ref graphicsTier, -1, GraphicsPreset.TierCount - 1);
            if (!Enum.IsDefined(typeof(DrivingProfile), drivingProfile))
            {
                ApplyProfile(DrivingProfile.Balanced);
                changed = true;
            }
            if (!Enum.IsDefined(typeof(ControlPreset), controlPreset))
            {
                controlPreset = ControlPreset.Keyboard;
                changed = true;
            }
            return changed;
        }

        /// <summary>Sets the assists of a preset (Custom keeps the current values).</summary>
        public void ApplyProfile(DrivingProfile profile)
        {
            drivingProfile = profile;
            switch (profile)
            {
                case DrivingProfile.Casual:
                    tractionLevel = 3;
                    abs = true;
                    steeringAssistLevel = 2;
                    autoGearbox = true;
                    speedSensitiveSteering = true;
                    break;
                case DrivingProfile.Balanced:
                    tractionLevel = 2;
                    abs = true;
                    steeringAssistLevel = 1;
                    autoGearbox = true;
                    speedSensitiveSteering = true;
                    break;
                case DrivingProfile.Expert:
                    // Keys and touch buttons give 0/100 % pedals: without ABS and TC the
                    // car is not drivable with them (keyboard lap test), so Expert keeps
                    // ABS and low TC. "Custom" can switch everything off.
                    tractionLevel = 1;
                    abs = true;
                    steeringAssistLevel = 0;
                    autoGearbox = true;
                    speedSensitiveSteering = true;
                    break;
            }
        }

        /// <summary>Steering assist strength for the simulation (0 off .. 1 full).</summary>
        public float SteeringAssistStrength => steeringAssistLevel <= 0 ? 0f : steeringAssistLevel == 1 ? 0.5f : 1f;

        public static string ProfileName(DrivingProfile p)
        {
            switch (p)
            {
                case DrivingProfile.Casual: return "Einsteiger";
                case DrivingProfile.Balanced: return "Ausgewogen";
                case DrivingProfile.Expert: return "Profi";
                default: return "Individuell";
            }
        }

        static bool ClampInt(ref int v, int min, int max)
        {
            int c = v < min ? min : v > max ? max : v;
            if (c == v) return false;
            v = c;
            return true;
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
