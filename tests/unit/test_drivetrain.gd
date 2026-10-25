extends TestCase


func test_engine_curve_and_limiter() -> void:
	var e := EngineModel.new()
	check_near(e.curve_at(10000.0), 1.0, 1e-4, "peak torque at 10k rpm")
	check(e.torque(e.limiter_rpm + 10.0, 1.0) <= 0.0, "limiter cuts drive torque")
	check(e.torque(9000.0, 0.0) < 0.0, "closed throttle gives engine braking")
	check_range(e.power_kw(11000.0), 600.0, 760.0, "peak power plausible")


func test_gearbox_shifting() -> void:
	var g := Gearbox.new()
	g.gear = 1
	check(g.shift_up(), "can upshift from 1st")
	check(g.is_shifting(), "shift takes time")
	g.update(1.0)
	check(not g.is_shifting(), "shift completes")
	g.gear = g.gear_count()
	check(not g.shift_up(), "cannot shift past top gear")
	check(g.total_ratio() > 0.0, "forward gear has positive ratio")
	g.gear = -1
	check(g.total_ratio() < 0.0, "reverse has negative ratio")


func test_gear_ratios_descend_and_top_speed() -> void:
	var g := Gearbox.new()
	for i in range(1, g.gear_count()):
		check(g.ratio_for(i) > g.ratio_for(i + 1), "ratios descend")
	# Top gear at the limiter should allow ~330-360 km/h with 0.36 m wheels.
	var top := 12300.0 / g.ratio_for(g.gear_count()) * TAU / 60.0 * 0.36 * 3.6
	check_range(top, 320.0, 380.0, "top-gear speed at limiter")


func test_auto_shift_up_and_down() -> void:
	var g := Gearbox.new()
	g.gear = 2
	var wheel_rpm := 12100.0 / g.ratio_for(2)
	check(g.auto_shift(wheel_rpm, 1.0, 0.0, 40.0), "upshift near limiter")
	check(g.gear == 3, "now in 3rd")
	g.update(1.0)
	var slow := 5000.0 / g.ratio_for(3)
	check(g.auto_shift(slow, 0.0, 1.0, 10.0), "downshift when slow")
	check(g.gear == 2, "back in 2nd")
