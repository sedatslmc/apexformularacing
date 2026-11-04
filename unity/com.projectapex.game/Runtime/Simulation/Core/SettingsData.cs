using System;

namespace Apex.Core
{
    public enum ControlPreset
    {
        Keyboard = 0,
        TouchButtons = 1,
        Tilt = 2,
        /// <summary>Virtual steering zone: thumb travel steers (InputLogic.TouchWheel).</summary>
        TouchWheel = 3,
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
        /// <summary>Touch steering zone: screen widths of thumb travel for full lock (0.06 .. 0.25).</summary>
        public float touchWheelRange = 0.12f;
        /// <summary>Player key assignment (InputLogic.KeyBindings.Serialize); empty = the default keys.</summary>
        public string keyBindings = "";
        /// <summary>Keyboard steering profile (InputLogic.KeyboardSteering.Profile): 0 Direkt, 1 Ausgewogen, 2 Smooth.</summary>
        public int keyboardProfile = 1;
        /// <summary>"Lenkempfindlichkeit" 0.5 .. 1.5: ramp rates of the keys, curve of the stick.</summary>
        public float steeringSensitivity = 1f;
        /// <summary>"Lenkglättung" 0 .. 1: extra smoothing of digital steering (up to 60 ms).</summary>
        public float steeringSmoothing;
        /// <summary>Dynamic racing line: 0 off, 1 corners only, 2 whole lap (Apex.Race.RacingLineGuide).</summary>
        public int racingLineMode = 2;
        /// <summary>"Bremsempfehlung" of the racing line: 0 Anfänger, 1 Standard, 2 Experte (RacingLineGuide.AdviceScale).</summary>
        public int brakeAdvice = 1;
        /// <summary>Colour the racing line by the braking need at the current speed (off = green line only).</summary>
        public bool dynamicBrakeLine = true;
        /// <summary>Speed-sensitive steering: input filter limiting lock at speed (not a grip aid).</summary>
        public bool speedSensitiveSteering = true;
        /// <summary>Front brake bias (share of brake torque on the front axle).</summary>
        public float brakeBiasFront = 0.57f;
        /// <summary>Pit assist: the car drives itself through the pit lane and stops in its box.</summary>
        public bool pitAssist = true;
        /// <summary>Parking aid for a pit stop driven by hand: the crew settles the car onto the box marks (last centimetres).</summary>
        public bool pitParkAssist = true;
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
        /// <summary>HUD preset (<see cref="Apex.UI.HudPreset"/>: 0 Minimal, 1 Standard, 2 Erweitert, 3 Aus).</summary>
        public int hudPreset = 1;
        /// <summary>Player's HUD editor adjustments (position, size, opacity per element).</summary>
        public Apex.UI.HudLayoutData hudLayout = new Apex.UI.HudLayoutData();
        /// <summary>UI language of the new screens (<see cref="Apex.UI.Language"/>).</summary>
        public int language;
        /// <summary>HUD colours for colour-vision deficiencies (<see cref="Apex.UI.ColourVision"/>).</summary>
        public int colourVision;
        /// <summary>Opaque panels and brighter secondary text.</summary>
        public bool highContrast;
        /// <summary>Text size of the new screens (1.0 .. 1.3).</summary>
        public float uiTextScale = 1f;
        /// <summary>Menu music volume (0 = off).</summary>
        public float musicVolume = 0.5f;
        /// <summary>Environment density: 0 = low (mobile), 1 = medium, 2 = high, 3 = extreme (PC).</summary>
        public int environmentDetail = 1;
        /// <summary>Camera motion blur at the tier's strength (High and above); off = no blur.</summary>
        public bool motionBlur = true;
        /// <summary>Main menu in front of the real Istanbul scene (PC; built behind the boot overlay).</summary>
        public bool menuBackdrop = true;
        /// <summary>Opening intro: 0 the first time only, 1 at every start, 2 never.</summary>
        public int introMode;
        /// <summary>The opening intro has been shown once.</summary>
        public bool introSeen;
        /// <summary>Race start presentation (camera shots before the grid); time trial and practice get a short one.</summary>
        public bool raceIntro = true;
        /// <summary>UI sound volume (menus, confirmations), separate from the master and music volume.</summary>
        public float uiVolume = 0.8f;
        /// <summary>HUD elements switched off (bit = Apex.UI.HudElement: 4 tyres, 5 map, 6 timing).</summary>
        public int hudHidden;
        /// <summary>Display (desktop): 0 exclusive full screen, 1 borderless full screen, 2 window.</summary>
        public int displayMode = 1;
        /// <summary>Index into the screen's resolutions (-1 = native / current).</summary>
        public int resolutionIndex = -1;
        public bool vsync = true;
        /// <summary>Frame rate cap (0 = none; used when VSync is off, and always on mobile).</summary>
        public int fpsLimit = 0;
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
            changed |= ClampInt(ref hudPreset, 0, 3);
            changed |= ClampInt(ref language, 0, 1);
            changed |= ClampInt(ref colourVision, 0, 3);
            changed |= ClampField(ref uiTextScale, 1f, 1.3f);
            changed |= ClampField(ref musicVolume, 0f, 1f);
            if (hudLayout == null) { hudLayout = new Apex.UI.HudLayoutData(); changed = true; }
            changed |= hudLayout.Normalize();
            changed |= ClampField(ref brakeBiasFront, 0.5f, 0.65f);
            changed |= ClampInt(ref tractionLevel, 0, 3);
            changed |= ClampInt(ref steeringAssistLevel, 0, 2);
            changed |= ClampInt(ref keyboardProfile, 0, 2);
            changed |= ClampField(ref steeringSensitivity, 0.5f, 1.5f);
            changed |= ClampField(ref steeringSmoothing, 0f, 1f);
            changed |= ClampInt(ref racingLineMode, 0, 2);
            changed |= ClampInt(ref brakeAdvice, 0, 2);
            changed |= ClampField(ref touchWheelRange, 0.06f, 0.25f);
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
