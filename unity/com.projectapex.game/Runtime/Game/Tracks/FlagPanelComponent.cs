using System.Collections.Generic;
using Apex.Race;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Drives the trackside LED flag panels from the live race state through
    /// <see cref="FlagPanelLogic"/>. Panels stay dark outside races and when nothing is
    /// signalled; yellow and blue flash, double yellow alternates the two halves, SC/VSC show
    /// text boards. Emission levels are high enough to read in sunlight but not blown out.
    /// </summary>
    public sealed class FlagPanelComponent : MonoBehaviour
    {
        public float trackLength;

        sealed class Panel
        {
            public float distance;
            public Material top, bottom;
            public TextMesh label;
            public PanelSignal shown = (PanelSignal)(-1);
        }

        readonly List<Panel> panels = new List<Panel>();
        readonly FlagPanelLogic logic = new FlagPanelLogic();
        RaceController race;
        float findTimer, evalTimer;

        static readonly Color Yellow = new Color(1f, 0.78f, 0.05f) * 3.2f;
        static readonly Color Green = new Color(0.1f, 1f, 0.3f) * 2.6f;
        static readonly Color Blue = new Color(0.1f, 0.4f, 1f) * 3.2f;

        public void Add(float distance, Material top, Material bottom, TextMesh label) =>
            panels.Add(new Panel { distance = distance, top = top, bottom = bottom, label = label });

        static void Emit(Material m, Color c)
        {
            if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        void Update()
        {
            findTimer -= Time.deltaTime;
            if (race == null && findTimer <= 0f)
            {
                findTimer = 1f;
                race = FindAnyObjectByType<RaceController>();
            }
            double now = race != null ? race.SimTime : Time.timeSinceLevelLoad;
            var control = race != null ? race.Control : null;
            evalTimer -= Time.deltaTime;
            bool evaluate = evalTimer <= 0f;
            if (evaluate)
            {
                evalTimer = 0.1f;
                logic.Update(control, now);
            }
            bool flashOn = Mathf.Repeat(Time.time, 0.5f) < 0.3f;
            bool alternate = Mathf.Repeat(Time.time, 0.6f) < 0.3f;
            foreach (var p in panels)
            {
                if (evaluate) p.shown = logic.Evaluate(p.distance, trackLength, control, race != null ? race.Reports : null, now);
                Color top = Color.black, bottom = Color.black;
                string text = "";
                switch (p.shown)
                {
                    case PanelSignal.Yellow:
                        if (flashOn) top = bottom = Yellow;
                        break;
                    case PanelSignal.DoubleYellow:
                        top = alternate ? Yellow : Color.black;
                        bottom = alternate ? Color.black : Yellow;
                        break;
                    case PanelSignal.Green:
                        top = bottom = Green;
                        break;
                    case PanelSignal.Blue:
                        if (flashOn) top = bottom = Blue;
                        break;
                    case PanelSignal.SafetyCar:
                        top = bottom = Yellow * 0.6f;
                        text = "SC";
                        break;
                    case PanelSignal.VirtualSafetyCar:
                        top = bottom = Yellow * 0.6f;
                        text = "VSC";
                        break;
                }
                Emit(p.top, top);
                Emit(p.bottom, bottom);
                if (p.label != null && p.label.text != text) p.label.text = text;
            }
        }
    }
}
