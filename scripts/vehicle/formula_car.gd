class_name FormulaCar
extends RigidBody3D
## Raycast-suspension open-wheel car with a combined-slip tire model, rear-wheel
## drive through a limited-slip differential, aero downforce/drag and optional
## driver assists. All dynamics run in _integrate_forces at the fixed physics
## rate; wheel spin is sub-stepped for stability. See docs/PHYSICS_MODEL.md.

signal gear_changed(gear: int)

const AIR_DENSITY := 1.225

@export_group("Chassis")
@export var car_mass: float = 800.0
## Body-frame inertia (pitch, yaw, roll) in kg·m².
@export var body_inertia: Vector3 = Vector3(1100.0, 1250.0, 320.0)
@export var wheelbase: float = 3.6
## Fraction of static weight on the front axle.
@export_range(0.3, 0.7) var weight_front: float = 0.45
@export var track_front: float = 1.62
@export var track_rear: float = 1.56
## Height of the centre of mass above the ground at static ride height.
@export var cog_height: float = 0.30

@export_group("Suspension")
@export var suspension_travel: float = 0.09
@export var spring_front: float = 140000.0
@export var spring_rear: float = 125000.0
@export var damper_bump: float = 6000.0
@export var damper_rebound: float = 8500.0
@export var anti_roll_front: float = 60000.0
@export var anti_roll_rear: float = 40000.0
@export var bump_stop_rate: float = 900000.0

@export_group("Wheels")
@export var wheel_radius_front: float = 0.355
@export var wheel_radius_rear: float = 0.36
@export var wheel_inertia_front: float = 1.1
@export var wheel_inertia_rear: float = 1.6
@export var max_steer_angle_deg: float = 19.0
## Steering lock retained at high speed when the steering assist is on.
@export var high_speed_steer_deg: float = 4.5
@export var steer_assist_speed: float = 75.0
@export var tire_relaxation_length: float = 0.35
@export var wheel_substeps: int = 6

@export_group("Brakes")
@export var max_brake_torque: float = 5200.0
@export_range(0.4, 0.7) var brake_bias_front: float = 0.57

@export_group("Aero")
@export var drag_area: float = 1.25
@export var downforce_area: float = 3.6
@export_range(0.3, 0.6) var aero_balance_front: float = 0.43

@export_group("Drivetrain")
@export var diff_lock_rate: float = 220.0
@export var diff_preload: float = 120.0
@export var diff_lock_ratio: float = 0.35
@export var launch_rpm: float = 7500.0

@export_group("Assists")
## Traction control acts when a driven tire's combined (longitudinal +
## lateral) normalised slip exceeds this; 1.0 = at the grip peak.
@export var tc_combined_threshold: float = 1.0
@export var abs_slip_threshold: float = 0.12

## Driver inputs, written by a controller (player or AI) every tick.
var input_throttle: float = 0.0
var input_brake: float = 0.0
## -1 full left .. +1 full right.
var input_steer: float = 0.0
var assist_tc: bool = true
var assist_abs: bool = true
var assist_steering: bool = true
var auto_gearbox: bool = true

var engine := EngineModel.new()
var gearbox := Gearbox.new()
var tire := TireModel.new()

var wheels: Array[Wheel] = []
var engine_rpm: float = 4000.0
var speed: float = 0.0
var forward_speed: float = 0.0
var steer_angle: float = 0.0
var tc_active: bool = false
var abs_active: bool = false
var downforce: float = 0.0
var drag_force: float = 0.0
var sim_time: float = 0.0

var _pending_reset: bool = false
var _reset_transform: Transform3D
var _tc_cut: float = 0.0
var _reverse_timer: float = 0.0
var _shift_up_requested: bool = false
var _shift_down_requested: bool = false
var _exclude: Array[RID] = []


