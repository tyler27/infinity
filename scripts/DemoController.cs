using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;
using LazerSystem.Preview;

/// <summary>
/// Demo controller with audio-reactive laser patterns.
/// Analyzes audio spectrum and drives pattern parameters from bass/mid/high energy.
/// Press 1-9 to switch patterns, Space to cycle.
/// </summary>
public partial class DemoController : Node
{
    [Export] public float CycleInterval = 8f;
    [Export] public float AudioReactivity = 3f;
    [Export] public float BeatThreshold = 0.6f;
    [Export] public float BeatDecay = 0.9f;

    private LaserPreviewRenderer[] _renderers;
    private Dictionary<LaserPatternType, ILaserPattern> _patterns;
    private LaserPatternType[] _patternOrder;
    private int _currentPatternIndex;
    private double _elapsed;
    private double _cycleTimer;

    private AudioStreamPlayer _audioPlayer;
    private AudioEffectSpectrumAnalyzerInstance _spectrumAnalyzer;

    // Audio analysis
    private float _bass;
    private float _mid;
    private float _high;
    private float _energy;
    private float _beatPulse;
    private float _lastBass;
    private bool _beatHit;

    public override void _Ready()
    {
        // Find renderers
        _renderers = new LaserPreviewRenderer[4];
        var preview3D = GetNode<Node3D>("../Preview3D");
        _renderers[0] = preview3D.GetNode<LaserPreviewRenderer>("Projector1");
        _renderers[1] = preview3D.GetNode<LaserPreviewRenderer>("Projector2");
        _renderers[2] = preview3D.GetNode<LaserPreviewRenderer>("Projector3");
        _renderers[3] = preview3D.GetNode<LaserPreviewRenderer>("Projector4");

        // Build patterns
        _patterns = new Dictionary<LaserPatternType, ILaserPattern>();
        _patternOrder = new LaserPatternType[]
        {
            LaserPatternType.Fan,
            LaserPatternType.Circle,
            LaserPatternType.Cone,
            LaserPatternType.Wave,
            LaserPatternType.Tunnel,
            LaserPatternType.Star,
            LaserPatternType.Line,
            LaserPatternType.Beam,
            LaserPatternType.Triangle,
            LaserPatternType.Square,
        };

        foreach (var pt in _patternOrder)
            _patterns[pt] = PatternFactory.Create(pt);

        _currentPatternIndex = 0;

        // Setup audio with spectrum analyzer
        SetupAudio();

        GD.Print($"[Demo] Audio-reactive mode. Pattern: {_patternOrder[_currentPatternIndex]}");
        GD.Print("[Demo] Keys: 1-9 = patterns, Space = next, +/- = reactivity");
    }

    private void SetupAudio()
    {
        // Add a spectrum analyzer to the audio bus
        int busIdx = AudioServer.GetBusIndex("Master");

        // Check if analyzer already exists
        bool hasAnalyzer = false;
        for (int i = 0; i < AudioServer.GetBusEffectCount(busIdx); i++)
        {
            if (AudioServer.GetBusEffect(busIdx, i) is AudioEffectSpectrumAnalyzer)
            {
                hasAnalyzer = true;
                _spectrumAnalyzer = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(busIdx, i);
                break;
            }
        }

        if (!hasAnalyzer)
        {
            var analyzer = new AudioEffectSpectrumAnalyzer();
            analyzer.BufferLength = 0.1f;
            analyzer.FftSize = AudioEffectSpectrumAnalyzer.FftSizeEnum.Size2048;
            AudioServer.AddBusEffect(busIdx, analyzer);
            int effectIdx = AudioServer.GetBusEffectCount(busIdx) - 1;
            _spectrumAnalyzer = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(busIdx, effectIdx);
        }

        // Find or create audio player and load the track
        _audioPlayer = GetNode<AudioStreamPlayer>("../AudioPlayer");
        var stream = GD.Load<AudioStream>("res://audio/swinggg.wav");
        if (stream != null)
        {
            _audioPlayer.Stream = stream;
            _audioPlayer.Play();
            GD.Print("[Demo] Playing: swinggg.wav");
        }
        else
        {
            GD.PushWarning("[Demo] Could not load res://audio/swinggg.wav");
        }
    }

