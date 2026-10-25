class_name RaceHud
extends CanvasLayer
## In-race heads-up display: timing, sectors, speed/gear/RPM, assists,
## messages and an optional telemetry panel. Built in code so layout scales
## with the canvas_items stretch mode on any landscape aspect ratio.

signal pause_pressed

const COLOR_BEST := Color(0.72, 0.35, 1.0)
const COLOR_FASTER := Color(0.3, 0.9, 0.4)
const COLOR_SLOWER := Color(1.0, 0.82, 0.25)
const COLOR_INVALID := Color(1.0, 0.3, 0.25)
const PANEL_BG := Color(0.04, 0.05, 0.07, 0.62)

var car: FormulaCar
var timer: LapTimer

var _lap_label: Label
var _time_label: Label
var _delta_label: Label
var _last_label: Label
var _best_label: Label
var _record_label: Label
var _sector_labels: Array[Label] = []
var _speed_label: Label
var _gear_label: Label
var _rpm_bar: ProgressBar
var _rpm_fill: StyleBoxFlat
var _assist_label: Label
var _message_label: Label
var _fps_label: Label
var _telemetry: Label
var _message_time: float = 0.0
var _lap_distance: float = 0.0


func _ready() -> void:
	layer = 10
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	_build_timing(root)
	_build_dash(root)
	_build_misc(root)


func _panel(parent: Control, preset: int, pos: Vector2, size: Vector2) -> PanelContainer:
	var p := PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = PANEL_BG
	sb.set_corner_radius_all(10)
	sb.set_content_margin_all(14)
	p.add_theme_stylebox_override("panel", sb)
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(p)
	p.set_anchors_preset(preset)
	p.position = pos
	p.custom_minimum_size = size
	return p


func _label(parent: Node, text: String, size: int, color: Color = Color.WHITE) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	l.add_theme_constant_override("outline_size", 4)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l


func _build_timing(root: Control) -> void:
	var p := _panel(root, Control.PRESET_TOP_LEFT, Vector2(24, 24), Vector2(360, 0))
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 2)
	p.add_child(v)
	_lap_label = _label(v, "OUT LAP", 26, Color(0.8, 0.85, 0.9))
	_time_label = _label(v, "-:--.---", 54)
	_delta_label = _label(v, "", 30)
	var sectors := HBoxContainer.new()
	sectors.add_theme_constant_override("separation", 10)
	v.add_child(sectors)
	for i in 3:
		var l := _label(sectors, "S%d --.---" % (i + 1), 22, Color(0.75, 0.75, 0.75))
		_sector_labels.append(l)
	_last_label = _label(v, "Last  -:--.---", 26)
	_best_label = _label(v, "Best  -:--.---", 26, COLOR_BEST)
	_record_label = _label(v, "Record -:--.---", 20, Color(0.7, 0.7, 0.75))


func _build_dash(root: Control) -> void:
	var p := _panel(root, Control.PRESET_CENTER_BOTTOM, Vector2(-190, -190), Vector2(380, 0))
	var v := VBoxContainer.new()
	v.alignment = BoxContainer.ALIGNMENT_CENTER
	p.add_child(v)
	_rpm_bar = ProgressBar.new()
	_rpm_bar.min_value = 4000
	_rpm_bar.max_value = 12500
	_rpm_bar.show_percentage = false
	_rpm_bar.custom_minimum_size = Vector2(350, 16)
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(1, 1, 1, 0.12)
	bg.set_corner_radius_all(4)
	_rpm_fill = StyleBoxFlat.new()
	_rpm_fill.bg_color = COLOR_FASTER
	_rpm_fill.set_corner_radius_all(4)
	_rpm_bar.add_theme_stylebox_override("background", bg)
	_rpm_bar.add_theme_stylebox_override("fill", _rpm_fill)
	_rpm_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	v.add_child(_rpm_bar)
	var h := HBoxContainer.new()
	h.alignment = BoxContainer.ALIGNMENT_CENTER
	h.add_theme_constant_override("separation", 28)
	v.add_child(h)
	_gear_label = _label(h, "N", 72, COLOR_SLOWER)
	var sv := VBoxContainer.new()
	h.add_child(sv)
	_speed_label = _label(sv, "0", 56)
	_label(sv, "km/h", 20, Color(0.7, 0.7, 0.7))
	_assist_label = _label(v, "", 18, Color(0.6, 0.8, 1.0))
	_assist_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


func _build_misc(root: Control) -> void:
	_message_label = _label(root, "", 40, COLOR_SLOWER)
	_message_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_message_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_message_label.position = Vector2(-400, 40)
	_message_label.size = Vector2(800, 60)
	_fps_label = _label(root, "", 18, Color(0.7, 0.7, 0.7))
	_fps_label.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_fps_label.position = Vector2(-260, 30)
	var pause := Button.new()
	pause.text = "II"
	pause.add_theme_font_size_override("font_size", 34)
	pause.custom_minimum_size = Vector2(90, 90)
	pause.focus_mode = Control.FOCUS_NONE
	root.add_child(pause)
	pause.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	pause.position = Vector2(-114, 24)
	pause.pressed.connect(func() -> void: pause_pressed.emit())
	var tp := _panel(root, Control.PRESET_CENTER_RIGHT, Vector2(-430, -170), Vector2(400, 0))
	_telemetry = _label(tp, "", 17, Color(0.85, 0.9, 0.95))
	tp.visible = Settings.show_telemetry
	_telemetry.set_meta(&"panel", tp)


