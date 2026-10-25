class_name Circuit
extends Node3D
## Procedurally builds a circuit (road, kerbs, run-off, barriers, scenery and
## collision) from a layout JSON in data/tracks/. All geometry is original and
## generated at load time, so no third-party track assets are involved.

signal built

@export_file("*.json") var layout_path: String = "res://data/tracks/kestrel_ridge.json"
@export var sample_spacing: float = 2.0
@export var build_scenery: bool = true

const KERB_WIDTH := 1.3
const KERB_HEIGHT := 0.045
const KERB_CURVATURE := 1.0 / 260.0
const LINE_WIDTH := 0.3
const RUNOFF_WIDTH := 16.0
const RUNOFF_DROP := 0.12
const WALL_HEIGHT := 1.1
const EMBANKMENT_WIDTH := 24.0
const GROUND_LEVEL := -0.8

var geometry: TrackGeometry
var display_name: String = ""
var start_line_distance: float = 0.0
## Sector start distances measured from the start line (first is always 0).
var sector_starts: PackedFloat32Array = PackedFloat32Array([0.0])

var _kerb_left: PackedFloat32Array
var _kerb_right: PackedFloat32Array


func _ready() -> void:
	build()


func build() -> void:
	var data := load_layout(layout_path)
	if data.is_empty():
		push_error("Circuit: could not load layout %s" % layout_path)
		return
	display_name = data.get("name", "Unnamed circuit")
	geometry = TrackGeometry.from_points(data["points"], sample_spacing)
	start_line_distance = float(data.get("start_line_distance", 0.0))
	sector_starts = PackedFloat32Array()
	for f in data.get("sector_fractions", [0.0]):
		sector_starts.append(float(f) * geometry.length)
	_compute_kerbs()
	_build_road()
	_build_kerbs()
	_build_runoff_and_walls()
	_build_ground()
	_build_start_line()
	if build_scenery:
		_build_scenery()
	built.emit()


static func load_layout(path: String) -> Dictionary:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var parsed: Variant = JSON.parse_string(f.get_as_text())
	return parsed if parsed is Dictionary else {}


## Distance along the track measured from the start/finish line.
func lap_distance(raw_distance: float) -> float:
	return fposmod(raw_distance - start_line_distance, geometry.length)


func raw_distance(lap_dist: float) -> float:
	return geometry.wrap_distance(lap_dist + start_line_distance)


## Grid slot transform: slot 0 is pole. Staggered two-wide, 8 m apart.
func grid_transform(slot: int) -> Transform3D:
	var back := 12.0 + slot * 8.0
	var side := -3.0 if slot % 2 == 0 else 3.0
	return geometry.transform_at(raw_distance(-back), side, 0.0)


# --- Kerb placement -----------------------------------------------------------

func _compute_kerbs() -> void:
	var n := geometry.sample_count()
	_kerb_left = PackedFloat32Array()
	_kerb_right = PackedFloat32Array()
	_kerb_left.resize(n)
	_kerb_right.resize(n)
	var raw_l := PackedFloat32Array()
	var raw_r := PackedFloat32Array()
	raw_l.resize(n)
	raw_r.resize(n)
	for i in n:
		var c := geometry.curvatures[i]
		if absf(c) > KERB_CURVATURE:
			# Kerbs on both sides through corners; inside kerb is what drivers clip.
			raw_l[i] = 1.0
			raw_r[i] = 1.0
	# Dilate by ~16 m and taper the ends so kerbs blend in/out smoothly.
	var dilate := int(16.0 / geometry.spacing)
	var taper := 4
	for i in n:
		var l := 0.0
		var r := 0.0
		for o in range(-dilate - taper, dilate + taper + 1):
			var j := (i + o + n) % n
			var w := 1.0
			if absi(o) > dilate:
				w = 1.0 - float(absi(o) - dilate) / float(taper + 1)
			l = maxf(l, raw_l[j] * w)
			r = maxf(r, raw_r[j] * w)
		_kerb_left[i] = l
		_kerb_right[i] = r


func _kerb_width(i: int, side: float) -> float:
	return KERB_WIDTH * (_kerb_right[i] if side > 0.0 else _kerb_left[i])


# --- Mesh helpers -------------------------------------------------------------

