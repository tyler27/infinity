# LazerSystem — Codebase Audit & Next Steps

*Generated 2026-03-18 from full codebase audit (6 parallel agents across all subsystems)*

---

## Current State Summary

The system is a Godot 4 C# laser show controller with:
- **Live performance engine** — 6x10 cue grid, 256 pages, 4 FB4 projectors via ArtNet
- **Timeline editor** — multi-track blocks, automation envelopes, undo/redo, markers, loop regions
- **3D preview** — volumetric haze rendering, keystone correction, zone transforms
- **ArtNet output** — UDP broadcast, FB4 DMX channel mapping, node discovery

3 phases of timeline work are complete (basic playback, editing power, automation). The codebase is functional but has bugs, performance gaps, and missing validation that should be addressed before live venue use.

---

## Critical Bugs (Fix First)

### 1. Division by zero in automation evaluation
**`PlaybackManager.cs:179`** — `(currentTime - block.StartTime) / block.Duration` crashes if Duration is 0.
```
Fix: Guard with if (block.Duration <= 0f) continue;
```

### 2. WavePattern renders duplicate first point
**`WavePattern.cs:44-49`** — First point emits both a blanked AND colored point (67 points instead of 64).
```
Fix: Add else — if (i == 0) { blank } else { colored } or skip the blank entirely
```

### 3. Off-by-one in pattern point counts
**`LinePattern.cs:37`, `CirclePattern.cs:36`, `WavePattern.cs:31`** — All use `i <= PointCount` producing one extra point.
```
Fix: Change to i < PointCount in all three patterns
```

### 4. ZoneBoundaryDisplay plane coordinates don't match renderer
**`ZoneBoundaryDisplay.cs:19-23`** — Uses `PlaneZ = -20f` but LaserPreviewRenderer raycasts to `z = -25`.
```
Fix: Sync constants or read from shared config
```

### 5. ArtPoll reply buffer overread
**`ArtNetManager.cs:396`** — Checks `data.Length >= 19` but reads `data[19]`. Needs `>= 20`.

### 6. AddKeyframeCommand undo can remove wrong keyframe
**`AutomationCommands.cs:19-24`** — Uses `FindKeyframeNear` after insert which may match wrong keyframe if two are close in time.
```
Fix: Store the actual inserted index from the sorted insert, or store/match by reference
```

### 7. Cross product zero-vector in beam rendering
**`LaserPreviewRenderer.cs:245`** — When beam direction is parallel to camera, cross product is zero, producing NaN vertices.
```
Fix: Check side.LengthSquared() < epsilon and use fallback normal
```

---

## Performance Issues (High Impact)

### 8. TimelineUI redraws every frame unconditionally
**`TimelineUI.cs:176`** — `_Process` calls `QueueRedraw()` every frame even when nothing changed.
```
Fix: Dirty flag — only redraw when state changes (selection, drag, scroll, playback position)
```

### 9. Per-frame allocations in PlaybackManager.EvaluateTimeline
**`PlaybackManager.cs:148-202`** — Creates new `Dictionary`, `List<LaserPoint>`, and copies every frame.
```
Fix: Pre-allocate and reuse containers like LiveEngine does
```

### 10. GetActiveBlocks allocates new List every call
**`TimelineTrack.cs:53-66`** — Called per track per frame. Should reuse a cached list or use binary search on sorted blocks.

### 11. O(n^2) point blending
**`PlaybackManager.cs:320-353`** — `BlendPointsAdditive` does nested loop comparing every point pair.
```
Fix: Spatial hash or skip blending for non-overlapping zones
```

### 12. Unbounded undo stack
**`UndoManager.cs`** — No max depth. Long sessions accumulate unbounded memory.
```
Fix: Add configurable max depth (e.g., 200), drop oldest commands
```

---

## Safety & Reliability