    private void AnalyzeAudio()
    {
        if (_spectrumAnalyzer == null) return;

        // Get magnitude in frequency bands
        // Bass: 20-200Hz, Mid: 200-2000Hz, High: 2000-16000Hz
        _bass = GetBandEnergy(20f, 200f);
        _mid = GetBandEnergy(200f, 2000f);
        _high = GetBandEnergy(2000f, 16000f);
        _energy = (_bass + _mid + _high) / 3f;

        // Beat detection (onset in bass)
        _beatHit = _bass > BeatThreshold && _bass > _lastBass * 1.3f;
        if (_beatHit)
            _beatPulse = 1f;
        else
            _beatPulse *= BeatDecay;

        _lastBass = _bass;
    }

    private float GetBandEnergy(float freqLow, float freqHigh)
    {
        var mag = _spectrumAnalyzer.GetMagnitudeForFrequencyRange(freqLow, freqHigh);
        return (mag.X + mag.Y) * 0.5f; // average L+R
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _cycleTimer += delta;

        if (_cycleTimer >= CycleInterval)
        {
            _cycleTimer = 0;
            _currentPatternIndex = (_currentPatternIndex + 1) % _patternOrder.Length;
            GD.Print($"[Demo] Pattern: {_patternOrder[_currentPatternIndex]}");
        }

        AnalyzeAudio();

        float time = (float)_elapsed;
        float react = AudioReactivity;
        var patternType = _patternOrder[_currentPatternIndex];
        var pattern = _patterns[patternType];

        for (int i = 0; i < 4; i++)
        {
            if (_renderers[i] == null) continue;

            // Color shifts with audio - bass=red, mid=green, high=blue, beat=white flash
            float hue = (i * 0.25f + time * 0.05f + _mid * 0.3f) % 1f;
            float sat = Mathf.Clamp(1f - _beatPulse * 0.5f, 0.3f, 1f);
            float val = Mathf.Clamp(0.5f + _energy * react, 0.3f, 1f);
            Color color = Color.FromHsv(hue, sat, val);

            // Audio-reactive parameters
            var parameters = new PatternParameters
            {
                color = color,
                intensity = Mathf.Clamp(0.5f + _energy * react, 0f, 1f),
                size = Mathf.Clamp(0.3f + _bass * react * 0.5f + _beatPulse * 0.3f, 0.1f, 1f),
                rotation = time * (20f + _mid * 100f * react) + i * 90f,
                speed = 1f + _high * react * 2f,
                spread = 60f + _bass * 60f * react,
                count = 6 + (int)(_mid * 10f * react) + i * 2,
                frequency = 2f + _high * 4f * react + i * 0.5f,
                amplitude = 0.2f + _bass * 0.5f * react,
                position = new Vector2(
                    0.05f * Mathf.Sin(time * 0.7f + i * 1.5f) * (1f + _mid * react),
                    0.05f * Mathf.Cos(time * 0.5f + i * 1.2f) * (1f + _bass * react)
                )
            };

            List<LaserPoint> points = pattern.Generate(time, parameters);
            _renderers[i].RenderFrame(points);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            int index = -1;
            switch (keyEvent.Keycode)
            {
                case Key.Key1: index = 0; break;
                case Key.Key2: index = 1; break;
                case Key.Key3: index = 2; break;
                case Key.Key4: index = 3; break;
                case Key.Key5: index = 4; break;
                case Key.Key6: index = 5; break;
                case Key.Key7: index = 6; break;
                case Key.Key8: index = 7; break;
                case Key.Key9: index = 8; break;
                case Key.Space:
                    _cycleTimer = CycleInterval;
                    break;
                case Key.Equal: // +
                    AudioReactivity = Mathf.Min(AudioReactivity + 0.5f, 10f);
                    GD.Print($"[Demo] Reactivity: {AudioReactivity}");
                    break;
                case Key.Minus:
                    AudioReactivity = Mathf.Max(AudioReactivity - 0.5f, 0f);
                    GD.Print($"[Demo] Reactivity: {AudioReactivity}");
                    break;
            }

            if (index >= 0 && index < _patternOrder.Length)
            {
                _currentPatternIndex = index;
                _cycleTimer = 0;
                GD.Print($"[Demo] Pattern: {_patternOrder[_currentPatternIndex]}");
            }
        }
    }
}