## Adds a ribbon between two lateral offsets along the whole track.
## offset_fn(i) -> Vector4(offset_a, height_a, offset_b, height_b), offsets
## signed (+right). color_fn(i) -> Color. Quads where both widths are ~0 are
## skipped.
func _ribbon(st: SurfaceTool, offset_fn: Callable, color_fn: Callable, uv_scale: float = 0.1) -> void:
	var n := geometry.sample_count()
	for i in n:
		var j := (i + 1) % n
		var oi: Vector4 = offset_fn.call(i)
		var oj: Vector4 = offset_fn.call(j)
		if absf(oi.z - oi.x) < 0.01 and absf(oj.z - oj.x) < 0.01:
			continue
		var ai := geometry.positions[i] + geometry.rights[i] * oi.x + Vector3.UP * oi.y
		var bi := geometry.positions[i] + geometry.rights[i] * oi.z + Vector3.UP * oi.w
		var aj := geometry.positions[j] + geometry.rights[j] * oj.x + Vector3.UP * oj.y
		var bj := geometry.positions[j] + geometry.rights[j] * oj.z + Vector3.UP * oj.w
		var vi := geometry.distances[i] * uv_scale
		var vj := vi + geometry.spacing * uv_scale
		var ci: Color = color_fn.call(i)
		# Make sure triangles face up regardless of which side is "a".
		var flip := oi.x > oi.z
		_quad(st, ai, bi, aj, bj, Vector2(oi.x * uv_scale, vi), Vector2(oi.z * uv_scale, vi),
				Vector2(oj.x * uv_scale, vj), Vector2(oj.z * uv_scale, vj), ci, flip)


func _quad(st: SurfaceTool, ai: Vector3, bi: Vector3, aj: Vector3, bj: Vector3,
		uai: Vector2, ubi: Vector2, uaj: Vector2, ubj: Vector2, c: Color, flip: bool) -> void:
	# Driving direction is i -> j, "b" is to the right of "a" unless flipped.
	# Godot uses clockwise winding for front faces.
	var verts := [ai, aj, bi, bi, aj, bj]
	var uvs := [uai, uaj, ubi, ubi, uaj, ubj]
	if flip:
		verts = [ai, bi, aj, bi, bj, aj]
		uvs = [uai, ubi, uaj, ubi, ubj, uaj]
	for k in 6:
		st.set_color(c)
		st.set_uv(uvs[k])
		st.add_vertex(verts[k])


func _finish_mesh(st: SurfaceTool, material: Material) -> ArrayMesh:
	st.generate_normals()
	st.generate_tangents()
	var mesh := st.commit()
	mesh.surface_set_material(0, material)
	return mesh


func _add_static(mesh: ArrayMesh, surface: StringName, node_name: String, cast_shadows: bool = false) -> void:
	var mi := MeshInstance3D.new()
	mi.name = node_name
	mi.mesh = mesh
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if cast_shadows else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	var body := StaticBody3D.new()
	body.name = node_name + "Body"
	body.set_meta(&"surface", surface)
	body.collision_layer = 1
	body.collision_mask = 0
	var pm := PhysicsMaterial.new()
	pm.friction = 0.6 if surface != SurfaceTypes.WALL else 0.25
	pm.bounce = 0.0 if surface != SurfaceTypes.WALL else 0.15
	body.physics_material_override = pm
	var cs := CollisionShape3D.new()
	cs.shape = mesh.create_trimesh_shape()
	body.add_child(cs)
	add_child(body)


static func _make_material(albedo: Color, roughness: float, use_vertex_color: bool, texture: Texture2D = null) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = albedo
	m.roughness = roughness
	m.vertex_color_use_as_albedo = use_vertex_color
	if texture:
		m.albedo_texture = texture
		m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	return m


static func _noise_texture(seed_value: int, frequency: float, size: int = 256) -> NoiseTexture2D:
	var noise := FastNoiseLite.new()
	noise.seed = seed_value
	noise.frequency = frequency
	noise.fractal_octaves = 4
	var tex := NoiseTexture2D.new()
	tex.width = size
	tex.height = size
	tex.seamless = true
	tex.generate_mipmaps = true
	tex.noise = noise
	# Remap to a narrow bright range so it modulates rather than dominates.
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.82, 0.82, 0.82))
	ramp.set_color(1, Color(1.0, 1.0, 1.0))
	tex.color_ramp = ramp
	return tex


# --- Track parts --------------------------------------------------------------