class Wheel:
	var label: String
	var mount: Vector3
	var front: bool
	var left: bool
	var radius: float
	var inertia: float
	var spring: float
	var visual: Node3D
	var contact: bool = false
	var contact_point: Vector3
	var contact_normal: Vector3 = Vector3.UP
	var surface: StringName = SurfaceTypes.ASPHALT
	var grip: float = 1.0
	var rolling_drag: float = 0.012
	var compression: float = 0.0
	var prev_compression: float = 0.0
	var spring_length: float = 0.0
	var load: float = 0.0
	var omega: float = 0.0
	var spin_angle: float = 0.0
	var steer: float = 0.0
	var slip_ratio: float = 0.0
	var slip_angle: float = 0.0
	var fx: float = 0.0
	var fy: float = 0.0
	var drive_torque: float = 0.0
	var brake_torque: float = 0.0


func _ready() -> void:
	mass = car_mass
	can_sleep = false
	continuous_cd = true
	custom_integrator = false
	center_of_mass_mode = RigidBody3D.CENTER_OF_MASS_MODE_CUSTOM
	center_of_mass = Vector3.ZERO
	inertia = body_inertia
	linear_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	linear_damp = 0.0
	angular_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	angular_damp = 0.05
	_exclude = [get_rid()]
	_create_wheels()
	gearbox.gear = 1


func _create_wheels() -> void:
	# Body origin is the centre of mass. Forward is -Z, right is +X.
	var front_z := -wheelbase * (1.0 - weight_front)
	var rear_z := wheelbase * weight_front
	wheels.clear()
	for spec: Array in [["FL", true, true], ["FR", true, false], ["RL", false, true], ["RR", false, false]]:
		var w := Wheel.new()
		w.label = spec[0]
		w.front = spec[1]
		w.left = spec[2]
		w.radius = wheel_radius_front if w.front else wheel_radius_rear
		w.inertia = wheel_inertia_front if w.front else wheel_inertia_rear
		w.spring = spring_front if w.front else spring_rear
		var half_track := (track_front if w.front else track_rear) * 0.5
		# Mount sits at the top of suspension travel, placed so that the static
		# ride height puts the centre of mass at cog_height.
		var axle_load := car_mass * 9.81 * (weight_front if w.front else 1.0 - weight_front) * 0.5
		var static_comp := axle_load / w.spring
		var wheel_centre_y := w.radius - cog_height
		var mount_y := wheel_centre_y + (suspension_travel - static_comp)
		w.mount = Vector3(-half_track if w.left else half_track, mount_y, front_z if w.front else rear_z)
		w.compression = static_comp
		w.prev_compression = static_comp
		w.visual = get_node_or_null("Visual/Wheel" + w.label) as Node3D
		wheels.append(w)


## Teleport the car (applied on the next physics step).
func reset_to(xf: Transform3D) -> void:
	_reset_transform = xf
	_pending_reset = true


func request_shift_up() -> void:
	_shift_up_requested = true


func request_shift_down() -> void:
	_shift_down_requested = true


func speed_kmh() -> float:
	return speed * 3.6


## Number of wheels whose contact surface counts as on-track.
func wheels_on_track() -> int:
	var n := 0
	for w in wheels:
		if not w.contact or SurfaceTypes.get_props(w.surface)["on_track"]:
			n += 1
	return n


