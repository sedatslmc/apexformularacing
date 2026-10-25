extends Node
## Player settings and persistent records, saved to user://settings.cfg.

signal changed

const PATH := "user://settings.cfg"

var traction_control: bool = true
var abs_enabled: bool = true
var steering_assist: bool = true
var auto_gearbox: bool = true
var show_touch_controls: bool = false
var show_telemetry: bool = false
var camera_mode: int = 0
var master_volume: float = 0.8

var _best_laps: Dictionary = {}


func _ready() -> void:
	show_touch_controls = DisplayServer.is_touchscreen_available()
	load_settings()


func load_settings() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(PATH) != OK:
		return
	traction_control = cfg.get_value("assists", "traction_control", traction_control)
	abs_enabled = cfg.get_value("assists", "abs", abs_enabled)
	steering_assist = cfg.get_value("assists", "steering_assist", steering_assist)
	auto_gearbox = cfg.get_value("assists", "auto_gearbox", auto_gearbox)
	show_touch_controls = cfg.get_value("ui", "touch_controls", show_touch_controls)
	show_telemetry = cfg.get_value("ui", "telemetry", show_telemetry)
	camera_mode = cfg.get_value("ui", "camera_mode", camera_mode)
	master_volume = cfg.get_value("audio", "master_volume", master_volume)
	_best_laps = cfg.get_value("records", "best_laps", {})


func save_settings() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("assists", "traction_control", traction_control)
	cfg.set_value("assists", "abs", abs_enabled)
	cfg.set_value("assists", "steering_assist", steering_assist)
	cfg.set_value("assists", "auto_gearbox", auto_gearbox)
	cfg.set_value("ui", "touch_controls", show_touch_controls)
	cfg.set_value("ui", "telemetry", show_telemetry)
	cfg.set_value("ui", "camera_mode", camera_mode)
	cfg.set_value("audio", "master_volume", master_volume)
	cfg.set_value("records", "best_laps", _best_laps)
	cfg.save(PATH)
	changed.emit()


func best_lap(track_id: String) -> float:
	return float(_best_laps.get(track_id, 0.0))


## Stores the lap if it beats the saved record. Returns true when it did.
func submit_lap(track_id: String, lap_time: float) -> bool:
	var current := best_lap(track_id)
	if current > 0.0 and lap_time >= current:
		return false
	_best_laps[track_id] = lap_time
	save_settings()
	return true