### 13. Projector enable not enforced in DMX send
**`LiveEngine.cs:536`** — `SendProjectorDmx` doesn't check `ProjectorEnabled[]`. Disabled projectors still get DMX.
```
Fix: Check ProjectorEnabled[projectorIndex] and send blackout instead
```

### 14. Safety zones are visualization-only, never enforced
**`ZoneManager.cs`** — `IsInSafetyZone()` and `ClampToSafety()` exist but are never called.
```
Decision needed: Enforce safety zones on output, or document they're advisory only
```

### 15. No socket reconnection backoff
**`ArtNetManager.cs:329-335`** — On socket error, immediately closes and reopens. Rapid failures = spam.
```
Fix: Exponential backoff or rate-limit reconnection attempts
```

### 16. Thread-unsafe ArtNet sequence counter
**`ArtNetPacket.cs:24`** — Global static `_sequence` shared across all universes, non-atomic increment.
```
Fix: Per-universe sequence tracking or Interlocked.Increment
```

### 17. GetNode crash risk in LiveEngine._Ready
**`LiveEngine.cs:179-183`** — Uses `GetNode<>()` instead of `GetNodeOrNull<>()`. Missing nodes = crash.
```
Fix: Replace with GetNodeOrNull and log warnings
```

---

## Missing Validation

### 18. Block Duration can be zero or negative
**`LaserCueBlock.cs:11`** — No minimum enforcement. Causes division by zero in automation.
```
Fix: Clamp to minimum (e.g., 0.01f) in setter or at evaluation time
```

### 19. Automation keyframe Time not bounded to 0-1
**`AutomationKeyframe.cs:9`** — Can be set to any float; breaks binary search and evaluation.
```
Fix: Clamp in setter or InsertKeyframe
```

### 20. FadeIn + FadeOut can exceed Duration
No validation prevents this — block is always fading with unexpected intensity.
```
Fix: Clamp in inspector or warn user
```

### 21. IP address fields not validated
**`ProjectorConfig.cs:10`** — String IP accepted without parsing validation.

### 22. BPM field accepts zero/negative
No guard against invalid BPM which would cause division by zero in grid snapping.

---

## Architecture Gaps

### 23. Hardcoded 4-projector limit everywhere
`LiveEngine`, `ArtNetManager`, `ZoneManager`, `LaserPreviewManager`, `DemoController` all hardcode `4`. Should be a central constant.

### 24. PlaybackManager._tracks vs tracks export desync
Internal list and Godot export array can drift. No single source of truth.

### 25. UndoManager not tied to Godot lifecycle
Static singleton created outside scene tree. Not cleared between show loads.

### 26. ClipboardManager state persists across shows
Pasting blocks from a different show may reference invalid zone indices.

### 27. Sync source switching has undefined behavior
Changing from Internal to MIDI mid-playback doesn't sync clocks.

---

## Feature Gaps & Next Phase Candidates

### Timeline Phase 4: Production Polish
- [ ] **Track cross-fading** — overlapping blocks on same track blend instead of layer
- [ ] **Block snapping to other blocks** — magnetic edges when dragging near adjacent blocks
- [ ] **Time signature support** — 3/4, 6/8, etc. (currently assumes 4/4)
- [ ] **Tempo changes** — BPM automation over time
- [ ] **Track groups/folders** — collapse related tracks
- [ ] **Block color coding** — visual categories beyond pattern color
- [ ] **Zoom to fit** — keyboard shortcut to zoom to show all blocks
- [ ] **Minimap** — overview of entire timeline for navigation

### Live Performance
- [ ] **Cue grid drag-to-timeline** — event defined but never wired
- [ ] **Live cue duration/auto-stop** — currently play forever until manual stop
- [ ] **Flash mode release** — keyboard key-up should release flash cues
- [ ] **MIDI input for cue triggering** — hardware controller support
- [ ] **OSC input/output** — integration with other show control software
- [ ] **Cue grid right-click context menu** — edit, copy, delete cues

