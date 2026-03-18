using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;
using LazerSystem.Preview;
using LazerSystem.ArtNet;
using LazerSystem.Timeline;

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

	// Pre-allocated buffers to eliminate per-frame allocations
	private PatternParameters _cueParams;
	private PatternParameters _finalParams;
	private List<LaserPoint> _rawPoints;
	private byte[][] _dmxFrameBuffers;

	// DMX accumulator per projector (computed inline during point generation)
	private float[] _dmxAvgR, _dmxAvgG, _dmxAvgB, _dmxAvgX, _dmxAvgY;
	private float[] _dmxMinX, _dmxMaxX, _dmxMinY, _dmxMaxY;
	private int[] _dmxVisibleCount;

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
			_projectorPoints[i] = new List<LaserPoint>(512);

		// Pre-allocate reusable buffers
		_cueParams = new PatternParameters();
		_finalParams = new PatternParameters();
		_rawPoints = new List<LaserPoint>(256);
		_dmxFrameBuffers = new byte[ProjectorCount][];
		for (int i = 0; i < ProjectorCount; i++)
			_dmxFrameBuffers[i] = new byte[512];

		// DMX accumulators
		_dmxAvgR = new float[ProjectorCount];
		_dmxAvgG = new float[ProjectorCount];
		_dmxAvgB = new float[ProjectorCount];
		_dmxAvgX = new float[ProjectorCount];
		_dmxAvgY = new float[ProjectorCount];
		_dmxMinX = new float[ProjectorCount];
		_dmxMaxX = new float[ProjectorCount];
		_dmxMinY = new float[ProjectorCount];
		_dmxMaxY = new float[ProjectorCount];
		_dmxVisibleCount = new int[ProjectorCount];

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
			_dmxAvgR[i] = 0f; _dmxAvgG[i] = 0f; _dmxAvgB[i] = 0f;
			_dmxAvgX[i] = 0f; _dmxAvgY[i] = 0f;
			_dmxMinX[i] = float.MaxValue; _dmxMaxX[i] = float.MinValue;
			_dmxMinY[i] = float.MaxValue; _dmxMaxY[i] = float.MinValue;
			_dmxVisibleCount[i] = 0;
		}

		var zoneManager = LazerSystem.Zones.ZoneManager.Instance;

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

				// Build pattern parameters from cue (no allocation)
				_cueParams.CopyFromCue(cue);

				// Apply overrides (no allocation)
				ApplyOverrides(_cueParams, _finalParams);

				// Get pattern generator
				ILaserPattern pattern = PatternFactory.Create(cue.PatternType);
				if (pattern == null)
					continue;

				// Generate points into reusable list
				_rawPoints.Clear();
				pattern.Generate(time, _finalParams, _rawPoints);
				if (_rawPoints.Count == 0)
					continue;

				// Detect clipping
				if (!_overflowDetected)
				{
					float extentX = _finalParams.size + Mathf.Abs(_finalParams.position.X) + _finalParams.amplitude;
					float extentY = _finalParams.size + Mathf.Abs(_finalParams.position.Y) + _finalParams.amplitude;
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

					_projectorHasOutput[zone] = true;

					// Record start index before adding points
					int startIdx = _projectorPoints[zone].Count;

					// Add raw points directly to the projector list
					_projectorPoints[zone].AddRange(_rawPoints);

					// Transform in-place if zone manager available
					if (zoneManager != null)
					{
						zoneManager.TransformPointsInPlace(zone, _projectorPoints[zone], startIdx);
					}

					// Accumulate DMX stats for these points
					AccumulateDmxStats(zone, _projectorPoints[zone], startIdx);
				}
			}
		}

		// Render and send DMX for each projector
		// When PlaybackManager is playing, skip clearing renderers so timeline output persists
		bool timelinePlaying = PlaybackManager.Instance != null && PlaybackManager.Instance.IsPlaying && !PlaybackManager.Instance.IsPaused;

		for (int i = 0; i < ProjectorCount; i++)
		{
			if (_projectorHasOutput[i] && _projectorPoints[i].Count > 0)
			{
				if (_renderers[i] != null)
					_renderers[i].RenderFrame(_projectorPoints[i]);

				SendProjectorDmx(i);
			}
			else if (!timelinePlaying)
			{
				if (_renderers[i] != null)
					_renderers[i].Clear();

				SendProjectorBlackout(i);
			}
		}

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

	private void ApplyOverrides(PatternParameters cueParams, PatternParameters result)
	{
		// Master controls always apply
		float finalSize = cueParams.size * MasterSize;
		float finalIntensity = cueParams.intensity * MasterIntensity;

		// Live overrides always apply (they default to neutral values: speed=1, rotation=0, position=0,0)
		float finalRotation = cueParams.rotation + LiveOverrides.rotation;
		Vector2 finalPosition = cueParams.position + LiveOverrides.position;
		float finalSpeed = cueParams.speed * LiveOverrides.speed;

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

		// Write into pre-allocated result
		result.color = finalColor;
		result.intensity = finalIntensity;
		result.size = finalSize;
		result.rotation = finalRotation;
		result.speed = finalSpeed;
		result.spread = cueParams.spread;
		result.count = cueParams.count;
		result.frequency = cueParams.frequency;
		result.amplitude = cueParams.amplitude;
		result.position = finalPosition;
	}

	// --------------- DMX Stats Accumulation ---------------

	/// <summary>
	/// Accumulates color/position stats for DMX output while points are being added,
	/// avoiding a separate O(n) pass later.
	/// </summary>
	private void AccumulateDmxStats(int projector, List<LaserPoint> points, int startIndex)
	{
		for (int i = startIndex; i < points.Count; i++)
		{
			LaserPoint pt = points[i];
			if (pt.blanking) continue;

			_dmxAvgR[projector] += pt.r;
			_dmxAvgG[projector] += pt.g;
			_dmxAvgB[projector] += pt.b;
			_dmxAvgX[projector] += pt.x;
			_dmxAvgY[projector] += pt.y;
			_dmxVisibleCount[projector]++;

			if (pt.x < _dmxMinX[projector]) _dmxMinX[projector] = pt.x;
			if (pt.x > _dmxMaxX[projector]) _dmxMaxX[projector] = pt.x;
			if (pt.y < _dmxMinY[projector]) _dmxMinY[projector] = pt.y;
			if (pt.y > _dmxMaxY[projector]) _dmxMaxY[projector] = pt.y;
		}
	}

	// --------------- DMX Output ---------------

	private void SendProjectorDmx(int projectorIndex)
	{
		if (ArtNetManager.Instance == null)
			return;

		int visibleCount = _dmxVisibleCount[projectorIndex];
		float avgR = 0f, avgG = 0f, avgB = 0f, avgX = 0f, avgY = 0f;

		if (visibleCount > 0)
		{
			float inv = 1f / visibleCount;
			avgR = _dmxAvgR[projectorIndex] * inv;
			avgG = _dmxAvgG[projectorIndex] * inv;
			avgB = _dmxAvgB[projectorIndex] * inv;
			avgX = _dmxAvgX[projectorIndex] * inv;
			avgY = _dmxAvgY[projectorIndex] * inv;
		}

		Color dmxColor = new Color(
			Mathf.Clamp(avgR, 0f, 1f),
			Mathf.Clamp(avgG, 0f, 1f),
			Mathf.Clamp(avgB, 0f, 1f)
		);

		float sizeX = visibleCount > 0 ? Mathf.Clamp((_dmxMaxX[projectorIndex] - _dmxMinX[projectorIndex]) * 0.5f, 0f, 1f) : 0f;
		float sizeY = visibleCount > 0 ? Mathf.Clamp((_dmxMaxY[projectorIndex] - _dmxMinY[projectorIndex]) * 0.5f, 0f, 1f) : 0f;

		// Fill into pre-allocated buffer (no allocation)
		FB4ChannelMap.FillDmxFrame(
			_dmxFrameBuffers[projectorIndex],
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

		ArtNetManager.Instance.UpdateDmxFrame(projectorIndex, _dmxFrameBuffers[projectorIndex]);
	}

	private void SendProjectorBlackout(int projectorIndex)
	{
		if (ArtNetManager.Instance == null)
			return;

		// Fill into pre-allocated buffer (no allocation)
		FB4ChannelMap.FillDmxFrame(
			_dmxFrameBuffers[projectorIndex],
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

		ArtNetManager.Instance.UpdateDmxFrame(projectorIndex, _dmxFrameBuffers[projectorIndex]);
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