func _integrate_forces(state: PhysicsDirectBodyState3D) -> void:
	var dt := state.step
	if _pending_reset:
		_pending_reset = false
		state.transform = _reset_transform
		state.linear_velocity = Vector3.ZERO
		state.angular_velocity = Vector3.ZERO
		for w in wheels:
			w.omega = 0.0
			w.slip_angle = 0.0
			w.slip_ratio = 0.0
		gearbox.gear = 1
		engine_rpm = engine.idle_rpm
		_tc_cut = 0.0
		return
	sim_time += dt
	var xf := state.transform
	var b := xf.basis
	var up := b.y
	var fwd := -b.z
	var com := xf.origin + state.center_of_mass
	var lin_vel := state.linear_velocity
	var ang_vel := state.angular_velocity
	speed = lin_vel.length()
	forward_speed = lin_vel.dot(fwd)

	_update_gearbox(dt)
	var throttle := input_throttle
	var brake := input_brake
	if gearbox.gear < 0:
		# In reverse the pedals swap so "brake" backs the car up.
		throttle = input_brake
		brake = input_throttle
	_update_steering(dt)

	# --- Suspension and contact detection ----------------------------------
	var space := state.get_space_state()
	var ray := PhysicsRayQueryParameters3D.new()
	ray.exclude = _exclude
	ray.collision_mask = 1
	for w in wheels:
		var origin := xf * w.mount
		var reach := suspension_travel + w.radius
		ray.from = origin
		ray.to = origin - up * reach
		var hit := space.intersect_ray(ray)
		w.prev_compression = w.compression
		if hit.is_empty():
			w.contact = false
			w.compression = 0.0
			w.spring_length = suspension_travel
			w.load = 0.0
			continue
		w.contact = true
		w.contact_point = hit["position"]
		w.contact_normal = hit["normal"]
		var surf: Variant = (hit["collider"] as Object).get_meta(&"surface", SurfaceTypes.ASPHALT) if hit["collider"] else SurfaceTypes.ASPHALT
		w.surface = surf
		var props := SurfaceTypes.get_props(w.surface)
		w.grip = props["grip"]
		w.rolling_drag = props["drag"]
		var dist := origin.distance_to(w.contact_point)
		w.spring_length = dist - w.radius
		w.compression = suspension_travel - w.spring_length
	# Anti-roll bars couple left/right compression on each axle.
	var arb_front := (wheels[0].compression - wheels[1].compression) * anti_roll_front
	var arb_rear := (wheels[2].compression - wheels[3].compression) * anti_roll_rear
	for i in wheels.size():
		var w := wheels[i]
		if not w.contact:
			continue
		var comp_vel := (w.compression - w.prev_compression) / dt
		var force := w.spring * w.compression
		force += (damper_bump if comp_vel > 0.0 else damper_rebound) * comp_vel
		if w.compression > suspension_travel:
			force += bump_stop_rate * (w.compression - suspension_travel)
		var arb := arb_front if w.front else arb_rear
		force += arb if w.left else -arb
		# Normal load is what the spring transmits, scaled by how square the
		# ground is to the suspension axis.
		w.load = maxf(0.0, force) * maxf(w.contact_normal.dot(up), 0.0)
		state.apply_force(up * w.load, b * w.mount)

	# --- Drivetrain ---------------------------------------------------------
	var wheel_rpm := (wheels[2].omega + wheels[3].omega) * 0.5 * 60.0 / TAU
	var ratio := gearbox.total_ratio()
	var engaged_rpm := absf(wheel_rpm * ratio)
	var clutch_slipping := gearbox.gear == 0 or engaged_rpm < engine.idle_rpm \
			or (throttle > 0.05 and engaged_rpm < launch_rpm)
	_update_traction_control(throttle, dt)
	var eff_throttle := throttle * (1.0 - _tc_cut)
	if gearbox.is_shifting():
		eff_throttle = 0.0
	var crank_torque := 0.0
	if clutch_slipping:
		var target := lerpf(engine.idle_rpm, launch_rpm, clampf(throttle * 1.4, 0.0, 1.0))
		engine_rpm = move_toward(engine_rpm, maxf(target, engaged_rpm), 25000.0 * dt)
		if gearbox.gear != 0 and eff_throttle > 0.0:
			crank_torque = engine.torque(engine_rpm, eff_throttle)
	else:
		engine_rpm = minf(engaged_rpm, engine.max_rpm)
		crank_torque = engine.torque(engaged_rpm, eff_throttle)
	var axle_torque := crank_torque * ratio * gearbox.efficiency

	# Brake torques per wheel with optional ABS modulation.
	abs_active = false
	for w in wheels:
		var share := brake_bias_front if w.front else 1.0 - brake_bias_front
		var tb := brake * max_brake_torque * share * 2.0 * w.grip
		if assist_abs and brake > 0.05 and w.slip_ratio < -abs_slip_threshold and speed > 3.0:
			tb *= 0.55
			abs_active = true
		w.brake_torque = tb

	# --- Tire forces with sub-stepped wheel spin ----------------------------
	var sub_n := maxi(1, wheel_substeps)
	var h := dt / float(sub_n)
	var fx_sum: Array[float] = [0.0, 0.0, 0.0, 0.0]
	var fy_sum: Array[float] = [0.0, 0.0, 0.0, 0.0]
	var vx_arr: Array[float] = [0.0, 0.0, 0.0, 0.0]
	var vy_arr: Array[float] = [0.0, 0.0, 0.0, 0.0]
	var fwd_arr: Array[Vector3] = [Vector3.ZERO, Vector3.ZERO, Vector3.ZERO, Vector3.ZERO]
	var right_arr: Array[Vector3] = [Vector3.ZERO, Vector3.ZERO, Vector3.ZERO, Vector3.ZERO]
	for i in wheels.size():
		var w := wheels[i]
		var n := w.contact_normal if w.contact else up
		var wfwd := fwd.rotated(up, -w.steer)
		wfwd = (wfwd - n * wfwd.dot(n)).normalized()
		var wright := wfwd.cross(n).normalized()
		fwd_arr[i] = wfwd
		right_arr[i] = wright
		var p := w.contact_point if w.contact else xf * w.mount
		var v := lin_vel + ang_vel.cross(p - com)
		vx_arr[i] = v.dot(wfwd)
		vy_arr[i] = v.dot(wright)
		# Lateral slip with relaxation-length lag (smooths low-speed jitter).
		var target_alpha := atan2(vy_arr[i], maxf(absf(vx_arr[i]), 2.0))
		var k := clampf(absf(vx_arr[i]) * dt / tire_relaxation_length, 0.05, 1.0)
		w.slip_angle = lerpf(w.slip_angle, target_alpha, k)
	for s in sub_n:
		# Open diff torque split plus a clutch-type locking torque.
		var dw := wheels[2].omega - wheels[3].omega
		var lock_cap := diff_preload + diff_lock_ratio * absf(axle_torque)
		var lock := clampf(dw * diff_lock_rate, -lock_cap, lock_cap)
		wheels[2].drive_torque = axle_torque * 0.5 - lock * 0.5
		wheels[3].drive_torque = axle_torque * 0.5 + lock * 0.5
		wheels[0].drive_torque = 0.0
		wheels[1].drive_torque = 0.0
		for i in wheels.size():
			var w := wheels[i]
			var vx := vx_arr[i]
			var fx := 0.0
			var fy := 0.0
			if w.contact and w.load > 0.0:
				w.slip_ratio = (w.omega * w.radius - vx) / maxf(absf(vx), 3.0)
				var f := tire.compute(w.load, w.slip_ratio, w.slip_angle, w.grip)
				fx = f.x
				fy = f.y
			var rolling := w.rolling_drag * w.load * w.radius * signf(w.omega)
			var torque := w.drive_torque - fx * w.radius - rolling
			var new_omega := w.omega + torque / w.inertia * h
			# Brakes oppose rotation but never reverse it.
			var brake_dw := w.brake_torque / w.inertia * h
			if absf(new_omega) <= brake_dw:
				new_omega = 0.0
			else:
				new_omega -= signf(new_omega) * brake_dw
			w.omega = new_omega
			fx_sum[i] += fx
			fy_sum[i] += fy
	for i in wheels.size():
		var w := wheels[i]
		w.fx = fx_sum[i] / sub_n
		w.fy = fy_sum[i] / sub_n
		w.spin_angle = fmod(w.spin_angle + w.omega * dt, TAU)
		if not w.contact:
			continue
		var force := fwd_arr[i] * w.fx + right_arr[i] * w.fy
		state.apply_force(force, w.contact_point - xf.origin)

	# --- Aerodynamics ------------------------------------------------------
	var q := 0.5 * AIR_DENSITY * forward_speed * forward_speed
	downforce = downforce_area * q
	drag_force = drag_area * 0.5 * AIR_DENSITY * speed * speed
	var front_axle := b * Vector3(0, 0, -wheelbase * (1.0 - weight_front))
	var rear_axle := b * Vector3(0, 0, wheelbase * weight_front)
	state.apply_force(-up * downforce * aero_balance_front, front_axle)
	state.apply_force(-up * downforce * (1.0 - aero_balance_front), rear_axle)
	if speed > 0.1:
		state.apply_central_force(-lin_vel.normalized() * drag_force)

	_update_visuals()


