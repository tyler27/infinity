using Godot;
using LazerSystem.Core;

public static class PresetCues
{
    private static LaserCue MakeCue(string name, LaserPatternType type, Color color,
        float intensity = 1f, float size = 0.5f, float rotation = 0f, float speed = 1f,
        float spread = 0f, int count = 1, float frequency = 1f, float amplitude = 0.5f)
    {
        var cue = new LaserCue();
        cue.CueName = name;
        cue.PatternType = type;
        cue.Color = color;
        cue.Intensity = intensity;
        cue.Size = size;
        cue.Rotation = rotation;
        cue.Speed = speed;
        cue.Spread = spread;
        cue.Count = count;
        cue.Frequency = frequency;
        cue.Amplitude = amplitude;
        return cue;
    }

    public static void PopulateDefaults(LiveEngine engine)
    {
        engine.PageNames[0] = "Beams & Fans";
        engine.PageNames[1] = "Shapes";
        engine.PageNames[2] = "Waves & Tunnels";
        engine.PageNames[3] = "Color Themes";

        PopulatePage0(engine);
        PopulatePage1(engine);
        PopulatePage2(engine);
        PopulatePage3(engine);
        // Pages 4-7 left empty for user content
    }

    // =========================================================================
    // Page 0 - Beams & Fans
    // =========================================================================
    private static void PopulatePage0(LiveEngine engine)
    {
        // Row 0: Single beams, different colors
        engine.SetCue(0, 0, 0, MakeCue("Red Beam", LaserPatternType.Beam, Colors.Red));
        engine.SetCue(0, 0, 1, MakeCue("Green Beam", LaserPatternType.Beam, Colors.Green));
        engine.SetCue(0, 0, 2, MakeCue("Blue Beam", LaserPatternType.Beam, Colors.Blue));
        engine.SetCue(0, 0, 3, MakeCue("White Beam", LaserPatternType.Beam, Colors.White));
        engine.SetCue(0, 0, 4, MakeCue("Yellow Beam", LaserPatternType.Beam, Colors.Yellow));
        engine.SetCue(0, 0, 5, MakeCue("Cyan Beam", LaserPatternType.Beam, Colors.Cyan));
        engine.SetCue(0, 0, 6, MakeCue("Magenta Beam", LaserPatternType.Beam, Colors.Magenta));
        engine.SetCue(0, 0, 7, MakeCue("Orange Beam", LaserPatternType.Beam, new Color(1f, 0.5f, 0f)));
        engine.SetCue(0, 0, 8, MakeCue("Pink Beam", LaserPatternType.Beam, new Color(1f, 0.3f, 0.6f)));
        engine.SetCue(0, 0, 9, MakeCue("Rainbow Beam", LaserPatternType.Beam, Colors.White, speed: 2f));

        // Row 1: Fans, increasing beam count
        engine.SetCue(0, 1, 0, MakeCue("Fan 2", LaserPatternType.Fan, Colors.Green, count: 2, spread: 45f));
        engine.SetCue(0, 1, 1, MakeCue("Fan 3", LaserPatternType.Fan, Colors.Cyan, count: 3, spread: 60f));
        engine.SetCue(0, 1, 2, MakeCue("Fan 4", LaserPatternType.Fan, Colors.Blue, count: 4, spread: 90f));
        engine.SetCue(0, 1, 3, MakeCue("Fan 5", LaserPatternType.Fan, Colors.Magenta, count: 5, spread: 90f));
        engine.SetCue(0, 1, 4, MakeCue("Fan 6", LaserPatternType.Fan, Colors.Red, count: 6, spread: 120f));
        engine.SetCue(0, 1, 5, MakeCue("Fan 8", LaserPatternType.Fan, Colors.Yellow, count: 8, spread: 120f));
        engine.SetCue(0, 1, 6, MakeCue("Fan 10", LaserPatternType.Fan, Colors.White, count: 10, spread: 150f));
        engine.SetCue(0, 1, 7, MakeCue("Fan 12", LaserPatternType.Fan, Colors.Green, count: 12, spread: 180f));
        engine.SetCue(0, 1, 8, MakeCue("Wide Fan", LaserPatternType.Fan, Colors.Cyan, count: 16, spread: 180f));
        engine.SetCue(0, 1, 9, MakeCue("Full Fan", LaserPatternType.Fan, Colors.White, count: 20, spread: 360f));

        // Row 2: Rotating fans
        engine.SetCue(0, 2, 0, MakeCue("Spin Fan 2", LaserPatternType.Fan, Colors.Red, count: 2, spread: 90f, speed: 2f));
        engine.SetCue(0, 2, 1, MakeCue("Spin Fan 4", LaserPatternType.Fan, Colors.Green, count: 4, spread: 90f, speed: 2f));
        engine.SetCue(0, 2, 2, MakeCue("Spin Fan 6", LaserPatternType.Fan, Colors.Blue, count: 6, spread: 120f, speed: 3f));
        engine.SetCue(0, 2, 3, MakeCue("Spin Fan 8", LaserPatternType.Fan, Colors.Cyan, count: 8, spread: 120f, speed: 3f));
        engine.SetCue(0, 2, 4, MakeCue("Fast Spin 4", LaserPatternType.Fan, Colors.Yellow, count: 4, spread: 90f, speed: 5f));
        engine.SetCue(0, 2, 5, MakeCue("Fast Spin 8", LaserPatternType.Fan, Colors.Magenta, count: 8, spread: 180f, speed: 5f));
        engine.SetCue(0, 2, 6, MakeCue("Slow Spin", LaserPatternType.Fan, Colors.White, count: 6, spread: 120f, speed: 0.5f));
        engine.SetCue(0, 2, 7, MakeCue("Rev Spin", LaserPatternType.Fan, Colors.Red, count: 4, spread: 90f, speed: -2f));
        engine.SetCue(0, 2, 8, MakeCue("Rev Spin 8", LaserPatternType.Fan, Colors.Green, count: 8, spread: 180f, speed: -3f));
        engine.SetCue(0, 2, 9, MakeCue("Sweep", LaserPatternType.Fan, Colors.White, count: 1, spread: 0f, speed: 2f));

        // Row 3: Cones
        engine.SetCue(0, 3, 0, MakeCue("Cone 4", LaserPatternType.Cone, Colors.Red, count: 4, size: 0.5f));
        engine.SetCue(0, 3, 1, MakeCue("Cone 6", LaserPatternType.Cone, Colors.Green, count: 6, size: 0.5f));
        engine.SetCue(0, 3, 2, MakeCue("Cone 8", LaserPatternType.Cone, Colors.Blue, count: 8, size: 0.5f));
        engine.SetCue(0, 3, 3, MakeCue("Wide Cone", LaserPatternType.Cone, Colors.Cyan, count: 8, size: 0.8f));
        engine.SetCue(0, 3, 4, MakeCue("Tight Cone", LaserPatternType.Cone, Colors.Yellow, count: 8, size: 0.2f));
        engine.SetCue(0, 3, 5, MakeCue("Spin Cone", LaserPatternType.Cone, Colors.Magenta, count: 6, size: 0.5f, speed: 2f));
        engine.SetCue(0, 3, 6, MakeCue("Fast Cone", LaserPatternType.Cone, Colors.White, count: 8, size: 0.5f, speed: 4f));
        engine.SetCue(0, 3, 7, MakeCue("Small Cone", LaserPatternType.Cone, Colors.Red, count: 4, size: 0.3f));
        engine.SetCue(0, 3, 8, MakeCue("Big Cone", LaserPatternType.Cone, Colors.Green, count: 12, size: 0.7f));
        engine.SetCue(0, 3, 9, MakeCue("Mega Cone", LaserPatternType.Cone, Colors.Blue, count: 16, size: 0.9f));

        // Row 4: Lines
        engine.SetCue(0, 4, 0, MakeCue("H Line", LaserPatternType.Line, Colors.Red, rotation: 0f));
        engine.SetCue(0, 4, 1, MakeCue("V Line", LaserPatternType.Line, Colors.Green, rotation: 90f));
        engine.SetCue(0, 4, 2, MakeCue("Diag Line", LaserPatternType.Line, Colors.Blue, rotation: 45f));
        engine.SetCue(0, 4, 3, MakeCue("Spin Line", LaserPatternType.Line, Colors.Cyan, speed: 2f));
        engine.SetCue(0, 4, 4, MakeCue("Fast Line", LaserPatternType.Line, Colors.Yellow, speed: 5f));
        engine.SetCue(0, 4, 5, MakeCue("Thick Line", LaserPatternType.Line, Colors.Magenta, size: 0.8f));
        engine.SetCue(0, 4, 6, MakeCue("Thin Line", LaserPatternType.Line, Colors.White, size: 0.2f));
        engine.SetCue(0, 4, 7, MakeCue("Short Line", LaserPatternType.Line, Colors.Red, size: 0.3f));
        engine.SetCue(0, 4, 8, MakeCue("Long Line", LaserPatternType.Line, Colors.Green, size: 0.9f));
        engine.SetCue(0, 4, 9, MakeCue("Rev Line", LaserPatternType.Line, Colors.Blue, speed: -3f));

        // Row 5: Mixed shapes
        engine.SetCue(0, 5, 0, MakeCue("Circle", LaserPatternType.Circle, Colors.Red, size: 0.5f, speed: 1f));
        engine.SetCue(0, 5, 1, MakeCue("Big Circle", LaserPatternType.Circle, Colors.Green, size: 0.8f, speed: 1f));
        engine.SetCue(0, 5, 2, MakeCue("Fast Circle", LaserPatternType.Circle, Colors.Blue, size: 0.5f, speed: 3f));
        engine.SetCue(0, 5, 3, MakeCue("Wave", LaserPatternType.Wave, Colors.Cyan, frequency: 2f, amplitude: 0.5f));
        engine.SetCue(0, 5, 4, MakeCue("Fast Wave", LaserPatternType.Wave, Colors.Yellow, frequency: 4f, amplitude: 0.3f, speed: 2f));
        engine.SetCue(0, 5, 5, MakeCue("Triangle", LaserPatternType.Triangle, Colors.Magenta, size: 0.5f, speed: 1f));
        engine.SetCue(0, 5, 6, MakeCue("Square", LaserPatternType.Square, Colors.White, size: 0.5f, speed: 1f));
        engine.SetCue(0, 5, 7, MakeCue("Star", LaserPatternType.Star, Colors.Red, size: 0.5f, count: 5));
        engine.SetCue(0, 5, 8, MakeCue("Tunnel", LaserPatternType.Tunnel, Colors.Green, size: 0.5f, speed: 2f, count: 6));
        engine.SetCue(0, 5, 9, MakeCue("Deep Tunnel", LaserPatternType.Tunnel, Colors.Blue, size: 0.8f, speed: 3f, count: 10));
    }

