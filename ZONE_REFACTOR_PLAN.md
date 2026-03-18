# Zone-Centric Architecture Refactor Plan

*Proposed 2026-03-18 — Many-to-many zone/projector routing, keystone as boundary, zone sub-rows in timeline*

---

## Problem Statement

The current system conflates zone index, projector index, and ArtNet universe. A zone maps to exactly one projector, tracks are flat (one zone per track), and "safety zones" are a separate concept from keystone boundaries. In reality:

- **Projectors and zones are many-to-many** — a zone can target multiple projectors, a projector can receive from multiple zones
- **Keystone IS the boundary** — the keystone quad defines where laser physically projects; safety zones are redundant
- **Timeline should be zone-aware** — tracks should show zone sub-rows, and each cue block fires its pattern from a specific zone

---

## Current Architecture (What Exists)

```
Block.ZoneIndex (int) → zone list position
Track.zoneIndex (int) → one zone per track
ProjectionZone.ProjectorIndex (int) → one projector per zone
PlaybackManager sends output keyed by zoneIndex → preview/ArtNet treat it as projectorIndex
LiveEngine.ActiveZones is actually projector indices
```

**Key conflation:** `zoneIndex == projectorIndex == universeIndex` throughout the codebase.

---

## Target Architecture

```
Block.ZoneId (string) → stable zone reference
Track.ZoneIds (list) → multiple zone sub-rows per track
ProjectionZone.ProjectorIndices (int[]) → multiple projectors per zone
ProjectionZone.ZoneId (string) → stable ID for referencing

Output flow:
  Block → Pattern Generation (points in -1..1)
    → Zone Resolution (ZoneId → ProjectionZone)
    → Zone Transform (scale → rotation → offset → keystone)
    → Projector Fan-out (zone.ProjectorIndices → [P1, P3, ...])
    → Per-Projector Accumulation (merge all zones targeting this projector)
    → Preview Renderer[projectorIndex] + ArtNet[projectorIndex]
```

---

## Phase 1: Data Model Changes (Foundation)

**Files:** `ProjectionZone.cs`, `LaserCueBlock.cs`, `LaserSystemManager.cs`

### ProjectionZone.cs
- **Add** `[Export] public string ZoneId` — auto-generated GUID on creation
- **Add** `[Export] public int[] ProjectorIndices` — replaces single `ProjectorIndex`
- **Keep** `ProjectorIndex` temporarily for backward compat (migration reads it)
- **Remove** `Rect2 SafetyZone` — keystone corners are the source of truth
- Keep: `KeystoneCorners`, `PositionOffset`, `Scale`, `Rotation`, `Enabled`, `ZoneName`

### LaserCueBlock.cs
- **Add** `[Export] public string ZoneId` — stable zone reference
- **Keep** `int ZoneIndex` temporarily for migration
- Update `DeepClone()` to copy `ZoneId`

### LaserSystemManager.cs
- **Add** `ProjectionZone GetZoneById(string zoneId)` lookup method
- **Add** `int GetZoneIndex(string zoneId)` for compat during transition

**Risk:** Both old and new fields coexist. This phase compiles and runs without behavioral changes.

---

## Phase 2: Zone Manager Multi-Projector Support

**Files:** `ZoneManager.cs`

- Update `RebuildProjectorZoneCache()` to iterate `ProjectorIndices` (plural)
- Fallback: if `ProjectorIndices` is null/empty, read `ProjectorIndex` (backward compat)
- **Add** `List<int> GetProjectorsForZone(int zoneIndex)` — the key new query
- **Add** `ProjectionZone GetZoneById(string zoneId)` wrapper
- **Remove** `IsInSafetyZone()` and `ClampToSafety()` — replaced by keystone boundary (points in -1..1 are already within the keystone quad after transform)
- Keep `TransformPoint`, `TransformPoints`, `TransformPointsInPlace` as-is

---

## Phase 3: Playback Manager Zone-Routed Output

**Files:** `PlaybackManager.cs`

### EvaluateTimeline rewrite:
```
1. For each active block:
   - Resolve zone via ZoneId → ProjectionZone
   - Generate pattern points
   - Apply automation
   - Apply zone transform (scale/rot/offset/keystone)
   - Look up zone.ProjectorIndices
   - For each target projector: accumulate transformed points

2. Per-projector output:
   - previewManager.UpdatePreview(projectorIndex, mergedPoints)
   - artNetManager.SendDmx(projectorIndex, dmxFrame)
```

### Other changes:
- `_activeZonesLastFrame` → `_activeProjectorsLastFrame`
- `ConvertPointsToDmx` parameterized by projector index (already is, just clarify)
- LoadShow/SaveShow: add migration logic (read ZoneIndex → resolve to ZoneId)

---

## Phase 4: Timeline Track Restructure

**Files:** `TimelineTrack.cs`

- **Remove** `int zoneIndex` from TimelineTrack
- **Add** `Godot.Collections.Array<string> ZoneIds` — the zones that appear as sub-rows
- Blocks within a track can have different ZoneIds
- `GetActiveBlocks()` unchanged (returns all active blocks regardless of zone)

---

## Phase 5: Show File Migration

