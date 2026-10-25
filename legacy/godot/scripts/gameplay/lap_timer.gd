class_name LapTimer
extends RefCounted
## Engine-independent lap and sector timing.
##
## Fed with consecutive (lap_distance, simulation_time) samples from the fixed
## physics step. Line crossings are interpolated between samples, so timing
## resolution is far finer than one tick. Sectors must be passed in order for a
## lap to count, which defeats shortcuts and reversing over the line.

signal lap_started(lap_number: int)
signal lap_completed(lap_number: int, lap_time: float, valid: bool, is_personal_best: bool)
signal sector_completed(sector_index: int, sector_time: float, delta_to_best: float)
signal lap_invalidated(reason: String)

## Max forward progress accepted in one sample; bigger jumps are teleports.
const MAX_STEP_DISTANCE := 60.0
## Spacing of the reference trace used for the live delta.
const TRACE_STEP := 10.0

var track_length: float
var sector_starts: PackedFloat32Array

var lap_number: int = 0
var lap_active: bool = false
var lap_valid: bool = true
var lap_start_time: float = 0.0
var next_sector: int = 0
var sector_times: PackedFloat64Array = PackedFloat64Array()
var last_lap_time: float = 0.0
var last_lap_valid: bool = false
var best_lap_time: float = 0.0
var best_sector_times: PackedFloat64Array = PackedFloat64Array()
var completed_laps: Array[Dictionary] = []

var _sector_start_time: float = 0.0
var _current_trace: PackedFloat64Array = PackedFloat64Array()
var _best_trace: PackedFloat64Array = PackedFloat64Array()
var _prev_distance: float = -1.0
var _prev_time: float = 0.0
var _last_time: float = 0.0


func _init(length: float, sectors: PackedFloat32Array = PackedFloat32Array([0.0])) -> void:
	track_length = length
	sector_starts = sectors if sectors.size() > 0 else PackedFloat32Array([0.0])
	if sector_starts[0] != 0.0:
		sector_starts.insert(0, 0.0)
	best_sector_times.resize(sector_starts.size())
	best_sector_times.fill(0.0)


func sector_count() -> int:
	return sector_starts.size()


## Current lap elapsed time (0 when no lap is running).
func current_lap_time(now: float = -1.0) -> float:
	if not lap_active:
		return 0.0
	return (now if now >= 0.0 else _last_time) - lap_start_time


## Live delta against the best valid lap at the given lap distance; NAN when
## there is no reference yet.
func live_delta(lap_distance: float, now: float = -1.0) -> float:
	if not lap_active or _best_trace.is_empty():
		return NAN
	var x := clampf(lap_distance / TRACE_STEP, 0.0, float(_best_trace.size() - 1))
	var i := mini(int(x), _best_trace.size() - 2)
	if i < 0:
		return NAN
	var ref := lerpf(_best_trace[i], _best_trace[i + 1], x - float(i))
	return current_lap_time(now) - ref


## Forget the previous sample (after a reset/teleport). The running lap is
## aborted and timing resumes at the next start-line crossing.
func reset_position() -> void:
	_prev_distance = -1.0
	if lap_active:
		lap_active = false
		lap_invalidated.emit("Car reset")


func invalidate(reason: String) -> void:
	if lap_active and lap_valid:
		lap_valid = false
		lap_invalidated.emit(reason)


## Feed one sample. [param lap_distance] is measured from the start line in
## [0, track_length).
func update(lap_distance: float, time: float) -> void:
	_last_time = time
	if _prev_distance < 0.0:
		_prev_distance = lap_distance
		_prev_time = time
		return
	var delta := lap_distance - _prev_distance
	# Unwrap across the start line.
	if delta < -track_length * 0.5:
		delta += track_length
	elif delta > track_length * 0.5:
		delta -= track_length
	if absf(delta) > MAX_STEP_DISTANCE:
		reset_position()
		_prev_distance = lap_distance
		_prev_time = time
		return
	if delta > 0.0:
		_check_crossings(_prev_distance, delta, _prev_time, time)
	if lap_active:
		_record_trace(lap_distance, time)
	_prev_distance = lap_distance
	_prev_time = time


func _check_crossings(from_dist: float, delta: float, t0: float, t1: float) -> void:
	# Process boundaries in driving order; at most a couple per sample.
	var guard := 0
	while guard < sector_count():
		guard += 1
		var boundary := sector_starts[next_sector]
		var ahead := fposmod(boundary - from_dist, track_length)
		if ahead > delta:
			return
		var crossing_time := t0 + (t1 - t0) * (ahead / delta)
		_on_boundary(next_sector, crossing_time)


func _on_boundary(index: int, t: float) -> void:
	if index == 0:
		if lap_active:
			_finish_sector(sector_count() - 1, t)
			_finish_lap(t)
		_start_lap(t)
	else:
		if lap_active:
			_finish_sector(index - 1, t)
	next_sector = (index + 1) % sector_count()


func _start_lap(t: float) -> void:
	lap_number += 1
	lap_active = true
	lap_valid = true
	lap_start_time = t
	_sector_start_time = t
	sector_times = PackedFloat64Array()
	sector_times.resize(sector_count())
	sector_times.fill(0.0)
	_current_trace = PackedFloat64Array([0.0])
	lap_started.emit(lap_number)


func _finish_sector(index: int, t: float) -> void:
	var st := t - _sector_start_time
	sector_times[index] = st
	_sector_start_time = t
	var best := best_sector_times[index]
	var d := st - best if best > 0.0 else NAN
	if lap_valid and (best <= 0.0 or st < best):
		best_sector_times[index] = st
	sector_completed.emit(index, st, d)


func _finish_lap(t: float) -> void:
	var lap_time := t - lap_start_time
	last_lap_time = lap_time
	last_lap_valid = lap_valid
	var pb := lap_valid and (best_lap_time <= 0.0 or lap_time < best_lap_time)
	if pb:
		best_lap_time = lap_time
		_current_trace.append(lap_time)
		_best_trace = _current_trace
	completed_laps.append({
		"lap": lap_number, "time": lap_time, "valid": lap_valid,
		"sectors": sector_times.duplicate(),
	})
	lap_completed.emit(lap_number, lap_time, lap_valid, pb)


func _record_trace(lap_distance: float, time: float) -> void:
	var idx := int(lap_distance / TRACE_STEP)
	while _current_trace.size() <= idx and _current_trace.size() * TRACE_STEP <= track_length:
		_current_trace.append(time - lap_start_time)
