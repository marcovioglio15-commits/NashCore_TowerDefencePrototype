using UnityEngine;

namespace UnityExtensions
{
    // Extension methods for UnityEngine.Color.
    public static class ColorExtensions
    {

        // Sets the r component of the color.
        public static Color SetR(this Color color, float r)
        {
            return new Color(r, color.g, color.b, color.a);
        }

        // Sets the green component of the color.
        public static Color SetG(this Color color, float g)
        {
            return new Color(color.r, g, color.b, color.a);
        }

        // Sets the blue component of the color.
        public static Color SetB(this Color color, float b)
        {
            return new Color(color.r, color.g, b, color.a);
        }

        // Sets the alpha component of the color.
        public static Color SetA(this Color color, float a)
        {
            return new Color(color.r, color.g, color.b, a);
        }
    }
}
