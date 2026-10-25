using UnityEngine;

namespace Apex.Game
{
    /// <summary>Conversions between System.Numerics (simulation) and UnityEngine types.</summary>
    public static class Num
    {
        public static Vector3 ToUnity(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static System.Numerics.Vector3 ToNum(this Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
    }

    /// <summary>Unity 6 renamed several Rigidbody members; this keeps one code path.</summary>
    public static class RigidbodyCompat
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

#if UNITY_6000_0_OR_NEWER
        public static PhysicsMaterial MakeMaterial(string name, float friction, float bounce)
        {
            return new PhysicsMaterial(name)
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = bounce,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
        }
#else
        public static PhysicMaterial MakeMaterial(string name, float friction, float bounce)
        {
            return new PhysicMaterial(name)
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = bounce,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounceCombine = PhysicMaterialCombine.Minimum,
            };
        }
#endif
    }
}
