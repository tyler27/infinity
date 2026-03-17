using Godot;
using Godot.Collections;

namespace LazerSystem.Core
{
	public partial class LaserSystemManager : Node
	{
		public static LaserSystemManager Instance { get; private set; }

		[Export] private Array<ProjectorConfig> projectors = new Array<ProjectorConfig>();

		[Export] private Array<ProjectionZone> zones = new Array<ProjectionZone>();

		[Export] private LaserShow currentShow;

		private bool _isPlaying;
		private float _currentTime;

		public Array<ProjectorConfig> Projectors => projectors;
		public Array<ProjectionZone> Zones => zones;

		public LaserShow CurrentShow
		{
			get => currentShow;
			set => currentShow = value;
		}

		public bool IsPlaying => _isPlaying;
		public float CurrentTime => _currentTime;

		public override void _Ready()
		{
			if (Instance != null && Instance != this)
			{
				GD.PushWarning("[LaserSystemManager] Duplicate instance destroyed.");
				QueueFree();
				return;
			}
			Instance = this;
			ValidateSetup();
		}

		public override void _ExitTree()
		{
			if (Instance == this)
			{
				Instance = null;
			}
		}

		/// <summary>Validates that the manager has a reasonable configuration.</summary>
		private void ValidateSetup()
		{
			if (projectors == null || projectors.Count == 0)
			{
				GD.PushWarning("[LaserSystemManager] No projectors configured.");
			}
			else
			{
				for (int i = 0; i < projectors.Count; i++)
				{
					if (projectors[i] == null)
					{
						GD.PushWarning($"[LaserSystemManager] Projector slot {i} is empty.");
					}
				}
			}

			if (zones == null || zones.Count == 0)
			{
				GD.PushWarning("[LaserSystemManager] No projection zones configured.");
			}
			else
			{
				for (int i = 0; i < zones.Count; i++)
				{
					if (zones[i] == null)
					{
						GD.PushWarning($"[LaserSystemManager] Zone slot {i} is empty.");
					}
				}
			}

			if (currentShow == null)
			{
				GD.Print("[LaserSystemManager] No show loaded.");
			}
		}
	}
}
