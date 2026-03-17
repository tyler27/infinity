using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;
using LazerSystem.Preview;
using LazerSystem.ArtNet;

/// <summary>
/// Core live performance engine. Manages active cues, generates patterns,
/// drives 3D preview renderers, and sends DMX data to ArtNet for FB4 projectors.
/// </summary>
public partial class LiveEngine : Node
{
	// --------------- Singleton ---------------
	private static LiveEngine _instance;
	public static LiveEngine Instance => _instance;

	// --------------- Constants ---------------
	private const int RowCount = 6;
	private const int ColCount = 10;
	public const int PageCount = 256;
	private const int ProjectorCount = 4;

	// --------------- Master Controls ---------------

	/// <summary>Master output enable (safety). When false, all output is black.</summary>
	public bool LaserEnabled { get; set; } = true;

	/// <summary>Temporary kill switch. When true, output is black but LaserEnabled stays true.</summary>
	public bool Blackout { get; set; }

	/// <summary>Per-projector enable flags.</summary>
	public bool[] ProjectorEnabled { get; private set; }

	/// <summary>Master intensity multiplier applied to all cue output.</summary>
	public float MasterIntensity { get; set; } = 1f;

	/// <summary>Master size multiplier applied to all cue output.</summary>
	public float MasterSize { get; set; } = 1f;

	// --------------- Cue Grid State ---------------

	/// <summary>2D array tracking which cue index is active per grid cell. -1 = inactive.</summary>
	public int[,] ActiveCueGrid { get; private set; }

	/// <summary>Current cue page (0-7).</summary>
	public int CurrentPage { get; set; }

	/// <summary>3D array [page, row, col] holding cue definitions.</summary>
	public LaserCue[,,] CuePages { get; private set; }

	/// <summary>Page names. Index = page number.</summary>
	public string[] PageNames { get; private set; }

	/// <summary>Favorited page indices for quick access bar.</summary>
	public System.Collections.Generic.List<int> FavoritePages { get; private set; } = new System.Collections.Generic.List<int>();

	/// <summary>Returns true if a page has any cues defined.</summary>
	public bool PageHasCues(int page)
	{
		if (page < 0 || page >= PageCount) return false;
		for (int r = 0; r < RowCount; r++)
			for (int c = 0; c < ColCount; c++)
				if (CuePages[page, r, c] != null) return true;
		return false;
	}

	/// <summary>Gets the display name for a page.</summary>
	public string GetPageDisplayName(int page)
	{
		if (page < 0 || page >= PageCount) return "?";
		string name = PageNames[page];
		return string.IsNullOrEmpty(name) ? $"Page {page + 1}" : name;
	}

	// --------------- Live Overrides ---------------

	/// <summary>Live control overrides from sliders.</summary>
	public PatternParameters LiveOverrides { get; set; }

	/// <summary>Whether to apply live overrides on top of cue parameters.</summary>
	public bool UseOverrides { get; set; }

	// --------------- Zone Assignment ---------------

	/// <summary>Which projectors to send active cues to. Default: all four.</summary>
	public int[] ActiveZones { get; set; } = { 0, 1, 2, 3 };

	// --------------- Private State ---------------

	private LaserPreviewRenderer[] _renderers;
	private double _elapsed;

	// Track which projectors received points this frame so we can clear idle ones
	private bool[] _projectorHasOutput;
	private bool _overflowDetected;

	// Accumulate points per projector per frame so multiple cues can layer
	private List<LaserPoint>[] _projectorPoints;

	// --------------- Godot Lifecycle ---------------

	public override void _Ready()
	{
		if (_instance != null && _instance != this)
		{
			GD.PushWarning("[LiveEngine] Duplicate instance detected, removing this one.");
			QueueFree();
			return;
		}
		_instance = this;

		// Initialize state arrays
		ProjectorEnabled = new bool[ProjectorCount];
		for (int i = 0; i < ProjectorCount; i++)
			ProjectorEnabled[i] = true;

		ActiveCueGrid = new int[RowCount, ColCount];
		ClearActiveGrid();

		CuePages = new LaserCue[PageCount, RowCount, ColCount];

		PageNames = new string[PageCount];
		FavoritePages = new System.Collections.Generic.List<int> { 0, 1, 2, 3 };

		LiveOverrides = new PatternParameters
		{
			color = Colors.White,
			intensity = 1f,
			size = 1f,
			rotation = 0f,
			speed = 1f,
			spread = 0f,
			count = 1,
			frequency = 1f,
			amplitude = 0.5f,
			position = Vector2.Zero
		};

		_projectorHasOutput = new bool[ProjectorCount];
		_projectorPoints = new List<LaserPoint>[ProjectorCount];
		for (int i = 0; i < ProjectorCount; i++)
			_projectorPoints[i] = new List<LaserPoint>();

		// Find renderers in the scene tree
		_renderers = new LaserPreviewRenderer[ProjectorCount];
		var preview3D = GetNode<Node3D>("../Preview3D");
		_renderers[0] = preview3D.GetNode<LaserPreviewRenderer>("Projector1");
		_renderers[1] = preview3D.GetNode<LaserPreviewRenderer>("Projector2");
		_renderers[2] = preview3D.GetNode<LaserPreviewRenderer>("Projector3");
		_renderers[3] = preview3D.GetNode<LaserPreviewRenderer>("Projector4");

		// Populate default cue library
		PresetCues.PopulateDefaults(this);

		GD.Print("[LiveEngine] Initialized. 4 projectors, 8 pages, 6x10 cue grid.");
	}

