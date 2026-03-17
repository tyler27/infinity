using System;
using Godot;

namespace LazerSystem.Sync
{
    /// <summary>
    /// Singleton manager for show synchronization. Supports internal audio playback,
    /// MIDI Time Code, and Art-Net Time Code as sync sources.
    /// </summary>
    public partial class SyncManager : Node
    {
        private static SyncManager _instance;

        public static SyncManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GD.PushError("[SyncManager] No instance found in scene.");
                }
                return _instance;
            }
        }

        public enum SyncSource
        {
            Internal,
            MidiTimeCode,
            ArtNetTimeCode
        }

        [ExportGroup("Sync Source")]
        [Export] private SyncSource _currentSource = SyncSource.Internal;

        [ExportGroup("Internal Audio")]
        [Export] private AudioStreamPlayer _audioPlayer;

        [ExportGroup("External Receivers")]
        [Export] private MidiTimecodeReceiver _midiTimecodeReceiver;
        [Export] private ArtNetTimecodeReceiver _artNetTimecodeReceiver;

        private bool _isRunning;

        // Events
        public event Action OnPlay;
        public event Action OnStop;
        public event Action OnPause;
        public event Action<float> OnSeek;

        private bool _looping;

        public SyncSource CurrentSyncSource
        {
            get => _currentSource;
            set => _currentSource = value;
        }

        /// <summary>Whether the playback is set to loop.</summary>
        public bool Looping => _looping;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Current playback time in seconds, read from the active sync source.
        /// </summary>
        public float CurrentTime
        {
            get
            {
                switch (_currentSource)
                {
                    case SyncSource.Internal:
                        return _audioPlayer != null ? (float)_audioPlayer.GetPlaybackPosition() : 0f;

                    case SyncSource.MidiTimeCode:
                        return _midiTimecodeReceiver != null ? _midiTimecodeReceiver.CurrentTime : 0f;

                    case SyncSource.ArtNetTimeCode:
                        return _artNetTimecodeReceiver != null ? _artNetTimecodeReceiver.CurrentTime : 0f;

                    default:
                        return 0f;
                }
            }
        }

        public override void _Ready()
        {
            if (_instance != null && _instance != this)
            {
                GD.PushWarning("[SyncManager] Duplicate instance destroyed.");
                QueueFree();
                return;
            }
            _instance = this;
        }

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>Starts playback from the current position.</summary>
        public void Play()
        {
            _isRunning = true;

            if (_currentSource == SyncSource.Internal && _audioPlayer != null)
            {
                _audioPlayer.Play();
            }

            OnPlay?.Invoke();
            GD.Print($"[SyncManager] Play (source: {_currentSource})");
        }

        /// <summary>Pauses playback at the current position.</summary>
        public void Pause()
        {
            _isRunning = false;

            if (_currentSource == SyncSource.Internal && _audioPlayer != null)
            {
                _audioPlayer.StreamPaused = true;
            }

            OnPause?.Invoke();
            GD.Print("[SyncManager] Pause");
        }

        /// <summary>Stops playback and resets to the beginning.</summary>
        public void Stop()
        {
            _isRunning = false;

            if (_currentSource == SyncSource.Internal && _audioPlayer != null)
            {
                _audioPlayer.Stop();
            }

            OnStop?.Invoke();
            GD.Print("[SyncManager] Stop");
        }

        /// <summary>Seeks to a specific time in seconds.</summary>
        public void Seek(float time)
        {
            if (_currentSource == SyncSource.Internal && _audioPlayer != null)
            {
                float duration = _audioPlayer.Stream != null ? (float)_audioPlayer.Stream.GetLength() : 0f;
                _audioPlayer.Seek(Mathf.Clamp(time, 0f, duration));
            }

            OnSeek?.Invoke(time);
            GD.Print($"[SyncManager] Seek to {time:F2}s");
        }

        /// <summary>Total duration of the audio in seconds, or 0 if no audio is loaded.</summary>
        public float Duration
        {
            get
            {
                if (_currentSource == SyncSource.Internal && _audioPlayer != null && _audioPlayer.Stream != null)
                {
                    return (float)_audioPlayer.Stream.GetLength();
                }
                return 0f;
            }
        }

        /// <summary>Sets the sync source.</summary>
        public void SetSyncSource(SyncSource source)
        {
            _currentSource = source;
        }

        /// <summary>Enables or disables looping.</summary>
        public void SetLooping(bool loop)
        {
            _looping = loop;
            // Note: In Godot, looping is typically configured on the AudioStream resource itself
            // (e.g., AudioStreamOggVorbis.Loop or AudioStreamWAV.LoopMode).
        }

        /// <summary>Sets the audio player for internal sync.</summary>
        public void SetAudioPlayer(AudioStreamPlayer player)
        {
            _audioPlayer = player;
        }

        /// <summary>Sets the MIDI timecode receiver reference.</summary>
        public void SetMidiReceiver(MidiTimecodeReceiver receiver)
        {
            _midiTimecodeReceiver = receiver;
        }

        /// <summary>Sets the Art-Net timecode receiver reference.</summary>
        public void SetArtNetReceiver(ArtNetTimecodeReceiver receiver)
        {
            _artNetTimecodeReceiver = receiver;
        }
    }
}
