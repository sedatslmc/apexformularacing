extends Control
## Boot scene / main menu. Milestone 1 offers free practice on one circuit.

const SESSION_SCENE := "res://scenes/gameplay/race_session.tscn"


func _ready() -> void:
	var bg := ColorRect.new()
	bg.color = Color(0.06, 0.07, 0.09)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 18)
	add_child(v)
	v.set_anchors_preset(Control.PRESET_CENTER)
	var title := Label.new()
	title.text = "PROJECT APEX"
	title.add_theme_font_size_override("font_size", 96)
	title.add_theme_color_override("font_color", Color(0.93, 0.3, 0.24))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(title)
	var sub := Label.new()
	sub.text = "Prototype — Kestrel Ridge Circuit"
	sub.add_theme_font_size_override("font_size", 30)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(sub)
	var record := Settings.best_lap("kestrel_ridge")
	var rec := Label.new()
	rec.text = "Your record: " + RaceHud.format_time(record)
	rec.add_theme_font_size_override("font_size", 26)
	rec.add_theme_color_override("font_color", Color(0.72, 0.35, 1.0))
	rec.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(rec)
	_button(v, "Free Practice", func() -> void: get_tree().change_scene_to_file(SESSION_SCENE))
	_button(v, "Quit", func() -> void: get_tree().quit())
	await get_tree().process_frame
	v.position = (size - v.size) * 0.5


func _button(parent: Control, text: String, cb: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(520, 96)
	b.add_theme_font_size_override("font_size", 38)
	b.pressed.connect(cb)
	parent.add_child(b)
	if parent.get_child_count() == 4:
		b.grab_focus.call_deferred()