func _build_road() -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var asphalt := Color(0.25, 0.25, 0.27)
	var line := Color(0.92, 0.92, 0.92)
	var g := geometry
	var left_line := func(i: int) -> Vector4:
		var h := g.widths[i] * 0.5
		return Vector4(-h, 0.0, -h + LINE_WIDTH, 0.0)
	var centre := func(i: int) -> Vector4:
		var h := g.widths[i] * 0.5
		return Vector4(-h + LINE_WIDTH, 0.0, h - LINE_WIDTH, 0.0)
	var right_line := func(i: int) -> Vector4:
		var h := g.widths[i] * 0.5
		return Vector4(h - LINE_WIDTH, 0.0, h, 0.0)
	var line_color := func(_i: int) -> Color: return line
	var asphalt_color := func(_i: int) -> Color: return asphalt
	_ribbon(st, left_line, line_color)
	_ribbon(st, centre, asphalt_color)
	_ribbon(st, right_line, line_color)
	var mat := _make_material(Color.WHITE, 0.85, true, _noise_texture(11, 0.08))
	mat.uv1_scale = Vector3(1.0, 1.0, 1.0)
	_add_static(_finish_mesh(st, mat), SurfaceTypes.ASPHALT, "Road")


func _build_kerbs() -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var red := Color(0.82, 0.12, 0.1)
	var white := Color(0.95, 0.95, 0.95)
	var g := geometry
	var stripe := func(i: int) -> Color:
		return red if int(g.distances[i] / 3.0) % 2 == 0 else white
	for side: float in [-1.0, 1.0]:
		# Rising half then falling half to form a shallow ridge.
		var rising := func(i: int) -> Vector4:
			var h := g.widths[i] * 0.5
			var w := _kerb_width(i, side)
			var k := KERB_HEIGHT * minf(w / KERB_WIDTH * 2.0, 1.0)
			return Vector4(side * h, 0.0, side * (h + w * 0.5), k)
		var falling := func(i: int) -> Vector4:
			var h := g.widths[i] * 0.5
			var w := _kerb_width(i, side)
			var k := KERB_HEIGHT * minf(w / KERB_WIDTH * 2.0, 1.0)
			return Vector4(side * (h + w * 0.5), k, side * (h + w), 0.0)
		_ribbon(st, rising, stripe)
		_ribbon(st, falling, stripe)
	_add_static(_finish_mesh(st, _make_material(Color.WHITE, 0.6, true)), SurfaceTypes.KERB, "Kerbs")


func _wall_offset(i: int, side: float) -> float:
	return geometry.widths[i] * 0.5 + _kerb_width(i, side) + RUNOFF_WIDTH


func _build_runoff_and_walls() -> void:
	var g := geometry
	var grass_st := SurfaceTool.new()
	grass_st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var wall_st := SurfaceTool.new()
	wall_st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var bank_st := SurfaceTool.new()
	bank_st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var grass := Color(0.32, 0.5, 0.2)
	var grass_color := func(_i: int) -> Color: return grass
	var bank_color := func(_i: int) -> Color: return grass.darkened(0.08)
	for side: float in [-1.0, 1.0]:
		var runoff := func(i: int) -> Vector4:
			var inner := g.widths[i] * 0.5 + _kerb_width(i, side)
			return Vector4(side * inner, 0.0, side * _wall_offset(i, side), -RUNOFF_DROP)
		_ribbon(grass_st, runoff, grass_color, 0.25)
		# Wall: a vertical ribbon built from two "offsets" at the same lateral
		# position, so build it with explicit quads instead.
		var n := g.sample_count()
		for i in n:
			var j := (i + 1) % n
			var pi := g.positions[i] + g.rights[i] * side * _wall_offset(i, side)
			var pj := g.positions[j] + g.rights[j] * side * _wall_offset(j, side)
			var bot := Vector3.UP * (-RUNOFF_DROP - 0.6)
			var top := Vector3.UP * (-RUNOFF_DROP + WALL_HEIGHT)
			var shade := 0.85 if int(g.distances[i] / 6.0) % 2 == 0 else 0.7
			var c := Color(shade, shade, shade * 1.02)
			var v := g.distances[i] * 0.25
			# Face the track (inwards).
			_quad(wall_st, pi + bot, pi + top, pj + bot, pj + top,
					Vector2(0, v), Vector2(1, v), Vector2(0, v + 0.5), Vector2(1, v + 0.5), c, side < 0.0)
		# Visual embankment from the wall base down to the surrounding ground.
		var embankment := func(i: int) -> Vector4:
			var o := _wall_offset(i, side)
			var drop := GROUND_LEVEL - g.positions[i].y
			return Vector4(side * o, -RUNOFF_DROP, side * (o + EMBANKMENT_WIDTH), drop)
		_ribbon(bank_st, embankment, bank_color, 0.25)
	var grass_tex := _noise_texture(23, 0.05)
	_add_static(_finish_mesh(grass_st, _make_material(Color.WHITE, 0.95, true, grass_tex)), SurfaceTypes.GRASS, "Runoff")
	var wall_mat := _make_material(Color.WHITE, 0.7, true)
	wall_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	_add_static(_finish_mesh(wall_st, wall_mat), SurfaceTypes.WALL, "Barriers", true)
	var bank := MeshInstance3D.new()
	bank.name = "Embankment"
	bank.mesh = _finish_mesh(bank_st, _make_material(Color.WHITE, 0.95, true, grass_tex))
	bank.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(bank)


