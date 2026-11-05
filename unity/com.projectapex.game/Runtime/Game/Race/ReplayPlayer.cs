using System.Collections.Generic;
using Apex.Race;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Plays the session replay (docs/REPLAY.md) over the real scene: the race logic and the vehicle physics are
    /// stopped, every car is set kinematically to its recorded pose, and the camera follows the selected car in one of
    /// five views (TV coverage from trackside sites, chase, nose, cockpit, helicopter). Keyboard: Space play/pause,
    /// ←/→ skip 10 s, ↑/↓ speed, C camera, Tab / Shift+Tab car; Esc (handled by ApexGame) returns to the results.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class ReplayPlayer : MonoBehaviour
    {
        RaceController race;
        Camera cam;
        RaceCamera raceCam;
        ReplayClip clip;
        ReplayTransport transport;
        List<TvDirector.Site> sites;
        int site = -1, hint = -1;
        Vector3 chaseForward = Vector3.forward;
        CarVisuals firstPerson;
        float baseFov;
        bool safetyCarWasActive;

        ApexUi ui;
        VisualElement fill;
        Label carLabel, cameraLabel, clockLabel, speedLabel, pausedLabel;

        public bool Playing { get; private set; }

        /// <summary>True when <paramref name="r"/> recorded enough for a replay.</summary>
        public static bool Available(RaceController r) => r != null && r.Replay != null && r.Replay.Duration > 2f;

        public void Begin(RaceController r, Camera camera, RaceCamera driving)
        {
            if (!Available(r)) return;
            race = r; cam = camera; raceCam = driving; clip = r.Replay;
            race.enabled = false;
            foreach (var rc in race.Cars)
            {
                rc.car.enabled = false;
                if (rc.car.Body != null)
                {
                    RigidbodyCompat.SetVelocity(rc.car.Body, Vector3.zero);
                    rc.car.Body.isKinematic = true;
                    rc.car.Body.interpolation = RigidbodyInterpolation.None;
                }
                rc.car.gameObject.SetActive(true);
            }
            if (race.SafetyCar != null)
            {
                safetyCarWasActive = race.SafetyCar.gameObject.activeSelf;
                race.SafetyCar.gameObject.SetActive(false);
            }
            if (raceCam != null) raceCam.enabled = false;
            if (cam != null) baseFov = cam.fieldOfView;
            var ci = race.Circuit;
            sites = TvDirector.Sites(ci.Geometry, ci.WallLeft, ci.WallRight);
            transport = new ReplayTransport(clip.Start, clip.End, clip.cars, race.Cars.IndexOf(race.Player));
            site = -1; hint = -1;
            EnsureOverlay();
            ui.SetVisible(true);
            Playing = true;
            Apply();
        }

        public void End()
        {
            if (!Playing) return;
            Playing = false;
            ui?.SetVisible(false);
            SetFirstPerson(null);
            if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
            if (race != null && race.SafetyCar != null && safetyCarWasActive) race.SafetyCar.gameObject.SetActive(true);
            if (raceCam != null) { raceCam.enabled = true; raceCam.Snap(); }
            // The cars stay where the recording ended (the session is over; the results screen shows the podium).
            if (transport != null) { transport.time = transport.end; Apply(); }
        }

        void Update()
        {
            if (!Playing) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) transport.TogglePause();
                if (kb.leftArrowKey.wasPressedThisFrame) transport.Skip(-ReplayTransport.SkipSeconds);
                if (kb.rightArrowKey.wasPressedThisFrame) transport.Skip(ReplayTransport.SkipSeconds);
                if (kb.upArrowKey.wasPressedThisFrame) transport.Faster();
                if (kb.downArrowKey.wasPressedThisFrame) transport.Slower();
                if (kb.cKey.wasPressedThisFrame) { transport.NextCamera(); site = -1; }
                if (kb.tabKey.wasPressedThisFrame) { transport.NextCar(kb.shiftKey.isPressed ? -1 : 1); site = -1; hint = -1; }
                if (kb.homeKey.wasPressedThisFrame) transport.time = transport.start;
            }
            transport.Step(Time.unscaledDeltaTime);
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < race.Cars.Count && i < clip.cars; i++)
            {
                var (p, q, _) = clip.Sample(i, transport.time);
                race.Cars[i].car.transform.SetPositionAndRotation(p.ToUnity(), new Quaternion(q.X, q.Y, q.Z, q.W));
            }
        }

        void LateUpdate()
        {
            if (!Playing || cam == null) return;
            var rc = race.Cars[transport.car];
            var t = rc.car.transform;
            var (_, _, speed) = clip.Sample(transport.car, transport.time);
            float dt = Time.unscaledDeltaTime;
            var flat = new Vector3(t.forward.x, 0f, t.forward.z);
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : chaseForward;
            SetFirstPerson(transport.camera == ReplayCamera.Cockpit ? rc.car.GetComponent<CarVisuals>() : null);
            switch (transport.camera)
            {
                case ReplayCamera.Tv:
                {
                    var g = race.Circuit.Geometry;
                    var pr = g.Project(t.position.ToNum(), hint);
                    hint = pr.index;
                    site = TvDirector.Choose(sites, g.Length, pr.distance, t.position.ToNum(), site);
                    if (site < 0) goto default;
                    Vector3 eye = sites[site].position.ToUnity();
                    Vector3 look = t.position + Vector3.up * 0.4f;
                    cam.fieldOfView = TvDirector.Fov(Vector3.Distance(eye, look));
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
                    break;
                }
                case ReplayCamera.Nose:
                    cam.fieldOfView = 74f;
                    cam.transform.SetPositionAndRotation(t.TransformPoint(RaceCamera.NoseEye), Quaternion.LookRotation(t.forward, Vector3.Slerp(t.up, Vector3.up, 0.15f)));
                    break;
                case ReplayCamera.Cockpit:
                    cam.fieldOfView = 70f;
                    cam.transform.SetPositionAndRotation(t.TransformPoint(new Vector3(0f, 0.47f, 0.02f)), Quaternion.LookRotation(t.forward, Vector3.Slerp(t.up, Vector3.up, 0.5f)));
                    break;
                case ReplayCamera.Helicopter:
                {
                    // High and behind, drifting slowly sideways: the classic aerial follow.
                    float s = Mathf.Sin(transport.time * 0.07f);
                    var side = Vector3.Cross(Vector3.up, flat);
                    Vector3 eye = t.position - flat * 38f + side * (18f * s) + Vector3.up * 42f;
                    cam.fieldOfView = 38f;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(t.position + flat * 10f - eye, Vector3.up));
                    break;
                }
                default:
                {
                    chaseForward = Vector3.Slerp(chaseForward, flat, 1f - Mathf.Exp(-5f * dt)).normalized;
                    Vector3 eye = t.position - chaseForward * 6.5f + Vector3.up * 2f;
                    cam.fieldOfView = 66f;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(t.position + chaseForward * 6f + Vector3.up * 0.6f - eye, Vector3.up));
                    break;
                }
            }
            if (transport.camera != ReplayCamera.Chase) chaseForward = flat;

            fill.style.width = 1680f * transport.Progress;
            carLabel.text = $"P{rc.entry.position}  {rc.number}  {rc.name.ToUpperInvariant()}   {Mathf.RoundToInt(Mathf.Abs(speed) * 3.6f)} KM/H";
            cameraLabel.text = Loc.T(ReplayTransport.CameraNames[(int)transport.camera]).ToUpperInvariant();
            clockLabel.text = $"{ReplayTransport.Clock(transport.time - transport.start)} / {ReplayTransport.Clock(transport.end - transport.start)}";
            speedLabel.text = transport.SpeedLabel;
            pausedLabel.text = transport.paused ? Loc.T("PAUSE") : "";
        }

        void SetFirstPerson(CarVisuals v)
        {
            if (firstPerson == v) return;
            if (firstPerson != null) firstPerson.FirstPerson = false;
            firstPerson = v;
            if (firstPerson != null) firstPerson.FirstPerson = true;
        }

        void EnsureOverlay()
        {
            if (ui != null) return;
            ui = new ApexUi("ApexReplay", 15);
            // Top left: replay badge and the focused car.
            var badge = ApexUi.Box(ui.root, ApexUi.C(UiTokens.Red));
            ApexUi.Place(badge, 60, 48, 6, 64);
            var caption = ApexUi.Text(ui.root, Loc.T("WIEDERHOLUNG"), ApexUi.BodyStrong, 16, ApexUi.C(UiTokens.Red), 6f);
            ApexUi.Place(caption, 82, 46);
            carLabel = ApexUi.Text(ui.root, "", ApexUi.DisplayStrong, 30, ApexUi.C(UiTokens.Ivory), 3f);
            ApexUi.Place(carLabel, 82, 70, 1100);
            cameraLabel = ApexUi.Text(ui.root, "", ApexUi.BodyStrong, 18, ApexUi.C(UiTokens.Cyan), 5f, TextAnchor.MiddleRight);
            ApexUi.Place(cameraLabel, 1360, 56, 500);
            pausedLabel = ApexUi.Text(ui.root, "", ApexUi.DisplayStrong, 26, ApexUi.C(UiTokens.Gold), 8f, TextAnchor.MiddleRight);
            ApexUi.Place(pausedLabel, 1360, 86, 500);
            // Bottom: timeline, clock, speed and the key hints.
            var panel = ApexUi.Panel(ui.root, UiTokens.PanelAlpha);
            ApexUi.Place(panel, 0, 970, 1920, 110);
            var track = ApexUi.Box(ui.root, ApexUi.C(UiTokens.Surface));
            ApexUi.Place(track, 120, 994, 1680, 4);
            fill = ApexUi.Box(track, ApexUi.C(UiTokens.Cyan));
            ApexUi.Place(fill, 0, 0, 0, 4);
            clockLabel = ApexUi.Text(ui.root, "", ApexUi.Display, 22, ApexUi.C(UiTokens.Ivory), 2f);
            ApexUi.Place(clockLabel, 120, 1012, 300);
            speedLabel = ApexUi.Text(ui.root, "", ApexUi.Display, 22, ApexUi.C(UiTokens.Cyan), 2f);
            ApexUi.Place(speedLabel, 420, 1012, 120);
            var keys = ApexUi.Text(ui.root, Loc.T("Leertaste Pause · ←/→ 10 s · ↑/↓ Tempo · C Kamera · Tab Auto · Esc zurück"),
                ApexUi.Body, UiTokens.Small, ApexUi.C(UiTokens.Steel), 1.5f, TextAnchor.MiddleRight);
            ApexUi.Place(keys, 700, 1012, 1100);
            ui.SetVisible(false);
        }

        void OnDestroy() => ui?.Dispose();
    }
}
