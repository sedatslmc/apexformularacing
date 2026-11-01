using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Follows the car's interpolated transform in LateUpdate. Modes: cockpit/halo
    /// (primary), T-cam, chase and far chase. The cockpit view keeps the horizon
    /// partly stabilised to reduce motion discomfort.
    /// </summary>
    public sealed class RaceCamera : MonoBehaviour
    {
        public enum Mode { Cockpit, TCam, Chase, FarChase }

        public static readonly string[] ModeNames = { "Cockpit", "T-Cam", "Verfolger", "Verfolger weit" };

        public Transform target;
        public Mode mode = Mode.Cockpit;
        [Range(0f, 1f)] public float horizonStabilisation = 0.5f;

        Camera cam;
        Vector3 smoothedForward = Vector3.forward;
        bool initialised;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void SetMode(int m)
        {
            mode = (Mode)Mathf.Clamp(m, 0, ModeNames.Length - 1);
            initialised = false;
        }

        public int Cycle()
        {
            SetMode(((int)mode + 1) % ModeNames.Length);
            return (int)mode;
        }

        public void Snap() => initialised = false;

        // Start sequence sweep (IntroCamera) and fixed shots (podium).
        float introTime = -1f;
        bool hold;
        Vector3 holdPos, holdLook;

        /// <summary>Plays the start sweep around the car that blends into the driving camera.</summary>
        public void PlayIntro() => introTime = 0f;
        public bool IntroPlaying => introTime >= 0f && introTime < Apex.UI.IntroCamera.Seconds;

        /// <summary>Holds a fixed shot (e.g. the podium) until <see cref="ClearHold"/>.</summary>
        public void Hold(Vector3 position, Vector3 lookAt) { hold = true; holdPos = position; holdLook = lookAt; }
        public void ClearHold() => hold = false;

        Transform visualsOwner;
        CarVisuals visuals;

        void LateUpdate()
        {
            if (hold)
            {
                if (cam) cam.fieldOfView = 42f;
                transform.SetPositionAndRotation(holdPos, Quaternion.LookRotation(holdLook - holdPos, Vector3.up));
                return;
            }
            if (target == null) return;
            float dt = Time.deltaTime;
            Vector3 fwd = target.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : smoothedForward;
            if (!initialised)
            {
                smoothedForward = flat;
                initialised = true;
            }
            if (target != visualsOwner)
            {
                if (visuals != null) visuals.FirstPerson = false;
                visualsOwner = target;
                visuals = target.GetComponent<CarVisuals>();
            }
            if (visuals != null) visuals.FirstPerson = mode == Mode.Cockpit;
            switch (mode)
            {
                case Mode.Cockpit:
                {
                    if (cam) cam.fieldOfView = 70f;
                    // Driver's eye inside the helmet: steering wheel, halo and mirrors in view.
                    Vector3 eye = target.TransformPoint(new Vector3(0f, 0.47f, 0.02f));
                    Vector3 up = Vector3.Slerp(target.up, Vector3.up, horizonStabilisation);
                    transform.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd, up));
                    smoothedForward = flat;
                    break;
                }
                case Mode.TCam:
                {
                    if (cam) cam.fieldOfView = 68f;
                    Vector3 eye = target.TransformPoint(new Vector3(0f, 0.95f, -0.15f));
                    Vector3 up = Vector3.Slerp(target.up, Vector3.up, 0.3f);
                    transform.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd, up));
                    smoothedForward = flat;
                    break;
                }
                default:
                {
                    bool far = mode == Mode.FarChase;
                    if (cam) cam.fieldOfView = far ? 62f : 66f;
                    float distance = far ? 10f : 6.5f;
                    float height = far ? 3.2f : 2f;
                    // Heading lags a little so slides and rotation remain visible.
                    smoothedForward = Vector3.Slerp(smoothedForward, flat, 1f - Mathf.Exp(-5f * dt)).normalized;
                    Vector3 pos = target.position - smoothedForward * distance + Vector3.up * height;
                    Vector3 look = target.position + smoothedForward * 6f + Vector3.up * 0.6f;
                    transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                    break;
                }
            }
            if (introTime >= 0f)
            {
                introTime += dt;
                float w = Apex.UI.IntroCamera.Blend(introTime);
                if (w >= 1f) { introTime = -1f; return; }
                Apex.UI.IntroCamera.Pose(introTime, out float angle, out float dist, out float height);
                // Angle 0 = in front of the car looking back at it.
                var dir = Quaternion.AngleAxis(angle, Vector3.up) * flat;
                Vector3 ip = target.position + dir * dist + Vector3.up * height;
                var ir = Quaternion.LookRotation(target.position + Vector3.up * 0.4f - ip, Vector3.up);
                transform.SetPositionAndRotation(Vector3.Lerp(ip, transform.position, w), Quaternion.Slerp(ir, transform.rotation, w));
                if (visuals != null && w < 0.9f) visuals.FirstPerson = false;
            }
        }
    }
}