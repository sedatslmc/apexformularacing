using Apex.Vehicle;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>Tags a collider with its drivable surface type (read by wheel raycasts).</summary>
    public sealed class TrackSurface : MonoBehaviour
    {
        public SurfaceType surface = SurfaceType.Asphalt;
    }
}
