class_name RaceCamera
extends Camera3D
## Follows a car using its physics-interpolated transform. Modes: chase,
## close chase (T-cam height) and cockpit.

enum Mode { CHASE, CLOSE, COCKPIT }

const MODE_NAMES := ["Chase", "Close", "Cockpit"]

@export var target_path: NodePath
var target: Node3D
var mode: Mode = Mode.CHASE

var _smoothed_pos: Vector3
var _smoothed_fwd: Vector3 = Vector3.FORWARD
var _initialised := false


func _ready() -> void:
	if target == null and not target_path.is_empty():
		target = get_node_or_null(target_path) as Node3D
	# Camera is moved manually from the interpolated transform each frame.
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF


func set_mode(m: int) -> void:
	mode = m as Mode
	_initialised = false


func cycle_mode() -> int:
	set_mode((int(mode) + 1) % Mode.size())
	return int(mode)


func snap() -> void:
	_initialised = false


func _process(delta: float) -> void:
	if target == null:
		return
	var xf: Transform3D = target.get_global_transform_interpolated()
	var fwd := -xf.basis.z
	var flat_fwd := Vector3(fwd.x, 0.0, fwd.z).normalized()
	if flat_fwd.length_squared() < 0.5:
		flat_fwd = _smoothed_fwd
	match mode:
		Mode.COCKPIT:
			fov = 72.0
			var eye := xf * Vector3(0.0, 0.42, -0.45)
			global_transform = Transform3D(Basis.looking_at(fwd, xf.basis.y), eye)
			_initialised = false
			return
		Mode.CLOSE:
			fov = 68.0
			_follow(xf, flat_fwd, 4.6, 1.35, 6.0, delta)
		_:
			fov = 66.0
			_follow(xf, flat_fwd, 7.0, 2.1, 4.5, delta)


func _follow(xf: Transform3D, flat_fwd: Vector3, distance: float, height: float, stiffness: float, delta: float) -> void:
	if not _initialised:
		_smoothed_fwd = flat_fwd
		_initialised = true
	# Heading lags slightly so slides and rotation are visible to the player.
	var k := 1.0 - exp(-stiffness * delta)
	_smoothed_fwd = _smoothed_fwd.slerp(flat_fwd, k).normalized()
	var desired := xf.origin - _smoothed_fwd * distance + Vector3.UP * height
	_smoothed_pos = desired
	var look_target := xf.origin + _smoothed_fwd * 6.0 + Vector3.UP * 0.6
	global_transform = Transform3D(Basis.looking_at(look_target - _smoothed_pos, Vector3.UP), _smoothed_pos)
