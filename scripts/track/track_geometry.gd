class_name TrackGeometry
extends RefCounted
## Baked centreline of a closed circuit, generated from layout control points.
##
## Control points are interpolated with a closed centripetal Catmull-Rom spline
## (identical to tools/preview_track.py), then resampled at a uniform spacing.
## Provides arc-length queries used by mesh generation, lap timing and AI.

const SAMPLES_PER_SEGMENT := 64

var spacing: float = 2.0
var length: float = 0.0
var positions: PackedVector3Array = PackedVector3Array()
var tangents: PackedVector3Array = PackedVector3Array()
## Horizontal unit vector pointing to the right of the driving direction.
var rights: PackedVector3Array = PackedVector3Array()
var widths: PackedFloat32Array = PackedFloat32Array()
## Signed curvature (1/m). Positive = turning right.
var curvatures: PackedFloat32Array = PackedFloat32Array()
var distances: PackedFloat32Array = PackedFloat32Array()


## Build from rows of [x, z, elevation, width].
static func from_points(points: Array, sample_spacing: float = 2.0) -> TrackGeometry:
	var g := TrackGeometry.new()
	g.spacing = sample_spacing
	g._build(points)
	return g


func sample_count() -> int:
	return positions.size()


func _build(points: Array) -> void:
	var n := points.size()
	assert(n >= 4, "A circuit needs at least 4 control points")
	var ctrl: Array[Vector4] = []
	for p in points:
		ctrl.append(Vector4(float(p[0]), float(p[2]), float(p[1]), float(p[3])))
	# Dense sampling of the spline (x, y, z, width packed in Vector4).
	var dense: Array[Vector4] = []
	for i in n:
		var p0 := ctrl[(i - 1 + n) % n]
		var p1 := ctrl[i]
		var p2 := ctrl[(i + 1) % n]
		var p3 := ctrl[(i + 2) % n]
		for s in SAMPLES_PER_SEGMENT:
			dense.append(_catmull_rom(p0, p1, p2, p3, float(s) / SAMPLES_PER_SEGMENT))
	# Cumulative horizontal+vertical arc length of the dense polyline.
	var cum := PackedFloat32Array()
	cum.resize(dense.size() + 1)
	cum[0] = 0.0
	for i in dense.size():
		var a := dense[i]
		var b := dense[(i + 1) % dense.size()]
		cum[i + 1] = cum[i] + Vector3(b.x - a.x, b.y - a.y, b.z - a.z).length()
	var total := cum[dense.size()]
	var count := maxi(16, int(round(total / spacing)))
	spacing = total / count
	length = total
	positions.resize(count)
	widths.resize(count)
	distances.resize(count)
	var j := 0
	for k in count:
		var d := k * spacing
		while cum[j + 1] < d:
			j += 1
		var t := (d - cum[j]) / maxf(cum[j + 1] - cum[j], 1e-6)
		var v := dense[j].lerp(dense[(j + 1) % dense.size()], t)
		positions[k] = Vector3(v.x, v.y, v.z)
		widths[k] = v.w
		distances[k] = d
	tangents.resize(count)
	rights.resize(count)
	curvatures.resize(count)
	for k in count:
		var prev := positions[(k - 1 + count) % count]
		var next := positions[(k + 1) % count]
		var tng := (next - prev).normalized()
		tangents[k] = tng
		var flat := Vector3(tng.x, 0.0, tng.z).normalized()
		rights[k] = flat.cross(Vector3.UP).normalized()
	for k in count:
		var a := tangents[(k - 2 + count) % count]
		var b := tangents[(k + 2) % count]
		var a2 := Vector2(a.x, a.z).normalized()
		var b2 := Vector2(b.x, b.z).normalized()
		curvatures[k] = a2.angle_to(b2) / (4.0 * spacing)


