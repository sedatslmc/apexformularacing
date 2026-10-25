@echo off
rem Runs the headless test suite on Windows. Set GODOT to your Godot executable
rem (e.g. set GODOT=C:\Godot\Godot_v4.7.2-stable_win64_console.exe).
if "%GODOT%"=="" set GODOT=godot
cd /d "%~dp0\.."
"%GODOT%" --headless --path . --fixed-fps 120 -s res://tests/run_tests.gd -- %*