func _build_ground() -> void:
	var aabb := AABB(geometry.positions[0], Vector3.ZERO)
	for p in geometry.positions:
		aabb = aabb.expand(p)
	var size := maxf(aabb.size.x, aabb.size.z) + 1200.0
	var plane := PlaneMesh.new()
	plane.size = Vector2(size, size)
	var mat := _make_material(Color(0.3, 0.46, 0.19), 1.0, false, _noise_texture(5, 0.02))
	mat.uv1_scale = Vector3(size / 40.0, size / 40.0, 1.0)
	plane.material = mat
	var mi := MeshInstance3D.new()
	mi.name = "Ground"
	mi.mesh = plane
	mi.position = Vector3(aabb.get_center().x, GROUND_LEVEL - 0.05, aabb.get_center().z)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	# Safety net far below the track so an escaped car can always be recovered.
	var body := StaticBody3D.new()
	body.name = "SafetyFloor"
	body.set_meta(&"surface", SurfaceTypes.GRASS)
	var cs := CollisionShape3D.new()
	var wb := WorldBoundaryShape3D.new()
	cs.shape = wb
	body.position = Vector3(0, GROUND_LEVEL - 4.0, 0)
	body.add_child(cs)
	add_child(body)


func _build_start_line() -> void:
	var xf := geometry.transform_at(start_line_distance)
	var w := geometry.width_at(start_line_distance)
	# Chequered strip.
	var img := Image.create(16, 2, false, Image.FORMAT_RGB8)
	for x in 16:
		for y in 2:
			img.set_pixel(x, y, Color.WHITE if (x + y) % 2 == 0 else Color(0.08, 0.08, 0.08))
	var tex := ImageTexture.create_from_image(img)
	var quad := QuadMesh.new()
	quad.size = Vector2(w, 1.0)
	var mat := _make_material(Color.WHITE, 0.6, false, tex)
	mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS
	quad.material = mat
	var mi := MeshInstance3D.new()
	mi.name = "StartLine"
	mi.mesh = quad
	mi.transform = xf * Transform3D(Basis(Vector3.RIGHT, -PI / 2.0), Vector3(0, 0.012, 0))
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	# Gantry over the line.
	var gantry := Node3D.new()
	gantry.name = "Gantry"
	gantry.transform = xf
	add_child(gantry)
	var steel := _make_material(Color(0.2, 0.22, 0.25), 0.4, false)
	steel.metallic = 0.6
	for side: float in [-1.0, 1.0]:
		var post := MeshInstance3D.new()
		var pm := BoxMesh.new()
		pm.size = Vector3(0.5, 7.0, 0.5)
		pm.material = steel
		post.mesh = pm
		post.position = Vector3(side * (w * 0.5 + 2.5), 3.5, 0)
		gantry.add_child(post)
	var beam := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = Vector3(w + 5.5, 1.2, 0.6)
	bm.material = _make_material(Color(0.1, 0.1, 0.12), 0.5, false)
	beam.mesh = bm
	beam.position = Vector3(0, 7.0, 0)
	gantry.add_child(beam)
	for k in 5:
		var lamp := MeshInstance3D.new()
		var lm := SphereMesh.new()
		lm.radius = 0.22
		lm.height = 0.44
		var lmat := _make_material(Color(0.9, 0.1, 0.05), 0.3, false)
		lmat.emission_enabled = true
		lmat.emission = Color(0.6, 0.05, 0.02)
		lm.material = lmat
		lamp.mesh = lm
		lamp.position = Vector3((k - 2) * 0.9, 7.0, 0.32)
		gantry.add_child(lamp)


