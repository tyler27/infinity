using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Basic vector text pattern. Uses a simple built-in vector font stored as point arrays
    /// for characters A-Z and 0-9. Each character is defined as a series of strokes
    /// (line segments) in a normalized 0-1 cell.
    /// </summary>
    public class TextPattern : ILaserPattern
    {
        public string PatternName => "Text";

        private const int PointsPerStroke = 6;

        // Vector font: each character is an array of strokes.
        // A stroke is (x1, y1, x2, y2) in normalized 0-1 character space.
        private static readonly Dictionary<char, float[][]> Font = BuildFont();

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            float charWidth = parameters.size * 0.3f;
            float charHeight = parameters.size * 0.5f;
            float spacing = charWidth * 1.3f;
            float rotation = Mathf.DegToRad(parameters.rotation) + time * parameters.speed * 0.2f;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            string text = string.IsNullOrEmpty(parameters.text) ? "TEXT" : parameters.text.ToUpperInvariant();
            if (string.IsNullOrEmpty(text)) return;

            float totalWidth = text.Length * spacing - (spacing - charWidth);
            float startX = -totalWidth * 0.5f;

            float cosA = Mathf.Cos(rotation);
            float sinA = Mathf.Sin(rotation);

            for (int ci = 0; ci < text.Length; ci++)
            {
                char ch = text[ci];
                if (ch == ' ') continue;

                if (!Font.TryGetValue(ch, out float[][] strokes)) continue;

                float charOriginX = startX + ci * spacing;
                float charOriginY = -charHeight * 0.5f;

                foreach (float[] stroke in strokes)
                {
                    float lx1 = charOriginX + stroke[0] * charWidth;
                    float ly1 = charOriginY + stroke[1] * charHeight;
                    float lx2 = charOriginX + stroke[2] * charWidth;
                    float ly2 = charOriginY + stroke[3] * charHeight;

                    // Rotate and translate
                    float rx1 = lx1 * cosA - ly1 * sinA + cx;
                    float ry1 = lx1 * sinA + ly1 * cosA + cy;
                    float rx2 = lx2 * cosA - ly2 * sinA + cx;
                    float ry2 = lx2 * sinA + ly2 * cosA + cy;

                    // Blank to start of stroke
                    output.Add(LaserPoint.Blanked(rx1, ry1));

                    // Draw stroke
                    for (int p = 0; p <= PointsPerStroke; p++)
                    {
                        float t = (float)p / PointsPerStroke;
                        float px = Mathf.Lerp(rx1, rx2, t);
                        float py = Mathf.Lerp(ry1, ry2, t);
                        output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
                    }
                }
            }
        }

        private static Dictionary<char, float[][]> BuildFont()
        {
            // Simplified vector font. Each stroke is {x1, y1, x2, y2} in 0-1 space.
            // Characters are designed on a grid: x: 0-1, y: 0(bottom)-1(top)
            var font = new Dictionary<char, float[][]>();

            font['A'] = new[] {
                new[] {0f,0f, 0.5f,1f}, new[] {0.5f,1f, 1f,0f}, new[] {0.25f,0.5f, 0.75f,0.5f}
            };
            font['B'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 0.8f,1f}, new[] {0.8f,1f, 0.8f,0.55f},
                new[] {0.8f,0.55f, 0f,0.5f}, new[] {0f,0.5f, 0.8f,0.45f},
                new[] {0.8f,0.45f, 0.8f,0f}, new[] {0.8f,0f, 0f,0f}
            };
            font['C'] = new[] {
                new[] {1f,0.2f, 0.5f,0f}, new[] {0.5f,0f, 0f,0.2f}, new[] {0f,0.2f, 0f,0.8f},
                new[] {0f,0.8f, 0.5f,1f}, new[] {0.5f,1f, 1f,0.8f}
            };
            font['D'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 0.6f,1f}, new[] {0.6f,1f, 1f,0.7f},
                new[] {1f,0.7f, 1f,0.3f}, new[] {1f,0.3f, 0.6f,0f}, new[] {0.6f,0f, 0f,0f}
            };
            font['E'] = new[] {
                new[] {1f,0f, 0f,0f}, new[] {0f,0f, 0f,1f}, new[] {0f,1f, 1f,1f},
                new[] {0f,0.5f, 0.7f,0.5f}
            };
            font['F'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 1f,1f}, new[] {0f,0.5f, 0.7f,0.5f}
            };
            font['G'] = new[] {
                new[] {1f,0.8f, 0.5f,1f}, new[] {0.5f,1f, 0f,0.8f}, new[] {0f,0.8f, 0f,0.2f},
                new[] {0f,0.2f, 0.5f,0f}, new[] {0.5f,0f, 1f,0.2f}, new[] {1f,0.2f, 1f,0.5f},
                new[] {1f,0.5f, 0.5f,0.5f}
            };
            font['H'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {1f,0f, 1f,1f}, new[] {0f,0.5f, 1f,0.5f}
            };
            font['I'] = new[] {
                new[] {0.2f,0f, 0.8f,0f}, new[] {0.5f,0f, 0.5f,1f}, new[] {0.2f,1f, 0.8f,1f}
            };
            font['J'] = new[] {
                new[] {0.3f,1f, 1f,1f}, new[] {0.7f,1f, 0.7f,0.2f},
                new[] {0.7f,0.2f, 0.4f,0f}, new[] {0.4f,0f, 0.1f,0.2f}
            };
            font['K'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {1f,1f, 0f,0.5f}, new[] {0f,0.5f, 1f,0f}
            };
            font['L'] = new[] {
                new[] {0f,1f, 0f,0f}, new[] {0f,0f, 1f,0f}
            };
            font['M'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 0.5f,0.5f},
                new[] {0.5f,0.5f, 1f,1f}, new[] {1f,1f, 1f,0f}
            };
            font['N'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 1f,0f}, new[] {1f,0f, 1f,1f}
            };
            font['O'] = new[] {
                new[] {0f,0.2f, 0f,0.8f}, new[] {0f,0.8f, 0.3f,1f}, new[] {0.3f,1f, 0.7f,1f},
                new[] {0.7f,1f, 1f,0.8f}, new[] {1f,0.8f, 1f,0.2f}, new[] {1f,0.2f, 0.7f,0f},
                new[] {0.7f,0f, 0.3f,0f}, new[] {0.3f,0f, 0f,0.2f}
            };
            font['P'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 0.8f,1f},
                new[] {0.8f,1f, 0.8f,0.5f}, new[] {0.8f,0.5f, 0f,0.5f}
            };
            font['Q'] = new[] {
                new[] {0f,0.2f, 0f,0.8f}, new[] {0f,0.8f, 0.3f,1f}, new[] {0.3f,1f, 0.7f,1f},
                new[] {0.7f,1f, 1f,0.8f}, new[] {1f,0.8f, 1f,0.2f}, new[] {1f,0.2f, 0.7f,0f},
                new[] {0.7f,0f, 0.3f,0f}, new[] {0.3f,0f, 0f,0.2f}, new[] {0.6f,0.3f, 1f,0f}
            };
            font['R'] = new[] {
                new[] {0f,0f, 0f,1f}, new[] {0f,1f, 0.8f,1f},
                new[] {0.8f,1f, 0.8f,0.5f}, new[] {0.8f,0.5f, 0f,0.5f}, new[] {0.5f,0.5f, 1f,0f}
            };
            font['S'] = new[] {
                new[] {1f,0.8f, 0.5f,1f}, new[] {0.5f,1f, 0f,0.8f}, new[] {0f,0.8f, 0f,0.6f},
                new[] {0f,0.6f, 1f,0.4f}, new[] {1f,0.4f, 1f,0.2f},
                new[] {1f,0.2f, 0.5f,0f}, new[] {0.5f,0f, 0f,0.2f}
            };
            font['T'] = new[] {
                new[] {0f,1f, 1f,1f}, new[] {0.5f,1f, 0.5f,0f}
            };
            font['U'] = new[] {
                new[] {0f,1f, 0f,0.2f}, new[] {0f,0.2f, 0.3f,0f}, new[] {0.3f,0f, 0.7f,0f},
                new[] {0.7f,0f, 1f,0.2f}, new[] {1f,0.2f, 1f,1f}
            };
            font['V'] = new[] {
                new[] {0f,1f, 0.5f,0f}, new[] {0.5f,0f, 1f,1f}
            };
            font['W'] = new[] {
                new[] {0f,1f, 0.25f,0f}, new[] {0.25f,0f, 0.5f,0.5f},
                new[] {0.5f,0.5f, 0.75f,0f}, new[] {0.75f,0f, 1f,1f}
            };
            font['X'] = new[] {
                new[] {0f,0f, 1f,1f}, new[] {0f,1f, 1f,0f}
            };
            font['Y'] = new[] {
                new[] {0f,1f, 0.5f,0.5f}, new[] {1f,1f, 0.5f,0.5f}, new[] {0.5f,0.5f, 0.5f,0f}
            };
            font['Z'] = new[] {
                new[] {0f,1f, 1f,1f}, new[] {1f,1f, 0f,0f}, new[] {0f,0f, 1f,0f}
            };

            // Digits
            font['0'] = font['O'];
            font['1'] = new[] {
                new[] {0.3f,0.8f, 0.5f,1f}, new[] {0.5f,1f, 0.5f,0f},
                new[] {0.2f,0f, 0.8f,0f}
            };
            font['2'] = new[] {
                new[] {0f,0.8f, 0.3f,1f}, new[] {0.3f,1f, 0.7f,1f}, new[] {0.7f,1f, 1f,0.8f},
                new[] {1f,0.8f, 1f,0.6f}, new[] {1f,0.6f, 0f,0f}, new[] {0f,0f, 1f,0f}
            };
            font['3'] = new[] {
                new[] {0f,0.8f, 0.3f,1f}, new[] {0.3f,1f, 0.7f,1f}, new[] {0.7f,1f, 1f,0.8f},
                new[] {1f,0.8f, 1f,0.6f}, new[] {1f,0.6f, 0.5f,0.5f},
                new[] {0.5f,0.5f, 1f,0.4f}, new[] {1f,0.4f, 1f,0.2f},
                new[] {1f,0.2f, 0.7f,0f}, new[] {0.7f,0f, 0.3f,0f}, new[] {0.3f,0f, 0f,0.2f}
            };
            font['4'] = new[] {
                new[] {0.7f,0f, 0.7f,1f}, new[] {0.7f,1f, 0f,0.4f}, new[] {0f,0.4f, 1f,0.4f}
            };
            font['5'] = new[] {
                new[] {1f,1f, 0f,1f}, new[] {0f,1f, 0f,0.5f}, new[] {0f,0.5f, 0.8f,0.5f},
                new[] {0.8f,0.5f, 1f,0.4f}, new[] {1f,0.4f, 1f,0.2f},
                new[] {1f,0.2f, 0.7f,0f}, new[] {0.7f,0f, 0f,0f}
            };
            font['6'] = new[] {
                new[] {0.8f,1f, 0.3f,1f}, new[] {0.3f,1f, 0f,0.8f}, new[] {0f,0.8f, 0f,0.2f},
                new[] {0f,0.2f, 0.3f,0f}, new[] {0.3f,0f, 0.7f,0f}, new[] {0.7f,0f, 1f,0.2f},
                new[] {1f,0.2f, 1f,0.4f}, new[] {1f,0.4f, 0.7f,0.5f}, new[] {0.7f,0.5f, 0f,0.5f}
            };
            font['7'] = new[] {
                new[] {0f,1f, 1f,1f}, new[] {1f,1f, 0.3f,0f}
            };
            font['8'] = new[] {
                new[] {0.3f,0.5f, 0f,0.6f}, new[] {0f,0.6f, 0f,0.8f}, new[] {0f,0.8f, 0.3f,1f},
                new[] {0.3f,1f, 0.7f,1f}, new[] {0.7f,1f, 1f,0.8f}, new[] {1f,0.8f, 1f,0.6f},
                new[] {1f,0.6f, 0.7f,0.5f}, new[] {0.7f,0.5f, 1f,0.4f}, new[] {1f,0.4f, 1f,0.2f},
                new[] {1f,0.2f, 0.7f,0f}, new[] {0.7f,0f, 0.3f,0f}, new[] {0.3f,0f, 0f,0.2f},
                new[] {0f,0.2f, 0f,0.4f}, new[] {0f,0.4f, 0.3f,0.5f}
            };
            font['9'] = new[] {
                new[] {1f,0.5f, 0.3f,0.5f}, new[] {0.3f,0.5f, 0f,0.6f},
                new[] {0f,0.6f, 0f,0.8f}, new[] {0f,0.8f, 0.3f,1f}, new[] {0.3f,1f, 0.7f,1f},
                new[] {0.7f,1f, 1f,0.8f}, new[] {1f,0.8f, 1f,0.2f},
                new[] {1f,0.2f, 0.7f,0f}, new[] {0.7f,0f, 0.2f,0f}
            };

            return font;
        }
    }
}