func _update_gearbox(dt: float) -> void:
	gearbox.update(dt)
	var shifted := false
	if _shift_up_requested:
		shifted = gearbox.shift_up()
	elif _shift_down_requested:
		shifted = gearbox.shift_down()
	_shift_up_requested = false
	_shift_down_requested = false
	if auto_gearbox and not shifted:
		var wheel_rpm := forward_speed / wheel_radius_rear * 60.0 / TAU
		# Simcade convenience: hold brake at a standstill to engage reverse,
		# press throttle to return to first.
		if absf(forward_speed) < 0.8 and input_brake > 0.5 and input_throttle < 0.05 and gearbox.gear > 0:
			_reverse_timer += dt
			if _reverse_timer > 0.6:
				gearbox.gear = -1
				shifted = true
		else:
			_reverse_timer = 0.0
		if gearbox.gear < 0:
			if input_throttle > 0.3 and forward_speed > -1.0:
				gearbox.gear = 1
				shifted = true
		else:
			shifted = gearbox.auto_shift(wheel_rpm, input_throttle, input_brake, forward_speed) or shifted
	if shifted:
		gear_changed.emit(gearbox.gear)


func _update_steering(dt: float) -> void:
	var max_lock := deg_to_rad(max_steer_angle_deg)
	if assist_steering:
		var t := clampf(absf(forward_speed) / steer_assist_speed, 0.0, 1.0)
		max_lock = lerpf(max_lock, deg_to_rad(high_speed_steer_deg), t * t * (3.0 - 2.0 * t))
	var target := input_steer * max_lock
	# Rack speed limit keeps the front from snapping on digital input.
	steer_angle = move_toward(steer_angle, target, deg_to_rad(120.0) * dt)
	# Light Ackermann: inside wheel turns a little more.
	var ack := 0.12 * steer_angle
	for w in wheels:
		if not w.front:
			continue
		var inside := (steer_angle > 0.0 and not w.left) or (steer_angle < 0.0 and w.left)
		w.steer = steer_angle + (ack if inside else -ack * 0.5)


