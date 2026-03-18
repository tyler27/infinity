using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Godot;

namespace LazerSystem.ArtNet
{
	/// <summary>
	/// Represents a discovered Art-Net node on the network.
	/// </summary>
	[Serializable]
	public class ArtNetNode
	{
		public string ip;
		public string shortName;
		public int universe;

		public ArtNetNode(string ip, string shortName, int universe)
		{
			this.ip = ip;
			this.shortName = shortName;
			this.universe = universe;
		}

		public override string ToString()
		{
			return $"{shortName} ({ip}) Universe {universe}";
		}
	}

	/// <summary>
	/// Art-Net DMX manager singleton. Handles UDP socket communication, DMX buffer
	/// management, and timed transmission of Art-Net packets to laser projectors.
	/// Add as a Node in the scene tree.
	/// </summary>
	public partial class ArtNetManager : Node
	{
		// --------------- Singleton ---------------
		private static ArtNetManager _instance;
		public static ArtNetManager Instance
		{
			get
			{
				return _instance;
			}
		}

		// --------------- Configuration ---------------

		[ExportGroup("Network Settings")]
		[Export] private string _broadcastAddress = "2.255.255.255";
		[Export] private string[] _projectorAddresses = Array.Empty<string>();

		[ExportGroup("DMX Settings")]
		[Export] private int _universeCount = 4;
		[Export] private float _sendRate = 44f;

		// --------------- Public Properties ---------------

		/// <summary>Whether the UDP socket is currently open and ready to send.</summary>
		public bool IsConnected { get; private set; }

		/// <summary>The broadcast address used when no specific projector IP is set.</summary>
		public string BroadcastAddress
		{
			get => _broadcastAddress;
			set => _broadcastAddress = value;
		}

		/// <summary>Per-projector IP addresses. Index corresponds to universe.</summary>
		public string[] ProjectorAddresses => _projectorAddresses;

		/// <summary>Nodes discovered via ArtPoll.</summary>
		public List<ArtNetNode> DiscoveredNodes => _discoveredNodes;

		/// <summary>Current send rate in Hz.</summary>
		public float SendRate
		{
			get => _sendRate;
			set => _sendRate = Math.Max(1f, value);
		}

		// --------------- Private State ---------------

		private UdpClient _udpClient;
		private byte[][] _dmxBuffers;
		private List<ArtNetNode> _discoveredNodes = new List<ArtNetNode>();
		private double _sendAccumulator = 0;
		private bool _isReceivingPollReplies = false;
		private double _pollReplyElapsed = 0;
		private const double PollReplyTimeout = 3.0;

		// --------------- Godot Lifecycle ---------------

		public override void _Ready()
		{
			if (_instance != null && _instance != this)
			{
				GD.PushWarning("[ArtNetManager] Duplicate instance detected, removing this one.");
				QueueFree();
				return;
			}
			_instance = this;

			InitializeBuffers();
			OpenSocket();
		}

		public override void _Process(double delta)
		{
			// Send loop via delta accumulator
			_sendAccumulator += delta;
			double sendInterval = 1.0 / Math.Max(1.0, _sendRate);
			while (_sendAccumulator >= sendInterval)
			{
				_sendAccumulator -= sendInterval;
				SendAllUniverses();
			}

			// ArtPollReply receive window
			if (_isReceivingPollReplies)
			{
				_pollReplyElapsed += delta;
				ReceiveArtPollReplies();

				if (_pollReplyElapsed >= PollReplyTimeout)
				{
					_isReceivingPollReplies = false;
				}
			}
		}

		public override void _ExitTree()
		{
			CloseSocket();
			if (_instance == this)
				_instance = null;
		}

		// --------------- Initialization ---------------

		private void InitializeBuffers()
		{
			_universeCount = Math.Clamp(_universeCount, 1, 4);
			_dmxBuffers = new byte[_universeCount][];
			for (int i = 0; i < _universeCount; i++)
			{
				_dmxBuffers[i] = new byte[512];
			}
		}

