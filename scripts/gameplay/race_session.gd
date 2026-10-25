class_name RaceSession
extends Node3D
## Free-practice session: one car on one circuit with lap timing, track
## limits, reset-to-track, pause menu and camera switching.

signal lap_recorded(lap_time: float, valid: bool)

## Seconds with all four wheels outside track limits before a lap is voided.
const TRACK_LIMIT_GRACE := 0.2

@export var autopilot_enabled: bool = false
@export var autopilot_skill: float = 0.8
@export var build_environment: bool = true
## When false the session leaves car inputs alone (tests/tools drive directly).
@export var player_input_enabled: bool = true

@onready var circuit: Circuit = $Circuit
@onready var car: FormulaCar = $FormulaCar
@onready var camera: RaceCamera = $RaceCamera
@onready var hud: RaceHud = $Hud
@onready var touch: TouchControls = $TouchLayer/TouchControls
@onready var pause_menu: PauseMenu = $PauseMenu

var timer: LapTimer
var autopilot: Autopilot
var track_id: String = ""
var lap_distance: float = 0.0
var track_index: int = -1

var _off_track_time: float = 0.0
var _wrong_way_time: float = 0.0


func _ready() -> void:
	if "--autopilot" in OS.get_cmdline_user_args():
		autopilot_enabled = true
	if build_environment:
		_build_environment()
	track_id = circuit.layout_path.get_file().get_basename()
	timer = LapTimer.new(circuit.geometry.length, circuit.sector_starts)
	timer.lap_completed.connect(_on_lap_completed)
	autopilot = Autopilot.new(circuit.geometry, autopilot_skill)
	camera.target = car
	hud.bind(car, timer, Settings.best_lap(track_id))
	hud.pause_pressed.connect(_toggle_pause)
	camera.set_mode(Settings.camera_mode)
	pause_menu.resume_requested.connect(_toggle_pause)
	pause_menu.restart_requested.connect(_restart)
	pause_menu.reset_car_requested.connect(func() -> void:
		_toggle_pause()
		reset_car_to_track())
	pause_menu.quit_requested.connect(_quit_to_menu)
	pause_menu.settings_changed.connect(_apply_settings)
	_apply_settings()
	place_on_grid()
	hud.show_message(circuit.display_name + "  —  Free Practice", Color.WHITE, 3.0)


func _apply_settings() -> void:
	car.assist_tc = Settings.traction_control
	car.assist_abs = Settings.abs_enabled
	car.assist_steering = Settings.steering_assist and not autopilot_enabled
	car.auto_gearbox = Settings.auto_gearbox or autopilot_enabled
	touch.visible = Settings.show_touch_controls and not autopilot_enabled


func _build_environment() -> void:
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.22, 0.42, 0.72)
	sky_mat.sky_horizon_color = Color(0.68, 0.78, 0.88)
	sky_mat.ground_horizon_color = Color(0.55, 0.6, 0.55)
	sky_mat.ground_bottom_color = Color(0.25, 0.3, 0.22)
	sky_mat.sun_angle_max = 20.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.9
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_white = 6.0
	env.fog_enabled = true
	env.fog_light_color = Color(0.7, 0.78, 0.86)
	env.fog_density = 0.0006
	env.fog_sky_affect = 0.0
	var we := WorldEnvironment.new()
	we.name = "WorldEnvironment"
	we.environment = env
	add_child(we)
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-48.0, -35.0, 0.0)
	sun.light_energy = 1.25
	sun.light_color = Color(1.0, 0.96, 0.9)
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	sun.directional_shadow_max_distance = 120.0
	add_child(sun)


func place_on_grid() -> void:
	var xf := circuit.grid_transform(0)
	xf.origin += xf.basis.y * (car.cog_height + 0.02)
	car.global_transform = xf
	car.reset_to(xf)
	timer.reset_position()
	track_index = -1
	GameInput.reset_controls()
	camera.snap()


func reset_car_to_track() -> void:
	var proj := circuit.geometry.project(car.global_position, track_index)
	var xf := circuit.geometry.transform_at(proj.x - 5.0, 0.0, car.cog_height + 0.05)
	car.reset_to(xf)
	timer.reset_position()
	track_index = -1
	GameInput.reset_controls()
	camera.snap()
	hud.show_message("Car reset", Color.WHITE, 1.5)


func _physics_process(delta: float) -> void:
	# Progress and timing use the state from the last completed physics step.
	var proj := circuit.geometry.project(car.global_position, track_index)
	track_index = int(proj.z)
	lap_distance = circuit.lap_distance(proj.x)
	timer.update(lap_distance, car.sim_time)
	hud.set_lap_distance(lap_distance)
	_check_track_limits(delta)
	_check_wrong_way(proj.x, delta)
	if autopilot_enabled:
		var c := autopilot.drive(car)
		car.input_throttle = c.x
		car.input_brake = c.y
		car.input_steer = c.z
	elif player_input_enabled:
		GameInput.update_controls(delta)
		car.input_throttle = GameInput.throttle
		car.input_brake = GameInput.brake
		car.input_steer = GameInput.steer
		if Input.is_action_just_pressed(&"shift_up"):
			car.request_shift_up()
		if Input.is_action_just_pressed(&"shift_down"):
			car.request_shift_down()


func _check_track_limits(delta: float) -> void:
	if car.wheels_on_track() == 0:
		_off_track_time += delta
		if _off_track_time > TRACK_LIMIT_GRACE:
			timer.invalidate("track limits")
	else:
		_off_track_time = 0.0


func _check_wrong_way(raw_distance: float, delta: float) -> void:
	var tangent := circuit.geometry.tangent_at(raw_distance)
	var heading := car.linear_velocity.dot(tangent)
	if car.speed > 5.0 and heading < -3.0:
		_wrong_way_time += delta
		if _wrong_way_time > 1.0:
			hud.show_message("WRONG WAY", RaceHud.COLOR_INVALID, 0.5)
	else:
		_wrong_way_time = 0.0


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed(&"pause"):
		_toggle_pause()
	elif get_tree().paused:
		return
	elif event.is_action_pressed(&"reset_car"):
		reset_car_to_track()
	elif event.is_action_pressed(&"camera_next"):
		Settings.camera_mode = camera.cycle_mode()
		hud.show_message(RaceCamera.MODE_NAMES[camera.mode] + " camera", Color.WHITE, 1.0)
	elif event.is_action_pressed(&"toggle_telemetry"):
		hud.toggle_telemetry()


func _notification(what: int) -> void:
	# Auto-pause when the app is backgrounded on mobile.
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT or what == NOTIFICATION_APPLICATION_PAUSED:
		if is_inside_tree() and not get_tree().paused:
			_toggle_pause()


func _toggle_pause() -> void:
	var tree := get_tree()
	tree.paused = not tree.paused
	if tree.paused:
		touch.release_all()
		pause_menu.open()
	else:
		pause_menu.close()


func _restart() -> void:
	get_tree().paused = false
	get_tree().reload_current_scene()


func _quit_to_menu() -> void:
	get_tree().paused = false
	Settings.save_settings()
	get_tree().change_scene_to_file("res://scenes/boot/main.tscn")


func _on_lap_completed(_n: int, lap_time: float, valid: bool, _pb: bool) -> void:
	if valid and not autopilot_enabled and Settings.submit_lap(track_id, lap_time):
		hud.set_record(lap_time)
	lap_recorded.emit(lap_time, valid)
