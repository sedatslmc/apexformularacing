class_name PauseMenu
extends CanvasLayer
## Pause overlay with session actions and driving-assist toggles.

signal resume_requested
signal restart_requested
signal reset_car_requested
signal quit_requested
signal settings_changed

var _panel: PanelContainer


func _ready() -> void:
	layer = 20
	process_mode = Node.PROCESS_MODE_ALWAYS
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	_panel = PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.07, 0.08, 0.1, 0.95)
	sb.set_corner_radius_all(14)
	sb.set_content_margin_all(28)
	_panel.add_theme_stylebox_override("panel", sb)
	add_child(_panel)
	_panel.set_anchors_preset(Control.PRESET_CENTER)
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 12)
	_panel.add_child(v)
	var title := Label.new()
	title.text = "PAUSED"
	title.add_theme_font_size_override("font_size", 48)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(title)
	_button(v, "Resume", func() -> void: resume_requested.emit())
	_button(v, "Reset car to track", func() -> void: reset_car_requested.emit())
	_button(v, "Restart session", func() -> void: restart_requested.emit())
	v.add_child(HSeparator.new())
	_toggle(v, "Traction control", Settings.traction_control, func(on: bool) -> void: Settings.traction_control = on)
	_toggle(v, "ABS", Settings.abs_enabled, func(on: bool) -> void: Settings.abs_enabled = on)
	_toggle(v, "Steering assist", Settings.steering_assist, func(on: bool) -> void: Settings.steering_assist = on)
	_toggle(v, "Automatic gearbox", Settings.auto_gearbox, func(on: bool) -> void: Settings.auto_gearbox = on)
	_toggle(v, "Touch controls", Settings.show_touch_controls, func(on: bool) -> void: Settings.show_touch_controls = on)
	v.add_child(HSeparator.new())
	_button(v, "Main menu", func() -> void: quit_requested.emit())
	visible = false


func _button(parent: Control, text: String, cb: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(460, 76)
	b.add_theme_font_size_override("font_size", 30)
	b.pressed.connect(cb)
	parent.add_child(b)


func _toggle(parent: Control, text: String, value: bool, cb: Callable) -> void:
	var c := CheckButton.new()
	c.text = text
	c.button_pressed = value
	c.custom_minimum_size = Vector2(460, 64)
	c.add_theme_font_size_override("font_size", 28)
	c.toggled.connect(func(on: bool) -> void:
		cb.call(on)
		Settings.save_settings()
		settings_changed.emit())
	parent.add_child(c)


func open() -> void:
	visible = true
	# Centre after layout is known.
	_panel.reset_size()
	_panel.position = (_panel.get_viewport_rect().size - _panel.size) * 0.5


func close() -> void:
	visible = false