func _update_traction_control(throttle: float, dt: float) -> void:
	tc_active = false
	if not assist_tc or throttle < 0.05:
		_tc_cut = move_toward(_tc_cut, 0.0, 6.0 * dt)
		return
	# Combined slip lets TC catch power oversteer mid-corner, not only
	# straight-line wheelspin: with the tire already near its lateral limit
	# there is little longitudinal budget left.
	var slip := 0.0
	var tan_peak := tan(tire.slip_angle_peak)
	for w: Wheel in [wheels[2], wheels[3]]:
		if w.slip_ratio <= 0.02:
			continue
		var sx := w.slip_ratio / tire.slip_ratio_peak
		var sy := tan(minf(absf(w.slip_angle), 1.4)) / tan_peak
		slip = maxf(slip, sqrt(sx * sx + sy * sy))
	if slip > tc_combined_threshold and speed > 1.0:
		tc_active = true
		var excess := (slip - tc_combined_threshold) * 1.5
		_tc_cut = move_toward(_tc_cut, clampf(excess, 0.0, 0.95), 14.0 * dt)
	else:
		_tc_cut = move_toward(_tc_cut, 0.0, 4.0 * dt)


func _update_visuals() -> void:
	for w in wheels:
		if w.visual == null:
			continue
		var y := w.mount.y - w.spring_length
		w.visual.position = Vector3(w.mount.x, y, w.mount.z)
		w.visual.basis = Basis(Vector3.UP, -w.steer) * Basis(Vector3.RIGHT, -w.spin_angle)
