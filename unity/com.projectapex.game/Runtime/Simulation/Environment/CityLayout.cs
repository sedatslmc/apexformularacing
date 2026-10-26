using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Procedural placement of Istanbul-style city blocks on the terrain: mid-rise
    /// apartment blocks with plaster colours, tiled hip roofs or flat roofs with water
    /// tanks, facades turned downhill towards the water as on the Bosphorus slopes.
    /// Keeps clear of the track corridor (run-off and barriers), the water and quays,
    /// landmarks and reserved areas (pit, grandstands). Deterministic for a seed.
    /// Buildings are generic placeholders, not models of real buildings.
    /// </summary>
    public sealed class CityLayout
    {
        public struct Lot
        {
            public float x, z;
            /// <summary>Base height (lowest footprint corner, the block sinks into slopes).</summary>
            public float ground;
            /// <summary>Footprint along the facade (w) and depth (d), metres.</summary>
            public float w, d;
            public float height;
            public int floors;
            /// <summary>Facade direction (radians, 0 = +z, clockwise towards +x).</summary>
            public float yaw;
            public int style;
            /// <summary>0 = flat roof with water tanks, 1 = tiled hip roof, 2 = tower (flat, glass).</summary>
            public int roof;
            /// <summary>0 = near (detailed), 1 = mid, 2 = far skyline mass.</summary>
            public int band;
        }

        public struct Tree
        {
            public float x, z, ground, height;
            /// <summary>0 = plane tree, 1 = umbrella pine, 2 = cypress.</summary>
            public int kind;
        }

        public struct Reserved
        {
            public float x, z, radius;
            public Reserved(float x, float z, float radius) { this.x = x; this.z = z; this.radius = radius; }
        }

        public sealed class Options
        {
            public int seed = 1453;
            /// <summary>0 = low, 1 = medium, 2 = high (spacing and far extent).</summary>
            public int quality = 1;
            /// <summary>Clearance from the track centreline beyond half width (kerb, run-off, wall, pavement).</summary>
            public float trackClearance = 26f;
            /// <summary>Free quay/promenade strip along the European coast in the near area.</summary>
            public float quay = 24f;
            public float farRadius = 7000f;
            public readonly List<Reserved> reserved = new List<Reserved>();
        }

        public readonly List<Lot> lots = new List<Lot>();
        public readonly List<Tree> trees = new List<Tree>();

        public const int Styles = 6;
        public const float FloorHeight = 3.1f;

        readonly TerrainField field;
        readonly CircuitDefinition def;
        readonly Options o;
        readonly Random rng;

        public CityLayout(TerrainField field, CircuitDefinition def, Options options)
        {
            this.field = field;
            this.def = def;
            o = options ?? new Options();
            rng = new Random(o.seed);
            AddLandmarkReservations();
            Generate();
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void AddLandmarkReservations()
        {
            foreach (var l in def.landmarks)
            {
                float r;
                switch (l.kind)
                {
                    case "palace": r = 200f; break;
                    case "palace_small": r = 120f; break;
                    case "mosque": r = 55f; break;
                    case "mosque_big": r = 130f; break;
                    case "tower": r = 30f; break;
                    case "pier": r = 45f; break;
                    case "bridge_end": r = 70f; break;
                    case "park": r = 230f; break;
                    default: continue;
                }
                o.reserved.Add(new Reserved(l.east, l.north, r));
            }
        }

        bool IsReserved(float x, float z, float pad)
        {
            foreach (var r in o.reserved)
            {
                float dx = x - r.x, dz = z - r.z, rr = r.radius + pad;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        CircuitDefinition.Landmark Find(string kind)
        {
            foreach (var l in def.landmarks) if (l.kind == kind) return l;
            return null;
        }

        void Generate()
        {
            var land = field.land;
            float cx = (field.MinX + field.MaxX) * 0.5f, cz = (field.MinZ + field.MaxZ) * 0.5f;
            float nearCell = o.quality >= 2 ? 24f : o.quality == 1 ? 28f : 36f;
            float farCell = o.quality >= 2 ? 56f : o.quality == 1 ? 70f : 96f;
            float farRadius = o.farRadius * (o.quality >= 2 ? 1f : o.quality == 1 ? 0.9f : 0.8f);
            var park = Find("park");
            var skyline = Find("skyline");

            // Near band: the whole near terrain area at full density.
            for (float z = field.MinZ + nearCell * 0.5f; z < field.MaxZ; z += nearCell)
                for (float x = field.MinX + nearCell * 0.5f; x < field.MaxX; x += nearCell)
                    TryLot(x + R(-0.3f, 0.3f) * nearCell, z + R(-0.3f, 0.3f) * nearCell, nearCell, 0, park);

            if (!land.HasLand) return;
            // Mid and far bands: coarser blocks on the land masses out to the far radius.
            float ext = farRadius;
            for (float z = cz - ext; z < cz + ext; z += farCell)
                for (float x = cx - ext; x < cx + ext; x += farCell)
                {
                    if (field.InNearArea(x, z)) continue;
                    float dist = MathF.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                    if (dist > farRadius) continue;
                    int band = dist < farRadius * 0.55f ? 1 : 2;
                    TryLot(x + R(-0.25f, 0.25f) * farCell, z + R(-0.25f, 0.25f) * farCell, farCell, band, null);
                }
            if (skyline != null) AddTowers(skyline);
        }

        void TryLot(float x, float z, float cell, int band, CircuitDefinition.Landmark park)
        {
            var land = field.land;
            if (field.IsWater(x, z)) return;
            string mass = land.HasLand ? land.MassAt(x, z) : "europe";
            float coast = land.HasLand ? land.CoastDistance(x, z) : float.MaxValue;
            bool asia = mass == "asia";
            float quay = band == 0 && !asia ? o.quay : 12f;
            float w = R(0.55f, 0.8f) * cell, d = R(0.5f, 0.75f) * cell;
            float half = MathF.Max(w, d) * 0.5f;
            if (coast < quay + half * 0.7f)
            {
                // Promenade trees along the quay instead of buildings.
                if (band == 0 && coast > 4f && coast < quay && rng.NextDouble() < 0.55)
                    AddTree(x, z, 0);
                return;
            }
            if (band == 0)
            {
                float td = field.TrackDistance(x, z, 90f, out _);
                if (td < 7.5f + o.trackClearance + half) return;
            }
            if (IsReserved(x, z, half))
            {
                if (park != null && InCircle(x, z, park, 230f) && rng.NextDouble() < 0.8)
                    AddTree(x + R(-6f, 6f), z + R(-6f, 6f), rng.NextDouble() < 0.4 ? 1 : 0);
                return;
            }
            if (band == 0 && rng.NextDouble() < 0.07)
            {
                // Pocket park / courtyard trees break up the carpet of roofs.
                AddTree(x, z, rng.NextDouble() < 0.5 ? 1 : (rng.NextDouble() < 0.5 ? 2 : 0));
                return;
            }
            // Facades face downhill (towards the water on the Bosphorus slopes).
            const float e = 6f;
            float gx = field.Height(x + e, z) - field.Height(x - e, z);
            float gz = field.Height(x, z + e) - field.Height(x, z - e);
            float yaw = MathF.Abs(gx) + MathF.Abs(gz) > 0.05f ? MathF.Atan2(-gx, -gz) : R(0f, MathF.PI * 2f);
            yaw = MathF.Round(yaw / (MathF.PI / 12f)) * (MathF.PI / 12f) + R(-0.05f, 0.05f);
            float ground = float.MaxValue;
            float s = MathF.Sin(yaw), c = MathF.Cos(yaw);
            foreach (var (u, v) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                float px = x + c * u * w * 0.5f + s * v * d * 0.5f;
                float pz = z - s * u * w * 0.5f + c * v * d * 0.5f;
                if (field.IsWater(px, pz)) return;
                ground = MathF.Min(ground, field.Height(px, pz));
            }
            int floors;
            if (asia) floors = coast < 80f ? rng.Next(2, 4) : rng.Next(3, 7);
            else if (mass == "peninsula") floors = rng.Next(3, 6);
            else if (band == 0) floors = coast < 120f ? rng.Next(3, 6) : rng.Next(4, 8);
            else floors = rng.Next(4, 9);
            float height = floors * FloorHeight + R(0.3f, 1.2f);
            int roof = band == 2 ? 0 : rng.NextDouble() < (mass == "europe" ? 0.45 : 0.65) ? 1 : 0;
            lots.Add(new Lot
            {
                x = x, z = z, ground = ground - 0.6f, w = w, d = d, height = height, floors = floors,
                yaw = yaw, style = rng.Next(Styles), roof = roof, band = band,
            });
        }

        static bool InCircle(float x, float z, CircuitDefinition.Landmark l, float r) =>
            (x - l.east) * (x - l.east) + (z - l.north) * (z - l.north) < r * r;

        void AddTree(float x, float z, int kind)
        {
            if (field.IsWater(x, z)) return;
            if (field.TrackDistance(x, z, 40f, out _) < 7.5f + 18f) return;
            float h = kind == 2 ? R(9f, 14f) : kind == 1 ? R(8f, 12f) : R(9f, 15f);
            trees.Add(new Tree { x = x, z = z, ground = field.Height(x, z), height = h, kind = kind });
        }

        /// <summary>High-rise business cluster (the Levent skyline north of Beşiktaş).</summary>
        void AddTowers(CircuitDefinition.Landmark sky)
        {
            int n = o.quality >= 1 ? 14 : 8;
            for (int i = 0; i < n; i++)
            {
                float a = R(0f, MathF.PI * 2f), r = R(0f, 420f);
                float x = sky.east + MathF.Cos(a) * r, z = sky.north + MathF.Sin(a) * r;
                if (field.IsWater(x, z)) continue;
                float h = i < 3 ? R(150f, sky.height + 20f) : R(70f, 140f);
                float w = R(28f, 42f);
                lots.Add(new Lot
                {
                    x = x, z = z, ground = field.Height(x, z) - 1f, w = w, d = w * R(0.7f, 1f), height = h,
                    floors = (int)(h / 3.8f), yaw = R(0f, MathF.PI), style = rng.Next(Styles), roof = 2, band = 2,
                });
            }
        }
    }
}
