using Godot;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class AppSettings : Resource
	{
		// Performance
		[Export] public int FpsCap = 165;
		[Export] public bool VSync = false;
		[Export] public int ArtNetSendRate = 44;

		// Safety
		[Export] public bool BlackoutOnLaunch = true;

		private static readonly string SavePath = "user://settings.tres";

		public static AppSettings LoadOrCreate()
		{
			if (ResourceLoader.Exists(SavePath))
			{
				var loaded = ResourceLoader.Load<AppSettings>(SavePath);
				if (loaded != null)
				{
					GD.Print("[AppSettings] Loaded from disk.");
					return loaded;
				}
			}

			GD.Print("[AppSettings] Creating defaults.");
			var settings = new AppSettings();
			settings.Save();
			return settings;
		}

		public void Save()
		{
			ResourceSaver.Save(this, SavePath);
		}

		public void ResetToDefaults()
		{
			FpsCap = 165;
			VSync = false;
			ArtNetSendRate = 44;
			BlackoutOnLaunch = true;
		}
	}
}
