using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Menu music placeholder: a calm 16-second electronic loop synthesised at start-up (pad chords Am–F–C–G, soft
    /// sub pulse, quiet hats; no audio assets, no licences). Fades in in the menus and out before a session. A
    /// licensed or commissioned soundtrack replaces it (docs/UI_UX_OVERHAUL.md, missing assets).
    /// </summary>
    public sealed class MenuMusic
    {
        const int Rate = 32000;
        const float Seconds = 16f, Bpm = 96f;
        readonly AudioSource source;
        float level, target;
        public float volume = 0.5f;

        public MenuMusic(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.volume = 0f;
            source.clip = Build();
        }

        public void SetPlaying(bool on) => target = on ? 1f : 0f;

        public void Tick(float dt)
        {
            level = Mathf.MoveTowards(level, target, dt / (target > level ? 2.5f : 0.8f));
            float v = level * volume * 0.35f;
            source.volume = v;
            if (v > 0.0001f && !source.isPlaying) source.Play();
            else if (v <= 0.0001f && source.isPlaying) source.Pause();
        }

        static AudioClip Build()
        {
            int n = (int)(Seconds * Rate);
            var data = new float[n];
            // Chord roots per 4 s: A minor, F major, C major, G major (MIDI notes).
            int[][] chords = { new[] { 57, 60, 64, 69 }, new[] { 53, 57, 60, 65 }, new[] { 48, 55, 60, 64 }, new[] { 55, 59, 62, 67 } };
            int[] bass = { 45, 41, 48, 43 };
            float beat = 60f / Bpm;
            uint seed = 12345u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int ci = Mathf.Min(3, (int)(t / 4f));
                float local = t - ci * 4f;
                // Pads: slow swell per chord with a crossfade into the next one (loop-safe).
                float env = Mathf.Clamp01(local / 0.8f) * Mathf.Clamp01((4f - local) / 0.6f);
                float pad = 0f;
                foreach (int note in chords[ci])
                {
                    float f = 440f * Mathf.Pow(2f, (note - 69) / 12f);
                    pad += Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 1.003f * t) * 0.5f;
                }
                pad *= env * 0.06f;
                // Sub pulse on every beat.
                float bt = t % beat;
                float fb = 440f * Mathf.Pow(2f, (bass[ci] - 69) / 12f);
                float sub = Mathf.Sin(2f * Mathf.PI * fb * t) * Mathf.Exp(-bt * 6f) * 0.22f;
                // Quiet hats on the off-beats.
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 9) / 4194304f) * 2f - 1f;
                float ht = (t + beat * 0.5f) % beat;
                float hat = noise * Mathf.Exp(-ht * 60f) * 0.035f;
                data[i] = pad + sub + hat;
            }
            var clip = AudioClip.Create("menu_music_placeholder", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
