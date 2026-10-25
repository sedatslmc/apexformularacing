using System;
using System.Collections;
using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Boot entry point and owner of the authoritative <see cref="GameStateMachine"/>.
    /// Builds the menu showroom, loads sessions, routes pause/back, applies and saves
    /// settings, and draws the interim IMGUI menus and HUD (M0/M1). Every menu item
    /// leads somewhere real; unimplemented modes are not listed.
    /// </summary>
    public sealed class ApexGame : MonoBehaviour
    {
        struct SessionOption
        {
            public string trackId;
            public string label;
            public bool autopilot;
        }

        static readonly SessionOption[] Sessions =
        {
            new SessionOption { trackId = "test_loop", label = "Freies Training – Apex Testoval", autopilot = false },
            new SessionOption { trackId = "bosphorus_gp", label = "Freies Training – Bosphorus GP (Graybox)", autopilot = false },
            new SessionOption { trackId = "test_loop", label = "Auto-Pilot-Demo – Testoval", autopilot = true },
        };

        readonly GameStateMachine sm = new GameStateMachine();
        public GameStateMachine State => sm;
        public SettingsData Settings { get; private set; }
        public RaceSession Session { get; private set; }

        InputRouter input;
        Camera cam;
        RaceCamera raceCam;
        GameObject showroom;
        GameObject world;
        string trackId = "";
        bool autopilotSession;

        // UI state
        bool settingsOpen;
        int focus;
        bool calibrating;
        bool showTelemetry;
        string message = "";
        Color messageColor = Color.white;
        float messageTime;
        float fps = 60f;
        double sessionRecord;
        readonly string[] sectorText = { "S1 --.---", "S2 --.---", "S3 --.---" };
        readonly Color[] sectorColor = { ApexStyle.TextDim, ApexStyle.TextDim, ApexStyle.TextDim };
        bool navUp, navDown, navLeft, navRight, navConfirm;
        float uiScale = 1f;
        Vector2 virtualSize;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 120f;
            Time.maximumDeltaTime = 1f / 15f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Settings = SaveService.LoadSettings();
            showTelemetry = Settings.showTelemetry;
            SetupCameraAndLighting();
            input = gameObject.AddComponent<InputRouter>();
            input.ApplySettings(Settings);
            sm.Changed += OnStateChanged;
            sm.TryTransition(GameState.MainMenu);
        }

        // --- Scene setup ---------------------------------------------------------------

        void SetupCameraAndLighting()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            raceCam = cam.GetComponent<RaceCamera>();
            if (raceCam == null) raceCam = cam.gameObject.AddComponent<RaceCamera>();
            raceCam.enabled = false;

            if (FindAnyObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.25f;
                sun.color = new Color(1f, 0.95f, 0.86f);
                sun.shadows = LightShadows.Soft;
                // Late-afternoon sun from the south-west lights the European shore.
                sun.transform.rotation = Quaternion.Euler(38f, 35f, 0f);
            }
            var sky = Resources.Load<Material>(ApexMaterials.ResourceFolder + "/Sky");
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null) sky = new Material(shader);
            }
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.84f);
            RenderSettings.fogStartDistance = 300f;
            RenderSettings.fogEndDistance = 3500f;
        }

        void BuildShowroom()
        {
            if (showroom != null) return;
            showroom = new GameObject("Showroom");
            var floor = new GameObject("Floor");
            floor.transform.SetParent(showroom.transform, false);
            floor.transform.localScale = new Vector3(14f, 0.05f, 14f);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cylinder);
            floor.AddComponent<MeshRenderer>().sharedMaterial = ApexMaterials.Get("Asphalt");
            var car = CarBuilder.Build("ShowroomCar", new Apex.Vehicle.VehicleSetup());
            car.GetComponent<Rigidbody>().isKinematic = true;
            car.enabled = false;
            car.transform.SetParent(showroom.transform, false);
            car.transform.localPosition = new Vector3(0f, car.setup.cogHeight, 0f);
            for (int i = 0; i < 4; i++)
            {
                var w = car.WheelVisuals[i];
                var m = car.setup.MountPosition(i, out float sc);
                w.localPosition = new Vector3(m.X, (i < 2 ? car.setup.wheelRadiusFront : car.setup.wheelRadiusRear) - car.setup.cogHeight, m.Z);
            }
        }

        void UpdateShowroomCamera()
        {
            float t = Time.unscaledTime * 0.12f;
            var pos = new Vector3(Mathf.Sin(t) * 7.5f, 2.2f, Mathf.Cos(t) * 7.5f);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(0f, 0.3f, 0f) - pos));
            cam.fieldOfView = 45f;
        }

        // --- Sessions -----------------------------------------------------------------

        void StartSession(SessionOption option)
        {
            if (!sm.TryTransition(GameState.Loading)) return;
            StartCoroutine(LoadSession(option));
        }

        IEnumerator LoadSession(SessionOption option)
        {
            // One frame so the loading screen is visible before the synchronous build.
            yield return null;
            var text = Resources.Load<TextAsset>("Tracks/" + option.trackId);
            if (text == null)
            {
                Debug.LogError("[Apex] Missing track data: " + option.trackId);
                sm.TryTransition(GameState.MainMenu);
                yield break;
            }
            CircuitDefinition def;
            try
            {
                def = CircuitDefinition.FromJson(text.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[Apex] Invalid track data: " + e.Message);
                sm.TryTransition(GameState.MainMenu);
                yield break;
            }
            if (showroom != null) Destroy(showroom);
            showroom = null;
            world = new GameObject("World");
            var circuit = CircuitBuilder.Build(def, world.transform);
            var car = CarBuilder.Build("PlayerCar", new Apex.Vehicle.VehicleSetup());
            car.transform.SetParent(world.transform, false);
            var audioSrc = car.gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            var engineAudio = car.gameObject.AddComponent<EngineAudio>();
            engineAudio.car = car;
            engineAudio.volume = Settings.masterVolume * 0.75f;
            Session = world.AddComponent<RaceSession>();
            Session.Setup(circuit, car, input, option.autopilot);
            Session.Message += ShowMessage;
            Session.Timer.LapCompleted += OnLapCompleted;
            Session.Timer.SectorCompleted += OnSectorCompleted;
            Session.Timer.LapStarted += OnLapStarted;
            Session.Timer.LapInvalidated += r => ShowMessage("Runde ungültig: " + r, ApexStyle.Urgent);
            trackId = def.id;
            autopilotSession = option.autopilot;
            sessionRecord = SaveService.BestLap(Settings, trackId);
            for (int i = 0; i < 3; i++) { sectorText[i] = $"S{i + 1} --.---"; sectorColor[i] = ApexStyle.TextDim; }
            raceCam.target = car.transform;
            raceCam.SetMode(Settings.cameraMode);
            raceCam.enabled = true;
            input.ReleaseAll();
            ApplyAssists();
            calibrating = Settings.controlPreset == ControlPreset.Tilt && !option.autopilot;
            sm.TryTransition(calibrating ? GameState.Grid : GameState.Driving);
            ShowMessage(def.name + " – Freies Training", Color.white, 3f);
        }

        void EndSession()
        {
            if (world != null) Destroy(world);
            world = null;
            Session = null;
            raceCam.enabled = false;
            raceCam.target = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void ApplyAssists()
        {
            if (Session == null) return;
            var sim = Session.Car.Sim;
            sim.assistTraction = Settings.tractionControl;
            sim.assistAbs = Settings.abs;
            sim.assistSteering = Settings.steeringAssist && !autopilotSession;
            sim.autoGearbox = Settings.autoGearbox || autopilotSession;
            input.manualGears = !sim.autoGearbox;
        }

        void OnStateChanged(GameState from, GameState to)
        {
            bool paused = to == GameState.Pause;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            focus = 0;
            settingsOpen = false;
            if (paused) input.ReleaseAll();
            if (to == GameState.MainMenu)
            {
                EndSession();
                BuildShowroom();
            }
        }

        // --- Timing events --------------------------------------------------------------

        void OnLapStarted(int lap)
        {
            for (int i = 0; i < 3; i++) sectorColor[i] = ApexStyle.TextDim;
        }

        void OnSectorCompleted(int index, double time, double delta)
        {
            if (index >= sectorText.Length) return;
            sectorText[index] = $"S{index + 1} {time:0.000}";
            var t = Session.Timer;
            if (!t.LapValid) sectorColor[index] = ApexStyle.Urgent;
            else if (double.IsNaN(delta) || Math.Abs(t.bestSectorTimes[index] - time) < 1e-9) sectorColor[index] = ApexStyle.Best;
            else sectorColor[index] = delta <= 0 ? ApexStyle.Faster : ApexStyle.Caution;
        }

        void OnLapCompleted(int lap, double time, bool valid, bool personalBest)
        {
            if (valid && !autopilotSession && SaveService.SubmitLap(Settings, trackId, time))
            {
                sessionRecord = time;
                ShowMessage("Neuer Rekord  " + ApexStyle.FormatTime(time), ApexStyle.Best, 3f);
            }
            else if (personalBest)
            {
                ShowMessage("Beste Runde  " + ApexStyle.FormatTime(time), ApexStyle.Best, 2.5f);
            }
        }

        void ShowMessage(string text, Color color) => ShowMessage(text, color, 2.5f);

        void ShowMessage(string text, Color color, float duration)
        {
            message = text;
            messageColor = color;
            messageTime = duration;
        }

        // --- Update ---------------------------------------------------------------------

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            if (messageTime > 0f) messageTime -= Time.unscaledDeltaTime;
            ReadNavigation();

            switch (sm.Current)
            {
                case GameState.MainMenu:
                    UpdateShowroomCamera();
                    if (input.PausePressed && settingsOpen) CloseSettings();
                    break;
                case GameState.Grid:
                case GameState.Driving:
                    if (input.PausePressed)
                    {
                        sm.TryTransition(GameState.Pause);
                        break;
                    }
                    if (calibrating && (input.ConfirmPressed || navConfirm)) FinishCalibration();
                    if (input.CameraPressed) ShowMessage(RaceCamera.ModeNames[Settings.cameraMode = raceCam.Cycle()] + "-Kamera", Color.white, 1f);
                    if (input.ResetPressed && Session != null && !autopilotSession) Session.ResetCarToTrack();
                    if (input.TelemetryPressed) showTelemetry = !showTelemetry;
                    if (Session != null && !autopilotSession)
                    {
                        if (input.ShiftUpPressed) Session.Car.Sim.RequestShiftUp();
                        if (input.ShiftDownPressed) Session.Car.Sim.RequestShiftDown();
                    }
                    break;
                case GameState.Pause:
                    if (input.PausePressed)
                    {
                        if (settingsOpen) CloseSettings();
                        else sm.Resume();
                    }
                    break;
            }
            if (Session != null) input.vehicleSpeed = Mathf.Abs(Session.Car.Sim.ForwardSpeed);
            if (Session != null)
                Session.InputEnabled = (sm.Current == GameState.Driving || sm.Current == GameState.RadioOverlay) && !calibrating;
        }

        void ReadNavigation()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            navUp = (kb != null && kb.upArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            navDown = (kb != null && kb.downArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            navLeft = (kb != null && kb.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame);
            navRight = (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            navConfirm = input.ConfirmPressed;
        }

        void FinishCalibration()
        {
            input.CalibrateTilt();
            calibrating = false;
            sm.TryTransition(GameState.Driving);
            ShowMessage("Lenkung zentriert", ApexStyle.Accent, 1.5f);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && (sm.Current == GameState.Driving || sm.Current == GameState.Grid)) sm.TryTransition(GameState.Pause);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && (sm.Current == GameState.Driving || sm.Current == GameState.Grid)) sm.TryTransition(GameState.Pause);
        }

        // --- Settings model -------------------------------------------------------------

        struct Row
        {
            public string label;
            public Func<string> value;
            public Action<int> change; // -1 / +1, 0 = activate
        }

        List<Row> SettingsRows()
        {
            var s = Settings;
            return new List<Row>
            {
                new Row { label = "Steuerung", value = () => s.controlPreset == ControlPreset.Keyboard ? "Tastatur/Gamepad" : s.controlPreset == ControlPreset.TouchButtons ? "Touch-Tasten" : "Neigung (Gyro)",
                    change = d => s.controlPreset = (ControlPreset)(((int)s.controlPreset + (d < 0 ? 2 : 1)) % 3) },
                new Row { label = "Traktionskontrolle", value = () => OnOff(s.tractionControl), change = d => s.tractionControl = !s.tractionControl },
                new Row { label = "ABS", value = () => OnOff(s.abs), change = d => s.abs = !s.abs },
                new Row { label = "Lenkhilfe (tempoabhängig)", value = () => OnOff(s.steeringAssist), change = d => s.steeringAssist = !s.steeringAssist },
                new Row { label = "Automatikgetriebe", value = () => OnOff(s.autoGearbox), change = d => s.autoGearbox = !s.autoGearbox },
                new Row { label = "Neigung: max. Winkel", value = () => $"{s.tiltMaxAngleDeg:0}°", change = d => s.tiltMaxAngleDeg += (d == 0 ? 5 : d * 5) },
                new Row { label = "Neigung: Empfindlichkeit", value = () => $"{s.tiltSensitivity:0.0}", change = d => s.tiltSensitivity += (d == 0 ? 0.1f : d * 0.1f) },
                new Row { label = "Neigung: Totzone", value = () => $"{s.tiltDeadZoneDeg:0.0}°", change = d => s.tiltDeadZoneDeg += (d == 0 ? 0.5f : d * 0.5f) },
                new Row { label = "Neigung: Glättung", value = () => $"{s.tiltSmoothing * 1000f:0} ms", change = d => s.tiltSmoothing += (d == 0 ? 0.01f : d * 0.01f) },
                new Row { label = "Neigung umkehren", value = () => OnOff(s.invertTilt), change = d => s.invertTilt = !s.invertTilt },
                new Row { label = "Linkshänder-Layout", value = () => OnOff(s.leftHanded), change = d => s.leftHanded = !s.leftHanded },
                new Row { label = "Tastengröße", value = () => $"{s.touchButtonScale * 100f:0} %", change = d => s.touchButtonScale += (d == 0 ? 0.1f : d * 0.1f) },
                new Row { label = "Tasten-Deckkraft", value = () => $"{s.touchButtonOpacity * 100f:0} %", change = d => s.touchButtonOpacity += (d == 0 ? 0.1f : d * 0.1f) },
                new Row { label = "HUD-Größe", value = () => $"{s.hudScale * 100f:0} %", change = d => s.hudScale += (d == 0 ? 0.05f : d * 0.05f) },
                new Row { label = "Lautstärke", value = () => $"{s.masterVolume * 100f:0} %", change = d => s.masterVolume += (d == 0 ? 0.1f : d * 0.1f) },
                new Row { label = "Telemetrie anzeigen", value = () => OnOff(showTelemetry), change = d => showTelemetry = !showTelemetry },
            };
        }

        static string OnOff(bool b) => b ? "An" : "Aus";

        void ChangeSetting(Row row, int dir)
        {
            row.change(dir);
            Settings.showTelemetry = showTelemetry;
            Settings.MigrateAndValidate();
            input.ApplySettings(Settings);
            ApplyAssists();
            SaveService.SaveSettings(Settings);
        }

        void CloseSettings()
        {
            settingsOpen = false;
            focus = 0;
        }

        // --- IMGUI ----------------------------------------------------------------------

        void OnGUI()
        {
            uiScale = UnityEngine.Screen.height / 1080f * Settings.hudScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            virtualSize = new Vector2(UnityEngine.Screen.width / uiScale, UnityEngine.Screen.height / uiScale);
            switch (sm.Current)
            {
                case GameState.MainMenu:
                    if (settingsOpen) DrawSettings();
                    else DrawMainMenu();
                    break;
                case GameState.Loading:
                    DrawLoading();
                    break;
                case GameState.Grid:
                case GameState.Driving:
                    DrawHud();
                    if (calibrating) DrawCalibration();
                    break;
                case GameState.Pause:
                    DrawHud();
                    if (settingsOpen) DrawSettings();
                    else DrawPause();
                    break;
            }
        }

        Rect SafeRect()
        {
            var s = UnityEngine.Screen.safeArea;
            return new Rect(s.x / uiScale, (UnityEngine.Screen.height - s.yMax) / uiScale, s.width / uiScale, s.height / uiScale);
        }

        int MenuNav(int count)
        {
            if (navDown) focus = (focus + 1) % count;
            if (navUp) focus = (focus - 1 + count) % count;
            return focus;
        }

        void DrawMainMenu()
        {
            var safe = SafeRect();
            float x = safe.x + 70f, y = safe.y + 70f;
            ApexStyle.Text(new Rect(x, y, 900f, 110f), "PROJECT APEX", 92, ApexStyle.Fg, FontStyle.Bold);
            ApexStyle.Box(new Rect(x, y + 112f, 160f, 5f), ApexStyle.Accent);
            ApexStyle.Text(new Rect(x, y + 128f, 900f, 40f), "Prototyp · Meilenstein 1 · Fahrphysik-Vertical-Slice", 26, ApexStyle.TextDim);
            var items = new List<(string, Action)>();
            foreach (var opt in Sessions)
            {
                var o = opt;
                double best = SaveService.BestLap(Settings, o.trackId);
                string label = o.label + (best > 0 && !o.autopilot ? $"   ·   Rekord {ApexStyle.FormatTime(best)}" : "");
                items.Add((label, () => StartSession(o)));
            }
            items.Add(("Einstellungen", () => { settingsOpen = true; focus = 0; }));
            items.Add(("Beenden", Application.Quit));
            int f = MenuNav(items.Count);
            float by = y + 220f;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, by + i * 92f, 900f, 80f);
                if (ApexStyle.Button(r, items[i].Item1, i == f) || (navConfirm && i == f))
                {
                    focus = i;
                    items[i].Item2();
                    break;
                }
            }
            ApexStyle.Text(new Rect(x, safe.yMax - 60f, 1600f, 40f),
                "Tastatur: W/S Gas/Bremse · A/D Lenken · Q/E Schalten · C Kamera · Rücktaste Reset · F3 Telemetrie · Esc Pause",
                20, ApexStyle.TextDim);
        }

        void DrawSettings()
        {
            var safe = SafeRect();
            float w = Mathf.Min(1100f, safe.width - 80f);
            float x = safe.x + (safe.width - w) * 0.5f;
            float y = safe.y + 40f;
            ApexStyle.Box(new Rect(x - 20f, y - 20f, w + 40f, safe.height - 40f), ApexStyle.PanelSolid);
            ApexStyle.Text(new Rect(x, y, w, 60f), "EINSTELLUNGEN", 44, ApexStyle.Fg, FontStyle.Bold);
            var rows = SettingsRows();
            int count = rows.Count + 1;
            int f = MenuNav(count);
            float rowH = Mathf.Min(58f, (safe.height - 220f) / count);
            float ry = y + 80f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = new Rect(x, ry + i * rowH, w, rowH - 6f);
                bool focused = i == f;
                ApexStyle.Box(r, focused ? new Color(0.12f, 0.2f, 0.24f, 0.95f) : new Color(1f, 1f, 1f, 0.03f));
                if (focused) ApexStyle.Box(new Rect(r.x, r.y, 5f, r.height), ApexStyle.Accent);
                ApexStyle.Text(new Rect(r.x + 20f, r.y, w * 0.55f, r.height), rows[i].label, 24, ApexStyle.Fg);
                var minus = new Rect(r.xMax - 330f, r.y + 4f, 70f, r.height - 8f);
                var plus = new Rect(r.xMax - 80f, r.y + 4f, 70f, r.height - 8f);
                ApexStyle.Text(new Rect(r.xMax - 255f, r.y, 170f, r.height), rows[i].value(), 24, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (ApexStyle.Button(minus, "−", false, 26)) { focus = i; ChangeSetting(rows[i], -1); }
                if (ApexStyle.Button(plus, "+", false, 26)) { focus = i; ChangeSetting(rows[i], 1); }
                if (focused && navLeft) ChangeSetting(rows[i], -1);
                if (focused && (navRight || navConfirm)) ChangeSetting(rows[i], navConfirm ? 0 : 1);
            }
            var back = new Rect(x, ry + rows.Count * rowH + 6f, 360f, 64f);
            if (ApexStyle.Button(back, "Zurück", f == rows.Count) || (navConfirm && f == rows.Count)) CloseSettings();
            if (!input.TiltAvailable)
                ApexStyle.Text(new Rect(x + 400f, back.y, w - 400f, 64f), "Kein Bewegungssensor erkannt – Neigungssteuerung fällt auf Touch-Tasten zurück.", 20, ApexStyle.Caution);
        }

        void DrawLoading()
        {
            ApexStyle.Box(new Rect(0, 0, virtualSize.x, virtualSize.y), new Color(0.04f, 0.05f, 0.06f, 1f));
            ApexStyle.Text(new Rect(0, virtualSize.y * 0.5f - 40f, virtualSize.x, 80f), "Strecke wird aufgebaut …", 40, ApexStyle.Fg, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawPause()
        {
            ApexStyle.Box(new Rect(0, 0, virtualSize.x, virtualSize.y), new Color(0f, 0f, 0f, 0.55f));
            float w = 620f, x = (virtualSize.x - w) * 0.5f, y = virtualSize.y * 0.5f - 280f;
            ApexStyle.Text(new Rect(x, y, w, 70f), "PAUSE", 54, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
            var items = new List<(string, Action)>
            {
                ("Weiter", () => sm.Resume()),
                ("Auto auf die Strecke setzen", () => { sm.Resume(); Session?.ResetCarToTrack(); }),
                ("Session neu starten", () => { sm.Resume(); Session?.PlaceOnGrid(); }),
                ("Einstellungen", () => { settingsOpen = true; focus = 0; }),
                ("Zum Hauptmenü", () => sm.TryTransition(GameState.MainMenu)),
            };
            if (autopilotSession) items.RemoveAt(1);
            int f = MenuNav(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, y + 100f + i * 86f, w, 74f);
                if (ApexStyle.Button(r, items[i].Item1, i == f, 28) || (navConfirm && i == f))
                {
                    items[i].Item2();
                    break;
                }
            }
        }

        void DrawCalibration()
        {
            float w = 900f, h = 300f, x = (virtualSize.x - w) * 0.5f, y = virtualSize.y * 0.28f;
            ApexStyle.Box(new Rect(x, y, w, h), ApexStyle.PanelSolid);
            ApexStyle.Text(new Rect(x, y + 20f, w, 50f), "Neigungssteuerung kalibrieren", 36, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
            string hint = input.TiltAvailable
                ? (input.TiltReliability < 0.5f ? "Gerät aufrechter halten – es liegt zu flach." : "Halte das Gerät bequem wie ein Lenkrad und tippe auf „Lenkung zentrieren“.")
                : "Kein Bewegungssensor gefunden. Es werden Touch-Tasten verwendet.";
            ApexStyle.Text(new Rect(x + 30f, y + 80f, w - 60f, 50f), hint, 24, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleCenter);
            ApexStyle.Text(new Rect(x, y + 130f, w, 40f), $"Aktueller Winkel: {input.RollDeg:0}°", 22, ApexStyle.Accent, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (ApexStyle.Button(new Rect(x + (w - 420f) * 0.5f, y + 190f, 420f, 80f), "Lenkung zentrieren", true, 30))
                FinishCalibration();
        }

        void DrawHud()
        {
            if (Session == null) return;
            var safe = SafeRect();
            var sim = Session.Car.Sim;
            var timer = Session.Timer;

            // Timing panel (top-left).
            var tp = new Rect(safe.x + 24f, safe.y + 24f, 420f, 300f);
            ApexStyle.Box(tp, ApexStyle.Panel);
            ApexStyle.Box(new Rect(tp.x, tp.y, 4f, tp.height), ApexStyle.Accent);
            ApexStyle.Text(new Rect(tp.x + 20f, tp.y + 10f, 380f, 34f), timer.LapActive ? $"RUNDE {timer.LapNumber}" : "AUSFAHRT", 24, ApexStyle.TextDim, FontStyle.Bold);
            ApexStyle.Text(new Rect(tp.x + 20f, tp.y + 44f, 380f, 64f), ApexStyle.FormatTime(timer.CurrentLapTime()), 54, timer.LapValid || !timer.LapActive ? ApexStyle.Fg : ApexStyle.Urgent, FontStyle.Bold);
            double delta = timer.LiveDelta(Session.LapDistance);
            ApexStyle.Text(new Rect(tp.x + 20f, tp.y + 108f, 380f, 36f), ApexStyle.FormatDelta(delta), 28, delta < 0 ? ApexStyle.Faster : ApexStyle.Urgent, FontStyle.Bold);
            for (int i = 0; i < 3; i++)
                ApexStyle.Text(new Rect(tp.x + 20f + i * 128f, tp.y + 148f, 128f, 32f), sectorText[i], 21, sectorColor[i]);
            ApexStyle.Text(new Rect(tp.x + 20f, tp.y + 188f, 380f, 32f), "Letzte  " + ApexStyle.FormatTime(timer.LastLapTime) + (timer.LastLapTime > 0 && !timer.LastLapValid ? "  (ungültig)" : ""), 24, timer.LastLapValid || timer.LastLapTime <= 0 ? ApexStyle.Fg : ApexStyle.Urgent);
            ApexStyle.Text(new Rect(tp.x + 20f, tp.y + 222f, 380f, 32f), "Beste    " + ApexStyle.FormatTime(timer.BestLapTime), 24, ApexStyle.Best);
            ApexStyle.Text(new Rect(tp.x + 20f, tp.y + 256f, 380f, 30f), "Rekord   " + ApexStyle.FormatTime(sessionRecord), 20, ApexStyle.TextDim);

            // Speed, gear, RPM (bottom-centre).
            float dw = 420f, dx = (virtualSize.x - dw) * 0.5f, dy = safe.yMax - 170f;
            ApexStyle.Box(new Rect(dx, dy, dw, 150f), ApexStyle.Panel);
            float rpmFrac = Mathf.InverseLerp(4000f, 12500f, sim.EngineRpm);
            const int segments = 15;
            for (int i = 0; i < segments; i++)
            {
                float segW = (dw - 40f) / segments;
                bool lit = rpmFrac * segments > i;
                Color c = i < 9 ? ApexStyle.Faster : (i < 12 ? ApexStyle.Caution : ApexStyle.Urgent);
                ApexStyle.Box(new Rect(dx + 20f + i * segW, dy + 14f, segW - 4f, 14f), lit ? c : new Color(1f, 1f, 1f, 0.08f));
            }
            int gear = sim.gearbox.gear;
            ApexStyle.Text(new Rect(dx + 30f, dy + 34f, 120f, 110f), gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString(), 88, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            float spd = Mathf.Abs(sim.ForwardSpeed) * (Settings.speedInMph ? 2.23694f : 3.6f);
            ApexStyle.Text(new Rect(dx + 170f, dy + 38f, 220f, 70f), spd.ToString("0"), 62, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleRight);
            ApexStyle.Text(new Rect(dx + 170f, dy + 104f, 220f, 30f), Settings.speedInMph ? "mph" : "km/h", 20, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleRight);
            string assists = (sim.TcActive ? "TC  " : "") + (sim.AbsActive ? "ABS" : "");
            if (assists.Length > 0) ApexStyle.Text(new Rect(dx, dy - 34f, dw, 30f), assists, 22, ApexStyle.Caution, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (autopilotSession) ApexStyle.Text(new Rect(dx, dy - 64f, dw, 30f), "AUTO-PILOT", 22, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);

            // Top-right: FPS and pause (keyboard/mouse; touch uses its own pause button).
            ApexStyle.Text(new Rect(safe.xMax - 300f, safe.y + 30f, 160f, 30f), $"{fps:0} FPS", 20, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleRight);
            if (Settings.controlPreset == ControlPreset.Keyboard && ApexStyle.Button(new Rect(safe.xMax - 120f, safe.y + 20f, 96f, 70f), "II", false, 30))
                sm.TryTransition(GameState.Pause);

            if (messageTime > 0f)
                ApexStyle.Text(new Rect(0, safe.y + 34f, virtualSize.x, 60f), message, 36, messageColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (showTelemetry) DrawTelemetry(safe, sim);
            DrawTouchButtons();
        }

        void DrawTelemetry(Rect safe, Apex.Vehicle.VehicleSimulation sim)
        {
            var r = new Rect(safe.xMax - 560f, safe.y + 110f, 536f, 330f);
            ApexStyle.Box(r, ApexStyle.Panel);
            float y = r.y + 12f;
            void Line(string s)
            {
                ApexStyle.Text(new Rect(r.x + 16f, y, r.width - 32f, 28f), s, 19, ApexStyle.Fg);
                y += 28f;
            }
            Line($"rpm {sim.EngineRpm:00000}  Gas {sim.throttle:0.00}  Bremse {sim.brake:0.00}  Lenk {sim.steer:+0.00;-0.00}");
            Line($"Lenkwinkel {sim.SteerAngle * ApexMath.Rad2Deg:+0.0;-0.0}°  TC-Kürzung {sim.TcCut:0.00}");
            Line($"Abtrieb V {sim.DownforceFront:0} N  H {sim.DownforceRear:0} N  Luftwid. {sim.Drag:0} N");
            foreach (var w in sim.wheels)
                Line($"{w.label} {w.load,5:0}N  sr {w.slipRatio:+0.00;-0.00}  sa {w.slipAngle * ApexMath.Rad2Deg:+0.0;-0.0}°  Fed. {w.compression * 1000f:0}mm  {w.surface}");
            Line($"Neigung {input.RollDeg:+0;-0}°  Zuverl. {input.TiltReliability:0.00}  Physik {1f / Time.fixedDeltaTime:0} Hz");
        }

        void DrawTouchButtons()
        {
            if (Settings.controlPreset == ControlPreset.Keyboard) return;
            float h = UnityEngine.Screen.height;
            foreach (var b in input.Buttons)
            {
                var r = new Rect(b.rect.x / uiScale, (h - b.rect.yMax) / uiScale, b.rect.width / uiScale, b.rect.height / uiScale);
                bool held = input.touch.IsHeld(b.id);
                float a = Settings.touchButtonOpacity * (held ? 1.6f : 1f);
                Color fill = b.id == TouchLayout.Throttle ? new Color(0.3f, 0.9f, 0.45f, a * 0.6f)
                    : b.id == TouchLayout.Brake ? new Color(1f, 0.32f, 0.27f, a * 0.6f)
                    : new Color(1f, 1f, 1f, a * 0.35f);
                ApexStyle.Box(r, fill);
                ApexStyle.Text(r, b.label, 26, new Color(1f, 1f, 1f, Mathf.Clamp01(a * 2f)), FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }
    }
}
