class_name Gearbox
extends RefCounted
## Sequential seamless-shift gearbox with optional automatic shifting.
## Gear 0 = neutral, -1 = reverse, 1..n = forward gears.

var ratios: PackedFloat32Array = PackedFloat32Array([6.8, 5.55, 4.53, 3.70, 3.02, 2.47, 2.01, 1.66])
var reverse_ratio: float = 3.2
var final_drive: float = 3.0
var efficiency: float = 0.92
## Seconds of torque interruption per shift (seamless boxes are very short).
var shift_time: float = 0.05
var upshift_rpm: float = 12000.0
var downshift_rpm: float = 8000.0
## Minimum time between automatic shifts (prevents hunting).
var auto_shift_interval: float = 0.45

var gear: int = 1
var _shift_timer: float = 0.0
var _since_shift: float = 10.0


func gear_count() -> int:
	return ratios.size()


func is_shifting() -> bool:
	return _shift_timer > 0.0


## Overall ratio from crank to wheel for the current gear (0 in neutral).
func total_ratio() -> float:
	if gear == 0:
		return 0.0
	if gear < 0:
		return -reverse_ratio * final_drive
	return ratios[gear - 1] * final_drive


func ratio_for(g: int) -> float:
	if g <= 0:
		return 0.0
	return ratios[g - 1] * final_drive


func shift_up() -> bool:
	if gear >= gear_count():
		return false
	gear += 1
	_shift_timer = shift_time
	_since_shift = 0.0
	return true


func shift_down() -> bool:
	if gear <= -1:
		return false
	gear -= 1
	_shift_timer = shift_time
	_since_shift = 0.0
	return true


func update(delta: float) -> void:
	_shift_timer = maxf(0.0, _shift_timer - delta)
	_since_shift += delta


## Automatic shift decision. [param wheel_rpm] should come from road speed,
## not from a spinning wheel, or wheelspin triggers premature upshifts.
## Returns true if a shift happened.
func auto_shift(wheel_rpm: float, throttle: float, brake: float, forward_speed: float) -> bool:
	if is_shifting() or _since_shift < auto_shift_interval:
		return false
	if gear <= 0:
		if forward_speed > -1.0 and throttle > 0.1:
			gear = 1
			return true
		return false
	var rpm := wheel_rpm * ratio_for(gear)
	if rpm > upshift_rpm and gear < gear_count() and throttle > 0.05:
		return shift_up()
	if gear > 1:
		var lower_rpm := wheel_rpm * ratio_for(gear - 1)
		# Downshift earlier while braking so the engine is ready on exit.
		var threshold := downshift_rpm + (1500.0 if brake > 0.2 else 0.0)
		if rpm < threshold and lower_rpm < upshift_rpm - 1200.0:
			return shift_down()
	return false
