using System;
using LazerSystem.Patterns;

namespace LazerSystem.Core
{
	public static class AutomatableParameter
	{
		public struct ParameterDef
		{
			public string Name;
			public float Min, Max, Default;
			public Func<LaserCue, float> GetFromCue;
			public Action<PatternParameters, float> ApplyToParams;
		}

		public static readonly ParameterDef[] All = new ParameterDef[]
		{
			new ParameterDef
			{
				Name = "Intensity", Min = 0f, Max = 1f, Default = 1f,
				GetFromCue = c => c.Intensity,
				ApplyToParams = (p, v) => p.intensity = v
			},
			new ParameterDef
			{
				Name = "Size", Min = 0f, Max = 1f, Default = 0.5f,
				GetFromCue = c => c.Size,
				ApplyToParams = (p, v) => p.size = v
			},
			new ParameterDef
			{
				Name = "Rotation", Min = -360f, Max = 360f, Default = 0f,
				GetFromCue = c => c.Rotation,
				ApplyToParams = (p, v) => p.rotation = v
			},
			new ParameterDef
			{
				Name = "Speed", Min = 0f, Max = 10f, Default = 1f,
				GetFromCue = c => c.Speed,
				ApplyToParams = (p, v) => p.speed = v
			},
			new ParameterDef
			{
				Name = "Spread", Min = 0f, Max = 1f, Default = 0f,
				GetFromCue = c => c.Spread,
				ApplyToParams = (p, v) => p.spread = v
			},
			new ParameterDef
			{
				Name = "Frequency", Min = 0f, Max = 20f, Default = 1f,
				GetFromCue = c => c.Frequency,
				ApplyToParams = (p, v) => p.frequency = v
			},
			new ParameterDef
			{
				Name = "Amplitude", Min = 0f, Max = 1f, Default = 0.5f,
				GetFromCue = c => c.Amplitude,
				ApplyToParams = (p, v) => p.amplitude = v
			},
			new ParameterDef
			{
				Name = "PositionX", Min = -1f, Max = 1f, Default = 0f,
				GetFromCue = c => c.Position.X,
				ApplyToParams = (p, v) => p.position = new Godot.Vector2(v, p.position.Y)
			},
			new ParameterDef
			{
				Name = "PositionY", Min = -1f, Max = 1f, Default = 0f,
				GetFromCue = c => c.Position.Y,
				ApplyToParams = (p, v) => p.position = new Godot.Vector2(p.position.X, v)
			},
			new ParameterDef
			{
				Name = "ColorR", Min = 0f, Max = 1f, Default = 1f,
				GetFromCue = c => c.Color.R,
				ApplyToParams = (p, v) => p.color = new Godot.Color(v, p.color.G, p.color.B, p.color.A)
			},
			new ParameterDef
			{
				Name = "ColorG", Min = 0f, Max = 1f, Default = 1f,
				GetFromCue = c => c.Color.G,
				ApplyToParams = (p, v) => p.color = new Godot.Color(p.color.R, v, p.color.B, p.color.A)
			},
			new ParameterDef
			{
				Name = "ColorB", Min = 0f, Max = 1f, Default = 1f,
				GetFromCue = c => c.Color.B,
				ApplyToParams = (p, v) => p.color = new Godot.Color(p.color.R, p.color.G, v, p.color.A)
			},
		};

		public static ParameterDef? Find(string name)
		{
			for (int i = 0; i < All.Length; i++)
			{
				if (All[i].Name == name)
					return All[i];
			}
			return null;
		}
	}
}
