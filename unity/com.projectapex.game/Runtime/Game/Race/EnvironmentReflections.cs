using Apex.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// One realtime reflection probe for the race world, so wet asphalt, car paint, glass and water
    /// reflect the current sky (and on Medium and above the city and the track) instead of the
    /// default sky cube. Tier settings (<see cref="GraphicsPreset"/>):
    /// <list type="bullet">
    /// <item>Mobile: 64 px, sky only, captured when the lighting changes.</item>
    /// <item>Medium: 128 px with the world, captured on lighting changes.</item>
    /// <item>High/Ultra: 256/512 px, follows the camera and re-captures every 4 s / 1.5 s, one cube face per frame.</item>
    /// </list>
    /// The probe sits 12 m above the camera: the reflections that matter (road, bonnet, windows)
    /// look up at sky and façades, and from there the player's car does not fill the lower half.
    /// Box projection is off; the probe stands for distant surroundings. Cost not measured yet (Phase 7).
    /// </summary>
    public sealed class EnvironmentReflections : MonoBehaviour
    {
        public Transform cameraTransform;
        const float Height = 12f;
        ReflectionProbe probe;
        GraphicsPreset preset;
        int renderId = -1;
        float nextCapture;
        bool dirty = true;

        public void Init(GraphicsPreset p)
        {
            preset = p;
            if (probe == null)
            {
                probe = gameObject.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                probe.boxProjection = false;
                probe.hdr = true;
                probe.importance = 0;
                probe.size = new Vector3(20000f, 6000f, 20000f);
                probe.nearClipPlane = 0.5f;
                probe.farClipPlane = 4000f;
            }
            probe.resolution = p.reflectionResolution;
            probe.cullingMask = p.reflectionCapturesWorld ? ~0 : 0;
            probe.shadowDistance = p.reflectionCapturesWorld ? 80f : 0f;
            probe.timeSlicingMode = p.reflectionInterval > 0f ? ReflectionProbeTimeSlicingMode.IndividualFaces : ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            dirty = true;
        }

        /// <summary>Lighting, weather or wetness changed: capture again as soon as the current capture is done.</summary>
        public void Invalidate() => dirty = true;

        void LateUpdate()
        {
            if (probe == null) return;
            bool busy = renderId >= 0 && !probe.IsFinishedRendering(renderId);
            if (busy) return;
            bool periodic = preset.reflectionInterval > 0f && Time.unscaledTime >= nextCapture;
            if (!dirty && !periodic) return;
            if (cameraTransform != null && preset.reflectionCapturesWorld)
                transform.position = cameraTransform.position + Vector3.up * Height;
            renderId = probe.RenderProbe();
            dirty = false;
            nextCapture = Time.unscaledTime + Mathf.Max(0.25f, preset.reflectionInterval);
        }
    }
}
