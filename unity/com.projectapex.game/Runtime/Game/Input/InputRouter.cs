using System.Collections.Generic;
using Apex.Core;
using Apex.InputLogic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Apex.Game
{
    /// <summary>
    /// Single owner of player input. Merges keyboard, gamepad, on-screen touch
    /// buttons (with per-finger ownership) and tilt steering into one driver
    /// command, plus edge-triggered actions. Releases everything on focus loss so
    /// no input can stay stuck.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        /// <summary>The player drives <see cref="VehicleSetup.Legacy"/>: old steering ramps and linear stick too.</summary>
        public bool legacyHandling;
        public SettingsData settings = new SettingsData();
        public bool manualGears;
        /// <summary>Car speed (m/s), set by the game; makes digital steering more progressive at speed.</summary>
        public float vehicleSpeed;

        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        /// <summary>Steering of the last <see cref="SteerForPhysics"/> step (or the analogue / tilt value).</summary>
        public float Steer { get; private set; }

        /// <summary>How the player steers right now (decides the input curve and the front-slip limiter).</summary>
        public enum SteerSource { Digital, Stick, Tilt }
        public SteerSource Source { get; private set; } = SteerSource.Digital;
        /// <summary>Digital steering key state this frame (−1, 0, +1; keys and touch buttons).</summary>
        public float SteerKey { get; private set; }
        float analogSteer;
        /// <summary>Digital steering ramp and progressive curve (KeyboardSteering), run on the physics clock.</summary>
        public readonly KeyboardSteering keyboard = new KeyboardSteering();

        // Edge-triggered actions (valid for the frame they occur in).
        public bool ShiftUpPressed { get; private set; }
        public bool ShiftDownPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool CameraPressed { get; private set; }
        public bool TelemetryPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public bool RadioPressed { get; private set; }
        public bool PitPressed { get; private set; }
        public bool TowerPressed { get; private set; }
        public bool TowerHeld { get; private set; }
        public bool MapPressed { get; private set; }
        public bool HudPresetPressed { get; private set; }
        public bool HelpPressed { get; private set; }
        public bool LayoutDebugPressed { get; private set; }
        /// <summary>F6: start or stop a performance capture (PerfCapture).</summary>
        public bool PerfPressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool ScreenshotPressed { get; private set; }
        public bool NavUp { get; private set; }
        public bool NavDown { get; private set; }
        /// <summary>Number key 1-9 pressed this frame (0 = none).</summary>
        public int NumberPressed { get; private set; }
        /// <summary>When true, Up/Down arrows navigate the radio instead of driving.</summary>
        public bool radioOpen;

        public readonly TiltSteering tilt = new TiltSteering();
        public bool TiltAvailable { get; private set; }
        public float RollDeg { get; private set; }
        public float TiltReliability { get; private set; }
        public List<TouchLayout.Button> Buttons { get; private set; } = new List<TouchLayout.Button>();
        public readonly TouchOwnership touch = new TouchOwnership();

        readonly DigitalAxis throttleAxis = new DigitalAxis(7f, 7f, 7f);
        readonly DigitalAxis brakeAxis = new DigitalAxis(9f, 9f, 9f);
        readonly HashSet<int> heldTouch = new HashSet<int>();
        readonly HashSet<int> prevHeldTouch = new HashSet<int>();
        Vector2Int layoutScreen;
        ControlPreset layoutPreset;
        bool layoutManual;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // Lets the mouse act as a finger when a touch preset is tested in the editor.
            TouchSimulation.Enable();
#endif
            EnableSensors();
        }

        void OnDisable()
        {
            ReleaseAll();
        }

        void EnableSensors()
        {
            if (GravitySensor.current != null) InputSystem.EnableDevice(GravitySensor.current);
            else if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
            TiltAvailable = GravitySensor.current != null || Accelerometer.current != null;
        }

        public void ApplySettings(SettingsData s)
        {
            settings = s;
            tilt.maxAngleDeg = s.tiltMaxAngleDeg;
            tilt.deadZoneDeg = s.tiltDeadZoneDeg;
            tilt.sensitivity = s.tiltSensitivity;
            tilt.smoothingTime = s.tiltSmoothing;
            layoutScreen = Vector2Int.zero; // force relayout
        }

        public void CalibrateTilt()
        {
            tilt.Calibrate(RollDeg);
        }

        public void ReleaseAll()
        {
            touch.ReleaseAll();
            keyboard.Reset();
            throttleAxis.Reset();
            brakeAxis.Reset();
            Throttle = Brake = Steer = 0f;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) ReleaseAll();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) ReleaseAll();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            ShiftUpPressed = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            ShiftDownPressed = (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            PausePressed = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            ResetPressed = (kb != null && kb.backspaceKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            CameraPressed = (kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            TelemetryPressed = kb != null && kb.f3Key.wasPressedThisFrame;
            ConfirmPressed = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            RadioPressed = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame && !radioOpen);
            PitPressed = kb != null && kb.pKey.wasPressedThisFrame;
            TowerPressed = kb != null && kb.tabKey.wasPressedThisFrame;
            TowerHeld = kb != null && kb.tabKey.isPressed;
            MapPressed = kb != null && kb.mKey.wasPressedThisFrame;
            HudPresetPressed = kb != null && kb.hKey.wasPressedThisFrame;
            HelpPressed = kb != null && kb.f1Key.wasPressedThisFrame;
            LayoutDebugPressed = kb != null && kb.f2Key.wasPressedThisFrame;
            PerfPressed = kb != null && kb.f6Key.wasPressedThisFrame;
            RestartPressed = kb != null && kb.f5Key.wasPressedThisFrame;
            ScreenshotPressed = kb != null && kb.f9Key.wasPressedThisFrame;
            NavUp = (kb != null && kb.upArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            NavDown = (kb != null && kb.downArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            NumberPressed = 0;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) NumberPressed = 1;
                else if (kb.digit2Key.wasPressedThisFrame) NumberPressed = 2;
                else if (kb.digit3Key.wasPressedThisFrame) NumberPressed = 3;
                else if (kb.digit4Key.wasPressedThisFrame) NumberPressed = 4;
                else if (kb.digit5Key.wasPressedThisFrame) NumberPressed = 5;
                else if (kb.digit6Key.wasPressedThisFrame) NumberPressed = 6;
                else if (kb.digit7Key.wasPressedThisFrame) NumberPressed = 7;
            }

            // Keyboard and gamepad.
            float kThrottle = 0f, kBrake = 0f, kSteer = 0f;
            bool analogSteer = false;
            if (kb != null)
            {
                // While the radio is open the arrows navigate it; W/S still drive.
                if (kb.wKey.isPressed || (kb.upArrowKey.isPressed && !radioOpen)) kThrottle = 1f;
                if (kb.sKey.isPressed || (kb.downArrowKey.isPressed && !radioOpen)) kBrake = 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kSteer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kSteer += 1f;
            }
            float padThrottle = 0f, padBrake = 0f, padSteer = 0f;
            if (pad != null)
            {
                padThrottle = pad.rightTrigger.ReadValue();
                padBrake = pad.leftTrigger.ReadValue();
                padSteer = pad.leftStick.ReadValue().x;
                // The stickDeadzone processor already returns exactly 0 in the centre; a threshold here
                // would cut the first percent of travel again (fine corrections at speed).
                if (Mathf.Abs(padSteer) > 0.001f) analogSteer = true;
            }

            // Touch buttons.
            UpdateTouch();
            bool tiltMode = settings.controlPreset == ControlPreset.Tilt;
            float tThrottle = touch.IsHeld(TouchLayout.Throttle) ? 1f : 0f;
            float tBrake = touch.IsHeld(TouchLayout.Brake) ? 1f : 0f;
            float tSteer = (touch.IsHeld(TouchLayout.SteerRight) ? 1f : 0f) - (touch.IsHeld(TouchLayout.SteerLeft) ? 1f : 0f);
            prevHeldTouch.Clear();
            foreach (int id in heldTouch) prevHeldTouch.Add(id);
            heldTouch.Clear();
            foreach (var b in Buttons)
                if (touch.IsHeld(b.id)) heldTouch.Add(b.id);
            if (TouchTapped(TouchLayout.ShiftUp)) ShiftUpPressed = true;
            if (TouchTapped(TouchLayout.ShiftDown)) ShiftDownPressed = true;
            if (TouchTapped(TouchLayout.Recenter)) CalibrateTilt();
            if (TouchTapped(TouchLayout.Pause)) PausePressed = true;
            if (TouchTapped(TouchLayout.Radio)) RadioPressed = true;
            for (int k = 0; k < TouchLayout.RadioLabels.Length; k++)
                if (TouchTapped(TouchLayout.RadioEntry0 + k)) NumberPressed = k + 1;

            // Tilt.
            float tiltSteer = 0f;
            if (tiltMode && TiltAvailable)
            {
                Vector3 g = Vector3.zero;
                if (GravitySensor.current != null) g = GravitySensor.current.gravity.ReadValue();
                else if (Accelerometer.current != null) g = Accelerometer.current.acceleration.ReadValue();
                var gn = g.ToNum();
                RollDeg = TiltSteering.ScreenRollDeg(gn, CurrentOrientation()) * (settings.invertTilt ? -1f : 1f);
                TiltReliability = TiltSteering.Reliability(gn);
                if (!tilt.IsCalibrated) tilt.Calibrate(RollDeg);
                tiltSteer = tilt.Process(RollDeg, dt, TiltReliability);
            }

            float digitalSteer = Mathf.Clamp(kSteer + tSteer, -1f, 1f);
            SteerKey = digitalSteer;
            if (analogSteer)
            {
                Source = SteerSource.Stick;
                this.analogSteer = InputTuning.StickSteer(padSteer, settings.steeringSensitivity);
            }
            else if (tiltMode && TiltAvailable && digitalSteer == 0f)
            {
                Source = SteerSource.Tilt;
                this.analogSteer = tiltSteer;
            }
            else Source = SteerSource.Digital;
            keyboard.profile = legacyHandling ? KeyboardSteering.Profile.Classic : (KeyboardSteering.Profile)Mathf.Clamp(settings.keyboardProfile, 0, 2);
            keyboard.sensitivity = settings.steeringSensitivity;
            keyboard.smoothing = settings.steeringSmoothing;
            if (Source != SteerSource.Digital) { Steer = this.analogSteer; keyboard.Reset(); }
            Throttle = Mathf.Max(throttleAxis.Update(Mathf.Max(kThrottle, tThrottle), dt), padThrottle);
            Brake = Mathf.Max(brakeAxis.Update(Mathf.Max(kBrake, tBrake), dt), padBrake);
        }

        /// <summary>
        /// Steering for this physics step (call once per FixedUpdate): the digital ramp advances on the physics clock,
        /// so keyboard steering feels the same at 30 and 240 fps; stick and tilt pass through.
        /// </summary>
        public float SteerForPhysics(float carSpeed, float dt)
        {
            if (Source == SteerSource.Digital) Steer = keyboard.Update(SteerKey, carSpeed, dt);
            return Steer;
        }

        /// <summary>
        /// Front-slip limiter target (VehicleSimulation.steerSlipTarget) for the current input: keys and touch buttons by
        /// keyboard profile (a key cannot modulate), stick and tilt by steering assist (Aus = raw lock, Leicht 1.15,
        /// Standard 1.0); none with the previous handling.
        /// </summary>
        public float SteerSlipTarget
        {
            get
            {
                if (legacyHandling) return 0f;
                if (Source == SteerSource.Digital) return KeyboardSteering.SlipTarget(keyboard.profile);
                int assist = settings.steeringAssistLevel;
                if (Source == SteerSource.Tilt) return 1f;
                return assist <= 0 ? 0f : assist == 1 ? 1.15f : 1f;
            }
        }

        /// <summary>True on the frame a touch button went down.</summary>
        public bool TouchTapped(int id) => heldTouch.Contains(id) && !prevHeldTouch.Contains(id);

        /// <summary>Show driving controls (false in menus).</summary>
        public bool drivingControls = true;
        bool layoutRadio;
        bool layoutDriving = true;

        void UpdateTouch()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != layoutScreen || layoutPreset != settings.controlPreset || layoutManual != manualGears || layoutRadio != radioOpen || layoutDriving != drivingControls)
            {
                layoutScreen = size;
                layoutPreset = settings.controlPreset;
                layoutManual = manualGears;
                layoutRadio = radioOpen;
                layoutDriving = drivingControls;
                Buttons = drivingControls
                    ? TouchLayout.Compute(settings, settings.controlPreset == ControlPreset.Tilt, manualGears, radioOpen)
                    : new List<TouchLayout.Button>();
                touch.SetControls(TouchLayout.ToControls(Buttons));
            }
            touch.BeginFrame();
            float w = Mathf.Max(Screen.width, 1), h = Mathf.Max(Screen.height, 1);
            foreach (var t in ETouch.activeTouches)
            {
                var phase = t.phase;
                if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    touch.Release(t.touchId);
                    continue;
                }
                var p = t.screenPosition;
                touch.Report(t.touchId, new System.Numerics.Vector2(p.x / w, p.y / h), phase == UnityEngine.InputSystem.TouchPhase.Began);
            }
            touch.EndFrame();
        }

        static PhoneOrientation CurrentOrientation()
        {
            switch (Screen.orientation)
            {
                case ScreenOrientation.LandscapeRight: return PhoneOrientation.LandscapeRight;
                case ScreenOrientation.Portrait: return PhoneOrientation.Portrait;
                case ScreenOrientation.PortraitUpsideDown: return PhoneOrientation.PortraitUpsideDown;
                default: return PhoneOrientation.LandscapeLeft;
            }
        }

        /// <summary>True if the screen point (pixels, bottom-left origin) is on a driving control.</summary>
        public bool IsOverControl(Vector2 screenPoint)
        {
            foreach (var b in Buttons)
                if (b.rect.Contains(screenPoint)) return true;
            return false;
        }
    }
}