func bind(new_car: FormulaCar, new_timer: LapTimer, record: float) -> void:
	car = new_car
	timer = new_timer
	timer.lap_started.connect(_on_lap_started)
	timer.lap_completed.connect(_on_lap_completed)
	timer.sector_completed.connect(_on_sector_completed)
	timer.lap_invalidated.connect(func(reason: String) -> void: show_message("Lap invalidated: " + reason, COLOR_INVALID))
	set_record(record)


func set_record(record: float) -> void:
	_record_label.text = "Record " + format_time(record)


func set_lap_distance(d: float) -> void:
	_lap_distance = d


func toggle_telemetry() -> void:
	var tp: Control = _telemetry.get_meta(&"panel")
	tp.visible = not tp.visible
	Settings.show_telemetry = tp.visible


func show_message(text: String, color: Color = COLOR_SLOWER, duration: float = 2.5) -> void:
	_message_label.text = text
	_message_label.add_theme_color_override("font_color", color)
	_message_time = duration


static func format_time(t: float) -> String:
	if t <= 0.0 or is_nan(t):
		return "-:--.---"
	var ms := int(round(t * 1000.0))
	return "%d:%02d.%03d" % [ms / 60000, (ms / 1000) % 60, ms % 1000]


static func format_delta(d: float) -> String:
	if is_nan(d):
		return ""
	return ("+" if d >= 0.0 else "-") + "%.3f" % absf(d)


func _process(delta: float) -> void:
	_fps_label.text = "%d FPS" % Engine.get_frames_per_second()
	if _message_time > 0.0:
		_message_time -= delta
		if _message_time <= 0.0:
			_message_label.text = ""
	if car == null or timer == null:
		return
	_speed_label.text = "%d" % int(round(absf(car.forward_speed) * 3.6))
	var g := car.gearbox.gear
	_gear_label.text = "R" if g < 0 else ("N" if g == 0 else str(g))
	_rpm_bar.value = car.engine_rpm
	var shift_frac := clampf((car.engine_rpm - 10500.0) / 1800.0, 0.0, 1.0)
	_rpm_fill.bg_color = COLOR_FASTER.lerp(COLOR_INVALID, shift_frac)
	var assists := PackedStringArray()
	if car.tc_active:
		assists.append("TC")
	if car.abs_active:
		assists.append("ABS")
	_assist_label.text = "  ".join(assists)
	if timer.lap_active:
		_time_label.text = format_time(timer.current_lap_time())
		_time_label.add_theme_color_override("font_color", Color.WHITE if timer.lap_valid else COLOR_INVALID)
		var d := timer.live_delta(_lap_distance)
		_delta_label.text = format_delta(d)
		_delta_label.add_theme_color_override("font_color", COLOR_FASTER if d < 0.0 else COLOR_INVALID)
	if _telemetry.get_meta(&"panel").visible:
		_update_telemetry()


func _update_telemetry() -> void:
	var lines := PackedStringArray()
	lines.append("rpm %5d  thr %.2f  brk %.2f  str %+.2f" % [car.engine_rpm, car.input_throttle, car.input_brake, car.input_steer])
	lines.append("downforce %5.0f N  drag %4.0f N" % [car.downforce, car.drag_force])
	for w in car.wheels:
		lines.append("%s %5.0fN  sr %+.2f  sa %+5.1f°  %s" % [
			w.label, w.load, w.slip_ratio, rad_to_deg(w.slip_angle), w.surface])
	_telemetry.text = "\n".join(lines)


func _on_lap_started(n: int) -> void:
	_lap_label.text = "LAP %d" % n
	for l in _sector_labels:
		l.add_theme_color_override("font_color", Color(0.75, 0.75, 0.75))


func _on_lap_completed(_n: int, lap_time: float, valid: bool, pb: bool) -> void:
	_last_label.text = "Last  " + format_time(lap_time) + ("" if valid else "  (invalid)")
	_last_label.add_theme_color_override("font_color", Color.WHITE if valid else COLOR_INVALID)
	_best_label.text = "Best  " + format_time(timer.best_lap_time)
	if pb:
		show_message("Best lap  " + format_time(lap_time), COLOR_BEST)


func _on_sector_completed(index: int, sector_time: float, delta_to_best: float) -> void:
	if index >= _sector_labels.size():
		return
	var l := _sector_labels[index]
	l.text = "S%d %.3f" % [index + 1, sector_time]
	var color := COLOR_SLOWER
	if not timer.lap_valid:
		color = COLOR_INVALID
	elif is_nan(delta_to_best) or delta_to_best <= 0.0:
		color = COLOR_BEST if is_equal_approx(timer.best_sector_times[index], sector_time) else COLOR_FASTER
	l.add_theme_color_override("font_color", color)