	public override void _ExitTree()
	{
		if (_instance == this)
			_instance = null;
	}

	public override void _Process(double delta)
	{
		_elapsed += delta;
		float time = (float)_elapsed;

		// Safety / blackout check
		if (!LaserEnabled || Blackout)
		{
			for (int i = 0; i < ProjectorCount; i++)
			{
				if (_renderers[i] != null)
					_renderers[i].Clear();
			}
			SendBlackoutDmx();
			return;
		}

		// Reset per-frame accumulation
		_overflowDetected = false;
		for (int i = 0; i < ProjectorCount; i++)
		{
			_projectorHasOutput[i] = false;
			_projectorPoints[i].Clear();
		}

		// Iterate over every cell in the active grid
		for (int row = 0; row < RowCount; row++)
		{
			for (int col = 0; col < ColCount; col++)
			{
				if (ActiveCueGrid[row, col] < 0)
					continue;

				LaserCue cue = CuePages[CurrentPage, row, col];
				if (cue == null)
					continue;

				// Build pattern parameters from cue
				PatternParameters cueParams = PatternParameters.FromCue(cue);

				// Apply overrides
				PatternParameters finalParams = ApplyOverrides(cueParams);

				// Get pattern generator
				ILaserPattern pattern = PatternFactory.Create(cue.PatternType);
				if (pattern == null)
					continue;

				// Generate points (unclamped so we can detect overflow)
				List<LaserPoint> rawPoints = pattern.Generate(time, finalParams);
				if (rawPoints == null || rawPoints.Count == 0)
					continue;

				// Detect clipping: check if the effective pattern parameters
				// would produce output that exceeds the -1..1 zone boundary.
				// This is simpler and more reliable than checking post-clamp points.
				if (!_overflowDetected)
				{
					float extentX = finalParams.size + Mathf.Abs(finalParams.position.X) + finalParams.amplitude;
					float extentY = finalParams.size + Mathf.Abs(finalParams.position.Y) + finalParams.amplitude;
					if (extentX > 1.0f || extentY > 1.0f)
						_overflowDetected = true;
				}

				// Distribute to target projectors
				foreach (int zone in ActiveZones)
				{
					if (zone < 0 || zone >= ProjectorCount)
						continue;
					if (!ProjectorEnabled[zone])
						continue;

					// Apply zone geometric transforms if available
					List<LaserPoint> zonePoints = rawPoints;
					var zoneManager = LazerSystem.Zones.ZoneManager.Instance;
					if (zoneManager != null)
					{
						zonePoints = zoneManager.TransformPoints(zone, rawPoints);
					}

					_projectorHasOutput[zone] = true;
					_projectorPoints[zone].AddRange(zonePoints);
				}
			}
		}

		// Append zone boundary points when enabled (treated as real laser output)
		for (int i = 0; i < ProjectorCount; i++)
		{
			if (_renderers[i] != null && _renderers[i].ShowZoneBoundary)
			{
				// Find the zone's keystone corners for this projector
				Vector2[] corners = null;
				var zoneManager = LazerSystem.Zones.ZoneManager.Instance;
				if (zoneManager != null)
				{
					var zoneIndices = zoneManager.GetZonesForProjector(i);
					if (zoneIndices.Count > 0 && zoneManager.Zones[zoneIndices[0]] != null)
					{
						corners = zoneManager.Zones[zoneIndices[0]].KeystoneCorners;
					}
				}

				var boundaryPts = _renderers[i].GenerateZoneBoundaryPoints(corners);
				if (boundaryPts != null && boundaryPts.Count > 0)
				{
					_projectorHasOutput[i] = true;
					_projectorPoints[i].AddRange(boundaryPts);
				}
			}
		}

		// Render and send DMX for each projector
		for (int i = 0; i < ProjectorCount; i++)
		{
			if (_projectorHasOutput[i] && _projectorPoints[i].Count > 0)
			{
				if (_renderers[i] != null)
					_renderers[i].RenderFrame(_projectorPoints[i]);

				SendProjectorDmx(i, _projectorPoints[i]);
			}
			else
			{
				if (_renderers[i] != null)
					_renderers[i].Clear();

				SendProjectorBlackout(i);
			}
		}

		// Zone boundary overflow is now handled per-projector via LaserPreviewRenderer

		// Feed 2D output view (if MainUI has one)
		var mainUI = GetNodeOrNull<MainUI>("../UI");
		var outputView = mainUI?.OutputView;
		if (outputView != null)
		{
			outputView.SetOverflow(_overflowDetected);
			for (int i = 0; i < ProjectorCount; i++)
			{
				outputView.SetProjectorPoints(i, _projectorPoints[i], _projectorHasOutput[i]);
			}
		}
	}

