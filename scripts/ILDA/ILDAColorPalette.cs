using Godot;

namespace LazerSystem.ILDA
{
    /// <summary>
    /// Provides the default ILDA 64-color palette used by indexed-color formats (0 and 1).
    /// </summary>
    public static class ILDAColorPalette
    {
        private static readonly Color[] DefaultPalette = new Color[]
        {
            // 0-7: Reds
            new Color(1.000f, 0.000f, 0.000f), // 0  Red
            new Color(1.000f, 0.063f, 0.000f), // 1
            new Color(1.000f, 0.125f, 0.000f), // 2
            new Color(1.000f, 0.188f, 0.000f), // 3
            new Color(1.000f, 0.251f, 0.000f), // 4
            new Color(1.000f, 0.314f, 0.000f), // 5
            new Color(1.000f, 0.376f, 0.000f), // 6
            new Color(1.000f, 0.439f, 0.000f), // 7

            // 8-15: Oranges / Yellows
            new Color(1.000f, 0.502f, 0.000f), // 8
            new Color(1.000f, 0.565f, 0.000f), // 9
            new Color(1.000f, 0.627f, 0.000f), // 10
            new Color(1.000f, 0.690f, 0.000f), // 11
            new Color(1.000f, 0.753f, 0.000f), // 12
            new Color(1.000f, 0.816f, 0.000f), // 13
            new Color(1.000f, 0.878f, 0.000f), // 14
            new Color(1.000f, 0.941f, 0.000f), // 15

            // 16-23: Yellows / Greens
            new Color(1.000f, 1.000f, 0.000f), // 16 Yellow
            new Color(0.882f, 1.000f, 0.000f), // 17
            new Color(0.753f, 1.000f, 0.000f), // 18
            new Color(0.627f, 1.000f, 0.000f), // 19
            new Color(0.502f, 1.000f, 0.000f), // 20
            new Color(0.376f, 1.000f, 0.000f), // 21
            new Color(0.251f, 1.000f, 0.000f), // 22
            new Color(0.125f, 1.000f, 0.000f), // 23

            // 24-31: Greens / Cyans
            new Color(0.000f, 1.000f, 0.000f), // 24 Green
            new Color(0.000f, 1.000f, 0.125f), // 25
            new Color(0.000f, 1.000f, 0.251f), // 26
            new Color(0.000f, 1.000f, 0.376f), // 27
            new Color(0.000f, 1.000f, 0.502f), // 28
            new Color(0.000f, 1.000f, 0.627f), // 29
            new Color(0.000f, 1.000f, 0.753f), // 30
            new Color(0.000f, 1.000f, 0.878f), // 31

            // 32-39: Cyans / Blues
            new Color(0.000f, 1.000f, 1.000f), // 32 Cyan
            new Color(0.000f, 0.878f, 1.000f), // 33
            new Color(0.000f, 0.753f, 1.000f), // 34
            new Color(0.000f, 0.627f, 1.000f), // 35
            new Color(0.000f, 0.502f, 1.000f), // 36
            new Color(0.000f, 0.376f, 1.000f), // 37
            new Color(0.000f, 0.251f, 1.000f), // 38
            new Color(0.000f, 0.125f, 1.000f), // 39

            // 40-47: Blues / Magentas
            new Color(0.000f, 0.000f, 1.000f), // 40 Blue
            new Color(0.125f, 0.000f, 1.000f), // 41
            new Color(0.251f, 0.000f, 1.000f), // 42
            new Color(0.376f, 0.000f, 1.000f), // 43
            new Color(0.502f, 0.000f, 1.000f), // 44
            new Color(0.627f, 0.000f, 1.000f), // 45
            new Color(0.753f, 0.000f, 1.000f), // 46
            new Color(0.878f, 0.000f, 1.000f), // 47

            // 48-55: Magentas / Pinks
            new Color(1.000f, 0.000f, 1.000f), // 48 Magenta
            new Color(1.000f, 0.000f, 0.878f), // 49
            new Color(1.000f, 0.000f, 0.753f), // 50
            new Color(1.000f, 0.000f, 0.627f), // 51
            new Color(1.000f, 0.000f, 0.502f), // 52
            new Color(1.000f, 0.000f, 0.376f), // 53
            new Color(1.000f, 0.000f, 0.251f), // 54
            new Color(1.000f, 0.000f, 0.125f), // 55

            // 56-63: Grays and White
            new Color(1.000f, 0.333f, 0.333f), // 56 Light red
            new Color(1.000f, 0.667f, 0.667f), // 57 Lighter red
            new Color(0.333f, 1.000f, 0.333f), // 58 Light green
            new Color(0.667f, 1.000f, 0.667f), // 59 Lighter green
            new Color(0.333f, 0.333f, 1.000f), // 60 Light blue
            new Color(0.667f, 0.667f, 1.000f), // 61 Lighter blue
            new Color(0.667f, 0.667f, 0.667f), // 62 Light gray
            new Color(1.000f, 1.000f, 1.000f), // 63 White
        };

        /// <summary>
        /// Returns the color for a given palette index. Indices outside the 0-63 range
        /// are clamped to the nearest valid index.
        /// </summary>
        public static Color GetColor(int index)
        {
            if (index < 0) index = 0;
            if (index >= DefaultPalette.Length) index = DefaultPalette.Length - 1;
            return DefaultPalette[index];
        }

        /// <summary>
        /// Returns the number of colors in the default palette.
        /// </summary>
        public static int PaletteSize => DefaultPalette.Length;
    }
}
