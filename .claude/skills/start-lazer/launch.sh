#!/bin/bash
# Launch script used by start-lazer skill
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# Skill lives in .claude/skills/start-lazer, project root is 3 levels up
PROJECT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
cd "$PROJECT_DIR"

# --- Find Godot ---
GODOT_EXE=""

if [ -n "$GODOT_HOME" ]; then
    for f in "$GODOT_HOME"/Godot_v*.exe "$GODOT_HOME"/godot.exe "$GODOT_HOME"/godot; do
        [ -f "$f" ] || continue
        case "$(basename "$f")" in *console*) continue;; esac
        GODOT_EXE="$f"
        break
    done
fi

if [ -z "$GODOT_EXE" ] && command -v godot &>/dev/null; then
    GODOT_EXE="godot"
fi

if [ -z "$GODOT_EXE" ]; then
    for dir in \
        "$USERPROFILE/Tools/Godot" \
        "$HOME/Tools/Godot" \
        "$LOCALAPPDATA/Godot" \
        "/c/Program Files/Godot" \
        "/c/Program Files (x86)/Godot" \
        "$HOME/.local/share/godot" \
        "/usr/local/bin"; do
        [ -d "$dir" ] || continue
        for f in $(find "$dir" -maxdepth 2 \( -name 'Godot_v*.exe' -o -name 'godot.exe' -o -name 'godot' \) 2>/dev/null); do
            case "$(basename "$f")" in *console*) continue;; esac
            GODOT_EXE="$f"
            break 2
        done
    done
fi

if [ -z "$GODOT_EXE" ]; then
    echo "ERROR: Could not find Godot. Set GODOT_HOME or add godot to PATH."
    exit 1
fi

echo "Found Godot: $GODOT_EXE"

# --- Build ---
echo "Building C# project (Debug)..."
dotnet build -c Debug

# --- Launch ---
mkdir -p logs

MODE="standalone"
if [ "$1" = "--editor" ] || [ "$1" = "-e" ]; then
    MODE="editor"
fi

if [ "$MODE" = "editor" ]; then
    echo "Launching Godot editor..."
    "$GODOT_EXE" --path "$PROJECT_DIR" -e --verbose > logs/lazer.log 2>&1 &
else
    echo "Launching LazerSystem..."
    "$GODOT_EXE" --path "$PROJECT_DIR" --verbose > logs/lazer.log 2>&1 &
fi

echo "PID: $!"
echo "Logs: logs/lazer.log"
