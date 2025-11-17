using UnityEngine;

namespace UnityExtensions
{
    // Extension methods for UnityEngine.Rigidbody.
    public static class RigidbodyExtensions
    {
        // Changes the direction of a rigidbody without changing its speed.
        public static void ChangeDirection(this Rigidbody rigidbody, Vector3 direction)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = direction * rigidbody.linearVelocity.magnitude;
#else
            rigidbody.velocity = direction * rigidbody.velocity.magnitude;
#endif
        }
    }
}
