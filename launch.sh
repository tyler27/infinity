#!/bin/bash

# Find Godot executable
# Priority: GODOT_HOME env var > PATH > common locations

find_godot() {
    if [ -n "$GODOT_HOME" ]; then
        for f in "$GODOT_HOME"/Godot_v*.exe "$GODOT_HOME"/godot "$GODOT_HOME"/godot.exe; do
            if [ -f "$f" ] && [[ ! "$(basename "$f")" =~ console ]]; then
                echo "$f"; return
            fi
        done
    fi

    if command -v godot &>/dev/null; then
        echo "godot"; return
    fi

    # Scan common locations
    local dirs=(
        "$USERPROFILE/Tools/Godot"
        "$HOME/Tools/Godot"
        "$LOCALAPPDATA/Godot"
        "/c/Program Files/Godot"
        "/c/Program Files (x86)/Godot"
        "$HOME/.local/share/godot"
        "/usr/local/bin"
    )
    for dir in "${dirs[@]}"; do
        [ -d "$dir" ] || continue
        while IFS= read -r f; do
            if [[ ! "$(basename "$f")" =~ console ]]; then
                echo "$f"; return
            fi
        done < <(find "$dir" -maxdepth 2 -name 'Godot_v*.exe' -o -name 'godot' 2>/dev/null)
    done
}

GODOT_EXE="$(find_godot)"

if [ -z "$GODOT_EXE" ]; then
    echo "Error: Could not find Godot executable."
    echo "Set GODOT_HOME to the directory containing the Godot executable, or add it to PATH."
    exit 1
fi

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
"$GODOT_EXE" --path "$PROJECT_DIR" -e &
