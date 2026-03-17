using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.ArtNet;
using LazerSystem.Zones;

namespace LazerSystem.UI
{
    /// <summary>
    /// UI panel for managing projector configurations including network settings,
    /// zone assignments, connection status, and test patterns.
    /// </summary>
    public partial class ProjectorSettingsUI : Control
    {
        [ExportGroup("References")]
        [Export] private LaserSystemManager systemManager;
        [Export] private ArtNetManager artNetManager;
        [Export] private ZoneManager zoneManager;

        [ExportGroup("Projector List")]
        [Export] private Control projectorListContainer;
        [Export] private PackedScene projectorRowScene;

        [ExportGroup("Network Settings")]
        [Export] private LineEdit broadcastAddressField;
        [Export] private LineEdit sendRateField;
        [Export] private Label networkStatusText;

        private List<ProjectorRowUI> projectorRows = new List<ProjectorRowUI>();

        public override void _Ready()
        {
            BuildProjectorList();
            SetupNetworkFields();
        }

        /// <summary>Builds the projector settings rows from the system manager's projector list.</summary>
        private void BuildProjectorList()
        {
            if (systemManager == null || projectorListContainer == null || projectorRowScene == null)
                return;

            // Clear existing rows
            foreach (var row in projectorRows)
            {
                if (row.Root != null)
                    row.Root.QueueFree();
            }
            projectorRows.Clear();

            var projectors = systemManager.Projectors;
            for (int i = 0; i < projectors.Count && i < 4; i++)
            {
                int projectorIndex = i;
                var config = projectors[i];

                var rowNode = projectorRowScene.Instantiate<Control>();
                rowNode.Name = $"Projector_{i + 1}";
                projectorListContainer.AddChild(rowNode);

                var row = new ProjectorRowUI(rowNode, config, projectorIndex);
                row.SetupListeners(
                    () => OnProjectorEnabledChanged(projectorIndex),
                    () => OnTestPatternClicked(projectorIndex),
                    (value) => OnIpAddressChanged(projectorIndex, value),
                    (value) => OnUniverseChanged(projectorIndex, value)
                );

                projectorRows.Add(row);
            }

            RefreshAll();
        }

        /// <summary>Sets up the network settings input fields.</summary>
        private void SetupNetworkFields()
        {
            if (broadcastAddressField != null)
            {
                broadcastAddressField.Text = "255.255.255.255";
                broadcastAddressField.TextSubmitted += OnBroadcastAddressChanged;
            }

            if (sendRateField != null)
            {
                sendRateField.Text = "44";
                sendRateField.TextSubmitted += OnSendRateChanged;
            }
        }

        /// <summary>Refreshes all projector row displays.</summary>
        public void RefreshAll()
        {
            if (systemManager == null)
                return;

            var projectors = systemManager.Projectors;

            for (int i = 0; i < projectorRows.Count && i < projectors.Count; i++)
            {
                var config = projectors[i];
                var row = projectorRows[i];

                if (config == null)
                {
                    row.SetEmpty(i);
                    continue;
                }

                row.Refresh(config);

                // Update connection status
                bool connected = artNetManager != null && artNetManager.IsConnected;
                row.SetConnectionStatus(connected);

                // Update zone assignments
                if (zoneManager != null)
                {
                    var zones = zoneManager.GetZonesForProjector(i);
                    row.SetZoneInfo(zones);
                }
            }
        }

        private void OnProjectorEnabledChanged(int index)
        {
            if (systemManager == null)
                return;

            var projectors = systemManager.Projectors;
            if (index < projectors.Count && projectors[index] != null)
            {
                projectors[index].Enabled = !projectors[index].Enabled;
                RefreshAll();
            }
        }

        private void OnTestPatternClicked(int index)
        {
            if (artNetManager == null)
                return;

            // Send a test pattern to the projector
            byte[] testFrame = FB4ChannelMap.BuildDmxFrame(
                enabled: true,
                pattern: 0,
                x: 0f,
                y: 0f,
                sizeX: 0.5f,
                sizeY: 0.5f,
                rotation: 0f,
                color: Colors.White,
                scanSpeed: 0.5f,
                effect: 0,
                effectSpeed: 0f,
                effectSize: 0f,
                zoom: 0.5f
            );

            artNetManager.SendDmx(index, testFrame);
            GD.Print($"[ProjectorSettingsUI] Test pattern sent to projector {index + 1}");
        }

