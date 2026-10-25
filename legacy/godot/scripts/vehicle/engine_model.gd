class_name EngineModel
extends RefCounted
## Fictional hybrid power unit: torque curve, rev limiter, engine braking.

var idle_rpm: float = 4000.0
var max_rpm: float = 12500.0
var limiter_rpm: float = 12300.0
## Peak torque in N·m (crankshaft, combined ICE + electric deployment).
var peak_torque: float = 600.0
## Normalised torque curve sampled every 1000 rpm from 0 rpm.
var torque_curve: PackedFloat32Array = PackedFloat32Array([
	0.45, 0.50, 0.56, 0.62, 0.70, 0.78, 0.85, 0.91,
	0.96, 0.99, 1.00, 0.99, 0.95, 0.88])
## Engine braking torque at max rpm with closed throttle (N·m).
var engine_brake_torque: float = 110.0


## Normalised wide-open-throttle torque factor at the given rpm.
func curve_at(rpm: float) -> float:
	var x := clampf(rpm / 1000.0, 0.0, float(torque_curve.size() - 1))
	var i := mini(int(x), torque_curve.size() - 2)
	return lerpf(torque_curve[i], torque_curve[i + 1], x - float(i))


## Net crank torque for a throttle in [0,1]. Includes engine braking off-throttle
## and a hard cut above the limiter.
func torque(rpm: float, throttle: float) -> float:
	var drive := peak_torque * curve_at(rpm) * throttle
	if rpm >= limiter_rpm:
		drive = 0.0
	var braking := engine_brake_torque * clampf(rpm / max_rpm, 0.0, 1.0) * (1.0 - throttle)
	return drive - braking


## Power in kW at full throttle (for docs/telemetry).
func power_kw(rpm: float) -> float:
	return torque(rpm, 1.0) * rpm * TAU / 60.0 / 1000.0