	// --------------- Public API ---------------

	/// <summary>
	/// Trigger a cue at the given grid position.
	/// Mode 0 = toggle (on/off), Mode 1 = flash-on (held, release to stop).
	/// </summary>
	public void TriggerCue(int row, int col, int mode = 0)
	{
		if (row < 0 || row >= RowCount || col < 0 || col >= ColCount)
			return;

		if (CuePages[CurrentPage, row, col] == null)
			return;

		if (mode == 0)
		{
			// Toggle
			if (ActiveCueGrid[row, col] >= 0)
				ActiveCueGrid[row, col] = -1;
			else
				ActiveCueGrid[row, col] = 0;
		}
		else if (mode == 1)
		{
			// Flash on
			ActiveCueGrid[row, col] = 1;
		}
	}

	/// <summary>
	/// Release a flash cue (mode 1 trigger). Only releases if cue was flash-activated.
	/// </summary>
	public void ReleaseCue(int row, int col)
	{
		if (row < 0 || row >= RowCount || col < 0 || col >= ColCount)
			return;

		// Only release flash cues (value == 1), not toggle cues (value == 0)
		if (ActiveCueGrid[row, col] == 1)
			ActiveCueGrid[row, col] = -1;
	}

	/// <summary>
	/// Clear all active cues.
	/// </summary>
	public void ClearAll()
	{
		ClearActiveGrid();
	}

	/// <summary>
	/// Set a cue definition in the grid.
	/// </summary>
	public void SetCue(int page, int row, int col, LaserCue cue)
	{
		if (page < 0 || page >= PageCount ||
			row < 0 || row >= RowCount ||
			col < 0 || col >= ColCount)
			return;

		CuePages[page, row, col] = cue;
	}

	/// <summary>
	/// Get a cue from the grid.
	/// </summary>
	public LaserCue GetCue(int page, int row, int col)
	{
		if (page < 0 || page >= PageCount ||
			row < 0 || row >= RowCount ||
			col < 0 || col >= ColCount)
			return null;

		return CuePages[page, row, col];
	}

	/// <summary>
	/// Get whether a cell is active.
	/// </summary>
	public bool IsCueActive(int row, int col)
	{
		if (row < 0 || row >= RowCount || col < 0 || col >= ColCount)
			return false;

		return ActiveCueGrid[row, col] >= 0;
	}

	/// <summary>
	/// Toggle the blackout state.
	/// </summary>
	public void ToggleBlackout()
	{
		Blackout = !Blackout;
		GD.Print($"[LiveEngine] Blackout: {Blackout}");
	}

	/// <summary>
	/// Toggle the master laser enable.
	/// </summary>
	public void ToggleLaserEnable()
	{
		LaserEnabled = !LaserEnabled;
		GD.Print($"[LiveEngine] LaserEnabled: {LaserEnabled}");
	}

	// --------------- Override Blending ---------------

