using System;
using System.Collections.Generic;
using Apex.Tracks;
using Apex.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Broadcast-style race HUD of APEX // RACE CONTROL (docs/UI_UX_OVERHAUL.md section 4, mockup ui_mock_race_hud.jpg):
    /// tower, race status with flag strip, event banner, dash with 15 shift lights, tyres, minimap, timing and the
    /// five red start lights. Positions come from <see cref="HudLayout"/> (presets, anchors, the player's editor
    /// adjustments); values come from a <see cref="HudFrame"/> the game fills from the simulation. Also hosts the
    /// HUD editor (move, resize, fade, reset per element; keyboard, gamepad and mouse drag).
    /// </summary>
    public sealed class RaceHudView
    {
        public struct TowerRow
        {
            public int position;
            public string code, gap, compound;
            public Color team, compoundColour;
            public bool player, pit;
        }

        /// <summary>Everything the HUD shows this frame (filled by the game from the simulation).</summary>
        public sealed class HudFrame
        {
            public string towerHeader = "";
            public bool checkered;
            public readonly List<TowerRow> tower = new List<TowerRow>();
            public string position = "", lap = "", conditions = "";
            public bool wet;
            public string flag;
            public Color flagColour;
            public string gear = "N", unit = "km/h", assists = "", pitNote = "";
            public int speed;
            public float rpmFraction;
            public int shiftLit;
            public bool shiftFlash;
            public string compound = "";
            public Color compoundColour = Color.white;
            public readonly float[] tyreWear = new float[4], tyreTemp = new float[4];
            public readonly TyreBand[] tyreBand = new TyreBand[4];
            public string lapTime = "", last = "", best = "", record = "";
            public bool lapValid = true;
            public double delta = double.NaN;
            public readonly string[] sectors = { "", "", "" };
            public readonly Color[] sectorColours = new Color[3];
            public bool lights;
            public int lightsOn;
            public readonly List<(float east, float north, Color colour, bool player)> cars = new List<(float, float, Color, bool)>();
            public bool legacy;
        }

        public event Action EditorClosed;
        /// <summary>A team radio entry was clicked (index into the labels given to <see cref="SetRadio"/>).</summary>
        public event Action<int> RadioClicked;

        readonly ApexUi ui;
        readonly VisualElement[] boxes = new VisualElement[HudLayout.All.Length];
        HudPreset preset = HudPreset.Standard;
        HudLayoutData layout = new HudLayoutData();
        float hudScale = 1f;
        public bool ReducedMotion;

        // Tower.
        Label towerHeader, towerFlag;
        readonly List<(VisualElement row, VisualElement hl, VisualElement team, Label pos, Label code, Label gap, Label tyre)> towerRows = new List<(VisualElement, VisualElement, VisualElement, Label, Label, Label, Label)>();
        // Info.
        Label infoPos, infoLap, infoCond, flagText;
        VisualElement flagStrip, flagEdge;
        // Events.
        VisualElement eventBox, eventEdge;
        Label eventTitle, eventDetail;
        HudEvent shownEvent;
        float eventAnim;
        // Dash.
        readonly VisualElement[] leds = new VisualElement[ShiftLights.Count];
        Label gear, speed, unit, assists, pitNote;
        VisualElement rpmFill;
        // Tyres.
        readonly VisualElement[] tyreFill = new VisualElement[4];
        readonly Label[] tyreWearBar = new Label[4];
        readonly Label[] tyreTemp = new Label[4];
        Label compound;
        // Map.
        TrackMapElement map;
        TrackMapLine mapLine;
        readonly List<VisualElement> carDots = new List<VisualElement>();
        // Timing.
        Label lapTime, delta, last, best, record;
        readonly VisualElement[] sectorBars = new VisualElement[3];
        readonly Label[] sectorLabels = new Label[3];
        // Lights and notes.
        VisualElement lightsBox, legacyNote;
        readonly VisualElement[] lamps = new VisualElement[10];
        // Team radio.
        VisualElement radioBox;
        readonly List<(VisualElement row, Label label)> radioRows = new List<(VisualElement, Label)>();
        readonly VisualElement[] waveBars = new VisualElement[28];
        bool radioOpen;
        int radioFocus = -1;
        sealed class RadioEntry { public int index; }
        // Editor.
        HudEditor editor;
        VisualElement editorLayer, editorPanel;
        readonly VisualElement[] outlines = new VisualElement[HudLayout.All.Length];
        Label editorTitle, editorValues, editorIssues;
        bool dragging;
        Vector2 dragLast;
        float repeatTimer;

        public RaceHudView()
        {
            ui = new ApexUi("ApexHud", 5);
            Build();
            SetVisible(false);
        }

        public bool Visible { get; private set; } = true;

        public void Dispose() => ui.Dispose();

        /// <summary>Rectangles of the visible elements (panel coordinates) for the F2 layout check.</summary>
        public IEnumerable<(string name, HudRect rect)> VisibleRects()
        {
            float pw = PanelWidth;
            foreach (var e in HudLayout.All)
            {
                if (!HudLayout.Shows(preset, e)) continue;
                var l = layout.Get(e);
                yield return ("HUD:" + HudLayout.Name(e), HudLayout.Rect(e, new HudElementLayout { element = l.element, dx = l.dx, dy = l.dy, scale = l.scale * hudScale }, pw, HudLayout.RefHeight));
            }
        }
        public bool Editing => editor != null;

        public void SetVisible(bool on)
        {
            if (on == Visible) return;
            Visible = on;
            ui.SetVisible(on);
        }

        static float PanelWidth => HudLayout.RefHeight * Screen.width / Mathf.Max(1f, Screen.height);

        // --- Build --------------------------------------------------------------------------------------------

        void Build()
        {
            foreach (var e in HudLayout.All)
            {
                var b = new VisualElement { pickingMode = PickingMode.Ignore };
                b.style.position = Position.Absolute;
                b.style.transformOrigin = new TransformOrigin(0, 0, 0);
                ui.root.Add(b);
                boxes[(int)e] = b;
            }
            BuildTower(boxes[(int)HudElement.Tower]);
            BuildInfo(boxes[(int)HudElement.Info]);
            BuildEvents(boxes[(int)HudElement.Events]);
            BuildDash(boxes[(int)HudElement.Dash]);
            BuildTyres(boxes[(int)HudElement.Tyres]);
            BuildMap(boxes[(int)HudElement.Map]);
            BuildTiming(boxes[(int)HudElement.Timing]);
            BuildLights();
            BuildRadio();
            legacyNote = ApexUi.Text(ui.root, Loc.T("ALTES FAHRVERHALTEN (VERGLEICH)"), ApexUi.BodyStrong, 14, ApexUi.C(UiTokens.Amber), 2f);
            legacyNote.style.position = Position.Absolute; legacyNote.style.top = 12; legacyNote.style.left = 0; legacyNote.style.right = 0;
            legacyNote.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        static VisualElement Size(VisualElement e, HudElement which)
        {
            var r = HudLayout.DefaultRect(which, 1920f, 1080f);
            e.style.width = r.w; e.style.height = r.h;
            return e;
        }

        void BuildTower(VisualElement box)
        {
            Size(box, HudElement.Tower).style.height = StyleKeyword.Auto;
            var p = ApexUi.Panel(box, UiTokens.PanelAlpha);
            p.style.paddingBottom = 4;
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.style.flexDirection = FlexDirection.Row; head.style.height = 34; head.style.alignItems = Align.Center;
            head.style.paddingLeft = 14; head.style.paddingRight = 12;
            p.Add(head);
            var red = ApexUi.Box(head, ApexUi.C(UiTokens.Red)); red.style.width = 3; red.style.height = 16; red.style.marginRight = 10;
            towerHeader = ApexUi.Text(head, "", ApexUi.DisplayStrong, 20, ApexUi.C(UiTokens.Ivory), 2f);
            var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; head.Add(sp);
            towerFlag = ApexUi.Text(head, Loc.T("ZIEL"), ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Ivory), 2f, TextAnchor.MiddleRight);
            for (int i = 0; i < 20; i++)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.height = 28;
                p.Add(row);
                var hl = ApexUi.Box(row, Color.clear);
                ApexUi.Place(hl, 0, 0); hl.style.right = 0; hl.style.bottom = 0;
                ApexUi.Background(hl, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), 0.32f, 0.08f));
                var edge = ApexUi.Box(hl, ApexUi.C(UiTokens.Cyan)); ApexUi.Place(edge, 0, 0, 3, -1); edge.style.bottom = 0;
                var pos = ApexUi.Text(row, "", ApexUi.DisplayStrong, 18, ApexUi.C(UiTokens.Ivory), 0f, TextAnchor.MiddleRight);
                pos.style.width = 34;
                var team = ApexUi.Box(row, Color.white); team.style.width = 3; team.style.height = 16; team.style.marginLeft = 12; team.style.marginRight = 10;
                var code = ApexUi.Text(row, "", ApexUi.DisplayStrong, 18, ApexUi.C(UiTokens.Ivory), 1.5f); code.style.width = 70;
                var gap = ApexUi.Text(row, "", ApexUi.Body, 15, ApexUi.C(UiTokens.Steel), 0f, TextAnchor.MiddleRight); gap.style.width = 150;
                var tyre = ApexUi.Text(row, "", ApexUi.BodyStrong, 14, Color.white, 0f, TextAnchor.MiddleCenter); tyre.style.width = 40;
                towerRows.Add((row, hl, team, pos, code, gap, tyre));
            }
        }

        void BuildInfo(VisualElement box)
        {
            Size(box, HudElement.Info);
            var p = ApexUi.Panel(box, UiTokens.PanelAlpha);
            p.style.paddingLeft = 18; p.style.paddingRight = 16; p.style.paddingTop = 8; p.style.height = 82;
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row; top.style.alignItems = Align.FlexEnd;
            p.Add(top);
            infoPos = ApexUi.Text(top, "", ApexUi.DisplayStrong, 40, ApexUi.C(UiTokens.Ivory), 1f);
            var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; top.Add(sp);
            infoLap = ApexUi.Text(top, "", ApexUi.Display, 22, ApexUi.C(UiTokens.Steel), 1.5f, TextAnchor.MiddleRight);
            infoLap.style.marginBottom = 6;
            infoCond = ApexUi.Text(p, "", ApexUi.Body, 14, ApexUi.C(UiTokens.Steel));
            flagStrip = ApexUi.Box(box, ApexUi.C(UiTokens.Navy, 0.9f));
            flagStrip.style.height = 36; flagStrip.style.marginTop = 6;
            flagStrip.style.flexDirection = FlexDirection.Row; flagStrip.style.alignItems = Align.Center;
            flagEdge = ApexUi.Box(flagStrip, Color.white); flagEdge.style.width = 6; flagEdge.style.height = 36; flagEdge.style.marginRight = 14;
            flagText = ApexUi.Text(flagStrip, "", ApexUi.DisplayStrong, 20, Color.white, 2f);
        }

        void BuildEvents(VisualElement box)
        {
            Size(box, HudElement.Events);
            box.style.alignItems = Align.Center;
            eventBox = ApexUi.Panel(box, UiTokens.PanelStrongAlpha);
            eventBox.style.flexDirection = FlexDirection.Row; eventBox.style.alignItems = Align.Center;
            eventBox.style.height = 56; eventBox.style.paddingRight = 26;
            eventEdge = ApexUi.Box(eventBox, ApexUi.C(UiTokens.Cyan)); eventEdge.style.width = 4; eventEdge.style.height = 56; eventEdge.style.marginRight = 20;
            eventTitle = ApexUi.Text(eventBox, "", ApexUi.DisplayStrong, 28, ApexUi.C(UiTokens.Ivory), 2.5f);
            eventDetail = ApexUi.Text(eventBox, "", ApexUi.Body, 16, ApexUi.C(UiTokens.Steel));
            eventDetail.style.marginLeft = 16;
            eventBox.style.opacity = 0f;
        }

        void BuildDash(VisualElement box)
        {
            Size(box, HudElement.Dash);
            var p = ApexUi.Panel(box, UiTokens.PanelAlpha);
            p.style.position = Position.Absolute; p.style.left = 0; p.style.right = 0; p.style.top = 0; p.style.bottom = 0;
            var ledRow = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(ledRow, 24, 12, 472, 12);
            ledRow.style.flexDirection = FlexDirection.Row; ledRow.style.justifyContent = Justify.SpaceBetween;
            p.Add(ledRow);
            for (int i = 0; i < ShiftLights.Count; i++)
            {
                var l = ApexUi.Box(ledRow, Color.white);
                l.style.width = 26; l.style.height = 10;
                l.style.borderTopLeftRadius = 5; l.style.borderTopRightRadius = 5; l.style.borderBottomLeftRadius = 5; l.style.borderBottomRightRadius = 5;
                leds[i] = l;
            }
            speed = ApexUi.Text(p, "0", ApexUi.DisplayStrong, UiTokens.HudSpeed, ApexUi.C(UiTokens.Ivory), 0f, TextAnchor.MiddleRight);
            ApexUi.Place(speed, 20, 36, 150, 62);
            unit = ApexUi.Text(p, "km/h", ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Steel), 2f, TextAnchor.MiddleRight);
            ApexUi.Place(unit, 20, 96, 150, 18);
            gear = ApexUi.Text(p, "N", ApexUi.DisplayStrong, UiTokens.HudGear, ApexUi.C(UiTokens.Cyan), 0f, TextAnchor.MiddleCenter);
            ApexUi.Place(gear, 200, 26, 120, 96);
            assists = ApexUi.Text(p, "", ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Amber), 1.5f);
            ApexUi.Place(assists, 350, 50, 160, 20);
            pitNote = ApexUi.Text(p, "", ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Cyan), 1.5f);
            ApexUi.Place(pitNote, 350, 74, 160, 36);
            pitNote.style.whiteSpace = WhiteSpace.Normal;
            var rpmBack = ApexUi.Box(p, ApexUi.C(UiTokens.Surface));
            ApexUi.Place(rpmBack, 24, 130, 472, 4);
            rpmFill = ApexUi.Box(rpmBack, ApexUi.C(UiTokens.Ivory, 0.85f));
            ApexUi.Place(rpmFill, 0, 0, 0, 4);
        }

        void BuildTyres(VisualElement box)
        {
            Size(box, HudElement.Tyres);
            var p = ApexUi.Panel(box, UiTokens.PanelAlpha);
            p.style.position = Position.Absolute; p.style.left = 0; p.style.right = 0; p.style.top = 0; p.style.bottom = 0;
            var cap = ApexUi.Caption(p, Loc.T("Reifen")); ApexUi.Place(cap, 16, 12);
            compound = ApexUi.Text(p, "", ApexUi.DisplayStrong, 22, Color.white, 1f, TextAnchor.MiddleCenter);
            ApexUi.Place(compound, 176, 52, 60, 40);
            for (int i = 0; i < 4; i++)
            {
                float x = 24 + (i % 2) * 74, y = 38 + (i / 2) * 54;
                var t = ApexUi.Box(p, ApexUi.C(UiTokens.Surface));
                ApexUi.Place(t, x, y, 30, 46);
                t.style.borderTopLeftRadius = 6; t.style.borderTopRightRadius = 6; t.style.borderBottomLeftRadius = 6; t.style.borderBottomRightRadius = 6;
                t.style.overflow = Overflow.Hidden;
                tyreFill[i] = ApexUi.Box(t, Color.white);
                tyreFill[i].style.position = Position.Absolute; tyreFill[i].style.left = 0; tyreFill[i].style.right = 0; tyreFill[i].style.bottom = 0; tyreFill[i].style.height = new Length(100, LengthUnit.Percent);
                tyreTemp[i] = ApexUi.Text(p, "", ApexUi.BodyStrong, 12, ApexUi.C(UiTokens.Ivory), 0f, i % 2 == 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                ApexUi.Place(tyreTemp[i], i % 2 == 0 ? x - 44 : x + 34, y + 6, 40, 16);
                tyreWearBar[i] = ApexUi.Text(p, "", ApexUi.Body, 11, ApexUi.C(UiTokens.Steel), 0f, i % 2 == 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                ApexUi.Place(tyreWearBar[i], i % 2 == 0 ? x - 44 : x + 34, y + 24, 40, 16);
            }
        }

        void BuildMap(VisualElement box)
        {
            Size(box, HudElement.Map);
            var p = ApexUi.Panel(box, UiTokens.PanelAlpha);
            p.style.position = Position.Absolute; p.style.left = 0; p.style.right = 0; p.style.top = 0; p.style.bottom = 0;
            map = new TrackMapElement();
            var r = HudLayout.DefaultRect(HudElement.Map, 1920f, 1080f);
            ApexUi.Place(map, 0, 0, r.w, r.h);
            p.Add(map);
        }

        void BuildTiming(VisualElement box)
        {
            Size(box, HudElement.Timing);
            var p = ApexUi.Panel(box, UiTokens.PanelAlpha);
            p.style.position = Position.Absolute; p.style.left = 0; p.style.right = 0; p.style.top = 0; p.style.bottom = 0;
            p.style.paddingLeft = 20; p.style.paddingRight = 20; p.style.paddingTop = 12;
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row; top.style.alignItems = Align.Center;
            p.Add(top);
            lapTime = ApexUi.Text(top, "", ApexUi.DisplayStrong, 38, ApexUi.C(UiTokens.Ivory), 0.5f);
            var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; top.Add(sp);
            delta = ApexUi.Text(top, "", ApexUi.DisplayStrong, 24, ApexUi.C(UiTokens.Ivory), 0.5f, TextAnchor.MiddleRight);
            delta.style.paddingLeft = 10; delta.style.paddingRight = 10; delta.style.height = 32;
            Row(p, Loc.T("Letzte"), out last);
            Row(p, Loc.T("Beste"), out best);
            best.style.color = ApexUi.C(UiPalette.Current.best);
            Row(p, Loc.T("Rekord"), out record);
            var bars = new VisualElement { pickingMode = PickingMode.Ignore };
            bars.style.flexDirection = FlexDirection.Row; bars.style.marginTop = 10;
            p.Add(bars);
            for (int i = 0; i < 3; i++)
            {
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.style.flexGrow = 1; col.style.marginRight = i < 2 ? 8 : 0;
                bars.Add(col);
                sectorBars[i] = ApexUi.Box(col, ApexUi.C(UiTokens.Surface)); sectorBars[i].style.height = 4;
                sectorLabels[i] = ApexUi.Text(col, "", ApexUi.Body, 12, ApexUi.C(UiTokens.Steel));
                sectorLabels[i].style.marginTop = 3;
            }
        }

        static void Row(VisualElement parent, string caption, out Label value)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row; r.style.height = 22;
            parent.Add(r);
            ApexUi.Text(r, caption.ToUpperInvariant(), ApexUi.BodyStrong, 12, ApexUi.C(UiTokens.Steel), 1.5f).style.width = 80;
            value = ApexUi.Text(r, "", ApexUi.Display, 18, ApexUi.C(UiTokens.Ivory));
        }

        void BuildLights()
        {
            lightsBox = ApexUi.Panel(ui.root, UiTokens.PanelStrongAlpha);
            lightsBox.style.position = Position.Absolute;
            lightsBox.style.flexDirection = FlexDirection.Row;
            lightsBox.style.paddingLeft = 22; lightsBox.style.paddingRight = 10; lightsBox.style.paddingTop = 14; lightsBox.style.paddingBottom = 14;
            for (int i = 0; i < 5; i++)
            {
                // One pod per light column, two lamps each (the gantry look).
                var pod = ApexUi.Box(lightsBox, ApexUi.C(UiTokens.Navy));
                pod.style.width = 64; pod.style.marginRight = 12; pod.style.paddingTop = 6; pod.style.paddingBottom = 6;
                pod.style.alignItems = Align.Center;
                pod.style.borderTopLeftRadius = 10; pod.style.borderTopRightRadius = 10; pod.style.borderBottomLeftRadius = 10; pod.style.borderBottomRightRadius = 10;
                for (int k = 0; k < 2; k++)
                {
                    var lamp = ApexUi.Box(pod, Color.black);
                    lamp.style.width = 44; lamp.style.height = 44; lamp.style.marginTop = k == 0 ? 0 : 8;
                    lamp.style.borderTopLeftRadius = 22; lamp.style.borderTopRightRadius = 22; lamp.style.borderBottomLeftRadius = 22; lamp.style.borderBottomRightRadius = 22;
                    lamps[i * 2 + k] = lamp;
                }
            }
        }

        void BuildRadio()
        {
            radioBox = ApexUi.Panel(ui.root, UiTokens.PanelStrongAlpha);
            radioBox.style.position = Position.Absolute; radioBox.style.width = 340;
            radioBox.style.paddingLeft = 16; radioBox.style.paddingRight = 16; radioBox.style.paddingTop = 12; radioBox.style.paddingBottom = 12;
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center; head.style.height = 30;
            radioBox.Add(head);
            var edge = ApexUi.Box(head, ApexUi.C(UiTokens.Amber)); edge.style.width = 3; edge.style.height = 18; edge.style.marginRight = 10;
            ApexUi.Text(head, Loc.T("TEAMFUNK"), ApexUi.DisplayStrong, 20, ApexUi.C(UiTokens.Amber), 2.5f);
            // Waveform: animated bars while the channel is open (decoration of the open channel, no audio analysis).
            var wave = new VisualElement { pickingMode = PickingMode.Ignore };
            wave.style.flexDirection = FlexDirection.Row; wave.style.alignItems = Align.Center; wave.style.height = 28; wave.style.marginLeft = 16; wave.style.flexGrow = 1;
            head.Add(wave);
            for (int i = 0; i < waveBars.Length; i++)
            {
                var b = ApexUi.Box(wave, ApexUi.C(UiTokens.Amber, 0.8f));
                b.style.width = 3; b.style.marginRight = 2; b.style.height = 4;
                waveBars[i] = b;
            }
            radioBox.style.display = DisplayStyle.None;
        }

        /// <summary>Team radio panel (keyboard/mouse): entries with the focused one highlighted; null hides it.</summary>
        public void SetRadio(bool open, IList<string> labels, int focus)
        {
            radioOpen = open && labels != null;
            radioBox.style.display = radioOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!radioOpen) return;
            while (radioRows.Count < labels.Count)
            {
                var row = new VisualElement { pickingMode = PickingMode.Position, userData = new RadioEntry { index = radioRows.Count } };
                row.style.height = 36; row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.paddingLeft = 10; row.style.marginTop = 2;
                radioBox.Add(row);
                var l = ApexUi.Text(row, "", ApexUi.Body, 16, ApexUi.C(UiTokens.Ivory));
                radioRows.Add((row, l));
            }
            for (int i = 0; i < radioRows.Count; i++)
            {
                bool on = i < labels.Count;
                radioRows[i].row.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) continue;
                radioRows[i].label.text = $"{i + 1}   {labels[i]}";
                bool f = i == focus;
                radioRows[i].row.style.backgroundColor = f ? ApexUi.C(UiTokens.Amber, 0.18f) : Color.clear;
                radioRows[i].label.style.color = f ? ApexUi.C(UiTokens.Ivory) : ApexUi.C(UiTokens.Ivory, 0.75f);
            }
            radioFocus = focus;
        }

        void TickRadio(double now)
        {
            if (!radioOpen) return;
            float pw = PanelWidth;
            radioBox.style.left = pw - 150f - 340f; radioBox.style.top = 240;
            for (int i = 0; i < waveBars.Length; i++)
            {
                float a = ReducedMotion ? 0.4f : 0.5f + 0.5f * Mathf.Sin((float)now * (5f + i * 0.37f) + i * 1.7f) * Mathf.Sin((float)now * 2.3f + i * 0.5f);
                waveBars[i].style.height = 3f + 18f * Mathf.Abs(a);
            }
            var p = Pointer.current;
            if (p != null && p.press.wasReleasedThisFrame && ui.root.panel != null)
            {
                var screen = p.position.ReadValue();
                var pos = RuntimePanelUtils.ScreenToPanel(ui.root.panel, new Vector2(screen.x, Screen.height - screen.y));
                var e = ui.PickData<RadioEntry>(pos);
                if (e != null) RadioClicked?.Invoke(e.index);
            }
        }

        // --- Configuration ------------------------------------------------------------------------------------

        public void Configure(HudPreset p, HudLayoutData l, float scale, bool reducedMotion)
        {
            preset = p;
            layout = l ?? new HudLayoutData();
            hudScale = scale;
            ReducedMotion = reducedMotion;
            ApplyLayout();
        }

        /// <summary>The circuit for the minimap (once per session).</summary>
        public void SetTrack(CircuitDefinition def, TrackGeometry g)
        {
            var r = HudLayout.DefaultRect(HudElement.Map, 1920f, 1080f);
            mapLine = TrackMap.Fit(def, g, r.w, r.h, 16f, 220);
            carDots.Clear();   // Set() below clears the map's children, the dots included
            map.Set(mapLine, 3f, false, ApexUi.C(UiTokens.Ivory, 0.75f), false);
            map.Progress = 1f;
            shownEvent = null;
        }

        void ApplyLayout()
        {
            float pw = PanelWidth;
            foreach (var e in HudLayout.All)
            {
                var b = boxes[(int)e];
                // While editing with the HUD off, the Standard elements are shown so they can be placed.
                bool on = HudLayout.Shows(Editing && preset == HudPreset.Off ? HudPreset.Standard : preset, e);
                b.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                // Flag warnings stay visible with the HUD off: the info box keeps only its flag strip.
                if (e == HudElement.Info && preset == HudPreset.Off && !Editing) b.style.display = DisplayStyle.Flex;
                var l = layout.Get(e);
                var eff = new HudElementLayout { element = l.element, dx = l.dx, dy = l.dy, scale = l.scale * hudScale, opacity = l.opacity };
                var r = HudLayout.Rect(e, eff, pw, HudLayout.RefHeight);
                b.style.left = r.x; b.style.top = r.y;
                ApexUi.Scale(b, eff.scale);
                b.style.opacity = l.opacity;
            }
            var lw = 5 * 76f + 32f;
            lightsBox.style.left = (pw - lw) * 0.5f; lightsBox.style.top = 140;
        }

        // --- Per frame ----------------------------------------------------------------------------------------

        public void Tick(HudFrame f, RaceEventFeed feed, double now, float dt)
        {
            if (!Visible) return;
            bool infoOnlyFlag = preset == HudPreset.Off && !Editing;
            // Tower.
            towerHeader.text = f.towerHeader;
            towerFlag.style.display = f.checkered ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < towerRows.Count; i++)
            {
                var r = towerRows[i];
                bool on = i < f.tower.Count;
                r.row.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) continue;
                var t = f.tower[i];
                r.pos.text = t.position.ToString();
                r.code.text = t.code;
                r.code.style.color = t.player ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Ivory);
                r.gap.text = t.pit ? "BOX" : t.gap;
                r.gap.style.color = t.pit ? ApexUi.C(UiTokens.Amber) : ApexUi.C(UiTokens.Steel);
                r.team.style.backgroundColor = t.team;
                r.tyre.text = t.compound;
                r.tyre.style.color = t.compoundColour;
                r.hl.style.display = t.player ? DisplayStyle.Flex : DisplayStyle.None;
            }
            // Info and flag.
            var infoPanel = infoPos.parent.parent;
            infoPanel.style.display = infoOnlyFlag ? DisplayStyle.None : DisplayStyle.Flex;
            infoPos.text = f.position;
            infoLap.text = f.lap;
            infoCond.text = f.conditions;
            infoCond.style.color = f.wet ? ApexUi.C(UiTokens.Amber) : ApexUi.C(UiTokens.Steel);
            bool flag = !string.IsNullOrEmpty(f.flag);
            flagStrip.style.display = flag ? DisplayStyle.Flex : DisplayStyle.None;
            if (flag) { flagText.text = f.flag; flagText.style.color = f.flagColour; flagEdge.style.backgroundColor = f.flagColour; }
            // Dash.
            gear.text = f.gear;
            speed.text = f.speed.ToString();
            unit.text = f.unit;
            assists.text = f.assists;
            pitNote.text = f.pitNote;
            rpmFill.style.width = 472f * Mathf.Clamp01(f.rpmFraction);
            bool blink = f.shiftFlash && (ReducedMotion || Mathf.Repeat((float)now * 8f, 1f) < 0.5f);
            for (int i = 0; i < leds.Length; i++)
            {
                var c = ShiftLights.ColourOf(i);
                var on = f.shiftFlash ? blink : i < f.shiftLit;
                Color col = c == ShiftLights.Colour.Green ? ApexUi.C(UiTokens.FlagGreen) : c == ShiftLights.Colour.Red ? ApexUi.C(UiTokens.FlagRed) : ApexUi.C(UiTokens.FlagBlue);
                if (f.shiftFlash) col = ApexUi.C(UiTokens.FlagBlue);
                leds[i].style.backgroundColor = on ? col : ApexUi.C(UiTokens.Surface, 0.9f);
            }
            // Tyres.
            compound.text = f.compound;
            compound.style.color = f.compoundColour;
            for (int i = 0; i < 4; i++)
            {
                var band = f.tyreBand[i];
                var pal = UiPalette.Current;
                Color c = band == TyreBand.Cold ? ApexUi.C(pal.cold) : band == TyreBand.Hot ? ApexUi.C(pal.hot) : ApexUi.C(pal.optimal);
                tyreFill[i].style.backgroundColor = c;
                tyreFill[i].style.height = new Length(Mathf.Clamp01(1f - f.tyreWear[i]) * 100f, LengthUnit.Percent);
                tyreTemp[i].text = $"{f.tyreTemp[i]:0}°";
                tyreWearBar[i].text = $"{f.tyreWear[i] * 100f:0} %";
            }
            // Map dots.
            if (mapLine != null)
            {
                while (carDots.Count < f.cars.Count)
                {
                    var d = ApexUi.Box(map, Color.white);
                    d.style.position = Position.Absolute;
                    carDots.Add(d);
                }
                for (int i = 0; i < carDots.Count; i++)
                {
                    bool on = i < f.cars.Count;
                    carDots[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!on) continue;
                    var c = f.cars[i];
                    float s = c.player ? 10f : 7f;
                    carDots[i].style.left = mapLine.MapX(c.east) - s * 0.5f;
                    carDots[i].style.top = mapLine.MapY(c.north) - s * 0.5f;
                    carDots[i].style.width = s; carDots[i].style.height = s;
                    float rad = s * 0.5f;
                    carDots[i].style.borderTopLeftRadius = rad; carDots[i].style.borderTopRightRadius = rad; carDots[i].style.borderBottomLeftRadius = rad; carDots[i].style.borderBottomRightRadius = rad;
                    carDots[i].style.backgroundColor = c.player ? ApexUi.C(UiTokens.Cyan) : c.colour;
                }
                if (f.cars.Count > 0) carDots[f.cars.FindIndex(c => c.player) < 0 ? 0 : f.cars.FindIndex(c => c.player)].BringToFront();
            }
            // Timing.
            lapTime.text = f.lapTime;
            lapTime.style.color = f.lapValid ? ApexUi.C(UiTokens.Ivory) : ApexUi.C(UiPalette.Current.bad);
            bool hasDelta = !double.IsNaN(f.delta);
            delta.style.display = hasDelta ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasDelta)
            {
                delta.text = (f.delta < 0 ? "−" : "+") + Math.Abs(f.delta).ToString("0.000");
                delta.style.backgroundColor = f.delta < 0 ? ApexUi.C(UiPalette.Current.good, 0.85f) : ApexUi.C(UiPalette.Current.bad, 0.85f);
            }
            last.text = f.last; best.text = f.best; record.text = f.record;
            for (int i = 0; i < 3; i++)
            {
                sectorLabels[i].text = f.sectors[i];
                sectorBars[i].style.backgroundColor = f.sectorColours[i];
            }
            // Lights.
            lightsBox.style.display = f.lights ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < lamps.Length; i++)
                lamps[i].style.backgroundColor = f.lights && f.lightsOn > i / 2 ? ApexUi.C(UiTokens.FlagRed) : new Color(0.08f, 0.02f, 0.03f);
            legacyNote.style.display = f.legacy ? DisplayStyle.Flex : DisplayStyle.None;
            // Event banner: slides in, fades out at the end.
            var ev = feed.Update(now);
            bool eventsOn = HudLayout.Shows(preset, HudElement.Events) || (ev != null && ev.kind == HudEventKind.Flag);
            if (ev != shownEvent)
            {
                shownEvent = ev;
                eventAnim = 0f;
                if (ev != null)
                {
                    eventTitle.text = ev.title;
                    eventDetail.text = ev.detail;
                    eventDetail.style.display = string.IsNullOrEmpty(ev.detail) ? DisplayStyle.None : DisplayStyle.Flex;
                    eventEdge.style.backgroundColor = ToneColour(ev.tone);
                    eventTitle.style.color = ev.tone == HudTone.Best ? ApexUi.C(UiPalette.Current.best) : ApexUi.C(UiTokens.Ivory);
                    // Plain messages (radio replies, penalties) are sentences: smaller than the headline events.
                    eventTitle.style.fontSize = ev.kind == HudEventKind.Info || ev.kind == HudEventKind.Notice ? 20 : 28;
                    eventTitle.style.letterSpacing = ev.kind == HudEventKind.Info || ev.kind == HudEventKind.Notice ? 0.3f : 2.5f;
                }
            }
            if (preset == HudPreset.Off && !Editing) boxes[(int)HudElement.Events].style.display = eventsOn ? DisplayStyle.Flex : DisplayStyle.None;
            eventAnim += dt;
            float a = ev == null ? 0f : Mathf.Min(UiMotion.OutCubic(Mathf.Clamp01(eventAnim / UiTokens.PanelTime)), Mathf.Clamp01((float)(ev.Ends - now) / 0.3f));
            eventBox.style.opacity = Editing && ev == null ? 0.35f : a;
            ApexUi.Translate(eventBox, 0f, ReducedMotion ? 0f : -12f * (1f - a));
            if (Editing && ev == null) { eventTitle.text = Loc.T("EREIGNISSE"); eventDetail.text = Loc.T("Beispiel"); eventDetail.style.display = DisplayStyle.Flex; }

            TickRadio(now);
            if (Editing) TickEditor(dt);
        }

        static Color ToneColour(HudTone t)
        {
            switch (t)
            {
                case HudTone.Good: return ApexUi.C(UiPalette.Current.good);
                case HudTone.Best: return ApexUi.C(UiPalette.Current.best);
                case HudTone.Warning: return ApexUi.C(UiTokens.Amber);
                case HudTone.Danger: return ApexUi.C(UiPalette.Current.bad);
                default: return ApexUi.C(UiTokens.Cyan);
            }
        }

        // --- HUD editor ---------------------------------------------------------------------------------------

        public void OpenEditor()
        {
            editor = new HudEditor(layout);
            if (editorLayer == null) BuildEditor();
            editorLayer.style.display = DisplayStyle.Flex;
            ApplyLayout();
            RefreshEditor();
        }

        public void CloseEditor()
        {
            if (editor == null) return;
            editor = null;
            dragging = false;
            editorLayer.style.display = DisplayStyle.None;
            ApplyLayout();
            EditorClosed?.Invoke();
        }

        void BuildEditor()
        {
            editorLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            editorLayer.style.position = Position.Absolute;
            editorLayer.style.left = 0; editorLayer.style.right = 0; editorLayer.style.top = 0; editorLayer.style.bottom = 0;
            ui.root.Add(editorLayer);
            // Keep-clear zone: the road ahead of the car.
            var clear = ApexUi.Box(editorLayer, ApexUi.C(UiTokens.Amber, 0.06f));
            clear.name = "keepClear";
            var amber = ApexUi.C(UiTokens.Amber, 0.5f);
            clear.style.borderLeftWidth = 1; clear.style.borderRightWidth = 1; clear.style.borderTopWidth = 1; clear.style.borderBottomWidth = 1;
            clear.style.borderLeftColor = amber; clear.style.borderRightColor = amber; clear.style.borderTopColor = amber; clear.style.borderBottomColor = amber;
            var cl = ApexUi.Text(clear, Loc.T("FAHRBAHN FREIHALTEN"), ApexUi.BodyStrong, 12, ApexUi.C(UiTokens.Amber, 0.8f), 3f);
            ApexUi.Place(cl, 12, 8);
            foreach (var e in HudLayout.All)
            {
                var o = new VisualElement { pickingMode = PickingMode.Position, userData = e };
                o.style.position = Position.Absolute;
                o.style.borderLeftWidth = 2; o.style.borderRightWidth = 2; o.style.borderTopWidth = 2; o.style.borderBottomWidth = 2;
                editorLayer.Add(o);
                var name = ApexUi.Text(o, HudLayout.Name(e).ToUpperInvariant(), ApexUi.BodyStrong, 11, ApexUi.C(UiTokens.Ivory), 2f);
                name.style.position = Position.Absolute; name.style.top = -18; name.style.left = 0;
                outlines[(int)e] = o;
            }
            editorPanel = ApexUi.Panel(editorLayer, UiTokens.PanelStrongAlpha);
            editorPanel.style.position = Position.Absolute; editorPanel.style.top = 280; editorPanel.style.width = 560;
            editorPanel.style.paddingLeft = 24; editorPanel.style.paddingRight = 24; editorPanel.style.paddingTop = 18; editorPanel.style.paddingBottom = 18;
            ApexUi.Caption(editorPanel, Loc.T("HUD anpassen"));
            editorTitle = ApexUi.Text(editorPanel, "", ApexUi.DisplayStrong, 30, ApexUi.C(UiTokens.Ivory), 2f);
            editorTitle.style.marginTop = 6;
            editorValues = ApexUi.Text(editorPanel, "", ApexUi.Body, 16, ApexUi.C(UiTokens.Cyan));
            editorValues.style.marginTop = 4;
            var help = ApexUi.Text(editorPanel, Loc.T(
                "Tab / LB RB  Element   ·   Pfeile / Steuerkreuz  Verschieben (Shift schneller)   ·   Maus  Ziehen\n" +
                "+ −  / Y X  Größe   ·   Bild↑ Bild↓  / RT LT  Deckkraft   ·   R  Zurücksetzen   ·   Shift+R  Alles\n" +
                "Enter / Esc / B  Fertig"),
                ApexUi.Body, 13, ApexUi.C(UiTokens.Steel));
            help.style.whiteSpace = WhiteSpace.Normal; help.style.marginTop = 12;
            editorIssues = ApexUi.Text(editorPanel, "", ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Amber));
            editorIssues.style.whiteSpace = WhiteSpace.Normal; editorIssues.style.marginTop = 10;
        }

        void RefreshEditor()
        {
            float pw = PanelWidth;
            var clear = editorLayer.Q("keepClear");
            var kc = HudLayout.KeepClear(pw, HudLayout.RefHeight);
            ApexUi.Place(clear, kc.x, kc.y, kc.w, kc.h);
            editorPanel.style.left = (pw - 560f) * 0.5f;
            foreach (var e in HudLayout.All)
            {
                var l = layout.Get(e);
                var r = HudLayout.Rect(e, new HudElementLayout { element = l.element, dx = l.dx, dy = l.dy, scale = l.scale * hudScale }, pw, HudLayout.RefHeight);
                if (e == HudElement.Tower) r.h = Mathf.Max(r.h, boxes[(int)e].layout.height * l.scale * hudScale);
                var o = outlines[(int)e];
                ApexUi.Place(o, r.x - 4, r.y - 4, r.w + 8, r.h + 8);
                bool sel = e == editor.Current;
                var c = sel ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Steel, 0.5f);
                o.style.borderLeftColor = c; o.style.borderRightColor = c; o.style.borderTopColor = c; o.style.borderBottomColor = c;
                o.style.display = HudLayout.Shows(preset == HudPreset.Off ? HudPreset.Standard : preset, e) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var cur = layout.Get(editor.Current);
            editorTitle.text = HudLayout.Name(editor.Current).ToUpperInvariant();
            editorValues.text = Loc.Format("Größe {0} %   ·   Deckkraft {1} %   ·   Preset {2}", (cur.scale * 100f).ToString("0"), (cur.opacity * 100f).ToString("0"), HudLayout.PresetName(preset));
            var issues = HudLayout.Conflicts(layout, preset == HudPreset.Off ? HudPreset.Standard : preset, pw, HudLayout.RefHeight);
            editorIssues.text = issues.Count == 0 ? "" : Loc.T("Hinweis: ") + string.Join(" · ", issues);
        }

        void TickEditor(float dt)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            float pw = PanelWidth;
            bool changed = false;
            bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) { editor.Select(shift ? -1 : 1); changed = true; }
            if (pad != null && pad.leftShoulder.wasPressedThisFrame) { editor.Select(-1); changed = true; }
            // Arrows: a step on press, repeating while held.
            float mx = 0f, my = 0f;
            bool L = (kb != null && kb.leftArrowKey.isPressed) || (pad != null && pad.dpad.left.isPressed);
            bool R = (kb != null && kb.rightArrowKey.isPressed) || (pad != null && pad.dpad.right.isPressed);
            bool U = (kb != null && kb.upArrowKey.isPressed) || (pad != null && pad.dpad.up.isPressed);
            bool D = (kb != null && kb.downArrowKey.isPressed) || (pad != null && pad.dpad.down.isPressed);
            bool pressedNow = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame));
            if (L || R || U || D)
            {
                repeatTimer = pressedNow ? -0.35f : repeatTimer + dt;
                if (pressedNow || repeatTimer >= 0.04f)
                {
                    if (!pressedNow) repeatTimer = 0f;
                    float step = shift ? 32f : 8f;
                    mx = (R ? step : 0f) - (L ? step : 0f);
                    my = (D ? step : 0f) - (U ? step : 0f);
                }
            }
            if (mx != 0f || my != 0f) { editor.Nudge(mx, my, pw, HudLayout.RefHeight); changed = true; }
            bool bigger = (kb != null && (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            bool smaller = (kb != null && (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            if (bigger) { editor.Resize(0.05f); changed = true; }
            if (smaller) { editor.Resize(-0.05f); changed = true; }
            if ((kb != null && kb.pageUpKey.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame)) { editor.Fade(0.1f); changed = true; }
            if ((kb != null && kb.pageDownKey.wasPressedThisFrame) || (pad != null && pad.leftTrigger.wasPressedThisFrame)) { editor.Fade(-0.1f); changed = true; }
            if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
            {
                if (shift) editor.ResetAll(); else editor.Reset();
                changed = true;
            }
            // Mouse: press on an element selects it, dragging moves it.
            var p = Pointer.current;
            if (p != null && ui.root.panel != null)
            {
                var screen = p.position.ReadValue();
                var pos = RuntimePanelUtils.ScreenToPanel(ui.root.panel, new Vector2(screen.x, Screen.height - screen.y));
                if (p.press.wasPressedThisFrame)
                {
                    var hit = ui.root.panel.Pick(pos);
                    while (hit != null && !(hit.userData is HudElement)) hit = hit.parent;
                    if (hit != null) { editor.SelectElement((HudElement)hit.userData); dragging = true; dragLast = pos; changed = true; }
                }
                if (dragging && p.press.isPressed)
                {
                    var d = pos - dragLast;
                    if (d.sqrMagnitude > 0.25f) { editor.Nudge(d.x, d.y, pw, HudLayout.RefHeight); dragLast = pos; changed = true; }
                }
                if (p.press.wasReleasedThisFrame) dragging = false;
            }
            if (changed) { ApplyLayout(); RefreshEditor(); }
            bool done = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
            if (done) CloseEditor();
        }
    }
}
