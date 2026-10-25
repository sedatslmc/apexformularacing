extends TestCase

const LENGTH := 1000.0


## Simulated car state (doubles; Vector2 would truncate to 32-bit floats).
var _dist := 0.0
var _time := 0.0


func _start(t: LapTimer, d: float) -> void:
	_dist = d
	_time = 0.0
	t.update(fposmod(d, LENGTH), 0.0)


## Drives at constant speed until the unwrapped distance reaches to_d.
func _drive(t: LapTimer, to_d: float, speed: float, dt: float = 1.0 / 120.0) -> void:
	var d := _dist
	var time := _time
	while d < to_d:
		d += speed * dt
		time += dt
		t.update(fposmod(d, LENGTH), time)
	_dist = d
	_time = time


func test_lap_time_is_interpolated_exactly() -> void:
	var t := LapTimer.new(LENGTH, PackedFloat32Array([0.0, 300.0, 700.0]))
	var laps: Array = []
	t.lap_completed.connect(func(_n: int, lt: float, _v: bool, _pb: bool) -> void: laps.append(lt))
	# Start 10 m behind the line at 50 m/s; constant speed -> lap = 20 s exactly.
	_start(t, LENGTH - 10.0)
	_drive(t, 2.0 * LENGTH + 5.0, 50.0)
	check(laps.size() == 1, "one completed lap (got %d)" % laps.size())
	if laps.size() == 1:
		check_near(laps[0], 20.0, 1e-6, "sub-tick interpolated lap time")
	check_near(t.completed_laps[0]["sectors"][0], 6.0, 1e-6, "sector 1 time")


func test_sectors_must_be_in_order() -> void:
	var t := LapTimer.new(LENGTH, PackedFloat32Array([0.0, 300.0, 700.0]))
	var completed := [0]
	t.lap_completed.connect(func(_n: int, _lt: float, _v: bool, _pb: bool) -> void: completed[0] += 1)
	_start(t, LENGTH - 5.0)
	_drive(t, LENGTH + 100.0, 50.0)
	# Drive backwards-in-distance (a shortcut) to just before the line, in
	# small steps so it is not treated as a teleport: must not count as a lap.
	var time := _time
	for k in 51:
		time += 0.1
		t.update(fposmod(LENGTH + 100.0 - k * 2.0 - 2.0, LENGTH), time)
	_dist = LENGTH - 4.0
	_time = time
	_drive(t, LENGTH + 10.0, 50.0)
	check(completed[0] == 0, "shortcut lap not counted")


func test_reverse_over_line_does_not_count() -> void:
	var t := LapTimer.new(LENGTH)
	var started := [0]
	t.lap_started.connect(func(_n: int) -> void: started[0] += 1)
	t.update(5.0, 0.0)
	t.update(LENGTH - 5.0, 0.5)  # drove backwards across the line
	t.update(LENGTH - 10.0, 1.0)
	check(started[0] == 0, "reversing across the line starts nothing")


func test_invalid_lap_not_best() -> void:
	var t := LapTimer.new(LENGTH)
	_start(t, LENGTH - 1.0)
	_drive(t, LENGTH + 500.0, 50.0)
	t.invalidate("track limits")
	_drive(t, 2.0 * LENGTH + 1.0, 50.0)
	check(t.last_lap_time > 0.0, "lap recorded")
	check(not t.last_lap_valid, "lap flagged invalid")
	check(t.best_lap_time == 0.0, "invalid lap is not a best lap")
	_drive(t, 3.0 * LENGTH + 1.0, 50.0)
	check_near(t.best_lap_time, 20.0, 1e-4, "next clean lap becomes best")


func test_live_delta() -> void:
	var t := LapTimer.new(LENGTH)
	_start(t, LENGTH - 1.0)
	_drive(t, 2.0 * LENGTH + 1.0, 50.0)
	check(is_nan(t.live_delta(500.0)) == false, "reference trace exists after a valid lap")
	# Second lap 10% faster: delta at half distance should be about -1 s.
	_drive(t, 2.0 * LENGTH + 500.0, 55.555)
	check_near(t.live_delta(500.0), -1.0, 0.05, "live delta vs best")