	private PatternParameters ApplyOverrides(PatternParameters cueParams)
	{
		// Master controls always apply
		float finalSize = cueParams.size * MasterSize;
		float finalIntensity = cueParams.intensity * MasterIntensity;

		// Live overrides always apply (they default to neutral values: speed=1, rotation=0, position=0,0)
		float finalRotation = cueParams.rotation + LiveOverrides.rotation;
		Vector2 finalPosition = cueParams.position + LiveOverrides.position;
		float finalSpeed = cueParams.speed * LiveOverrides.speed;
		float finalSpread = cueParams.spread;
		int finalCount = cueParams.count;
		float finalFrequency = cueParams.frequency;
		float finalAmplitude = cueParams.amplitude;

		// Color override: if not white, replace cue color
		Color finalColor = cueParams.color;
		if (LiveOverrides.color != Colors.White)
			finalColor = LiveOverrides.color;

		// Size/intensity from live overrides multiply on top of master
		finalSize *= LiveOverrides.size;
		finalIntensity *= LiveOverrides.intensity;

		// Clamp values to valid ranges
		finalIntensity = Mathf.Clamp(finalIntensity, 0f, 1f);
		finalSize = Mathf.Clamp(finalSize, 0f, 10f);

		return new PatternParameters
		{
			color = finalColor,
			intensity = finalIntensity,
			size = finalSize,
			rotation = finalRotation,
			speed = finalSpeed,
			spread = finalSpread,
			count = finalCount,
			frequency = finalFrequency,
			amplitude = finalAmplitude,
			position = finalPosition
		};
	}

	// --------------- DMX Output ---------------

	private void SendProjectorDmx(int projectorIndex, List<LaserPoint> points)
	{
		if (ArtNetManager.Instance == null)
			return;

		// Compute average color and position from all generated points for DMX mapping
		float avgR = 0f, avgG = 0f, avgB = 0f;
		float avgX = 0f, avgY = 0f;
		int visibleCount = 0;

		for (int i = 0; i < points.Count; i++)
		{
			LaserPoint pt = points[i];
			if (pt.blanking)
				continue;
			avgR += pt.r;
			avgG += pt.g;
			avgB += pt.b;
			avgX += pt.x;
			avgY += pt.y;
			visibleCount++;
		}

		if (visibleCount > 0)
		{
			float inv = 1f / visibleCount;
			avgR *= inv;
			avgG *= inv;
			avgB *= inv;
			avgX *= inv;
			avgY *= inv;
		}

		Color dmxColor = new Color(
			Mathf.Clamp(avgR, 0f, 1f),
			Mathf.Clamp(avgG, 0f, 1f),
			Mathf.Clamp(avgB, 0f, 1f)
		);

		// Compute bounding extent of visible points for size mapping
		float minX = float.MaxValue, maxX = float.MinValue;
		float minY = float.MaxValue, maxY = float.MinValue;
		for (int i = 0; i < points.Count; i++)
		{
			LaserPoint pt = points[i];
			if (pt.blanking) continue;
			if (pt.x < minX) minX = pt.x;
			if (pt.x > maxX) maxX = pt.x;
			if (pt.y < minY) minY = pt.y;
			if (pt.y > maxY) maxY = pt.y;
		}

		float sizeX = visibleCount > 0 ? Mathf.Clamp((maxX - minX) * 0.5f, 0f, 1f) : 0f;
		float sizeY = visibleCount > 0 ? Mathf.Clamp((maxY - minY) * 0.5f, 0f, 1f) : 0f;

		byte[] frame = FB4ChannelMap.BuildDmxFrame(
			enabled: true,
			pattern: 0,
			x: Mathf.Clamp(avgX, -1f, 1f),
			y: Mathf.Clamp(avgY, -1f, 1f),
			sizeX: sizeX,
			sizeY: sizeY,
			rotation: 0f,
			color: dmxColor,
			scanSpeed: 0.5f,
			effect: 0,
			effectSpeed: 0f,
			effectSize: 0f,
			zoom: 1f
		);

		ArtNetManager.Instance.UpdateDmxFrame(projectorIndex, frame);
	}

	private void SendProjectorBlackout(int projectorIndex)
	{
		if (ArtNetManager.Instance == null)
			return;

		byte[] frame = FB4ChannelMap.BuildDmxFrame(
			enabled: false,
			pattern: 0,
			x: 0f,
			y: 0f,
			sizeX: 0f,
			sizeY: 0f,
			rotation: 0f,
			color: Colors.Black,
			scanSpeed: 0f,
			effect: 0,
			effectSpeed: 0f,
			effectSize: 0f,
			zoom: 0f
		);

		ArtNetManager.Instance.UpdateDmxFrame(projectorIndex, frame);
	}

	private void SendBlackoutDmx()
	{
		if (ArtNetManager.Instance == null)
			return;

		ArtNetManager.Instance.BlackoutAll();
	}

	// --------------- Helpers ---------------

	private void ClearActiveGrid()
	{
		for (int row = 0; row < RowCount; row++)
			for (int col = 0; col < ColCount; col++)
				ActiveCueGrid[row, col] = -1;
	}
}
