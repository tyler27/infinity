using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
	/// <summary>
	/// Cone pattern: beams arranged in a full 360-degree circle, like a fan but covering all directions.
	/// Animated rotation over time.
	/// </summary>
	public class ConePattern : ILaserPattern
	{
		public string PatternName => "Cone";

		private const int PointsPerBeam = 12;

		public List<LaserPoint> Generate(float time, PatternParameters parameters)
		{
			var points = new List<LaserPoint>();
			Color c = parameters.EffectiveColor();
			int beamCount = Mathf.Max(3, parameters.count);
			float rotationOffset = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
			float length = parameters.size;
			float cx = parameters.position.X;
			float cy = parameters.position.Y;

			for (int i = 0; i < beamCount; i++)
			{
				float angle = rotationOffset + (float)i / beamCount * Mathf.Pi * 2f;
				float dx = Mathf.Cos(angle);
				float dy = Mathf.Sin(angle);

				// Blank move to center
				points.Add(LaserPoint.Blanked(cx, cy));

				// Draw beam outward
				for (int p = 0; p < PointsPerBeam; p++)
				{
					float frac = (float)p / (PointsPerBeam - 1);
					float px = cx + dx * length * frac;
					float py = cy + dy * length * frac;
					points.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
				}
			}

			return points;
		}
	}
}
