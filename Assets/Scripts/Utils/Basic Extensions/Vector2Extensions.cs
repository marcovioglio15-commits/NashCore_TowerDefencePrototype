using UnityEngine;

namespace UnityExtensions
{
    // Extension methods for UnityEngine.Vector2.
    public static class Vector2Extensions
    {
        // Sets the x component of the vector.
        public static Vector2 SetX(this Vector2 vector, float x)
        {
            return new Vector2(x, vector.y);
        }

        // Sets the y component of the vector.
        public static Vector2 SetY(this Vector2 vector, float y)
        {
            return new Vector2(vector.x, y);
        }
    }
}
