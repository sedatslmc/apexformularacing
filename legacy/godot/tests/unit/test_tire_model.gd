extends TestCase


func test_zero_slip_gives_zero_force() -> void:
	var t := TireModel.new()
	check(t.compute(3000.0, 0.0, 0.0).length() < 1e-3, "no slip -> no force")


func test_peak_at_configured_slip() -> void:
	var t := TireModel.new()
	var fz := t.nominal_load
	var peak := t.compute(fz, t.slip_ratio_peak, 0.0).x
	check_near(peak, t.mu_peak * fz, 1.0, "longitudinal peak equals mu * Fz")
	check(t.compute(fz, t.slip_ratio_peak * 0.5, 0.0).x < peak, "force rises up to the peak")
	check(t.compute(fz, t.slip_ratio_peak * 3.0, 0.0).x < peak, "force falls past the peak")
	check(t.compute(fz, t.slip_ratio_peak * 20.0, 0.0).x > peak * t.slide_floor * 0.98, "sliding keeps floor grip")


func test_lateral_force_opposes_slip() -> void:
	var t := TireModel.new()
	check(t.compute(3000.0, 0.0, 0.05).y < 0.0, "positive slip angle -> negative lateral force")
	check(t.compute(3000.0, 0.0, -0.05).y > 0.0, "negative slip angle -> positive lateral force")


func test_friction_circle() -> void:
	var t := TireModel.new()
	var fz := 4000.0
	var limit := t.mu_at_load(fz) * fz
	for sr in [0.0, 0.05, 0.1, 0.3, -0.2]:
		for sa in [0.0, 0.05, 0.12, 0.4, -0.3]:
			var f := t.compute(fz, sr, sa)
			check(f.length() <= limit * 1.0001, "combined force within friction circle (sr=%s sa=%s)" % [sr, sa])


func test_load_sensitivity_reduces_mu() -> void:
	var t := TireModel.new()
	check(t.mu_at_load(8000.0) < t.mu_at_load(2000.0), "mu drops with load")


func test_surface_grip_scales_force() -> void:
	var t := TireModel.new()
	var a := t.compute(3000.0, 0.1, 0.0, 1.0).x
	var b := t.compute(3000.0, 0.1, 0.0, 0.5).x
	check_near(b, a * 0.5, 1.0, "grip multiplier is linear")
