using System;
using System.Collections.Generic;
using System.Numerics;

namespace Apex.InputLogic
{
    /// <summary>
    /// Multi-touch ownership: on touch-down each pointer is assigned to the first
    /// control whose rect contains it and keeps that control until release or
    /// cancel, never reassigned mid-gesture. Pointers that stop being reported
    /// (lost touch-up after focus loss or a notification) are released by
    /// <see cref="EndFrame"/>, so inputs cannot get stuck.
    /// </summary>
    public sealed class TouchOwnership
    {
        public struct Control
        {
            public int id;
            /// <summary>Normalised screen rect, origin bottom-left.</summary>
            public float xMin, yMin, xMax, yMax;
            public bool enabled;

            public bool Contains(Vector2 p) => enabled && p.X >= xMin && p.X <= xMax && p.Y >= yMin && p.Y <= yMax;
        }

        readonly List<Control> controls = new List<Control>();
        readonly Dictionary<int, int> owner = new Dictionary<int, int>();
        readonly HashSet<int> seenThisFrame = new HashSet<int>();
        readonly List<int> scratch = new List<int>();

        public int ActivePointers => owner.Count;

        public void SetControls(IEnumerable<Control> list)
        {
            controls.Clear();
            controls.AddRange(list);
            ReleaseAll();
        }

        public IReadOnlyList<Control> Controls => controls;

        public void BeginFrame() => seenThisFrame.Clear();

        /// <summary>Reports an active pointer. Returns the owning control id or -1.</summary>
        public int Report(int pointerId, Vector2 normalisedPosition, bool began)
        {
            seenThisFrame.Add(pointerId);
            if (owner.TryGetValue(pointerId, out int c)) return c;
            if (!began) return -1; // a finger that started outside every control stays unowned
            foreach (var ctl in controls)
            {
                if (ctl.Contains(normalisedPosition))
                {
                    owner[pointerId] = ctl.id;
                    return ctl.id;
                }
            }
            owner[pointerId] = -1;
            return -1;
        }

        public void Release(int pointerId) => owner.Remove(pointerId);

        /// <summary>Releases pointers that were not reported since BeginFrame.</summary>
        public void EndFrame()
        {
            scratch.Clear();
            foreach (var kv in owner)
                if (!seenThisFrame.Contains(kv.Key)) scratch.Add(kv.Key);
            foreach (int id in scratch) owner.Remove(id);
        }

        public void ReleaseAll() => owner.Clear();

        public bool IsHeld(int controlId)
        {
            foreach (var kv in owner)
                if (kv.Value == controlId) return true;
            return false;
        }
    }
}