		private void OpenSocket()
		{
			try
			{
				_udpClient = new UdpClient();
				_udpClient.EnableBroadcast = true;
				// Allow address reuse so multiple Art-Net apps can coexist
				_udpClient.Client.SetSocketOption(
					SocketOptionLevel.Socket,
					SocketOptionName.ReuseAddress,
					true);
				IsConnected = true;
				GD.Print($"[ArtNetManager] Socket opened. Broadcast: {_broadcastAddress}:{ArtNetPacket.PORT}");
			}
			catch (Exception ex)
			{
				IsConnected = false;
				GD.PushError($"[ArtNetManager] Failed to open UDP socket: {ex.Message}");
			}
		}

		private void CloseSocket()
		{
			if (_udpClient != null)
			{
				try
				{
					_udpClient.Close();
				}
				catch (Exception ex)
				{
					GD.PushWarning($"[ArtNetManager] Error closing socket: {ex.Message}");
				}
				_udpClient = null;
				IsConnected = false;
				GD.Print("[ArtNetManager] Socket closed.");
			}
		}

		// --------------- Public API ---------------

		/// <summary>
		/// Sends a full DMX frame for a universe immediately.
		/// Also updates the internal buffer so the send loop continues with this data.
		/// </summary>
		/// <param name="universe">Universe index (0-based).</param>
		/// <param name="data">DMX data (up to 512 bytes).</param>
		public void SendDmx(int universe, byte[] data)
		{
			if (!ValidateUniverse(universe)) return;
			if (data == null) return;

			// Update internal buffer
			int length = Math.Min(data.Length, 512);
			Array.Copy(data, 0, _dmxBuffers[universe], 0, length);

			// Send immediately
			SendDmxPacket(universe);
		}

		/// <summary>
		/// Updates a single DMX channel in the buffer for a given universe.
		/// The change will be sent on the next send loop cycle.
		/// </summary>
		/// <param name="universe">Universe index (0-based).</param>
		/// <param name="channel">DMX channel (1-indexed, 1-512).</param>
		/// <param name="value">Channel value (0-255).</param>
		public void UpdateDmxChannel(int universe, int channel, byte value)
		{
			if (!ValidateUniverse(universe)) return;

			if (channel < 1 || channel > 512)
			{
				GD.PushError($"[ArtNetManager] Channel {channel} out of range (1-512).");
				return;
			}

			_dmxBuffers[universe][channel - 1] = value;
		}

		/// <summary>
		/// Bulk-updates an entire DMX frame in the buffer for a given universe.
		/// The data will be sent on the next send loop cycle.
		/// </summary>
		/// <param name="universe">Universe index (0-based).</param>
		/// <param name="frame">Full 512-byte DMX frame.</param>
		public void UpdateDmxFrame(int universe, byte[] frame)
		{
			if (!ValidateUniverse(universe)) return;
			if (frame == null) return;

			int length = Math.Min(frame.Length, 512);
			Array.Copy(frame, 0, _dmxBuffers[universe], 0, length);
		}