**Files:** `PlaybackManager.cs` (LoadShow/SaveShow)

On load, detect and upgrade:
1. Blocks with `ZoneId == null` → populate from `ZoneIndex` + zone list
2. Zones with `ProjectorIndices == null` → populate from `ProjectorIndex`
3. Tracks with old `zoneIndex` → populate `ZoneIds` list, set each block's `ZoneId`
4. Zones without `ZoneId` → assign `$"zone_{index}"` or generate GUID

On save:
- Always write new format fields
- After transition period, remove deprecated int fields

---

## Phase 6: Timeline UI Zone Sub-Rows (Largest Change)

**Files:** `TimelineUI.cs`

### Visual layout:
```
Track 1                     |                                    |
  Zone: Main Stage          |[===Block===]   [==Block==]        |
  Zone: Left Wing           |    [==Block==]                    |
Track 2                     |                                    |
  Zone: Floor               |        [===Block===]              |
  Zone: Ceiling             |                                    |
```

### Changes required:
- **Variable track heights:** Replace all `trackIdx * trackHeight + RulerHeight` with `GetTrackYOffset(trackIdx)` helper that sums preceding track heights (each track = header + N zone sub-rows)
- **Zone sub-row labels:** Draw indented zone names under each track header with zone color indicator
- **Block positioning:** Block Y = track Y offset + header height + (zoneSubRowIndex * subRowHeight)
- **Hit-testing:** Resolve clicks to `(trackIndex, zoneId)` instead of just `trackIndex`
- **Context menus:** Track header menu gets "Add/Remove Zone Sub-Row"; block placement uses zone from clicked sub-row
- **Draw mode:** Track which zone sub-row the draw started on
- **Block creation:** Set `block.ZoneId` from the sub-row, not `track.zoneIndex`
- **Drag & drop:** Blocks dragged between zone sub-rows change their ZoneId

This is the largest single phase due to pervasive Y-coordinate math (dozens of occurrences).

---

## Phase 7: Zone Editor Panel Updates

**Files:** `ZoneEditorPanel.cs`

- **Replace** single projector `OptionButton` with multi-select (4 checkboxes, one per projector)
- **Remove** Safety Zone slider section entirely (Left/Right/Top/Bottom sliders)
- **Update** OnAddZone: set `zone.ProjectorIndices = new[] { 0 }`, generate ZoneId
- **Update** OnRemoveZone: handle orphaned block ZoneIds (warn user or reassign)
- **Update** boundary display: show boundary on each target projector when zone has multiple

---

## Phase 8: Live Engine Zone Routing

**Files:** `LiveEngine.cs`

- Rework `ActiveZones` to be zone-based, not projector-based
- Route live cue output: cue → target zone(s) → zone transform → fan out to projectors
- User selects which zones receive live cues (UI change in MainUI cue grid)
- Update accumulation buffers for zone-first routing

---

## Phase 9: Preview Manager Cleanup

**Files:** `LaserPreviewManager.cs`

- Clarify `UpdatePreview` takes projector index (rename parameter)
- Optionally add `UpdatePreviewForZone(string zoneId, ...)` convenience
- No renderer changes — renderers remain per-projector (physical nodes in scene)

---

## Edge Cases & Risks

| Risk | Mitigation |
|------|------------|
| Zone targets multiple projectors with different physical positions | Keystone per-zone defines one virtual surface; if different projection is needed per projector, use separate zones |
| Multiple zones target same projector — DMX averaging is lossy | Pre-existing FB4 limitation (one position per frame). Preview handles correctly. Document. |
| Orphaned ZoneIds after zone deletion | Warn user, offer reassign or mute affected blocks |
| Old show files with int-based indices | Migration in LoadShow (Phase 5) handles gracefully |
| Variable track heights break all Y-coordinate math | Centralize in `GetTrackYOffset()` helper, used everywhere |
| Performance: fan-out multiplies points per projector | Reuse buffers, don't allocate per fan-out. Same pattern as existing LiveEngine. |
| Removing SafetyZone loses sub-keystone restriction | Consider keeping optional "inner clip rect" (0-1 within keystone) if users need it |

---

## Implementation Order Summary

| Phase | Scope | Files | Dependency |
|-------|-------|-------|------------|
| 1 | Data model (ZoneId, ProjectorIndices) | ProjectionZone, LaserCueBlock, LaserSystemManager | None |
| 2 | ZoneManager multi-projector | ZoneManager | Phase 1 |
| 3 | PlaybackManager zone routing | PlaybackManager | Phase 1, 2 |
| 4 | Track restructure | TimelineTrack | Phase 1 |
| 5 | Show file migration | PlaybackManager | Phase 1, 3, 4 |
| 6 | Timeline UI zone sub-rows | TimelineUI | Phase 1, 4 |
| 7 | Zone editor panel | ZoneEditorPanel | Phase 1, 2 |
| 8 | Live engine routing | LiveEngine | Phase 1, 2 |
| 9 | Preview manager cleanup | LaserPreviewManager | Phase 3 |

Phases 1-2 are pure foundation. Phase 3 is the core routing change. Phase 6 is the largest UI effort. Phases 7-9 can be done in parallel after Phase 3.
