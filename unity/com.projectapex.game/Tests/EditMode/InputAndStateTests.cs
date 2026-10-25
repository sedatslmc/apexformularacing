using System.Numerics;
using Apex.Core;
using Apex.InputLogic;
using NUnit.Framework;

namespace Apex.Tests
{
    public class InputAndStateTests
    {
        static Vector3 GravityForRoll(float rollDeg, DeviceOrientation o)
        {
            // Screen-space gravity for a phone rotated clockwise by roll, then mapped back to device axes.
            float r = rollDeg * ApexMath.Deg2Rad;
            float sx = System.MathF.Sin(r), sy = -System.MathF.Cos(r);
            switch (o)
            {
                case DeviceOrientation.LandscapeLeft: return new Vector3(sy, -sx, -0.3f);
                case DeviceOrientation.LandscapeRight: return new Vector3(-sy, sx, -0.3f);
                default: return new Vector3(sx, sy, -0.3f);
            }
        }

        [TestCase(DeviceOrientation.LandscapeLeft)]
        [TestCase(DeviceOrientation.LandscapeRight)]
        [TestCase(DeviceOrientation.Portrait)]
        public void ScreenRollMatchesPhysicalRotation(DeviceOrientation o)
        {
            foreach (float roll in new[] { -40f, -10f, 0f, 15f, 45f })
                Assert.That(TiltSteering.ScreenRollDeg(GravityForRoll(roll, o), o), Is.EqualTo(roll).Within(0.01f), $"{o} {roll}");
        }

        [Test]
        public void TiltIsRelativeToCalibrationWithDeadZoneAndClamp()
        {
            var tilt = new TiltSteering { maxAngleDeg = 30f, deadZoneDeg = 2f, curveExponent = 1f, smoothingTime = 0f };
            tilt.Calibrate(10f); // player holds the phone slightly rotated
            Assert.That(tilt.Map(11.5f), Is.EqualTo(0f), "inside dead zone");
            Assert.That(tilt.Map(10f + 16f), Is.EqualTo(0.5f).Within(1e-4f), "half lock");
            Assert.That(tilt.Map(10f - 16f), Is.EqualTo(-0.5f).Within(1e-4f), "symmetric");
            Assert.That(tilt.Map(10f + 80f), Is.EqualTo(1f), "clamped");
            // Wrap-around: neutral near 180° must not jump.
            tilt.Calibrate(179f);
            Assert.That(tilt.Map(-175f), Is.GreaterThan(0f));
        }

