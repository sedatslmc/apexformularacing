class_name Autopilot
extends RefCounted
## Simple racing-line follower used for automated tests, attract mode and as
## the seed of the future AI driver. Follows the centreline (plus an optional
## lateral offset) with pure pursuit and a precomputed speed profile.

var geometry: TrackGeometry
## Fraction of the estimated grip limit the speed profile uses.
var aggression: float = 0.8
var mu_estimate: float = 1.6
var downforce_per_mass: float = 3.6 * 1.225 * 0.5 / 800.0
var lateral_offset: float = 0.0

var _profile: PackedFloat32Array = PackedFloat32Array()
var _hint: int = -1


func _init(geo: TrackGeometry, skill: float = 0.8) -> void:
	geometry = geo
	aggression = skill
	_build_profile()


func _build_profile() -> void:
	var n := geometry.sample_count()
	var mu := mu_estimate * aggression
	var g := 9.81
	_profile.resize(n)
	for i in n:
		# Use the tightest curvature in a short window so we're slow enough
		# for the whole corner, not just its sampled apex.
		var k := 0.0
		for o in range(-3, 4):
			k = maxf(k, absf(geometry.curvatures[(i + o + n) % n]))
		var denom := k - mu * downforce_per_mass
		_profile[i] = 95.0 if denom <= 1e-5 else minf(95.0, sqrt(mu * g / denom))
	# Backward pass: braking limit. Two laps around to handle the wrap.
	for pass_i in 2:
		for step in n:
			var i := n - 1 - step
			var j := (i + 1) % n
			var v := _profile[j]
			var decel := mu * (g + downforce_per_mass * v * v) * 0.85
			_profile[i] = minf(_profile[i], sqrt(v * v + 2.0 * decel * geometry.spacing))
	# Forward pass: traction-limited acceleration (rough).
	for pass_i in 2:
		for i in n:
			var j := (i + 1) % n
			var v := _profile[i]
			var accel := clampf(14.0 - v * 0.09, 3.0, 14.0)
			_profile[j] = minf(_profile[j], sqrt(v * v + 2.0 * accel * geometry.spacing))


func target_speed(distance: float) -> float:
	return _profile[geometry.index_at(distance)]


## Computes controls for the car. Returns Vector3(throttle, brake, steer).
func drive(car: FormulaCar) -> Vector3:
	var pos := car.global_position
	var proj := geometry.project(pos, _hint)
	_hint = int(proj.z)
	var d := proj.x
	var v := maxf(car.forward_speed, 0.0)
	var lookahead := 7.0 + v * 0.42
	var target := geometry.position_at(d + lookahead)
	var tng := geometry.tangent_at(d + lookahead)
	target += tng.cross(Vector3.UP).normalized() * lateral_offset
	var local := car.global_transform.affine_inverse() * target
	# Pure pursuit: curvature = 2 * lateral / distance^2.
	var dist2 := maxf(local.x * local.x + local.z * local.z, 1.0)
	var curvature := 2.0 * local.x / dist2
	var wheel_angle := atan(curvature * car.wheelbase)
	var max_lock := deg_to_rad(car.max_steer_angle_deg)
	var steer := clampf(wheel_angle / max_lock, -1.0, 1.0)
	# Speed control looks slightly ahead to brake in time.
	var want := minf(target_speed(d + v * 0.25), target_speed(d + 4.0))
	var err := want - v
	var throttle := clampf(err * 0.35 + 0.15, 0.0, 1.0)
	var brake := clampf(-err * 0.18, 0.0, 1.0)
	if brake > 0.05:
		throttle = 0.0
	return Vector3(throttle, brake, steer)
