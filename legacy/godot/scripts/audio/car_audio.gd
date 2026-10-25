extends Node
## Procedural engine and tire audio synthesised with AudioStreamGenerator, so
## no recorded (licensable) sound assets are needed for the prototype.
## Engine: firing-frequency harmonics shaped by throttle. Tires: filtered
## noise scaled by combined slip.

const MIX_RATE := 22050.0
const CYLINDERS := 6

var _car: FormulaCar
var _player: AudioStreamPlayer
var _playback: AudioStreamGeneratorPlayback
var _phase: float = 0.0
var _rpm_smooth: float = 4000.0
var _throttle_smooth: float = 0.0
var _squeal_smooth: float = 0.0
var _noise_state: float = 0.0
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_car = get_parent() as FormulaCar
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = MIX_RATE
	gen.buffer_length = 0.1
	_player = AudioStreamPlayer.new()
	_player.stream = gen
	_player.volume_db = linear_to_db(Settings.master_volume * 0.5)
	add_child(_player)
	_player.play()
	_playback = _player.get_stream_playback() as AudioStreamGeneratorPlayback


func _process(delta: float) -> void:
	if _car == null or _playback == null:
		return
	var k := 1.0 - exp(-delta * 18.0)
	_rpm_smooth = lerpf(_rpm_smooth, _car.engine_rpm, k)
	_throttle_smooth = lerpf(_throttle_smooth, _car.input_throttle, k)
	var slip := 0.0
	for w in _car.wheels:
		if w.contact:
			slip = maxf(slip, maxf(absf(w.slip_ratio) / 0.15, absf(w.slip_angle) / 0.15))
	_squeal_smooth = lerpf(_squeal_smooth, clampf(slip - 0.9, 0.0, 1.0), k)
	_fill()


func _fill() -> void:
	var frames := _playback.get_frames_available()
	if frames <= 0:
		return
	var freq := _rpm_smooth / 60.0 * CYLINDERS * 0.5
	var step := freq / MIX_RATE
	var engine_load := 0.35 + 0.65 * _throttle_smooth
	var squeal := _squeal_smooth * 0.25
	var buf := PackedVector2Array()
	buf.resize(frames)
	for i in frames:
		_phase = fmod(_phase + step, 1.0)
		var p := _phase * TAU
		var s := sin(p) * 0.5 + sin(p * 2.0) * 0.3 * engine_load + sin(p * 3.0) * 0.18 * engine_load + sin(p * 0.5) * 0.15
		# Cheap low-passed noise for tire scrub.
		_noise_state = lerpf(_noise_state, _rng.randf_range(-1.0, 1.0), 0.3)
		var v := s * 0.32 * (0.6 + 0.4 * engine_load) + _noise_state * squeal
		buf[i] = Vector2(v, v)
	_playback.push_buffer(buf)
