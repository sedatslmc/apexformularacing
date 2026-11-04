using System;
using System.Collections.Generic;
using Apex.Race;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Own procedural models for the pit crew (placeholder art, docs/PIT_LANE.md): the ten body parts of a crew member
    /// in a fire-proof overall (team colour), helmet with visor, gloves and boots, built to the dimensions of
    /// Apex.Race.PitCrewFigure, plus the wheel (tyre and rim), the wheel gun and the trolley jack. Each part is built in
    /// its joint frame (origin at the joint; limbs hang along −y, torso and head rise along +y, z = forward), so the
    /// skeleton's frames place them directly. Shared by PitCrewComponent and the ModelPreview export.
    /// </summary>
    public static class PitCrewGeometry
    {
        /// <summary>Material slots of a body part: overall (team colour), dark (gloves, boots, visor, belt), helmet shell.</summary>
        public const int Overall = 0, Dark = 1, Shell = 2, BodySlots = 3;
        /// <summary>Material slots of a prop: dark rubber/tool body, metal.</summary>
        public const int PropDark = 0, PropMetal = 1, PropSlots = 2;

        public static readonly string[] BodyMaterialNames = { "CrewOverall", "CrewDark", "CrewHelmet" };
        public static readonly string[] PropMaterialNames = { "PitToolDark", "PitToolMetal" };

        static MeshBuilder[] Slots(int n)
        {
            var a = new MeshBuilder[n];
            for (int i = 0; i < n; i++) a[i] = new MeshBuilder();
            return a;
        }

        /// <summary>
        /// Solid of revolution around the part's vertical axis: profile (distance along the axis from the joint, radius),
        /// ordered away from the joint; <paramref name="down"/>: hangs along −y (limbs), else rises along +y.
        /// Cross-section scaled to <paramref name="width"/> (x) by <paramref name="depth"/> (z).
        /// </summary>
        static void Revolve(MeshBuilder mb, IList<Vector2> profile, bool down, float width = 1f, float depth = 1f, int segments = 10, Vector3 offset = default)
        {
            int before = mb.VertexCount;
            mb.Lathe(profile, segments);
            // Lathe axis = x; both maps are rotations (positive determinant) times a positive scale: winding kept.
            if (down) mb.TransformLast(mb.VertexCount - before, v => new Vector3(v.y * width, -v.x, v.z * depth) + offset);
            else mb.TransformLast(mb.VertexCount - before, v => new Vector3(-v.y * width, v.x, v.z * depth) + offset);
        }

        static List<Vector2> Profile(params float[] xr)
        {
            var l = new List<Vector2>(xr.Length / 2);
            for (int i = 0; i + 1 < xr.Length; i += 2) l.Add(new Vector2(xr[i], xr[i + 1]));
            return l;
        }

        /// <summary>One body part (slots <see cref="Overall"/>, <see cref="Dark"/>, <see cref="Shell"/>).</summary>
        public static MeshBuilder[] Part(PitCrewFigure.Part part, int detail = 1)
        {
            var mb = Slots(BodySlots);
            int seg = detail > 0 ? 12 : 8;
            switch (part)
            {
                case PitCrewFigure.Part.Torso:
                {
                    // Overall from the hips to the collar: hips, waist, chest, broad shoulders; flattened front to back.
                    Revolve(mb[Overall], Profile(-0.07f, 0f, -0.06f, 0.12f, 0.0f, 0.165f, 0.12f, 0.158f, 0.25f, 0.152f, 0.36f, 0.17f,
                        0.45f, 0.19f, 0.5f, 0.17f, 0.54f, 0.1f, 0.57f, 0f), false, 1f, 0.66f, seg);
                    // Belt and a reflective band across the chest.
                    Revolve(mb[Dark], Profile(-0.01f, 0.168f, 0.05f, 0.168f), false, 1f, 0.68f, seg);
                    Revolve(mb[Shell], Profile(0.3f, 0.165f, 0.335f, 0.168f), false, 1f, 0.68f, seg);
                    // Shoulder caps where the arms join.
                    foreach (float sx in new[] { -1f, 1f })
                        Revolve(mb[Overall], Profile(-0.07f, 0f, -0.05f, 0.05f, 0f, 0.07f, 0.05f, 0.05f, 0.07f, 0f), false, 1f, 1f, 8,
                            new Vector3(sx * PitCrewFigure.ShoulderHalfWidth, PitCrewFigure.TorsoLength - PitCrewFigure.ShoulderDrop, 0f));
                    break;
                }
                case PitCrewFigure.Part.Head:
                {
                    // Fire-proof hood at the neck, full helmet with a dark visor and a chin guard.
                    Revolve(mb[Dark], Profile(-0.02f, 0f, -0.01f, 0.055f, 0.08f, 0.055f, 0.1f, 0f), false, 1f, 1f, 8);
                    float c = 0.18f, R = 0.142f;
                    var shell = new List<Vector2>();
                    for (int i = 0; i <= 8; i++)
                    {
                        float a = -0.55f * Mathf.PI + 1.05f * Mathf.PI * i / 8f;   // from under the chin to the crown
                        shell.Add(new Vector2(c + R * Mathf.Sin(a), i == 8 ? 0f : R * Mathf.Cos(a)));
                    }
                    shell[0] = new Vector2(shell[0].x, 0.05f);
                    Revolve(mb[Shell], shell, false, 1f, 1.08f, seg);
                    mb[Dark].Box(new Vector3(0f, c + 0.005f, 0.128f), new Vector3(0.2f, 0.075f, 0.04f));
                    mb[Shell].Box(new Vector3(0f, c - 0.075f, 0.12f), new Vector3(0.14f, 0.05f, 0.05f));
                    break;
                }
                case PitCrewFigure.Part.UpperArmL:
                case PitCrewFigure.Part.UpperArmR:
                    Revolve(mb[Overall], Profile(-0.04f, 0f, -0.02f, 0.05f, 0.05f, 0.058f, 0.2f, 0.052f, PitCrewFigure.UpperArm, 0.046f, PitCrewFigure.UpperArm + 0.03f, 0f), true, 1f, 1f, seg);
                    break;
                case PitCrewFigure.Part.ForearmL:
                case PitCrewFigure.Part.ForearmR:
                {
                    float f = PitCrewFigure.Forearm;
                    Revolve(mb[Overall], Profile(-0.03f, 0f, -0.01f, 0.046f, 0.12f, 0.047f, f - 0.06f, 0.038f, f - 0.04f, 0f), true, 1f, 1f, seg);
                    // Gauntlet glove: cuff and a flattened hand.
                    Revolve(mb[Dark], Profile(f - 0.08f, 0f, f - 0.07f, 0.045f, f - 0.01f, 0.043f, f + 0.01f, 0.036f, f + 0.06f, 0.034f, f + 0.1f, 0f), true, 1f, 0.62f, seg);
                    break;
                }
                case PitCrewFigure.Part.ThighL:
                case PitCrewFigure.Part.ThighR:
                    Revolve(mb[Overall], Profile(-0.05f, 0f, -0.03f, 0.075f, 0.06f, 0.088f, 0.25f, 0.075f, PitCrewFigure.Thigh, 0.058f, PitCrewFigure.Thigh + 0.04f, 0f), true, 1f, 1f, seg);
                    break;
                default:   // shins with the boots
                {
                    float s = PitCrewFigure.Shin;
                    Revolve(mb[Overall], Profile(-0.04f, 0f, -0.02f, 0.058f, 0.12f, 0.06f, s - 0.12f, 0.047f, s - 0.08f, 0f), true, 1f, 1f, seg);
                    // Boot: shaft round the ankle, then the foot with its sole on the ground (ankle 0.08 m up).
                    Revolve(mb[Dark], Profile(s - 0.13f, 0f, s - 0.12f, 0.052f, s, 0.054f, s + 0.02f, 0f), true, 1f, 1.1f, seg);
                    float sole = s + PitCrewFigure.AnkleHeight;
                    mb[Dark].Box(new Vector3(0f, -(sole - 0.045f), 0.055f), new Vector3(0.105f, 0.09f, 0.27f));
                    mb[Dark].Box(new Vector3(0f, -(sole - 0.02f), 0.2f), new Vector3(0.095f, 0.04f, 0.04f));
                    break;
                }
            }
            return mb;
        }

        /// <summary>Wheel with tyre (slot dark) and rim (slot metal), axle along local z, centre at the origin.</summary>
        public static MeshBuilder[] Wheel(int detail = 1)
        {
            var mb = Slots(PropSlots);
            int seg = detail > 0 ? 20 : 12;
            const float R = 0.36f, w = 0.19f, rim = 0.23f;
            // Lathe around x, then x → z (rotation about y by −90°: (x, y, z) → (−z, y, x), determinant +1).
            void Around(MeshBuilder m, List<Vector2> prof)
            {
                int before = m.VertexCount;
                m.Lathe(prof, seg);
                m.TransformLast(m.VertexCount - before, v => new Vector3(-v.z, v.y, v.x));
            }
            // Tyre: inner bead, sidewall, rounded shoulders and the tread (outside to the left of the profile direction).
            Around(mb[PropDark], Profile(-w, rim, -w, R - 0.05f, -w + 0.03f, R - 0.005f, -w + 0.06f, R, w - 0.06f, R, w - 0.03f, R - 0.005f, w, R - 0.05f, w, rim));
            // Rim: the barrel inside the bead, then the face with spokes suggested by a dished disc and the centre lock.
            // (Ordered so the outside lies to the left: the +z face runs from the lip in to the axle, the −z face out.)
            Around(mb[PropMetal], Profile(w + 0.01f, rim, w - 0.01f, rim - 0.005f, w - 0.05f, 0.19f, w - 0.02f, 0.06f, w + 0.005f, 0.04f, w + 0.005f, 0f));
            Around(mb[PropMetal], Profile(-w - 0.005f, 0f, -w - 0.005f, rim));
            mb[PropDark].Box(new Vector3(0f, 0f, w + 0.03f), new Vector3(0.07f, 0.07f, 0.05f));
            return mb;
        }

        /// <summary>Wheel gun: grip at the origin, socket along +z (slots dark body, metal socket and hose fitting).</summary>
        public static MeshBuilder[] Gun()
        {
            var mb = Slots(PropSlots);
            mb[PropDark].Box(new Vector3(0f, 0.045f, 0.03f), new Vector3(0.1f, 0.12f, 0.24f));      // motor body
            mb[PropDark].Box(new Vector3(0f, -0.04f, -0.02f), new Vector3(0.045f, 0.12f, 0.055f));  // pistol grip
            mb[PropDark].Box(new Vector3(0.11f, 0.06f, 0.06f), new Vector3(0.12f, 0.035f, 0.035f)); // side handle
            mb[PropMetal].Tube(new Vector3(0f, 0.045f, 0.15f), new Vector3(0f, 0.045f, 0.32f), 0.034f, 10);  // socket
            mb[PropMetal].Tube(new Vector3(0f, 0.02f, -0.09f), new Vector3(0f, -0.03f, -0.16f), 0.018f, 8);  // air hose fitting
            return mb;
        }

        /// <summary>Jack head: the lifting saddle at the origin, its frame and wheels behind it towards the man (+z).</summary>
        public static MeshBuilder[] JackHead()
        {
            var mb = Slots(PropSlots);
            // Lifting saddle at the origin, the frame behind it down to two wheels on the ground.
            mb[PropDark].Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.05f, 0.16f));
            mb[PropMetal].Box(new Vector3(0f, -0.04f, 0.12f), new Vector3(0.3f, 0.07f, 0.32f));
            foreach (float sx in new[] { -0.17f, 0.17f })
                mb[PropDark].Tube(new Vector3(sx - 0.03f, -0.08f, 0.22f), new Vector3(sx + 0.03f, -0.08f, 0.22f), 0.045f, 10);
            return mb;
        }

        /// <summary>Jack handle: 1 m along +z from the head (scaled to the length), grip bar across at the end.</summary>
        public static MeshBuilder[] JackHandle()
        {
            var mb = Slots(PropSlots);
            mb[PropMetal].Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), 0.026f, 8);
            mb[PropDark].Tube(new Vector3(-0.2f, 0f, 0.985f), new Vector3(0.2f, 0f, 0.985f), 0.03f, 8);
            return mb;
        }
    }
}
