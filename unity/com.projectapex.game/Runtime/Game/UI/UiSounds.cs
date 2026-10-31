using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Short electronic UI sounds, synthesised at start-up (no audio assets, no licences): a soft tick for focus
    /// changes, a two-tone confirm, a falling "back", a low airy whoosh for screen transitions and a deeper,
    /// longer confirm for "start race". Volume follows the master volume.
    /// </summary>
    public sealed class UiSounds
    {
        public enum Cue { Focus, Confirm, Back, Transition, Start }

        readonly AudioSource source;
        readonly AudioClip[] clips = new AudioClip[5];
        public float volume = 0.8f;
        const int Rate = 44100;

        public UiSounds(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;   // menus are used while the game is paused
            clips[(int)Cue.Focus] = Make("ui_focus", 0.045f, (t, d) => Tone(t, 2300f, 2600f, d) * Env(t, d, 0.003f) * 0.22f);
            clips[(int)Cue.Confirm] = Make("ui_confirm", 0.16f, (t, d) => (t < 0.06f ? Tone(t, 880f, 880f, d) : Tone(t, 1320f, 1320f, d)) * Env(t, d, 0.004f) * 0.28f);
            clips[(int)Cue.Back] = Make("ui_back", 0.12f, (t, d) => Tone(t, 990f, 520f, d) * Env(t, d, 0.004f) * 0.24f);
            clips[(int)Cue.Transition] = Make("ui_whoosh", 0.32f, (t, d) => Noise(t) * Mathf.Sin(Mathf.PI * t / d) * 0.10f + Tone(t, 140f, 90f, d) * Env(t, d, 0.02f) * 0.12f);
            clips[(int)Cue.Start] = Make("ui_start", 0.42f, (t, d) => (Tone(t, 220f, 440f, d) * 0.6f + Tone(t, 660f, 1320f, d) * 0.4f) * Env(t, d, 0.006f) * 0.3f);
        }

        public void Play(Cue c)
        {
            if (clips[(int)c] != null && volume > 0.001f) source.PlayOneShot(clips[(int)c], volume);
        }

        static float Tone(float t, float f0, float f1, float d)
        {
            // Linear chirp: phase integral of f(t) = f0 + (f1 - f0) t / d.
            float ph = 2f * Mathf.PI * (f0 * t + 0.5f * (f1 - f0) * t * t / Mathf.Max(d, 1e-4f));
            return Mathf.Sin(ph) * 0.8f + Mathf.Sin(2f * ph) * 0.12f;
        }

        static float Env(float t, float d, float attack) => Mathf.Clamp01(t / attack) * Mathf.Exp(-5f * t / d);

        static uint seed = 22695477u;
        static float Noise(float t)
        {
            seed = seed * 1664525u + 1013904223u;
            return ((seed >> 9) / 4194304f) * 2f - 1f;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = f(i / (float)Rate, seconds);
            // 5 ms fade-out against clicks.
            int fade = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