func _build_scenery() -> void:
	var g := geometry
	var rng := RandomNumberGenerator.new()
	rng.seed = 1337
	# Trees: cheap two-part meshes instanced with MultiMesh, placed well
	# outside the barriers and away from every part of the circuit.
	var transforms: Array[Transform3D] = []
	var attempts := 0
	while transforms.size() < 900 and attempts < 6000:
		attempts += 1
		var d := rng.randf() * g.length
		var side := -1.0 if rng.randf() < 0.5 else 1.0
		var lateral := side * (g.width_at(d) * 0.5 + RUNOFF_WIDTH + 12.0 + rng.randf() * 120.0)
		var p := g.position_at(d) + g.tangent_at(d).cross(Vector3.UP).normalized() * lateral
		var proj := g.project(p)
		if absf(proj.y) < g.width_at(proj.x) * 0.5 + RUNOFF_WIDTH + EMBANKMENT_WIDTH + 4.0:
			continue
		var s := rng.randf_range(0.8, 1.5)
		var b := Basis(Vector3.UP, rng.randf() * TAU).scaled(Vector3(s, s * rng.randf_range(0.9, 1.3), s))
		transforms.append(Transform3D(b, Vector3(p.x, GROUND_LEVEL, p.z)))
	var tree := _tree_mesh()
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = tree
	mm.instance_count = transforms.size()
	for i in transforms.size():
		mm.set_instance_transform(i, transforms[i])
	var mmi := MultiMeshInstance3D.new()
	mmi.name = "Trees"
	mmi.multimesh = mm
	add_child(mmi)
	# Grandstand opposite the pit straight.
	var stand_d := start_line_distance - 60.0
	for k in 3:
		var d := stand_d + k * 45.0
		var xf := g.transform_at(d, -(g.width_at(d) * 0.5 + RUNOFF_WIDTH + 8.0))
		var stand := MeshInstance3D.new()
		stand.name = "Grandstand%d" % k
		stand.mesh = _grandstand_mesh()
		stand.transform = xf
		add_child(stand)


func _tree_mesh() -> ArrayMesh:
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.25
	trunk.bottom_radius = 0.35
	trunk.height = 2.5
	trunk.radial_segments = 6
	var crown := CylinderMesh.new()
	crown.top_radius = 0.0
	crown.bottom_radius = 2.6
	crown.height = 7.0
	crown.radial_segments = 8
	var mesh := ArrayMesh.new()
	_append_colored(mesh, trunk, Transform3D(Basis(), Vector3(0, 1.25, 0)), Color(0.36, 0.26, 0.16))
	_append_colored(mesh, crown, Transform3D(Basis(), Vector3(0, 5.5, 0)), Color(0.16, 0.33, 0.14))
	return mesh


func _append_colored(target: ArrayMesh, source: PrimitiveMesh, xf: Transform3D, color: Color) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_color(color)
	st.append_from(source, 0, xf)
	var arrays := st.commit_to_arrays()
	var colors := PackedColorArray()
	colors.resize((arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array).size())
	colors.fill(color)
	arrays[Mesh.ARRAY_COLOR] = colors
	target.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	target.surface_set_material(target.get_surface_count() - 1, _make_material(Color.WHITE, 0.9, true))


func _grandstand_mesh() -> ArrayMesh:
	var mesh := ArrayMesh.new()
	for row in 6:
		var step := BoxMesh.new()
		step.size = Vector3(1.2, 0.6, 40.0)
		_append_colored(mesh, step, Transform3D(Basis(), Vector3(-row * 1.2, 0.3 + row * 0.6, 0)),
				Color(0.55, 0.57, 0.6) if row % 2 == 0 else Color(0.2, 0.35, 0.6))
	var roof := BoxMesh.new()
	roof.size = Vector3(9.0, 0.25, 41.0)
	_append_colored(mesh, roof, Transform3D(Basis(), Vector3(-3.0, 7.0, 0)), Color(0.85, 0.85, 0.88))
	return mesh
