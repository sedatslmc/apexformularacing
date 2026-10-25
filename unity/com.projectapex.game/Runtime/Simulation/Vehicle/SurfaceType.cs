namespace Apex.Vehicle
{
    public enum SurfaceType
    {
        Asphalt = 0,
        Kerb = 1,
        Grass = 2,
        Gravel = 3,
        Wall = 4,
        PitLane = 5,
    }

    /// <summary>Physical properties per surface. Grip multiplies tire friction;
    /// rollingDrag is a fraction of wheel load; rumble drives audio/haptics.</summary>
    public readonly struct SurfaceProperties
    {
        public readonly float grip;
        public readonly float rollingDrag;
        public readonly float rumble;
        public readonly bool onTrack;

        public SurfaceProperties(float grip, float rollingDrag, float rumble, bool onTrack)
        {
            this.grip = grip;
            this.rollingDrag = rollingDrag;
            this.rumble = rumble;
            this.onTrack = onTrack;
        }

        public static SurfaceProperties For(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Kerb: return new SurfaceProperties(0.93f, 0.02f, 1f, true);
                case SurfaceType.Grass: return new SurfaceProperties(0.55f, 0.09f, 0.35f, false);
                case SurfaceType.Gravel: return new SurfaceProperties(0.42f, 0.35f, 0.8f, false);
                case SurfaceType.Wall: return new SurfaceProperties(0.5f, 0.05f, 0f, false);
                case SurfaceType.PitLane: return new SurfaceProperties(0.95f, 0.012f, 0f, true);
                default: return new SurfaceProperties(1f, 0.012f, 0f, true);
            }
        }
    }
}
