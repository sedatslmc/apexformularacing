using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Procedural engine and tire audio (no recorded assets): firing-frequency
    /// harmonics shaped by throttle, plus low-passed noise for tire scrub.
    /// Main thread publishes targets; the audio thread synthesises.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineAudio : MonoBehaviour
    {
        public VehicleController car;
        public float volume = 0.6f;
        const int Cylinders = 6;

        volatile float targetRpm = 4000f;
        volatile float targetLoad;
        volatile float targetScrub;
        float rpm = 4000f, load, scrub, phase, noise;
        int sampleRate = 48000;
        uint rng = 2463534242;

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            // A silent looping clip keeps the source running; OnAudioFilterRead adds the sound.
            src.clip = AudioClip.Create("EngineCarrier", sampleRate, 1, sampleRate, false);
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
        }

        void Update()
        {
            if (car == null || car.Sim == null) return;
            var sim = car.Sim;
            targetRpm = sim.EngineRpm;
            targetLoad = sim.throttle;
            float slip = 0f;
            foreach (var w in sim.wheels)
                if (w.contact) slip = Mathf.Max(slip, Mathf.Max(Mathf.Abs(w.slipRatio) / 0.15f, Mathf.Abs(w.slipAngle) / 0.15f));
            targetScrub = Mathf.Clamp01(slip - 0.9f);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float k = 1f - Mathf.Exp(-1f / (sampleRate * 0.04f));
            float tr = targetRpm, tl = targetLoad, ts = targetScrub;
            for (int i = 0; i < data.Length; i += channels)
            {
                rpm += (tr - rpm) * k;
                load += (tl - load) * k;
                scrub += (ts - scrub) * k;
                float freq = rpm / 60f * Cylinders * 0.5f;
                phase += freq / sampleRate;
                if (phase > 1f) phase -= 1f;
                float p = phase * Mathf.PI * 2f;
                float engineLoad = 0.35f + 0.65f * load;
                float s = Mathf.Sin(p) * 0.5f + Mathf.Sin(p * 2f) * 0.3f * engineLoad + Mathf.Sin(p * 3f) * 0.18f * engineLoad + Mathf.Sin(p * 0.5f) * 0.15f;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                noise += ((rng / (float)uint.MaxValue) * 2f - 1f - noise) * 0.3f;
                float v = (s * 0.3f * (0.6f + 0.4f * engineLoad) + noise * scrub * 0.25f) * volume;
                for (int c = 0; c < channels; c++) data[i + c] += v;
            }
        }
    }
}
