using System.Collections.Generic;
using Apex.Race;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>Finds the race controller lazily (once a second) for the trackside displays.</summary>
    public abstract class RaceDisplay : MonoBehaviour
    {
        RaceController race;
        float findTimer;

        protected RaceController Race
        {
            get
            {
                findTimer -= Time.deltaTime;
                if (race == null && findTimer <= 0f)
                {
                    findTimer = 1f;
                    race = FindAnyObjectByType<RaceController>();
                }
                return race;
            }
        }

        protected RaceDirector Director => Race != null ? Race.Director : null;
    }

    /// <summary>
    /// Start gantry lights: five red light pairs come on one per second during the start
    /// sequence and all go out together at the start (<see cref="EventIdentity.LitStartLamps"/>).
    /// </summary>
    public sealed class StartLightsComponent : RaceDisplay
    {
        static readonly Color Lit = new Color(1f, 0.04f, 0.02f) * 5f;
        static readonly Color OffBase = new Color(0.16f, 0.02f, 0.02f);
        readonly List<Material> lamps = new List<Material>();
        int shown = -1;

        public void Add(Material lamp) => lamps.Add(lamp);

        /// <summary>A lamp lens material (its own instance, emission driven at runtime).</summary>
        public static Material LampMaterial()
        {
            var m = new Material(ApexMaterials.LitShader()) { name = "StartLamp" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", OffBase);
            if (m.HasProperty("_Color")) m.SetColor("_Color", OffBase);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            return m;
        }

        void Update()
        {
            var d = Director;
            int lit = d != null ? EventIdentity.LitStartLamps(d.CurrentPhase, d.LightsOn) : 0;
            if (lit == shown) return;
            shown = lit;
            for (int k = 0; k < lamps.Count; k++)
                if (lamps[k] != null && lamps[k].HasProperty("_EmissionColor"))
                    lamps[k].SetColor("_EmissionColor", k < lit ? Lit : Color.black);
        }
    }

    /// <summary>Timing tower: the current race order as position and three-letter code.</summary>
    public sealed class TimingTowerComponent : RaceDisplay
    {
        public const int Rows = 10;
        readonly List<TextMesh> rows = new List<TextMesh>();
        float timer;

        public void Add(TextMesh row) => rows.Add(row);

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            var d = Director;
            var lines = EventIdentity.TowerRows(d != null ? d.Standings : null, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                string text = i < lines.Count ? lines[i] : "";
                if (rows[i] != null && rows[i].text != text) rows[i].text = text;
            }
        }
    }

    /// <summary>LED screens: event messages, start sequence, safety car and leader/lap (<see cref="EventIdentity.ScreenMessage"/>).</summary>
    public sealed class EventScreenComponent : RaceDisplay
    {
        readonly List<TextMesh> screens = new List<TextMesh>();
        float timer;

        public void Add(TextMesh screen) => screens.Add(screen);

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.25f;
            var race = Race;
            var d = race != null ? race.Director : null;
            string text;
            if (d == null) text = EventIdentity.ScreenMessage(RaceDirector.Phase.Waiting, TrackStatus.Green, Time.timeSinceLevelLoad);
            else
            {
                var leader = d.Standings.Count > 0 ? d.Standings[0] : null;
                var status = race.Control != null ? race.Control.Status : TrackStatus.Green;
                int lap = leader != null ? leader.LapsCompleted + 1 : 0;
                text = EventIdentity.ScreenMessage(d.CurrentPhase, status, race.SimTime, leader != null ? leader.code : null, lap, d.IsRace ? d.rules.laps : 0);
            }
            foreach (var s in screens)
                if (s != null && s.text != text) s.text = text;
        }
    }
}
