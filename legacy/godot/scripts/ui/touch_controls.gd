class_name TouchControls
extends Control
## On-screen driving controls for touch devices: two steering pads on the left,
## brake and throttle pedals on the right, plus gear paddles when the manual
## gearbox is selected. Multi-touch aware; drives the regular input actions so
## the rest of the game is input-device agnostic.

const ZONES := {
	&"steer_left": Rect2(0.02, 0.58, 0.13, 0.38),
	&"steer_right": Rect2(0.16, 0.58, 0.13, 0.38),
	&"brake": Rect2(0.70, 0.60, 0.13, 0.36),
	&"throttle": Rect2(0.85, 0.52, 0.13, 0.44),
	&"shift_down": Rect2(0.70, 0.40, 0.13, 0.15),
	&"shift_up": Rect2(0.85, 0.32, 0.13, 0.15),
}
const LABELS := {
	&"steer_left": "◀", &"steer_right": "▶", &"brake": "BRAKE",
	&"throttle": "GAS", &"shift_down": "−", &"shift_up": "+",
}

## touch index -> action currently held by that finger
var _touches: Dictionary = {}
var _font: Font


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_font = ThemeDB.fallback_font


func _active_zones() -> Array:
	var keys := ZONES.keys()
	if Settings.auto_gearbox:
		keys.erase(&"shift_up")
		keys.erase(&"shift_down")
	return keys


func _zone_rect(action: StringName) -> Rect2:
	var r: Rect2 = ZONES[action]
	return Rect2(r.position * size, r.size * size)


func _action_at(pos: Vector2) -> StringName:
	for action: StringName in _active_zones():
		if _zone_rect(action).grow(12.0).has_point(pos):
			return action
	return &""


func _input(event: InputEvent) -> void:
	if not visible:
		return
	if event is InputEventScreenTouch:
		var t := event as InputEventScreenTouch
		if t.pressed:
			_set_touch(t.index, _action_at(t.position))
		else:
			_set_touch(t.index, &"")
	elif event is InputEventScreenDrag:
		var d := event as InputEventScreenDrag
		# Sliding a finger between pads switches action (e.g. left <-> right).
		_set_touch(d.index, _action_at(d.position))


func _set_touch(index: int, action: StringName) -> void:
	var old: StringName = _touches.get(index, &"")
	if old == action:
		return
	if old != &"":
		_touches.erase(index)
		if not _touches.values().has(old):
			Input.action_release(old)
	if action != &"":
		_touches[index] = action
		Input.action_press(action, 1.0)
	queue_redraw()


func release_all() -> void:
	for action: StringName in _touches.values():
		Input.action_release(action)
	_touches.clear()
	queue_redraw()


func _notification(what: int) -> void:
	if what == NOTIFICATION_VISIBILITY_CHANGED and not visible:
		release_all()
	elif what == NOTIFICATION_RESIZED:
		queue_redraw()


func _draw() -> void:
	for action: StringName in _active_zones():
		var r := _zone_rect(action)
		var held := _touches.values().has(action)
		var base := Color(1, 1, 1, 0.28 if held else 0.12)
		if action == &"throttle":
			base = Color(0.3, 0.9, 0.4, 0.45 if held else 0.2)
		elif action == &"brake":
			base = Color(1.0, 0.3, 0.25, 0.45 if held else 0.2)
		draw_rect(r, base, true)
		draw_rect(r, Color(1, 1, 1, 0.35), false, 2.0)
		var text: String = LABELS[action]
		var fs := int(clampf(r.size.y * 0.18, 18.0, 48.0))
		var ts := _font.get_string_size(text, HORIZONTAL_ALIGNMENT_CENTER, -1, fs)
		draw_string(_font, r.get_center() + Vector2(-ts.x * 0.5, ts.y * 0.3), text,
				HORIZONTAL_ALIGNMENT_CENTER, -1, fs, Color(1, 1, 1, 0.8))