        private void OnIpAddressChanged(int index, string value)
        {
            if (systemManager == null)
                return;

            var projectors = systemManager.Projectors;
            if (index < projectors.Count && projectors[index] != null)
            {
                projectors[index].IpAddress = value;
            }
        }

        private void OnUniverseChanged(int index, string value)
        {
            if (systemManager == null)
                return;

            if (int.TryParse(value, out int universe))
            {
                var projectors = systemManager.Projectors;
                if (index < projectors.Count && projectors[index] != null)
                {
                    projectors[index].ArtNetUniverse = universe;
                }
            }
        }

        private void OnBroadcastAddressChanged(string value)
        {
            if (artNetManager != null)
            {
                artNetManager.BroadcastAddress = value;
            }
        }

        private void OnSendRateChanged(string value)
        {
            if (int.TryParse(value, out int rate) && artNetManager != null)
            {
                artNetManager.SendRate = Mathf.Clamp(rate, 1, 44);
            }
        }
    }

    /// <summary>
    /// Helper class managing a single projector row in the settings UI.
    /// </summary>
    public class ProjectorRowUI
    {
        public Control Root { get; private set; }

        private Label nameLabel;
        private LineEdit ipAddressField;
        private LineEdit universeField;
        private CheckButton enabledToggle;
        private ColorRect connectionStatusRect;
        private Label zoneInfoText;
        private Button testPatternButton;

        public ProjectorRowUI(Control root, ProjectorConfig config, int index)
        {
            Root = root;

            // Find UI elements by name convention
            nameLabel = root.GetNodeOrNull<Label>("Name");
            ipAddressField = root.GetNodeOrNull<LineEdit>("IpAddress");
            universeField = root.GetNodeOrNull<LineEdit>("Universe");
            enabledToggle = root.GetNodeOrNull<CheckButton>("Enabled");
            connectionStatusRect = root.GetNodeOrNull<ColorRect>("ConnectionStatus");
            zoneInfoText = root.GetNodeOrNull<Label>("ZoneInfo");
            testPatternButton = root.GetNodeOrNull<Button>("TestPattern");
        }

        public void SetupListeners(
            System.Action onEnabledChanged,
            System.Action onTestPattern,
            System.Action<string> onIpChanged,
            System.Action<string> onUniverseChanged)
        {
            if (enabledToggle != null)
                enabledToggle.Toggled += _ => onEnabledChanged?.Invoke();

            if (testPatternButton != null)
                testPatternButton.Pressed += () => onTestPattern?.Invoke();

            if (ipAddressField != null)
                ipAddressField.TextSubmitted += value => onIpChanged?.Invoke(value);

            if (universeField != null)
                universeField.TextSubmitted += value => onUniverseChanged?.Invoke(value);
        }

        public void Refresh(ProjectorConfig config)
        {
            if (config == null) return;

            if (nameLabel != null) nameLabel.Text = config.ProjectorName;
            if (ipAddressField != null) ipAddressField.Text = config.IpAddress;
            if (universeField != null) universeField.Text = config.ArtNetUniverse.ToString();
            if (enabledToggle != null) enabledToggle.ButtonPressed = config.Enabled;
        }

        public void SetEmpty(int index)
        {
            if (nameLabel != null) nameLabel.Text = $"Projector {index + 1} (empty)";
            if (ipAddressField != null) ipAddressField.Text = "";
            if (universeField != null) universeField.Text = "";
            if (enabledToggle != null) enabledToggle.ButtonPressed = false;
            SetConnectionStatus(false);
        }

        public void SetConnectionStatus(bool connected)
        {
            if (connectionStatusRect != null)
            {
                connectionStatusRect.Color = connected ? Colors.Green : Colors.Red;
            }
        }

        public void SetZoneInfo(List<int> zoneIndices)
        {
            if (zoneInfoText == null) return;

            if (zoneIndices == null || zoneIndices.Count == 0)
            {
                zoneInfoText.Text = "No zones";
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < zoneIndices.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"Zone {zoneIndices[i]}");
            }
            zoneInfoText.Text = sb.ToString();
        }
    }
}
