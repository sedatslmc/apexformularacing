extends TestCase
## Full-physics tests. Run with --fixed-fps 120 so simulation runs faster than
## real time (see tools/run_tests.sh).

const SESSION := preload("res://scenes/gameplay/race_session.tscn")


func _spawn(autopilot: bool) -> RaceSession:
	var s: RaceSession = SESSION.instantiate()
	s.autopilot_enabled = autopilot
	s.build_environment = false
	s.get_node("Circuit").build_scenery = false
	tree.root.add_child(s)
	return s


func _steps(n: int) -> void:
	for i in n:
		await tree.physics_frame


func test_car_settles_on_grid() -> void:
	var s := _spawn(false)
	await _steps(360)
	var car := s.car
	check(car.speed < 0.3, "car at rest (speed %.2f)" % car.speed)
	check(car.global_transform.basis.y.dot(Vector3.UP) > 0.99, "car upright")
	var total := 0.0
	for w in car.wheels:
		check(w.contact, "wheel %s in contact" % w.label)
		total += w.load
	check_near(total, car.car_mass * 9.81, car.car_mass * 9.81 * 0.05, "wheel loads carry the car's weight")
	var front := car.wheels[0].load + car.wheels[1].load
	check_near(front / total, car.weight_front, 0.03, "static weight distribution")
	s.queue_free()
	await _steps(2)


func test_standing_start_acceleration() -> void:
	var s := _spawn(false)
	s.player_input_enabled = false
	await _steps(120)
	var car := s.car
	var t := 0.0
	var t100 := -1.0
	var t200 := -1.0
	var dt := 1.0 / 120.0
	# Hold full throttle on the main straight with the TC on.
	var raw := s.circuit.geometry
	for i in 120 * 9:
		car.input_throttle = 1.0
		car.input_brake = 0.0
		car.input_steer = 0.0
		# Keep straight with a light heading correction so the test measures
		# acceleration, not steering.
		var proj := raw.project(car.global_position)
		var local := car.global_transform.affine_inverse() * raw.position_at(proj.x + 30.0)
		car.input_steer = clampf(local.x * 0.1, -0.3, 0.3)
		await tree.physics_frame
		t += dt
		var kmh := car.forward_speed * 3.6
		if t100 < 0.0 and kmh >= 100.0:
			t100 = t
		if t200 < 0.0 and kmh >= 200.0:
			t200 = t
	print("        0-100 km/h %.2f s, 0-200 km/h %.2f s, speed after 9 s %.0f km/h" % [t100, t200, car.forward_speed * 3.6])
	check_range(t100, 2.0, 3.6, "0-100 km/h time")
	check_range(t200, 4.0, 7.5, "0-200 km/h time")
	s.queue_free()
	await _steps(2)


func test_autopilot_completes_valid_lap() -> void:
	var s := _spawn(true)
	var laps: Array = []
	s.lap_recorded.connect(func(lt: float, valid: bool) -> void: laps.append([lt, valid]))
	var max_speed := 0.0
	var steps := 0
	var limit := 120 * 260
	while laps.size() < 2 and steps < limit:
		await tree.physics_frame
		steps += 1
		max_speed = maxf(max_speed, s.car.forward_speed)
		if steps % (120 * 20) == 0:
			print("        t=%3ds lap_dist=%5.0f speed=%3.0f km/h gear=%d" % [
				steps / 120, s.lap_distance, s.car.forward_speed * 3.6, s.car.gearbox.gear])
	print("        laps: %s  top speed %.0f km/h" % [str(laps), max_speed * 3.6])
	check(laps.size() >= 2, "autopilot completed two laps (got %d)" % laps.size())
	if laps.size() >= 2:
		var flying: Array = laps[1]
		check(flying[1], "flying lap is valid")
		check_range(flying[0], 70.0, 120.0, "flying lap time")
	check_range(max_speed * 3.6, 260.0, 360.0, "top speed")
	s.queue_free()
	await _steps(2)
