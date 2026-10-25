using System.Collections.Generic;
using Apex.Core;
using Apex.InputLogic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Screen layout of on-screen driving controls, inside the safe area. Rects are
    /// in pixels with a bottom-left origin (Input System touch convention).
    /// Buttons are at least ~12 mm (≈48 dp) and stay in the lower corners so the
    /// centre of the screen remains clear.
    /// </summary>
    public static class TouchLayout
    {
        public const int SteerLeft = 1;
        public const int SteerRight = 2;
        public const int Brake = 3;
        public const int Throttle = 4;
        public const int ShiftDown = 5;
        public const int ShiftUp = 6;
        public const int Recenter = 7;
        public const int Pause = 8;

        public struct Button
        {
            public int id;
            public Rect rect;
            public string label;
        }

        public static List<Button> Compute(SettingsData s, bool tilt, bool manualGears)
        {
            var list = new List<Button>();
            if (s.controlPreset == ControlPreset.Keyboard) return list;
            Rect safe = Screen.safeArea;
            float mm = Screen.dpi > 0 ? Screen.dpi / 25.4f : Screen.height / 70f;
            float size = Mathf.Max(Screen.height * 0.2f, 14f * mm) * s.touchButtonScale;
            float pad = Mathf.Max(3f * mm, size * 0.08f);
            float left = safe.xMin + pad;
            float right = safe.xMax - pad;
            float bottom = safe.yMin + pad;
            bool mirror = s.leftHanded;
            // Small pause button top-right, outside the thumb driving zones.
            float ps = Mathf.Max(11f * mm, Screen.height * 0.09f);
            list.Add(new Button { id = Pause, rect = new Rect(safe.xMax - pad - ps, safe.yMax - pad - ps, ps, ps), label = "II" });

            // Pedals on the right (left when mirrored), steering on the other side.
            float throttleX = mirror ? left : right - size;
            float brakeX = mirror ? left + size + pad : right - 2f * size - pad;
            list.Add(new Button { id = Throttle, rect = new Rect(throttleX, bottom, size, size * 1.35f), label = "GAS" });
            list.Add(new Button { id = Brake, rect = new Rect(brakeX, bottom, size, size), label = "BREMSE" });
            if (manualGears)
            {
                float gy = bottom + size * 1.35f + pad;
                list.Add(new Button { id = ShiftUp, rect = new Rect(throttleX, gy, size, size * 0.55f), label = "+" });
                list.Add(new Button { id = ShiftDown, rect = new Rect(brakeX, bottom + size + pad, size, size * 0.55f), label = "−" });
            }
            if (tilt)
            {
                float rx = mirror ? right - size * 0.8f : left;
                list.Add(new Button { id = Recenter, rect = new Rect(rx, bottom, size * 0.8f, size * 0.5f), label = "MITTE" });
            }
            else
            {
                float steerLeftX = mirror ? right - 2f * size - pad : left;
                float steerRightX = steerLeftX + size + pad;
                list.Add(new Button { id = SteerLeft, rect = new Rect(steerLeftX, bottom, size, size), label = "◀" });
                list.Add(new Button { id = SteerRight, rect = new Rect(steerRightX, bottom, size, size), label = "▶" });
            }
            return list;
        }

        /// <summary>Converts to normalised controls for <see cref="TouchOwnership"/>.</summary>
        public static List<TouchOwnership.Control> ToControls(List<Button> buttons)
        {
            var res = new List<TouchOwnership.Control>();
            float w = Mathf.Max(Screen.width, 1), h = Mathf.Max(Screen.height, 1);
            foreach (var b in buttons)
            {
                // Slightly enlarged hit area forgives imprecise thumbs.
                var r = new Rect(b.rect.x - 8f, b.rect.y - 8f, b.rect.width + 16f, b.rect.height + 16f);
                res.Add(new TouchOwnership.Control { id = b.id, xMin = r.xMin / w, yMin = r.yMin / h, xMax = r.xMax / w, yMax = r.yMax / h, enabled = true });
            }
            return res;
        }
    }
}
