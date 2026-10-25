#!/usr/bin/env sh
# Runs the headless test suite. Optional first argument filters tests by name.
# --fixed-fps decouples simulation from wall-clock time so physics tests run
# much faster than real time and are deterministic.
GODOT="${GODOT:-godot}"
cd "$(dirname "$0")/.." || exit 1
exec "$GODOT" --headless --path . --fixed-fps 120 -s res://tests/run_tests.gd -- "$@"
