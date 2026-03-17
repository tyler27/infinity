using Godot;

namespace LazerSystem.Zones
{
    /// <summary>
    /// Static utility class for applying keystone (perspective) correction
    /// via bilinear interpolation between four corner points.
    /// </summary>
    public static class KeystoneCorrection
    {
        /// <summary>
        /// Applies keystone correction by mapping a point from normalized space (-1..1)
        /// to a warped output quad defined by four corners.
        /// </summary>
        /// <param name="point">Input point in normalized space (-1 to 1).</param>
        /// <param name="corners">
        /// Four corners defining the warped output quad:
        ///   [0] = bottom-left, [1] = bottom-right,
        ///   [2] = top-right,   [3] = top-left.
        /// </param>
        /// <returns>The warped point position.</returns>
        public static Vector2 ApplyKeystone(Vector2 point, Vector2[] corners)
        {
            if (corners == null || corners.Length != 4)
            {
                GD.PushWarning("[KeystoneCorrection] Corners array must have exactly 4 elements.");
                return point;
            }

            // Map input from (-1..1) to (0..1) for bilinear interpolation.
            float u = (point.X + 1f) * 0.5f;
            float v = (point.Y + 1f) * 0.5f;

            // Bilinear interpolation across the quad.
            // Bottom edge: lerp between bottom-left [0] and bottom-right [1].
            Vector2 bottom = corners[0].Lerp(corners[1], u);

            // Top edge: lerp between top-left [3] and top-right [2].
            Vector2 top = corners[3].Lerp(corners[2], u);

            // Final: lerp between bottom and top edges.
            Vector2 result = bottom.Lerp(top, v);

            return result;
        }
    }
}
