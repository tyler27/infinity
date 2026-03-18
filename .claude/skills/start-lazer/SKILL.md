---
name: start-lazer
description: Build and launch the LazerSystem Godot project. Use this skill whenever the user wants to start, run, launch, or open the lazer system — whether they want the editor or a standalone run. Also use when they say things like "fire it up", "boot up the project", "start the app", "open godot", or "rebuild and run".
---

# Start Lazer System

Run the launch script to build and start the lazer system. Do not ask the user any questions — just run it.

The launch script is bundled with this skill at `start-lazer/launch.sh` (relative to the skills directory).

## Default (run the project)

```bash
bash <skill-dir>/launch.sh
```

## If the user asked for the editor

```bash
bash <skill-dir>/launch.sh --editor
```

Replace `<skill-dir>` with the actual path to this skill's directory (the directory containing this SKILL.md).

That's it. The script handles finding Godot, building with `dotnet build -c Debug`, creating the logs directory, and launching in the background with output going to `logs/lazer.log`.

After the script finishes, tell the user it's running and remind them logs are at `logs/lazer.log`.