    // =========================================================================
    // Page 1 - Shapes & Graphics
    // =========================================================================
    private static void PopulatePage1(LiveEngine engine)
    {
        // Row 0: Circles
        engine.SetCue(1, 0, 0, MakeCue("Red Circle", LaserPatternType.Circle, Colors.Red, size: 0.4f));
        engine.SetCue(1, 0, 1, MakeCue("Green Circle", LaserPatternType.Circle, Colors.Green, size: 0.5f));
        engine.SetCue(1, 0, 2, MakeCue("Blue Circle", LaserPatternType.Circle, Colors.Blue, size: 0.6f));
        engine.SetCue(1, 0, 3, MakeCue("Cyan Circle", LaserPatternType.Circle, Colors.Cyan, size: 0.7f));
        engine.SetCue(1, 0, 4, MakeCue("Yellow Circle", LaserPatternType.Circle, Colors.Yellow, size: 0.3f));
        engine.SetCue(1, 0, 5, MakeCue("Magenta Circle", LaserPatternType.Circle, Colors.Magenta, size: 0.5f));
        engine.SetCue(1, 0, 6, MakeCue("Spin Circle S", LaserPatternType.Circle, Colors.Red, size: 0.3f, speed: 2f));
        engine.SetCue(1, 0, 7, MakeCue("Spin Circle M", LaserPatternType.Circle, Colors.Green, size: 0.5f, speed: 3f));
        engine.SetCue(1, 0, 8, MakeCue("Spin Circle L", LaserPatternType.Circle, Colors.Blue, size: 0.8f, speed: 4f));
        engine.SetCue(1, 0, 9, MakeCue("Pulse Circle", LaserPatternType.Circle, Colors.White, size: 0.6f, speed: 5f));

        // Row 1: Triangles
        engine.SetCue(1, 1, 0, MakeCue("Red Triangle", LaserPatternType.Triangle, Colors.Red, size: 0.4f));
        engine.SetCue(1, 1, 1, MakeCue("Green Triangle", LaserPatternType.Triangle, Colors.Green, size: 0.5f));
        engine.SetCue(1, 1, 2, MakeCue("Blue Triangle", LaserPatternType.Triangle, Colors.Blue, size: 0.6f));
        engine.SetCue(1, 1, 3, MakeCue("Cyan Triangle", LaserPatternType.Triangle, Colors.Cyan, size: 0.7f));
        engine.SetCue(1, 1, 4, MakeCue("Tiny Triangle", LaserPatternType.Triangle, Colors.Yellow, size: 0.2f));
        engine.SetCue(1, 1, 5, MakeCue("Big Triangle", LaserPatternType.Triangle, Colors.Magenta, size: 0.8f));
        engine.SetCue(1, 1, 6, MakeCue("Spin Tri Slow", LaserPatternType.Triangle, Colors.White, size: 0.5f, speed: 1.5f));
        engine.SetCue(1, 1, 7, MakeCue("Spin Tri Med", LaserPatternType.Triangle, Colors.Red, size: 0.5f, speed: 3f));
        engine.SetCue(1, 1, 8, MakeCue("Spin Tri Fast", LaserPatternType.Triangle, Colors.Green, size: 0.5f, speed: 5f));
        engine.SetCue(1, 1, 9, MakeCue("Tilt Triangle", LaserPatternType.Triangle, Colors.Blue, size: 0.5f, rotation: 30f));

        // Row 2: Squares
        engine.SetCue(1, 2, 0, MakeCue("Red Square", LaserPatternType.Square, Colors.Red, size: 0.4f));
        engine.SetCue(1, 2, 1, MakeCue("Green Square", LaserPatternType.Square, Colors.Green, size: 0.5f));
        engine.SetCue(1, 2, 2, MakeCue("Blue Square", LaserPatternType.Square, Colors.Blue, size: 0.6f));
        engine.SetCue(1, 2, 3, MakeCue("Cyan Square", LaserPatternType.Square, Colors.Cyan, size: 0.7f));
        engine.SetCue(1, 2, 4, MakeCue("Tiny Square", LaserPatternType.Square, Colors.Yellow, size: 0.2f));
        engine.SetCue(1, 2, 5, MakeCue("Big Square", LaserPatternType.Square, Colors.Magenta, size: 0.8f));
        engine.SetCue(1, 2, 6, MakeCue("Spin Sq Slow", LaserPatternType.Square, Colors.White, size: 0.5f, speed: 1.5f));
        engine.SetCue(1, 2, 7, MakeCue("Spin Sq Med", LaserPatternType.Square, Colors.Red, size: 0.5f, speed: 3f));
        engine.SetCue(1, 2, 8, MakeCue("Spin Sq Fast", LaserPatternType.Square, Colors.Green, size: 0.5f, speed: 5f));
        engine.SetCue(1, 2, 9, MakeCue("Diamond", LaserPatternType.Square, Colors.Blue, size: 0.5f, rotation: 45f));

        // Row 3: Stars
        engine.SetCue(1, 3, 0, MakeCue("Red Star 4", LaserPatternType.Star, Colors.Red, size: 0.4f, count: 4));
        engine.SetCue(1, 3, 1, MakeCue("Green Star 5", LaserPatternType.Star, Colors.Green, size: 0.5f, count: 5));
        engine.SetCue(1, 3, 2, MakeCue("Blue Star 6", LaserPatternType.Star, Colors.Blue, size: 0.5f, count: 6));
        engine.SetCue(1, 3, 3, MakeCue("Cyan Star 8", LaserPatternType.Star, Colors.Cyan, size: 0.6f, count: 8));
        engine.SetCue(1, 3, 4, MakeCue("Yellow Star 5", LaserPatternType.Star, Colors.Yellow, size: 0.4f, count: 5));
        engine.SetCue(1, 3, 5, MakeCue("Big Star 5", LaserPatternType.Star, Colors.Magenta, size: 0.8f, count: 5));
        engine.SetCue(1, 3, 6, MakeCue("Tiny Star 4", LaserPatternType.Star, Colors.White, size: 0.2f, count: 4));
        engine.SetCue(1, 3, 7, MakeCue("Spin Star 5", LaserPatternType.Star, Colors.Red, size: 0.5f, count: 5, speed: 2f));
        engine.SetCue(1, 3, 8, MakeCue("Spin Star 6", LaserPatternType.Star, Colors.Green, size: 0.5f, count: 6, speed: 3f));
        engine.SetCue(1, 3, 9, MakeCue("Spin Star 8", LaserPatternType.Star, Colors.Blue, size: 0.6f, count: 8, speed: 4f));

        // Row 4: Mixed shapes with rotation
        engine.SetCue(1, 4, 0, MakeCue("Tilt Circle", LaserPatternType.Circle, Colors.Red, size: 0.5f, rotation: 15f));
        engine.SetCue(1, 4, 1, MakeCue("Tilt Square", LaserPatternType.Square, Colors.Green, size: 0.5f, rotation: 22f));
        engine.SetCue(1, 4, 2, MakeCue("Tilt Star", LaserPatternType.Star, Colors.Blue, size: 0.5f, count: 5, rotation: 36f));
        engine.SetCue(1, 4, 3, MakeCue("Rev Circle", LaserPatternType.Circle, Colors.Cyan, size: 0.5f, speed: -2f));
        engine.SetCue(1, 4, 4, MakeCue("Rev Square", LaserPatternType.Square, Colors.Yellow, size: 0.5f, speed: -2f));
        engine.SetCue(1, 4, 5, MakeCue("Rev Triangle", LaserPatternType.Triangle, Colors.Magenta, size: 0.5f, speed: -2f));
        engine.SetCue(1, 4, 6, MakeCue("Rev Star", LaserPatternType.Star, Colors.White, size: 0.5f, count: 5, speed: -2f));
        engine.SetCue(1, 4, 7, MakeCue("Slow Circle", LaserPatternType.Circle, Colors.Red, size: 0.6f, speed: 0.3f));
        engine.SetCue(1, 4, 8, MakeCue("Slow Square", LaserPatternType.Square, Colors.Green, size: 0.6f, speed: 0.3f));
        engine.SetCue(1, 4, 9, MakeCue("Slow Star", LaserPatternType.Star, Colors.Blue, size: 0.6f, count: 6, speed: 0.3f));

        // Row 5: Question Blocks
        engine.SetCue(1, 5, 0, MakeCue("? Block", LaserPatternType.QuestionBlock, Colors.Yellow, size: 0.5f, speed: 0f));
        engine.SetCue(1, 5, 1, MakeCue("? Block Spin", LaserPatternType.QuestionBlock, Colors.Yellow, size: 0.5f, speed: 1f));
        engine.SetCue(1, 5, 2, MakeCue("? Block Fast", LaserPatternType.QuestionBlock, Colors.Yellow, size: 0.5f, speed: 3f));
        engine.SetCue(1, 5, 3, MakeCue("? Block Big", LaserPatternType.QuestionBlock, Colors.Yellow, size: 0.8f, speed: 0f));
        engine.SetCue(1, 5, 4, MakeCue("? Block Small", LaserPatternType.QuestionBlock, Colors.Yellow, size: 0.3f, speed: 0f));
        engine.SetCue(1, 5, 5, MakeCue("? Block Red", LaserPatternType.QuestionBlock, Colors.Red, size: 0.5f, speed: 0f));
        engine.SetCue(1, 5, 6, MakeCue("? Block Green", LaserPatternType.QuestionBlock, Colors.Green, size: 0.5f, speed: 0f));
        engine.SetCue(1, 5, 7, MakeCue("? Block Blue", LaserPatternType.QuestionBlock, Colors.Blue, size: 0.5f, speed: 0f));
        engine.SetCue(1, 5, 8, MakeCue("? Block Cyan", LaserPatternType.QuestionBlock, Colors.Cyan, size: 0.5f, speed: 1.5f));
        engine.SetCue(1, 5, 9, MakeCue("? Block White", LaserPatternType.QuestionBlock, Colors.White, size: 0.6f, speed: 2f));
    }