		/// <summary>
		/// Broadcasts an ArtPoll packet to discover Art-Net nodes on the network.
		/// </summary>
		public void SendArtPoll()
		{
			if (!IsConnected)
			{
				GD.PushWarning("[ArtNetManager] Cannot send ArtPoll: socket not connected.");
				return;
			}

			try
			{
				byte[] packet = ArtNetPacket.BuildArtPollPacket();
				IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(_broadcastAddress), ArtNetPacket.PORT);
				_udpClient.Send(packet, packet.Length, endpoint);
				GD.Print("[ArtNetManager] ArtPoll sent.");

				// Start listening for replies
				_isReceivingPollReplies = true;
				_pollReplyElapsed = 0;
			}
			catch (Exception ex)
			{
				GD.PushError($"[ArtNetManager] Failed to send ArtPoll: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets the raw DMX buffer for a universe. Do not cache this reference
		/// across frames if universes may be reinitialized.
		/// </summary>
		public byte[] GetDmxBuffer(int universe)
		{
			if (!ValidateUniverse(universe)) return null;
			return _dmxBuffers[universe];
		}

		/// <summary>
		/// Clears all DMX channels for a universe to zero (blackout).
		/// </summary>
		public void BlackoutUniverse(int universe)
		{
			if (!ValidateUniverse(universe)) return;
			Array.Clear(_dmxBuffers[universe], 0, 512);
		}

		/// <summary>
		/// Clears all universes to zero (full blackout).
		/// </summary>
		public void BlackoutAll()
		{
			for (int i = 0; i < _dmxBuffers.Length; i++)
			{
				Array.Clear(_dmxBuffers[i], 0, 512);
			}
		}

		// --------------- Internal Send Logic ---------------

		private void SendAllUniverses()
		{
			if (IsConnected && _dmxBuffers != null)
			{
				for (int i = 0; i < _dmxBuffers.Length; i++)
				{
					SendDmxPacket(i);
				}
			}
		}

		private void SendDmxPacket(int universe)
		{
			if (!IsConnected || _udpClient == null) return;

			try
			{
				byte[] packet = ArtNetPacket.BuildArtDmxPacket(universe, _dmxBuffers[universe]);
				IPEndPoint endpoint = GetEndpointForUniverse(universe);
				_udpClient.Send(packet, packet.Length, endpoint);
			}
			catch (SocketException ex)
			{
				GD.PushError($"[ArtNetManager] Socket error sending universe {universe}: {ex.Message}");
				// Attempt to reconnect
				CloseSocket();
				OpenSocket();
			}
			catch (ObjectDisposedException)
			{
				IsConnected = false;
			}
			catch (Exception ex)
			{
				GD.PushError($"[ArtNetManager] Error sending universe {universe}: {ex.Message}");
			}
		}

		private IPEndPoint GetEndpointForUniverse(int universe)
		{
			// Use per-projector address if available for this universe
			if (universe < _projectorAddresses.Length &&
				!string.IsNullOrEmpty(_projectorAddresses[universe]))
			{
				try
				{
					return new IPEndPoint(
						IPAddress.Parse(_projectorAddresses[universe]),
						ArtNetPacket.PORT);
				}
				catch (FormatException)
				{
					GD.PushWarning($"[ArtNetManager] Invalid projector IP for universe {universe}, using broadcast.");
				}
			}

			return new IPEndPoint(IPAddress.Parse(_broadcastAddress), ArtNetPacket.PORT);
		}

		/// <summary>
		/// Non-blocking check for ArtPollReply packets. Called each frame from _Process
		/// while the receive window is active.
		/// </summary>
		private void ReceiveArtPollReplies()
		{
			if (_udpClient == null) return;

			while (_udpClient.Available > 0)
			{
				try
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					byte[] data = _udpClient.Receive(ref remoteEP);

					// Minimal ArtPollReply parse: check header and opcode 0x2100
					if (data.Length >= 10 &&
						data[0] == 0x41 && data[1] == 0x72 && data[2] == 0x74 && // "Art"
						data[8] == 0x00 && data[9] == 0x21) // opcode 0x2100 LE
					{
						string nodeIp = remoteEP.Address.ToString();
						// Short name is at offset 26, 18 bytes in ArtPollReply
						string shortName = "Unknown";
						if (data.Length >= 44)
						{
							shortName = System.Text.Encoding.ASCII.GetString(data, 26, 18).TrimEnd('\0');
						}

						int nodeUniverse = 0;
						if (data.Length >= 19)
						{
							nodeUniverse = data[18] | (data[19] << 8);
						}

						// Avoid duplicates
						if (!_discoveredNodes.Exists(n => n.ip == nodeIp))
						{
							var node = new ArtNetNode(nodeIp, shortName, nodeUniverse);
							_discoveredNodes.Add(node);
							GD.Print($"[ArtNetManager] Discovered node: {node}");
						}
					}
				}
				catch (Exception ex)
				{
					GD.PushWarning($"[ArtNetManager] Error receiving ArtPollReply: {ex.Message}");
					break;
				}
			}
		}

		// --------------- Validation ---------------

		private bool ValidateUniverse(int universe)
		{
			if (_dmxBuffers == null)
			{
				GD.PushError("[ArtNetManager] DMX buffers not initialized.");
				return false;
			}

			if (universe < 0 || universe >= _dmxBuffers.Length)
			{
				GD.PushError($"[ArtNetManager] Universe {universe} out of range (0-{_dmxBuffers.Length - 1}).");
				return false;
			}

			return true;
		}
	}
}
