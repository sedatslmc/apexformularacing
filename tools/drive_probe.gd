extends SceneTree
## Diagnostic: runs the autopilot for N seconds and prints telemetry.
## godot --headless --path . --fixed-fps 120 -s res://tools/drive_probe.gd -- [seconds]
## (Untyped on purpose: -s scripts compile before autoload names exist.)

func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var secs := 20
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		secs = int(args[0])
	var s = load("res://scenes/gameplay/race_session.tscn").instantiate()
	s.autopilot_enabled = true
	s.build_environment = false
	s.get_node("Circuit").build_scenery = false
	root.add_child(s)
	for i in 120 * secs:
		await physics_frame
		if i % 60 == 0:
			var c = s.car
			var w = c.wheels
			print("t=%5.1f d=%6.0f v=%5.1f tgt=%5.1f g=%d rpm=%5.0f thr=%.2f brk=%.2f st=%+.2f tc=%s | sr %+.2f %+.2f sa %+.2f %+.2f | Fz %4.0f %4.0f %4.0f %4.0f | %s" % [
				i / 120.0, s.lap_distance, c.forward_speed * 3.6, s.autopilot.target_speed(s.circuit.raw_distance(s.lap_distance)) * 3.6,
				c.gearbox.gear, c.engine_rpm, c.input_throttle, c.input_brake, c.input_steer, c.tc_active,
				w[2].slip_ratio, w[3].slip_ratio, w[0].slip_angle, w[2].slip_angle,
				w[0].load, w[1].load, w[2].load, w[3].load, w[2].surface])
	quit()