    // =========================================================================
    // Page 2 - Waves & Tunnels
    // =========================================================================
    private static void PopulatePage2(LiveEngine engine)
    {
        // Row 0: Waves - varying frequency
        engine.SetCue(2, 0, 0, MakeCue("Wave Slow", LaserPatternType.Wave, Colors.Red, frequency: 1f, amplitude: 0.5f, speed: 0.5f));
        engine.SetCue(2, 0, 1, MakeCue("Wave Med", LaserPatternType.Wave, Colors.Green, frequency: 2f, amplitude: 0.5f, speed: 1f));
        engine.SetCue(2, 0, 2, MakeCue("Wave Fast", LaserPatternType.Wave, Colors.Blue, frequency: 3f, amplitude: 0.5f, speed: 2f));
        engine.SetCue(2, 0, 3, MakeCue("Wave Rapid", LaserPatternType.Wave, Colors.Cyan, frequency: 5f, amplitude: 0.4f, speed: 3f));
        engine.SetCue(2, 0, 4, MakeCue("Wave Hyper", LaserPatternType.Wave, Colors.Yellow, frequency: 8f, amplitude: 0.3f, speed: 4f));
        engine.SetCue(2, 0, 5, MakeCue("Wave Gentle", LaserPatternType.Wave, Colors.Magenta, frequency: 1f, amplitude: 0.3f, speed: 0.3f));
        engine.SetCue(2, 0, 6, MakeCue("Wave Tight", LaserPatternType.Wave, Colors.White, frequency: 6f, amplitude: 0.2f, speed: 1f));
        engine.SetCue(2, 0, 7, MakeCue("Wave Loose", LaserPatternType.Wave, Colors.Red, frequency: 1.5f, amplitude: 0.8f, speed: 1f));
        engine.SetCue(2, 0, 8, MakeCue("Wave Ultra", LaserPatternType.Wave, Colors.Green, frequency: 10f, amplitude: 0.2f, speed: 5f));
        engine.SetCue(2, 0, 9, MakeCue("Wave Crawl", LaserPatternType.Wave, Colors.Blue, frequency: 2f, amplitude: 0.6f, speed: 0.2f));

        // Row 1: Waves - varying amplitude
        engine.SetCue(2, 1, 0, MakeCue("Flat Wave", LaserPatternType.Wave, Colors.Red, frequency: 3f, amplitude: 0.1f, speed: 1f));
        engine.SetCue(2, 1, 1, MakeCue("Low Wave", LaserPatternType.Wave, Colors.Green, frequency: 3f, amplitude: 0.2f, speed: 1f));
        engine.SetCue(2, 1, 2, MakeCue("Mid Wave", LaserPatternType.Wave, Colors.Blue, frequency: 3f, amplitude: 0.4f, speed: 1f));
        engine.SetCue(2, 1, 3, MakeCue("High Wave", LaserPatternType.Wave, Colors.Cyan, frequency: 3f, amplitude: 0.6f, speed: 1f));
        engine.SetCue(2, 1, 4, MakeCue("Max Wave", LaserPatternType.Wave, Colors.Yellow, frequency: 3f, amplitude: 0.9f, speed: 1f));
        engine.SetCue(2, 1, 5, MakeCue("Deep Wave", LaserPatternType.Wave, Colors.Magenta, frequency: 2f, amplitude: 0.8f, speed: 1.5f));
        engine.SetCue(2, 1, 6, MakeCue("Shallow Wave", LaserPatternType.Wave, Colors.White, frequency: 4f, amplitude: 0.15f, speed: 2f));
        engine.SetCue(2, 1, 7, MakeCue("Rolling Wave", LaserPatternType.Wave, Colors.Red, frequency: 1.5f, amplitude: 0.7f, speed: 0.8f));
        engine.SetCue(2, 1, 8, MakeCue("Choppy Wave", LaserPatternType.Wave, Colors.Green, frequency: 7f, amplitude: 0.35f, speed: 3f));
        engine.SetCue(2, 1, 9, MakeCue("Surge Wave", LaserPatternType.Wave, Colors.Blue, frequency: 2f, amplitude: 0.9f, speed: 4f));

        // Row 2: Waves - color variations
        engine.SetCue(2, 2, 0, MakeCue("Red Wave", LaserPatternType.Wave, Colors.Red, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 1, MakeCue("Green Wave", LaserPatternType.Wave, Colors.Green, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 2, MakeCue("Blue Wave", LaserPatternType.Wave, Colors.Blue, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 3, MakeCue("Cyan Wave", LaserPatternType.Wave, Colors.Cyan, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 4, MakeCue("Yellow Wave", LaserPatternType.Wave, Colors.Yellow, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 5, MakeCue("Magenta Wave", LaserPatternType.Wave, Colors.Magenta, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 6, MakeCue("White Wave", LaserPatternType.Wave, Colors.White, frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 7, MakeCue("Orange Wave", LaserPatternType.Wave, new Color(1f, 0.5f, 0f), frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 8, MakeCue("Pink Wave", LaserPatternType.Wave, new Color(1f, 0.3f, 0.6f), frequency: 3f, amplitude: 0.5f, speed: 1.5f));
        engine.SetCue(2, 2, 9, MakeCue("Rev Wave", LaserPatternType.Wave, Colors.White, frequency: 3f, amplitude: 0.5f, speed: -2f));

        // Row 3: Tunnels - varying count
        engine.SetCue(2, 3, 0, MakeCue("Tunnel 3", LaserPatternType.Tunnel, Colors.Red, size: 0.5f, speed: 1f, count: 3));
        engine.SetCue(2, 3, 1, MakeCue("Tunnel 4", LaserPatternType.Tunnel, Colors.Green, size: 0.5f, speed: 1f, count: 4));
        engine.SetCue(2, 3, 2, MakeCue("Tunnel 6", LaserPatternType.Tunnel, Colors.Blue, size: 0.5f, speed: 1.5f, count: 6));
        engine.SetCue(2, 3, 3, MakeCue("Tunnel 8", LaserPatternType.Tunnel, Colors.Cyan, size: 0.5f, speed: 1.5f, count: 8));
        engine.SetCue(2, 3, 4, MakeCue("Tunnel 10", LaserPatternType.Tunnel, Colors.Yellow, size: 0.5f, speed: 2f, count: 10));
        engine.SetCue(2, 3, 5, MakeCue("Tunnel 12", LaserPatternType.Tunnel, Colors.Magenta, size: 0.5f, speed: 2f, count: 12));
        engine.SetCue(2, 3, 6, MakeCue("Tunnel 16", LaserPatternType.Tunnel, Colors.White, size: 0.6f, speed: 2.5f, count: 16));
        engine.SetCue(2, 3, 7, MakeCue("Tunnel 20", LaserPatternType.Tunnel, Colors.Red, size: 0.6f, speed: 3f, count: 20));
        engine.SetCue(2, 3, 8, MakeCue("Sparse Tunnel", LaserPatternType.Tunnel, Colors.Green, size: 0.7f, speed: 1f, count: 3));
        engine.SetCue(2, 3, 9, MakeCue("Dense Tunnel", LaserPatternType.Tunnel, Colors.Blue, size: 0.4f, speed: 2f, count: 24));

        // Row 4: Tunnels - varying size and speed
        engine.SetCue(2, 4, 0, MakeCue("Tiny Tunnel", LaserPatternType.Tunnel, Colors.Red, size: 0.2f, speed: 1f, count: 6));
        engine.SetCue(2, 4, 1, MakeCue("Small Tunnel", LaserPatternType.Tunnel, Colors.Green, size: 0.3f, speed: 1.5f, count: 6));
        engine.SetCue(2, 4, 2, MakeCue("Med Tunnel", LaserPatternType.Tunnel, Colors.Blue, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 4, 3, MakeCue("Big Tunnel", LaserPatternType.Tunnel, Colors.Cyan, size: 0.7f, speed: 2f, count: 8));
        engine.SetCue(2, 4, 4, MakeCue("Huge Tunnel", LaserPatternType.Tunnel, Colors.Yellow, size: 0.9f, speed: 2.5f, count: 10));
        engine.SetCue(2, 4, 5, MakeCue("Slow Tunnel", LaserPatternType.Tunnel, Colors.Magenta, size: 0.5f, speed: 0.5f, count: 8));
        engine.SetCue(2, 4, 6, MakeCue("Fast Tunnel", LaserPatternType.Tunnel, Colors.White, size: 0.5f, speed: 4f, count: 8));
        engine.SetCue(2, 4, 7, MakeCue("Hyper Tunnel", LaserPatternType.Tunnel, Colors.Red, size: 0.5f, speed: 6f, count: 10));
        engine.SetCue(2, 4, 8, MakeCue("Rev Tunnel", LaserPatternType.Tunnel, Colors.Green, size: 0.5f, speed: -2f, count: 8));
        engine.SetCue(2, 4, 9, MakeCue("Rev Fast Tun", LaserPatternType.Tunnel, Colors.Blue, size: 0.5f, speed: -4f, count: 10));

        // Row 5: Tunnels - color variations
        engine.SetCue(2, 5, 0, MakeCue("Red Tunnel", LaserPatternType.Tunnel, Colors.Red, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 1, MakeCue("Green Tunnel", LaserPatternType.Tunnel, Colors.Green, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 2, MakeCue("Blue Tunnel", LaserPatternType.Tunnel, Colors.Blue, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 3, MakeCue("Cyan Tunnel", LaserPatternType.Tunnel, Colors.Cyan, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 4, MakeCue("Yellow Tunnel", LaserPatternType.Tunnel, Colors.Yellow, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 5, MakeCue("Mag Tunnel", LaserPatternType.Tunnel, Colors.Magenta, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 6, MakeCue("White Tunnel", LaserPatternType.Tunnel, Colors.White, size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 7, MakeCue("Orange Tunnel", LaserPatternType.Tunnel, new Color(1f, 0.5f, 0f), size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 8, MakeCue("Pink Tunnel", LaserPatternType.Tunnel, new Color(1f, 0.3f, 0.6f), size: 0.5f, speed: 2f, count: 8));
        engine.SetCue(2, 5, 9, MakeCue("Warp Tunnel", LaserPatternType.Tunnel, Colors.White, size: 0.8f, speed: 8f, count: 16));
    }

    // =========================================================================
    // Page 3 - Color Themes
    // =========================================================================
    private static void PopulatePage3(LiveEngine engine)
    {
        // Row 0: All red variations
        engine.SetCue(3, 0, 0, MakeCue("Red Beam", LaserPatternType.Beam, Colors.Red));
        engine.SetCue(3, 0, 1, MakeCue("Red Fan 4", LaserPatternType.Fan, Colors.Red, count: 4, spread: 90f));
        engine.SetCue(3, 0, 2, MakeCue("Red Fan 8", LaserPatternType.Fan, Colors.Red, count: 8, spread: 120f));
        engine.SetCue(3, 0, 3, MakeCue("Red Cone", LaserPatternType.Cone, Colors.Red, count: 6, size: 0.5f));
        engine.SetCue(3, 0, 4, MakeCue("Red Circle", LaserPatternType.Circle, Colors.Red, size: 0.5f));
        engine.SetCue(3, 0, 5, MakeCue("Red Line", LaserPatternType.Line, Colors.Red, size: 0.6f));
        engine.SetCue(3, 0, 6, MakeCue("Red Wave", LaserPatternType.Wave, Colors.Red, frequency: 3f, amplitude: 0.5f));
        engine.SetCue(3, 0, 7, MakeCue("Red Triangle", LaserPatternType.Triangle, Colors.Red, size: 0.5f));
        engine.SetCue(3, 0, 8, MakeCue("Red Star", LaserPatternType.Star, Colors.Red, size: 0.5f, count: 5));
        engine.SetCue(3, 0, 9, MakeCue("Red Tunnel", LaserPatternType.Tunnel, Colors.Red, size: 0.5f, speed: 2f, count: 8));

        // Row 1: All green variations
        engine.SetCue(3, 1, 0, MakeCue("Green Beam", LaserPatternType.Beam, Colors.Green));
        engine.SetCue(3, 1, 1, MakeCue("Green Fan 4", LaserPatternType.Fan, Colors.Green, count: 4, spread: 90f));
        engine.SetCue(3, 1, 2, MakeCue("Green Fan 8", LaserPatternType.Fan, Colors.Green, count: 8, spread: 120f));
        engine.SetCue(3, 1, 3, MakeCue("Green Cone", LaserPatternType.Cone, Colors.Green, count: 6, size: 0.5f));
        engine.SetCue(3, 1, 4, MakeCue("Green Circle", LaserPatternType.Circle, Colors.Green, size: 0.5f));
        engine.SetCue(3, 1, 5, MakeCue("Green Line", LaserPatternType.Line, Colors.Green, size: 0.6f));
        engine.SetCue(3, 1, 6, MakeCue("Green Wave", LaserPatternType.Wave, Colors.Green, frequency: 3f, amplitude: 0.5f));
        engine.SetCue(3, 1, 7, MakeCue("Green Triangle", LaserPatternType.Triangle, Colors.Green, size: 0.5f));
        engine.SetCue(3, 1, 8, MakeCue("Green Star", LaserPatternType.Star, Colors.Green, size: 0.5f, count: 5));
        engine.SetCue(3, 1, 9, MakeCue("Green Tunnel", LaserPatternType.Tunnel, Colors.Green, size: 0.5f, speed: 2f, count: 8));

        // Row 2: All blue variations
        engine.SetCue(3, 2, 0, MakeCue("Blue Beam", LaserPatternType.Beam, Colors.Blue));
        engine.SetCue(3, 2, 1, MakeCue("Blue Fan 4", LaserPatternType.Fan, Colors.Blue, count: 4, spread: 90f));
        engine.SetCue(3, 2, 2, MakeCue("Blue Fan 8", LaserPatternType.Fan, Colors.Blue, count: 8, spread: 120f));
        engine.SetCue(3, 2, 3, MakeCue("Blue Cone", LaserPatternType.Cone, Colors.Blue, count: 6, size: 0.5f));
        engine.SetCue(3, 2, 4, MakeCue("Blue Circle", LaserPatternType.Circle, Colors.Blue, size: 0.5f));
        engine.SetCue(3, 2, 5, MakeCue("Blue Line", LaserPatternType.Line, Colors.Blue, size: 0.6f));
        engine.SetCue(3, 2, 6, MakeCue("Blue Wave", LaserPatternType.Wave, Colors.Blue, frequency: 3f, amplitude: 0.5f));
        engine.SetCue(3, 2, 7, MakeCue("Blue Triangle", LaserPatternType.Triangle, Colors.Blue, size: 0.5f));
        engine.SetCue(3, 2, 8, MakeCue("Blue Star", LaserPatternType.Star, Colors.Blue, size: 0.5f, count: 5));
        engine.SetCue(3, 2, 9, MakeCue("Blue Tunnel", LaserPatternType.Tunnel, Colors.Blue, size: 0.5f, speed: 2f, count: 8));

        // Row 3: Warm colors (red, orange, yellow)
        engine.SetCue(3, 3, 0, MakeCue("Warm Beam", LaserPatternType.Beam, new Color(1f, 0.5f, 0f)));
        engine.SetCue(3, 3, 1, MakeCue("Warm Fan 4", LaserPatternType.Fan, Colors.Red, count: 4, spread: 90f));
        engine.SetCue(3, 3, 2, MakeCue("Warm Fan 8", LaserPatternType.Fan, Colors.Yellow, count: 8, spread: 120f));
        engine.SetCue(3, 3, 3, MakeCue("Orange Cone", LaserPatternType.Cone, new Color(1f, 0.5f, 0f), count: 6, size: 0.5f));
        engine.SetCue(3, 3, 4, MakeCue("Gold Circle", LaserPatternType.Circle, new Color(1f, 0.8f, 0f), size: 0.5f));
        engine.SetCue(3, 3, 5, MakeCue("Amber Line", LaserPatternType.Line, new Color(1f, 0.6f, 0f), size: 0.6f));
        engine.SetCue(3, 3, 6, MakeCue("Sunset Wave", LaserPatternType.Wave, new Color(1f, 0.4f, 0f), frequency: 3f, amplitude: 0.5f));
        engine.SetCue(3, 3, 7, MakeCue("Fire Triangle", LaserPatternType.Triangle, new Color(1f, 0.3f, 0f), size: 0.5f));
        engine.SetCue(3, 3, 8, MakeCue("Gold Star", LaserPatternType.Star, new Color(1f, 0.8f, 0f), size: 0.5f, count: 5));
        engine.SetCue(3, 3, 9, MakeCue("Lava Tunnel", LaserPatternType.Tunnel, new Color(1f, 0.3f, 0f), size: 0.5f, speed: 2f, count: 8));

        // Row 4: Cool colors (cyan, blue, magenta)
        engine.SetCue(3, 4, 0, MakeCue("Cool Beam", LaserPatternType.Beam, Colors.Cyan));
        engine.SetCue(3, 4, 1, MakeCue("Cool Fan 4", LaserPatternType.Fan, Colors.Cyan, count: 4, spread: 90f));
        engine.SetCue(3, 4, 2, MakeCue("Cool Fan 8", LaserPatternType.Fan, Colors.Magenta, count: 8, spread: 120f));
        engine.SetCue(3, 4, 3, MakeCue("Ice Cone", LaserPatternType.Cone, new Color(0.4f, 0.7f, 1f), count: 6, size: 0.5f));
        engine.SetCue(3, 4, 4, MakeCue("Frost Circle", LaserPatternType.Circle, new Color(0.5f, 0.8f, 1f), size: 0.5f));
        engine.SetCue(3, 4, 5, MakeCue("Indigo Line", LaserPatternType.Line, new Color(0.3f, 0.2f, 1f), size: 0.6f));
        engine.SetCue(3, 4, 6, MakeCue("Ocean Wave", LaserPatternType.Wave, new Color(0f, 0.5f, 1f), frequency: 3f, amplitude: 0.5f));
        engine.SetCue(3, 4, 7, MakeCue("Violet Tri", LaserPatternType.Triangle, new Color(0.6f, 0.2f, 1f), size: 0.5f));
        engine.SetCue(3, 4, 8, MakeCue("Sapphire Star", LaserPatternType.Star, new Color(0.2f, 0.3f, 1f), size: 0.5f, count: 5));
        engine.SetCue(3, 4, 9, MakeCue("Neon Tunnel", LaserPatternType.Tunnel, Colors.Magenta, size: 0.5f, speed: 2f, count: 8));

        // Row 5: White & pastel
        engine.SetCue(3, 5, 0, MakeCue("White Beam", LaserPatternType.Beam, Colors.White));
        engine.SetCue(3, 5, 1, MakeCue("White Fan 6", LaserPatternType.Fan, Colors.White, count: 6, spread: 120f));
        engine.SetCue(3, 5, 2, MakeCue("Pastel Pink", LaserPatternType.Circle, new Color(1f, 0.7f, 0.8f), size: 0.5f));
        engine.SetCue(3, 5, 3, MakeCue("Pastel Blue", LaserPatternType.Triangle, new Color(0.7f, 0.8f, 1f), size: 0.5f));
        engine.SetCue(3, 5, 4, MakeCue("Pastel Green", LaserPatternType.Square, new Color(0.7f, 1f, 0.8f), size: 0.5f));
        engine.SetCue(3, 5, 5, MakeCue("Pastel Lilac", LaserPatternType.Star, new Color(0.8f, 0.7f, 1f), size: 0.5f, count: 5));
        engine.SetCue(3, 5, 6, MakeCue("Pastel Peach", LaserPatternType.Wave, new Color(1f, 0.8f, 0.7f), frequency: 3f, amplitude: 0.5f));
        engine.SetCue(3, 5, 7, MakeCue("Silver Cone", LaserPatternType.Cone, new Color(0.85f, 0.85f, 0.9f), count: 6, size: 0.5f));
        engine.SetCue(3, 5, 8, MakeCue("Cream Line", LaserPatternType.Line, new Color(1f, 0.95f, 0.85f), size: 0.6f));
        engine.SetCue(3, 5, 9, MakeCue("White Tunnel", LaserPatternType.Tunnel, Colors.White, size: 0.5f, speed: 2f, count: 8));
    }
}
