class_name TireModel
extends RefCounted
## Combined-slip tire model (normalised-slip "magic formula" variant).
##
## Longitudinal slip ratio and lateral slip angle are each normalised by their
## peak values, combined into a single slip magnitude, and run through one
## Pacejka-style curve. This gives a believable friction circle, a clear peak
## and a gentle fall-off past the limit, which suits a simcade handling target.
## See docs/PHYSICS_MODEL.md for the derivation and tuning notes.

## Friction coefficient at nominal load.
var mu_peak: float = 1.75
## Slip ratio at which longitudinal force peaks.
var slip_ratio_peak: float = 0.10
## Slip angle (radians) at which lateral force peaks.
var slip_angle_peak: float = 0.12
## Curve shape factor (Pacejka C). ~1.9 gives a moderate post-peak drop.
var shape: float = 1.9
## Fraction of peak grip retained at very large slip (floor of the curve).
var slide_floor: float = 0.72
## Load at which mu_peak applies (N).
var nominal_load: float = 3000.0
## Grip loss per unit of relative load increase (load sensitivity).
var load_sensitivity: float = 0.12

var _stiffness_b: float = 1.0


func _init() -> void:
	recompute()


## Call after changing [member shape].
func recompute() -> void:
	# Choose B so that sin(C * atan(B * s)) peaks exactly at s = 1.
	_stiffness_b = tan(PI / (2.0 * shape))


## Effective friction coefficient at a given vertical load.
func mu_at_load(fz: float) -> float:
	var rel := (fz - nominal_load) / nominal_load
	return mu_peak * clampf(1.0 - load_sensitivity * rel, 0.6, 1.25)


## Normalised force curve: 0 at s=0, 1 at s=1, approaching slide_floor beyond.
func curve(s: float) -> float:
	if s <= 1.0:
		return sin(shape * atan(_stiffness_b * s))
	# Past the peak blend smoothly down towards the sliding floor.
	var t := 1.0 - exp(-(s - 1.0) * 0.9)
	return lerpf(1.0, slide_floor, t)


## Returns Vector2(longitudinal_force, lateral_force) in newtons.
## Positive slip ratio = driving (wheel faster than ground) -> positive Fx.
## Positive slip angle -> negative Fy (force opposes lateral sliding).
func compute(fz: float, slip_ratio: float, slip_angle: float, surface_grip: float = 1.0) -> Vector2:
	if fz <= 0.0:
		return Vector2.ZERO
	var sx := slip_ratio / slip_ratio_peak
	var sy := tan(clampf(slip_angle, -1.4, 1.4)) / tan(slip_angle_peak)
	var s := sqrt(sx * sx + sy * sy)
	if s < 1e-6:
		return Vector2.ZERO
	var f := mu_at_load(fz) * surface_grip * fz * curve(s)
	return Vector2(f * sx / s, -f * sy / s)