        [Test]
        public void TiltFadesToCentreWhenPhoneIsFlat()
        {
            Assert.That(TiltSteering.Reliability(new Vector3(0, 0, -1)), Is.EqualTo(0f));
            Assert.That(TiltSteering.Reliability(new Vector3(-1, 0, -0.2f)), Is.EqualTo(1f));
            var tilt = new TiltSteering { smoothingTime = 0f, curveExponent = 1f };
            tilt.Calibrate(0f);
            Assert.That(tilt.Process(30f, 0.016f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void TiltSmoothingConverges()
        {
            var tilt = new TiltSteering { smoothingTime = 0.05f, curveExponent = 1f, deadZoneDeg = 0f, maxAngleDeg = 30f };
            tilt.Calibrate(0f);
            float v = 0f;
            for (int i = 0; i < 30; i++) v = tilt.Process(15f, 1f / 60f);
            Assert.That(v, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void DigitalAxisRampsAndReturns()
        {
            var a = new DigitalAxis(2f, 4f, 8f);
            a.Update(1f, 0.25f);
            Assert.That(a.Value, Is.EqualTo(0.5f).Within(1e-5f));
            a.Update(-1f, 0.1f);
            Assert.That(a.Value, Is.EqualTo(-0.3f).Within(1e-5f), "reversal uses the faster rate");
            a.Update(0f, 1f);
            Assert.That(a.Value, Is.EqualTo(0f));
        }

        static TouchOwnership NewTouch()
        {
            var t = new TouchOwnership();
            t.SetControls(new[]
            {
                new TouchOwnership.Control { id = 1, xMin = 0f, yMin = 0f, xMax = 0.2f, yMax = 0.4f, enabled = true },
                new TouchOwnership.Control { id = 2, xMin = 0.8f, yMin = 0f, xMax = 1f, yMax = 0.4f, enabled = true },
            });
            return t;
        }

        [Test]
        public void TouchOwnershipIsKeptUntilRelease()
        {
            var t = NewTouch();
            t.BeginFrame();
            Assert.That(t.Report(7, new Vector2(0.9f, 0.1f), true), Is.EqualTo(2), "throttle finger");
            t.EndFrame();
            // Finger slides over the other control: ownership must not change.
            t.BeginFrame();
            Assert.That(t.Report(7, new Vector2(0.1f, 0.1f), false), Is.EqualTo(2));
            // A second finger starting in the middle of the screen owns nothing.
            Assert.That(t.Report(8, new Vector2(0.5f, 0.5f), true), Is.EqualTo(-1));
            t.EndFrame();
            Assert.That(t.IsHeld(2), Is.True);
            Assert.That(t.IsHeld(1), Is.False);
            t.Release(7);
            Assert.That(t.IsHeld(2), Is.False);
        }

        [Test]
        public void UnreportedTouchesAreReleased()
        {
            var t = NewTouch();
            t.BeginFrame();
            t.Report(3, new Vector2(0.1f, 0.1f), true);
            t.EndFrame();
            Assert.That(t.IsHeld(1), Is.True);
            // Touch-up lost (e.g. notification): next frame it is not reported.
            t.BeginFrame();
            t.EndFrame();
            Assert.That(t.IsHeld(1), Is.False);
        }

        [Test]
        public void StateMachineAllowsOnlyListedTransitions()
        {
            var sm = new GameStateMachine();
            Assert.That(sm.TryTransition(GameState.Driving), Is.False);
            Assert.That(sm.TryTransition(GameState.MainMenu), Is.True);
            Assert.That(sm.TryTransition(GameState.Loading), Is.True);
            Assert.That(sm.TryTransition(GameState.Driving), Is.True);
            Assert.That(sm.AcceptsDrivingInput, Is.True);
            Assert.That(sm.TryTransition(GameState.RadioOverlay), Is.True);
            Assert.That(sm.AcceptsDrivingInput, Is.True, "radio overlay never blocks driving");
            Assert.That(sm.TryTransition(GameState.Pause), Is.True);
            Assert.That(sm.AcceptsDrivingInput, Is.False);
            Assert.That(sm.Back(), Is.True, "back from pause resumes");
            Assert.That(sm.Current, Is.EqualTo(GameState.Driving));
        }

        [Test]
        public void BackNavigationIsPredictable()
        {
            var sm = new GameStateMachine();
            sm.TryTransition(GameState.MainMenu);
            sm.TryTransition(GameState.TrackSelect);
            sm.TryTransition(GameState.CarSelect);
            Assert.That(sm.Back(), Is.True);
            Assert.That(sm.Current, Is.EqualTo(GameState.TrackSelect));
            Assert.That(sm.Back(), Is.True);
            Assert.That(sm.Current, Is.EqualTo(GameState.MainMenu));
            Assert.That(sm.Back(), Is.False, "main menu has no back target");
        }

        [Test]
        public void SettingsMigrateAndClamp()
        {
            var s = new SettingsData { version = 0, masterVolume = 3f, tiltMaxAngleDeg = float.NaN, hudScale = 0.1f };
            Assert.That(s.MigrateAndValidate(), Is.True);
            Assert.That(s.version, Is.EqualTo(SettingsData.CurrentVersion));
            Assert.That(s.masterVolume, Is.EqualTo(1f));
            Assert.That(s.tiltMaxAngleDeg, Is.EqualTo(15f));
            Assert.That(s.hudScale, Is.EqualTo(0.75f));
            Assert.That(new SettingsData().MigrateAndValidate(), Is.False, "defaults are valid");
        }
    }
}
