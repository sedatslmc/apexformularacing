using System;
using System.Collections.Generic;
using System.Numerics;

namespace Apex.Vehicle
{
    /// <summary>
    /// The car's physical body collider, shared by the game's CarBuilder, the offline
    /// <see cref="SurfaceRig"/> and the road limits. Outline as before (1.0 m wide, 0.5 m high,
    /// 5.2 m long, top 0.24 m above the centre of mass), split into two boxes whose bottom follows the
    /// floor the setup defines:
    /// <list type="bullet">
    /// <item>Floor: rear end to front axle, bottom on the plank line, <see cref="VehicleSetup.rideHeightRear"/>
    /// at the rear axle and <see cref="VehicleSetup.rideHeightFront"/> at the front axle at rest (the
    /// rake the aero model uses), so it touches exactly when the modelled plank does.</item>
    /// <item>Nose: front axle to front end, bottom level at the front-axle plank height (nose and
    /// front wing are not lower than the plank).</item>
    /// </list>
    /// Body frame: origin at the centre of mass, x right, y up, z forward. Pitch about +x, positive =
    /// nose down.
    /// </summary>
    public sealed class BodyCollider
    {
        public const float Width = 1.0f, Length = 5.2f, TopAboveCom = 0.24f;

        public readonly struct Box
        {
            public readonly Vector3 centre, size;
            public readonly float pitch;
            public Box(Vector3 centre, Vector3 size, float pitch) { this.centre = centre; this.size = size; this.pitch = pitch; }
            public Quaternion Rotation => Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch);
            public float PitchDegrees => pitch * 180f / MathF.PI;
            public Vector3 ToBody(Vector3 local) => centre + Vector3.Transform(local, Rotation);
        }

        public readonly List<Box> boxes = new List<Box>();
        readonly VehicleSetup s;
        readonly float slope;

        public BodyCollider(VehicleSetup setup)
        {
            s = setup;
            float zF = s.FrontAxleZ, zR = s.RearAxleZ, front = Length * 0.5f, rear = -Length * 0.5f;
            slope = (s.rideHeightRear - s.rideHeightFront) / (zF - zR);
            // Floor box: bottom through (zF, rideHeightFront) and (zR, rideHeightRear), top at TopAboveCom.
            float pitch = MathF.Atan(slope);
            float len = (zF - rear) / MathF.Cos(pitch);
            float zMid = 0.5f * (zF + rear);
            float bottomMid = BottomOfFloor(zMid) - s.cogHeight;     // body-frame height of the bottom at zMid
            float height = TopAboveCom - bottomMid;
            // Centre: half the height above the bottom, perpendicular to the pitched bottom face.
            var c = new Vector3(0f, bottomMid, zMid) + Vector3.Transform(new Vector3(0f, height * 0.5f, 0f), Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch));
            boxes.Add(new Box(c, new Vector3(Width, height, len), pitch));
            // Nose box: level bottom at the front-axle plank height.
            float noseBottom = s.rideHeightFront - s.cogHeight;
            float noseH = TopAboveCom - noseBottom;
            boxes.Add(new Box(new Vector3(0f, noseBottom + noseH * 0.5f, 0.5f * (zF + front)), new Vector3(Width, noseH, front - zF), 0f));
        }

        /// <summary>Plank-line height above the road at rest at body position z (floor box).</summary>
        float BottomOfFloor(float z) => s.rideHeightFront + (s.FrontAxleZ - z) * slope;

        /// <summary>Height of the collider bottom above the road at rest at body position z (m).</summary>
        public float StaticClearance(float z) => z >= s.FrontAxleZ ? s.rideHeightFront : BottomOfFloor(z);

        public float FrontZ => Length * 0.5f;
        public float RearZ => -Length * 0.5f;

        /// <summary>Sample points on the bottom faces (body frame), `perMetre` along and three across.</summary>
        public List<Vector3> BottomPoints(float perMetre = 2f)
        {
            var pts = new List<Vector3>();
            foreach (var b in boxes)
            {
                int nz = Math.Max(1, (int)MathF.Ceiling(b.size.Z * perMetre));
                for (int ix = -1; ix <= 1; ix++)
                    for (int iz = 0; iz <= nz; iz++)
                        pts.Add(b.ToBody(new Vector3(ix * b.size.X * 0.5f, -b.size.Y * 0.5f, -b.size.Z * 0.5f + b.size.Z * iz / nz)));
            }
            return pts;
        }
    }
}
