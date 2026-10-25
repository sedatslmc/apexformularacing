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
        public SettingsData settings = new SettingsData();
        public bool manualGears;

        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }

        // Edge-triggered actions (valid for the frame they occur in).
        public bool ShiftUpPressed { get; private set; }
        public bool ShiftDownPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool CameraPressed { get; private set; }
        public bool TelemetryPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }

        public readonly TiltSteering tilt = new TiltSteering();
        public bool TiltAvailable { get; private set; }
        public float RollDeg { get; private set; }
        public float TiltReliability { get; private set; }
        public List<TouchLayout.Button> Buttons { get; private set; } = new List<TouchLayout.Button>();
        public readonly TouchOwnership touch = new TouchOwnership();

        readonly DigitalAxis steerAxis = new DigitalAxis(3.2f, 5f, 7f);
        readonly DigitalAxis throttleAxis = new DigitalAxis(7f, 7f, 7f);
        readonly DigitalAxis brakeAxis = new DigitalAxis(9f, 9f, 9f);
        bool prevShiftUpTouch, prevShiftDownTouch, prevRecenterTouch, prevPauseTouch;
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
            steerAxis.Reset();
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

            // Keyboard and gamepad.
            float kThrottle = 0f, kBrake = 0f, kSteer = 0f;
            bool analogSteer = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kThrottle = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kBrake = 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kSteer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kSteer += 1f;
            }
            float padThrottle = 0f, padBrake = 0f, padSteer = 0f;
            if (pad != null)
            {
                padThrottle = pad.rightTrigger.ReadValue();
                padBrake = pad.leftTrigger.ReadValue();
                padSteer = pad.leftStick.ReadValue().x;
                if (Mathf.Abs(padSteer) > 0.08f) analogSteer = true;
            }

            // Touch buttons.
            UpdateTouch();
            bool tiltMode = settings.controlPreset == ControlPreset.Tilt;
            float tThrottle = touch.IsHeld(TouchLayout.Throttle) ? 1f : 0f;
            float tBrake = touch.IsHeld(TouchLayout.Brake) ? 1f : 0f;
            float tSteer = (touch.IsHeld(TouchLayout.SteerRight) ? 1f : 0f) - (touch.IsHeld(TouchLayout.SteerLeft) ? 1f : 0f);
            bool su = touch.IsHeld(TouchLayout.ShiftUp), sd = touch.IsHeld(TouchLayout.ShiftDown), rc = touch.IsHeld(TouchLayout.Recenter);
            if (su && !prevShiftUpTouch) ShiftUpPressed = true;
            if (sd && !prevShiftDownTouch) ShiftDownPressed = true;
            if (rc && !prevRecenterTouch) CalibrateTilt();
            bool pz = touch.IsHeld(TouchLayout.Pause);
            if (pz && !prevPauseTouch) PausePressed = true;
            prevPauseTouch = pz;
            prevShiftUpTouch = su;
            prevShiftDownTouch = sd;
            prevRecenterTouch = rc;

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
            if (analogSteer) Steer = padSteer;
            else if (tiltMode && TiltAvailable && digitalSteer == 0f) Steer = tiltSteer;
            else Steer = steerAxis.Update(digitalSteer, dt);
            Throttle = Mathf.Max(throttleAxis.Update(Mathf.Max(kThrottle, tThrottle), dt), padThrottle);
            Brake = Mathf.Max(brakeAxis.Update(Mathf.Max(kBrake, tBrake), dt), padBrake);
        }

        void UpdateTouch()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != layoutScreen || layoutPreset != settings.controlPreset || layoutManual != manualGears)
            {
                layoutScreen = size;
                layoutPreset = settings.controlPreset;
                layoutManual = manualGears;
                Buttons = TouchLayout.Compute(settings, settings.controlPreset == ControlPreset.Tilt, manualGears);
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
