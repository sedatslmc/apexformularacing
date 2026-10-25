extends Node
## Registers input actions and turns raw keyboard / gamepad / touch input into
## smoothed driver controls. Digital inputs are ramped so keyboard and touch
## steering feel progressive; analog sticks and triggers pass through.

## Action -> list of default events (keys, joypad buttons/axes).
const ACTIONS := {
	&"throttle": [KEY_UP, KEY_W, [JOY_AXIS_TRIGGER_RIGHT, 1.0], JOY_BUTTON_A],
	&"brake": [KEY_DOWN, KEY_S, [JOY_AXIS_TRIGGER_LEFT, 1.0], JOY_BUTTON_B],
	&"steer_left": [KEY_LEFT, KEY_A, [JOY_AXIS_LEFT_X, -1.0]],
	&"steer_right": [KEY_RIGHT, KEY_D, [JOY_AXIS_LEFT_X, 1.0]],
	&"shift_up": [KEY_E, KEY_SHIFT, JOY_BUTTON_RIGHT_SHOULDER],
	&"shift_down": [KEY_Q, KEY_CTRL, JOY_BUTTON_LEFT_SHOULDER],
	&"reset_car": [KEY_R, JOY_BUTTON_Y],
	&"camera_next": [KEY_C, JOY_BUTTON_X],
	&"pause": [KEY_ESCAPE, KEY_P, JOY_BUTTON_START],
	&"toggle_telemetry": [KEY_F3],
}

## Steering ramp rates (units per second) for digital input.
const STEER_RATE := 3.2
const STEER_RETURN_RATE := 5.0
const PEDAL_RATE := 7.0

var throttle: float = 0.0
var brake: float = 0.0
var steer: float = 0.0
## True when the last steering input came from an analog device.
var analog_steering: bool = false


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	register_actions()


static func register_actions() -> void:
	for action: StringName in ACTIONS:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action, 0.15)
		for spec: Variant in ACTIONS[action]:
			var ev: InputEvent
			if spec is Array:
				var jm := InputEventJoypadMotion.new()
				jm.axis = spec[0]
				jm.axis_value = spec[1]
				ev = jm
			elif spec < 32:
				# Joypad button indices are small; every keycode used here is >= 32.
				var jb := InputEventJoypadButton.new()
				jb.button_index = spec
				ev = jb
			else:
				var k := InputEventKey.new()
				k.physical_keycode = spec
				ev = k
			InputMap.action_add_event(action, ev)


## Advance smoothing; call once per physics tick.
func update_controls(delta: float) -> void:
	var raw_t := Input.get_action_strength(&"throttle")
	var raw_b := Input.get_action_strength(&"brake")
	var raw_s := Input.get_action_strength(&"steer_right") - Input.get_action_strength(&"steer_left")
	var is_analog := absf(raw_s) > 0.0 and absf(raw_s) < 0.999
	if is_analog:
		analog_steering = true
	elif absf(raw_s) >= 0.999:
		analog_steering = false
	if analog_steering:
		steer = raw_s
	else:
		var rate := STEER_RATE if (raw_s != 0.0 and signf(raw_s) == signf(steer)) or steer == 0.0 else STEER_RETURN_RATE
		if raw_s == 0.0:
			rate = STEER_RETURN_RATE
		steer = move_toward(steer, raw_s, rate * delta)
	throttle = _pedal(throttle, raw_t, delta)
	brake = _pedal(brake, raw_b, delta)


func _pedal(current: float, target: float, delta: float) -> float:
	if target > 0.0 and target < 0.999:
		return target
	return move_toward(current, target, PEDAL_RATE * delta)


func reset_controls() -> void:
	throttle = 0.0
	brake = 0.0
	steer = 0.0