static func _catmull_rom(p0: Vector4, p1: Vector4, p2: Vector4, p3: Vector4, u: float) -> Vector4:
	# Centripetal parameterisation on the horizontal plane avoids cusps/loops.
	var t0 := 0.0
	var t1 := t0 + sqrt(maxf(Vector2(p1.x - p0.x, p1.z - p0.z).length(), 1e-4))
	var t2 := t1 + sqrt(maxf(Vector2(p2.x - p1.x, p2.z - p1.z).length(), 1e-4))
	var t3 := t2 + sqrt(maxf(Vector2(p3.x - p2.x, p3.z - p2.z).length(), 1e-4))
	var t := t1 + (t2 - t1) * u
	var a1 := p0 * ((t1 - t) / (t1 - t0)) + p1 * ((t - t0) / (t1 - t0))
	var a2 := p1 * ((t2 - t) / (t2 - t1)) + p2 * ((t - t1) / (t2 - t1))
	var a3 := p2 * ((t3 - t) / (t3 - t2)) + p3 * ((t - t2) / (t3 - t2))
	var b1 := a1 * ((t2 - t) / (t2 - t0)) + a2 * ((t - t0) / (t2 - t0))
	var b2 := a2 * ((t3 - t) / (t3 - t1)) + a3 * ((t - t1) / (t3 - t1))
	return b1 * ((t2 - t) / (t2 - t1)) + b2 * ((t - t1) / (t2 - t1))


func wrap_distance(d: float) -> float:
	return fposmod(d, length)


func index_at(d: float) -> int:
	return int(wrap_distance(d) / spacing) % positions.size()


func position_at(d: float) -> Vector3:
	var x := wrap_distance(d) / spacing
	var i := int(x) % positions.size()
	var j := (i + 1) % positions.size()
	return positions[i].lerp(positions[j], x - floorf(x))


func tangent_at(d: float) -> Vector3:
	var x := wrap_distance(d) / spacing
	var i := int(x) % positions.size()
	var j := (i + 1) % positions.size()
	return tangents[i].lerp(tangents[j], x - floorf(x)).normalized()


func curvature_at(d: float) -> float:
	return curvatures[index_at(d)]


func width_at(d: float) -> float:
	return widths[index_at(d)]


## Transform for placing an object on the centreline facing the driving direction.
## [param lateral] offsets to the right (metres).
func transform_at(d: float, lateral: float = 0.0, height: float = 0.0) -> Transform3D:
	var fwd := tangent_at(d)
	var right := fwd.cross(Vector3.UP).normalized()
	var up := right.cross(fwd).normalized()
	var origin := position_at(d) + right * lateral + up * height
	# Godot convention: -Z is forward.
	return Transform3D(Basis(right, up, -fwd), origin)


## Projects a world position onto the centreline. Searches a window around
## [param hint_index] when given (>= 0) for O(1) per-frame tracking, otherwise
## does a full search. Returns Vector3(distance_along, lateral_offset, index).
func project(world: Vector3, hint_index: int = -1, window: int = 40) -> Vector3:
	var count := positions.size()
	var best_i := 0
	var best_d2 := INF
	if hint_index < 0:
		for i in count:
			var d2 := _flat_dist2(positions[i], world)
			if d2 < best_d2:
				best_d2 = d2
				best_i = i
	else:
		for o in range(-window, window + 1):
			var i := (hint_index + o + count) % count
			var d2 := _flat_dist2(positions[i], world)
			if d2 < best_d2:
				best_d2 = d2
				best_i = i
	# Refine on the better of the two adjacent segments.
	var best := Vector3(distances[best_i], 0.0, best_i)
	var best_seg_d2 := INF
	for seg: int in [best_i - 1, best_i]:
		var a_i := (seg + count) % count
		var b_i := (a_i + 1) % count
		var a := Vector2(positions[a_i].x, positions[a_i].z)
		var b := Vector2(positions[b_i].x, positions[b_i].z)
		var p := Vector2(world.x, world.z)
		var ab := b - a
		var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 1e-9), 0.0, 1.0)
		var c := a + ab * t
		var d2 := p.distance_squared_to(c)
		if d2 < best_seg_d2:
			best_seg_d2 = d2
			var r := Vector2(rights[a_i].x, rights[a_i].z)
			var lateral := (p - c).dot(r)
			best = Vector3(wrap_distance(distances[a_i] + t * spacing), lateral, best_i)
	return best


static func _flat_dist2(a: Vector3, b: Vector3) -> float:
	var dx := a.x - b.x
	var dz := a.z - b.z
	return dx * dx + dz * dz
