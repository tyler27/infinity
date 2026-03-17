# Infinity -- Laser Show Control System

Infinity is a real-time laser show control application built with Godot 4.4.1 (.NET/C#). It provides a complete visual previewer and live ArtNet controller for up to 4 Pangolin FB4 laser projectors, combining a cue-based performance interface with a physically accurate 3D venue simulation.

The software generates laser patterns in real time, renders them in a 3D preview environment with proper beam physics (galvo angles, surface raycasting, additive blending), and simultaneously outputs ArtNet/DMX data to FB4 hardware over the network.

---

## Screenshots

_Screenshots coming soon._

---

## Features

### Live Performance Engine
- 6x10 cue grid with up to 256 pages of cues (15,360 total cue slots)
- Toggle and flash (momentary) cue triggering modes
- Multiple simultaneous active cues with point-level compositing
- Keyboard-driven cue triggering mapped to four rows of keys
- F1-F12 quick page switching via favorite pages
- Master intensity and master size multipliers
- Live override sliders for color, position, rotation, speed, intensity, and size
- Real-time pattern generation at full frame rate

### Pattern System
- **Beam** -- Single directional beam
- **Fan** -- Multi-beam fan with configurable count and spread angle
- **Cone** -- Conical beam arrangement
- **Circle** -- Circular trace pattern
- **Line** -- Straight line with rotation
- **Wave** -- Sine wave with adjustable frequency and amplitude
- **Triangle** -- Triangular shape
- **Square** -- Square/rectangle shape
- **Star** -- Star shape with configurable point count
- **Text** -- Text rendering
- **Tunnel** -- Concentric shape tunnel effect
- **CustomILDA** -- ILDA file playback (planned)

Each pattern supports parameters for color, intensity, size, rotation, speed, spread, count, frequency, amplitude, and position.

### 3D Preview
- Physically modeled 3D venue with floor grid, back wall, ceiling, and side walls
- 4 individually positioned projector nodes on a virtual truss
- Beam rendering uses galvo scan angle simulation (configurable FOV, default 50 degrees)
- Raycasting against venue surfaces for accurate beam termination
- Additive-blend beam rendering with configurable haze intensity
- Optional source beam visualization (thin lines from projector origin to impact point)
- Zone boundary overlay with keystone-warped grid, corner brackets, and center crosshair
- Color-coded projector markers (Red, Green, Blue, Yellow)

### 3D Camera Controls
- Orbit camera with left-click drag to orbit
- Middle-click drag to pan
- Scroll wheel to zoom (2m to 60m range)
- Preset views: Front, Top, Side, Projector View, Reset
- Pop-out window support for the 3D preview

### 2D Output View
- Real-time 2D laser output visualization per projector
- Overflow/clipping detection indicator

### ArtNet / DMX
- ArtNet (Art-Net 4) output over UDP on port 6454
- Up to 4 universes (one per projector)
- Configurable send rate (1-44 Hz, default 44 Hz)
- Per-projector unicast IP addressing or broadcast fallback
- ArtPoll discovery with automatic node detection
- FB4 Standard Mode 16-channel DMX mapping
- Automatic DMX frame generation from pattern output

### Zone Management
- Projection zones with per-zone position offset, scale, and rotation
- 4-corner keystone correction via bilinear interpolation
- Interactive keystone canvas with draggable corner handles
- Safety zone boundaries (left/right/top/bottom clipping)
- Zone-to-projector assignment
- Zone boundary visualization in 3D preview
- Test pattern output per zone

### Projector Configuration
- Per-projector name, IP address, ArtNet universe, and enable/disable
- Network scan (ArtPoll broadcast) to discover FB4 devices
- Auto-populate projector IP from discovered nodes
- Test connection with visual status indicator (green/yellow/red)
- Test pattern output per projector

### Safety Features
- Master Laser Output enable/disable (kills all output)
- Blackout toggle (temporary kill, preserves enable state)
- Per-projector enable/disable buttons (P1-P4)
- Zone routing controls (select which projectors receive output)
- Clear All button to deactivate every cue instantly
- Safety zone clamping prevents output outside defined boundaries
- Overflow detection warns when patterns exceed the scan area

### Cue Library
- 4 pre-built pages with 240 preset cues:
  - **Page 1: Beams & Fans** -- Single beams, multi-beam fans, rotating fans, cones, lines, and mixed shapes
  - **Page 2: Shapes** -- Circles, triangles, squares, stars in various sizes, speeds, and colors
  - **Page 3: Waves & Tunnels** -- Waves with varying frequency/amplitude, tunnels with varying count/speed
  - **Page 4: Color Themes** -- Full pattern sets in red, green, blue, warm, cool, and white/pastel palettes
- Pages 5-256 available for user content

---

## Requirements

- **Godot Engine 4.4.1** (.NET / C# edition)
- **.NET SDK 8.0** or later
- **Windows, macOS, or Linux** (any platform supported by Godot .NET)
- For live output: FB4 laser projector(s) on the same network, reachable via ArtNet (UDP port 6454)

---

## Getting Started

### 1. Install Prerequisites

1. Install the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later).
2. Download [Godot 4.4.1 .NET](https://godotengine.org/download) -- make sure to get the **.NET** version, not the standard version.

### 2. Clone and Open

```bash
git clone <repository-url>
```

Open Godot 4.4.1 .NET, click **Import**, and navigate to the project directory. Select the `project.godot` file and open the project.

### 3. Build and Run

- In the Godot editor, press **F5** (or the Play button) to build and launch.
- The first build will restore NuGet packages and compile all C# scripts automatically.
- The application opens at 1920x1080 with a dark theme.

### 4. Network Setup (for live output)

- Ensure your computer and FB4 projectors are on the same network.
- FB4 projectors typically use the `2.x.x.x` subnet. The default broadcast address is `2.255.255.255`.
- Open the **Projectors** settings panel and configure each projector's IP address and universe.
- Use **Scan Network** to discover FB4 devices automatically.

---

## How to Use

### Main UI Layout

The interface is organized into several regions:

```
+---------------------------------------------------------------+
|  LASER OUTPUT | BLACKOUT | P1 P2 P3 P4 | ZONE: Z1-Z4 ALL |   |
|  Projectors | Zones |               CLEAR ALL         | FPS   |
+-------+-------------------------------+-----------------------+
| PAGE  |                               |                       |
| LIST  |     6 x 10 CUE GRID           |    3D PREVIEW         |
|       |                               |    (orbit camera)     |
| [Fav  |  [1][2][3][4][5][6][7][8][9][0]|                       |
|  Bar] |  [Q][W][E][R][T][Y][U][I][O][P]|  Camera presets       |
|       |  [A][S][D][F][G][H][J][K][L][;]|                       |
|       |  [Z][X][C][V][B][N][M][,][.][/]|  2D Output View       |
|       |  (rows 5-6 mouse only)         |                       |
+-------+-------------------------------+-----------------------+
|  LIVE CONTROLS: Intensity | Size | PosX | PosY | Rot | Speed  |
+---------------------------------------------------------------+
|  Status bar                                                    |
+---------------------------------------------------------------+
```

**Safety Toolbar (top):**
- **LASER OUTPUT** -- Master enable. Green = output active, Red = output disabled. Click to toggle.
- **BLACKOUT** -- Temporary kill switch. Yellow = blackout active. Click to toggle.
- **P1-P4** -- Per-projector enable/disable. Color-coded (Red, Green, Blue, Yellow). Dimmed when disabled.
- **ZONE: Z1-Z4, ALL** -- Select which projectors/zones receive cue output. ALL sends to all four.
- **Projectors** -- Opens the Projector Settings floating panel.
- **Zones** -- Opens the Zone Editor floating panel.
- **CLEAR ALL** -- Immediately deactivates all running cues.
- **FPS** -- Current frame rate display.

**Page List Sidebar (left):**
- Scrollable list of all 256 pages with search filtering.
- Pages with cues are indicated. Click to switch pages.
- Favorite bar at the top for quick access to pinned pages.

**Cue Grid (center):**
- 6 rows by 10 columns of cue buttons.
- Each button shows the cue name (and keyboard shortcut for rows 1-4).
- Click or press the mapped key to toggle a cue on/off.
- Active cues are highlighted green.
- Empty slots appear as dark buttons.

**3D Preview (right):**
- Real-time 3D visualization of all active laser output.
- Camera preset buttons: Front, Top, Side, Projector, Reset.
- Source beams toggle to show/hide beam paths from projector to surface.
- Settings button for beam width, scan angle, and haze intensity.
- Can be popped out into a separate window.

**Live Controls (bottom):**
- **Master Intensity** -- Scales brightness of all output (0-100%).
- **Master Size** -- Scales size of all patterns (0-200%).
- **Position X/Y** -- Offsets all pattern output horizontally/vertically.
- **Rotation** -- Adds rotational offset to all patterns.
- **Speed** -- Multiplies animation speed of all patterns.
- **Color Picker** -- Override color for all active cues (white = no override).

**Status Bar (bottom edge):**
- ArtNet connection status and general system information.

### Keyboard Shortcuts

**Cue Triggering (4 rows mapped to keyboard):**

| Row | Keys |
|-----|------|
| 1 | `1` `2` `3` `4` `5` `6` `7` `8` `9` `0` |
| 2 | `Q` `W` `E` `R` `T` `Y` `U` `I` `O` `P` |
| 3 | `A` `S` `D` `F` `G` `H` `J` `K` `L` `;` |
| 4 | `Z` `X` `C` `V` `B` `N` `M` `,` `.` `/` |

Rows 5 and 6 are mouse-only (no keyboard mapping).

**Page Switching:**

| Key | Action |
|-----|--------|
| `F1` - `F12` | Switch to favorite page 1-12 (or sequential page if no favorite is set) |

### Cue System

**Pages:**
- The system supports 256 pages, each containing a 6x10 grid of cue slots.
- Pages can be named for organization (e.g., "Beams & Fans", "Color Themes").
- Pages can be favorited for quick F-key access.
- The first 4 pages come pre-populated with a library of 240 preset cues.

**Triggering Modes:**
- **Toggle (default):** Press a key or click a button to activate a cue. Press again to deactivate.
- **Flash (momentary):** Hold a key to activate, release to deactivate. Used for momentary hits.

**Multi-Cue Layering:**
Multiple cues can be active simultaneously. Their generated points are composited together per projector, allowing layered effects (e.g., a fan and a circle running at the same time).

### Live Control Sliders

The live control sliders modify all active cue output in real time:

- **Intensity** and **Size** multiply on top of cue values and master values.
- **Rotation** and **Position X/Y** add to cue values (additive offset).
- **Speed** multiplies the cue's animation speed.
- **Color** replaces the cue color when set to anything other than white.

All live controls default to neutral values (intensity 1.0, size 1.0, speed 1.0, rotation 0, position 0,0, color white) so they have no effect until adjusted.

### 3D Preview Controls

**Orbit Camera:**
- **Left-click drag** -- Orbit around the focus point (azimuth and elevation).
- **Middle-click drag** -- Pan the focus point in the camera's local plane.
- **Scroll wheel** -- Zoom in/out (2m minimum, 60m maximum distance).

**Camera Presets:**
- **Front** -- Standard audience perspective.
- **Top** -- Bird's eye view looking down.
- **Side** -- Profile view from the side.
- **Projector** -- Close view from near the projector truss.
- **Reset** -- Return to the default viewing angle.

**Source Beams:**
Toggle source beam visualization to see thin lines drawn from each projector's position to where the beams hit venue surfaces. Useful for understanding beam geometry and projector coverage.

### Zone Management

Open the Zone Editor from the toolbar. The panel provides:

**Zone List (left side):**
- Add and remove projection zones.
- Each zone is assigned to a projector (P1-P4).
- Click a zone to edit its properties.

**Zone Properties (right side):**

- **Identity** -- Zone name, projector assignment, enable/disable.
- **Position & Scale** -- X/Y offset (-100 to +100), X/Y scale (10% to 200%), rotation (-180 to +180 degrees).
- **Keystone** -- Interactive canvas for dragging the four corners of the output quad. Corrects for projector angle and surface geometry via bilinear interpolation. Reset button restores the default rectangular boundary.
- **Safety Zone** -- Left/right/top/bottom boundaries that clamp laser output to a safe area. Prevents beams from hitting areas outside the intended projection surface.

**Actions:**
- **Test Pattern** -- Sends a white test grid pattern to the zone's projector via ArtNet.
- **Show Zone Boundary** -- Overlays the zone boundary (with keystone warping) in the 3D preview for the selected zone.
- **Show All Boundaries** -- Overlays boundaries for all zones simultaneously, color-coded by projector.

### Projector Configuration

Open the Projector Settings panel from the toolbar:

**Network Settings:**
- **Broadcast Address** -- The ArtNet broadcast address (default: `2.255.255.255`).
- **Send Rate** -- ArtNet packet transmission rate in Hz (1-44, default 44).
- **Scan Network** -- Sends an ArtPoll broadcast and lists discovered Art-Net nodes.
- **Discovered Devices** -- Click a discovered node to auto-populate a projector's IP.

**Per-Projector Settings (P1-P4):**
- **Name** -- Display name for the projector.
- **IP Address** -- Unicast IP address for this projector. If empty, broadcast is used.
- **Universe** -- ArtNet universe number (0-15).
- **Enabled** -- Enable/disable this projector's output.
- **Test Connection** -- Sends an ArtPoll and checks if the projector responds (green = OK, red = no response).
- **Test Pattern** -- Sends a test DMX frame directly to the projector.
- **Zone Assignment** -- Shows which zones are routed to this projector.

### Safety Features

Infinity provides multiple layers of safety controls to prevent unintended laser output:

1. **Master Laser Output** -- Top-level kill switch. When disabled (red), no DMX data is sent and all renderers are cleared.
2. **Blackout** -- Temporary mute. Zeroes all DMX output but preserves the enable state so you can resume instantly.
3. **Per-Projector Enable** -- Disable individual projectors without affecting others.
4. **Zone Routing** -- Select which projectors receive cue output (Z1-Z4 or ALL).
5. **Safety Zones** -- Per-zone boundary clamping prevents output from exceeding defined safe areas.
6. **Clear All** -- One-click deactivation of all running cues.
7. **Overflow Detection** -- Visual warning when pattern parameters would cause output to exceed the -1..1 scan boundary.

---

## Architecture Overview

```
LiveEngine (singleton)
  |
  |-- Reads active cues from CuePages[page, row, col]
  |-- Generates patterns via PatternFactory / ILaserPattern
  |-- Applies live overrides and master controls
  |-- Distributes points to target projectors via ActiveZones
  |-- Applies zone transforms via ZoneManager (scale, rotation, offset, keystone)
  |-- Feeds points to LaserPreviewRenderer[0..3] for 3D visualization
  |-- Feeds points to LaserOutputView for 2D visualization
  |-- Builds DMX frames via FB4ChannelMap and sends via ArtNetManager

ArtNetManager (singleton)
  |-- UDP socket with broadcast support
  |-- 4 x 512-byte DMX buffers
  |-- Timed send loop at configurable rate
  |-- ArtPoll discovery

ZoneManager (singleton)
  |-- Manages ProjectionZone resources
  |-- Applies geometric transforms (scale, rotation, offset)
  |-- Applies KeystoneCorrection (bilinear quad warp)
  |-- Safety zone clamping

LaserPreviewRenderer (per projector)
  |-- Converts scan coordinates to 3D world positions via galvo angle simulation
  |-- Raycasts against venue planes (floor, ceiling, walls)
  |-- Renders beam quads with additive blending
  |-- Optional source beam rendering
  |-- Zone boundary overlay generation
```

**Key Data Types:**
- `LaserPoint` -- Struct with normalized X/Y position (-1..1), RGB color (0..1), and blanking flag.
- `LaserCue` -- Resource defining a pattern type, color, and all pattern parameters for a grid cell.
- `ProjectionZone` -- Resource defining zone geometry (offset, scale, rotation, keystone corners, safety bounds).
- `ProjectorConfig` -- Resource defining projector identity and network settings (name, IP, universe).
- `PatternParameters` -- Parameter bundle passed to pattern generators.

---

## ArtNet / DMX Protocol Details

### Network

- **Protocol:** Art-Net 4 (UDP)
- **Port:** 6454
- **Default Broadcast:** `2.255.255.255` (Pangolin FB4 standard subnet)
- **Addressing:** Per-projector unicast when IP is configured, broadcast fallback otherwise
- **Send Rate:** Configurable 1-44 Hz (default 44 Hz)

### FB4 DMX Channel Map (Standard Mode, 16 Channels)

| Channel | Function | Range | Notes |
|---------|----------|-------|-------|
| 1 | Control/Mode | 0 = blackout, 255 = enable | Master on/off |
| 2 | Pattern Select | 0-255 | Pattern index |
| 3 | X Position | 0-255 (128 = center) | Bipolar mapping |
| 4 | Y Position | 0-255 (128 = center) | Bipolar mapping |
| 5 | X Size | 0-255 | Unipolar mapping |
| 6 | Y Size | 0-255 | Unipolar mapping |
| 7 | Rotation | 0-255 (128 = none) | Bipolar mapping |
| 8 | Red | 0-255 | Color intensity |
| 9 | Green | 0-255 | Color intensity |
| 10 | Blue | 0-255 | Color intensity |
| 11 | Scan Speed | 0-255 | Unipolar mapping |
| 12 | Draw Mode | 0-255 | Blanking control |
| 13 | Effect Select | 0-255 | Effect index |
| 14 | Effect Speed | 0-255 | Unipolar mapping |
| 15 | Effect Size | 0-255 | Unipolar mapping |
| 16 | Zoom | 0-255 | Unipolar mapping |

DMX frames are computed from the averaged position, color, and bounding extent of all visible laser points generated by active cues for each projector.

---

## Roadmap

Planned features and improvements (not yet implemented):

- [ ] ILDA file import and playback (CustomILDA pattern type)
- [ ] Timeline/show sequencer with timed cue playback
- [ ] MIDI input for cue triggering and live control mapping
- [ ] Audio analysis / beat detection for reactive effects
- [ ] Cue editor for creating and modifying cues within the application
- [ ] Save/load show files (cue pages, zone configs, projector settings)
- [ ] Multi-zone cue routing (assign cues to specific zones independently)
- [ ] DMX input for external control integration
- [ ] Effect chaining and parameter modulation
- [ ] Network latency monitoring and diagnostics

---

## License

_License information to be added._