### Pattern System
- [ ] **ILDA file playback** — CustomILDA type exists but no loader
- [ ] **Spiral/Lissajous patterns** — mathematical curves
- [ ] **Grid/matrix pattern** — rectangular array
- [ ] **TextPattern lowercase + punctuation** — currently A-Z, 0-9 only
- [ ] **Pattern preview in cue inspector** — preview button is stubbed

### Output & Safety
- [ ] **Safety zone enforcement** — actually clamp/blank points outside zones
- [ ] **Per-universe send rate control** — configurable Hz per projector
- [ ] **ArtNet heartbeat/keepalive** — maintain connection health
- [ ] **DMX overflow detection & telemetry** — warn when clipping
- [ ] **Show file versioning** — schema migration for .tres resources

### UI
- [ ] **Keyboard shortcut overlay/help** — discoverable hotkeys
- [ ] **Tooltips on all controls** — especially automation, keystone
- [ ] **Theme switching** — light/dark mode
- [ ] **Fullscreen mode**
- [ ] **Settings persistence** — panel positions, zoom level, view state
- [ ] **Input validation feedback** — red borders on invalid fields

---

## Recommended Priority Order

### Tomorrow (Critical Fixes)
1. Guard `block.Duration <= 0` in PlaybackManager (crash fix)
2. Fix WavePattern duplicate first point + off-by-one in Line/Circle/Wave
3. Fix ArtPoll buffer overread (`>= 20`)
4. Replace `GetNode` with `GetNodeOrNull` in LiveEngine/DemoController
5. Fix beam renderer zero-vector cross product

### This Week (Performance & Stability)
6. Dirty-flag TimelineUI._Draw (stop redrawing every frame)
7. Pre-allocate containers in PlaybackManager.EvaluateTimeline
8. Cache GetActiveBlocks result (reuse list, binary search)
9. Add undo stack depth limit
10. Enforce projector enable in DMX send path

### Next Week (Validation & Polish)
11. Clamp automation keyframe times to 0-1
12. Validate FadeIn + FadeOut <= Duration
13. Add socket reconnection backoff
14. Centralize projector count constant
15. Sync ZoneBoundaryDisplay plane with renderer constants

### Ongoing (Feature Work)
16. ILDA file loading
17. MIDI cue triggering
18. Live cue auto-stop
19. Track groups
20. Show file versioning

---

## Files Modified in Phase 3 (for reference)

| File | Changes |
|------|---------|
| `scripts/Core/Models/AutomationCurveType.cs` | New — curve type enum |
| `scripts/Core/Models/AutomationKeyframe.cs` | New — keyframe resource |
| `scripts/Core/Models/AutomationLane.cs` | New — lane with evaluate/interpolate |
| `scripts/Core/Models/AutomationData.cs` | New — container of lanes |
| `scripts/Core/Models/AutomatableParameter.cs` | New — parameter registry |
| `scripts/Timeline/AutomationEvaluator.cs` | New — applies automation to params |
| `scripts/Timeline/Commands/AutomationCommands.cs` | New — keyframe undo commands |
| `scripts/Timeline/Commands/BlockCommands.cs` | Added CompoundCommand |
| `scripts/Timeline/ClipboardManager.cs` | Added multi-block copy/paste |
| `scripts/Core/Models/LaserCueBlock.cs` | Added Automation field + clone |
| `scripts/Timeline/PlaybackManager.cs` | Automation evaluation in pipeline |
| `scripts/Sync/SyncManager.cs` | Play from loop start after pause |
| `scripts/UI/TimelineUI.cs` | Automation mode, multi-select, rubber-band, overlap prevention, loop selection, Ctrl+D deselect |
| `scripts/UI/BlockInspectorPanel.cs` | Auto-key, automation dropdown, color auto-key |
| `scripts/UI/MainUI.cs` | Disabled cue hotkeys in timeline view |
