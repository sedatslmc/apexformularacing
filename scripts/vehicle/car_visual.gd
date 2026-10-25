extends Node3D
## Builds an original open-wheel car body from primitive meshes. This is a
## placeholder art pass with correct proportions; it will be replaced by a
## Blender model (see docs/ROADMAP.md) without touching the physics.
##
## Coordinates are relative to the car's centre of mass: ground is at
## y = -cog_height, forward is -Z.

@export var primary_color: Color = Color(0.06, 0.32, 0.62)
@export var secondary_color: Color = Color(0.95, 0.75, 0.1)
@export var carbon_color: Color = Color(0.07, 0.07, 0.08)
@export var ground_y: float = -0.30

var _paint: StandardMaterial3D
var _accent: StandardMaterial3D
var _carbon: StandardMaterial3D
var _tire: StandardMaterial3D
var _rim: StandardMaterial3D


func _ready() -> void:
	_paint = _mat(primary_color, 0.25, 0.3)
	_paint.clearcoat_enabled = true
	_paint.clearcoat = 0.6
	_accent = _mat(secondary_color, 0.3, 0.2)
	_carbon = _mat(carbon_color, 0.45, 0.2)
	_tire = _mat(Color(0.05, 0.05, 0.05), 0.92, 0.0)
	_rim = _mat(Color(0.55, 0.56, 0.6), 0.3, 0.8)
	_build_body()
	for label in ["FL", "FR", "RL", "RR"]:
		_build_wheel(label)


func _mat(c: Color, rough: float, metal: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.roughness = rough
	m.metallic = metal
	return m


func _box(size: Vector3, pos: Vector3, mat: Material, rot: Vector3 = Vector3.ZERO) -> MeshInstance3D:
	var mesh := BoxMesh.new()
	mesh.size = size
	mesh.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = pos
	mi.rotation = rot
	add_child(mi)
	return mi


func _prism(size: Vector3, pos: Vector3, mat: Material, rot: Vector3 = Vector3.ZERO) -> MeshInstance3D:
	var mesh := PrismMesh.new()
	mesh.size = size
	mesh.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = pos
	mi.rotation = rot
	add_child(mi)
	return mi


func _cyl(radius_top: float, radius_bottom: float, height: float, pos: Vector3, mat: Material, rot: Vector3) -> MeshInstance3D:
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius_top
	mesh.bottom_radius = radius_bottom
	mesh.height = height
	mesh.radial_segments = 16
	mesh.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = pos
	mi.rotation = rot
	add_child(mi)
	return mi


func _build_body() -> void:
	var g := ground_y
	# Floor / plank.
	_box(Vector3(1.4, 0.04, 3.2), Vector3(0, g + 0.06, 0.25), _carbon)
	# Monocoque and engine cover.
	_box(Vector3(0.62, 0.42, 2.6), Vector3(0, g + 0.3, 0.1), _paint)
	_box(Vector3(0.5, 0.3, 1.2), Vector3(0, g + 0.36, 1.6), _paint)
	# Tapered nose (cylinder lying along Z).
	_cyl(0.07, 0.25, 1.9, Vector3(0, g + 0.28, -2.05), _paint, Vector3(-PI / 2.0, 0, 0))
	# Front wing: main plane, flap and end plates.
	_box(Vector3(1.9, 0.03, 0.42), Vector3(0, g + 0.1, -2.95), _carbon)
	_box(Vector3(1.8, 0.03, 0.22), Vector3(0, g + 0.17, -2.82), _accent, Vector3(0.35, 0, 0))
	for side in [-1.0, 1.0]:
		_box(Vector3(0.025, 0.24, 0.6), Vector3(side * 0.96, g + 0.16, -2.9), _carbon)
	# Sidepods with undercut.
	for side in [-1.0, 1.0]:
		_box(Vector3(0.42, 0.34, 1.5), Vector3(side * 0.5, g + 0.27, 0.15), _paint)
		_prism(Vector3(0.42, 0.34, 0.5), Vector3(side * 0.5, g + 0.27, -0.83), _paint, Vector3(-PI / 2.0, 0, 0))
		# Accent stripe.
		_box(Vector3(0.43, 0.05, 1.5), Vector3(side * 0.5, g + 0.4, 0.15), _accent)
	# Cockpit opening, airbox and roll hoop.
	_box(Vector3(0.44, 0.06, 0.7), Vector3(0, g + 0.52, -0.55), _carbon)
	_box(Vector3(0.36, 0.42, 0.5), Vector3(0, g + 0.72, 0.1), _paint)
	_box(Vector3(0.22, 0.18, 0.12), Vector3(0, g + 0.85, -0.17), _carbon)
	# Driver helmet (generic, unbranded).
	var helmet := SphereMesh.new()
	helmet.radius = 0.14
	helmet.height = 0.28
	helmet.material = _mat(Color(0.92, 0.92, 0.95), 0.2, 0.1)
	var hm := MeshInstance3D.new()
	hm.mesh = helmet
	hm.position = Vector3(0, g + 0.68, -0.42)
	add_child(hm)
	# Cockpit safety hoop.
	_box(Vector3(0.05, 0.05, 0.75), Vector3(0, g + 0.82, -0.6), _carbon, Vector3(0.18, 0, 0))
	for side in [-1.0, 1.0]:
		_box(Vector3(0.04, 0.04, 0.6), Vector3(side * 0.24, g + 0.7, -0.5), _carbon, Vector3(0.1, side * 0.12, 0))
	# Rear wing on a central pylon, with end plates and beam wing.
	_box(Vector3(0.05, 0.6, 0.15), Vector3(0, g + 0.65, 2.35), _carbon)
	_box(Vector3(1.0, 0.03, 0.38), Vector3(0, g + 0.95, 2.4), _carbon, Vector3(-0.12, 0, 0))
	_box(Vector3(1.0, 0.03, 0.22), Vector3(0, g + 1.04, 2.52), _accent, Vector3(-0.45, 0, 0))
	for side in [-1.0, 1.0]:
		_box(Vector3(0.025, 0.55, 0.7), Vector3(side * 0.51, g + 0.8, 2.45), _paint)
	_box(Vector3(0.9, 0.03, 0.2), Vector3(0, g + 0.4, 2.45), _carbon)
	# Diffuser.
	_box(Vector3(1.0, 0.18, 0.4), Vector3(0, g + 0.15, 2.15), _carbon, Vector3(-0.4, 0, 0))
	# Rain light.
	var light_mat := _mat(Color(0.5, 0.0, 0.0), 0.3, 0.0)
	light_mat.emission_enabled = true
	light_mat.emission = Color(0.9, 0.05, 0.02)
	light_mat.emission_energy_multiplier = 0.5
	_box(Vector3(0.12, 0.05, 0.03), Vector3(0, g + 0.32, 2.62), light_mat)


func _build_wheel(label: String) -> void:
	var front := label.begins_with("F")
	var pivot := Node3D.new()
	pivot.name = "Wheel" + label
	add_child(pivot)
	var radius := 0.355 if front else 0.36
	var width := 0.3 if front else 0.4
	var tire := CylinderMesh.new()
	tire.top_radius = radius
	tire.bottom_radius = radius
	tire.height = width
	tire.radial_segments = 24
	tire.material = _tire
	var tm := MeshInstance3D.new()
	tm.mesh = tire
	tm.rotation = Vector3(0, 0, PI / 2.0)
	pivot.add_child(tm)
	var rim := CylinderMesh.new()
	rim.top_radius = radius * 0.62
	rim.bottom_radius = radius * 0.62
	rim.height = width + 0.01
	rim.radial_segments = 12
	rim.material = _rim
	var rm := MeshInstance3D.new()
	rm.mesh = rim
	rm.rotation = Vector3(0, 0, PI / 2.0)
	pivot.add_child(rm)
	# Spoke marker so wheel rotation is visible.
	var spoke := BoxMesh.new()
	spoke.size = Vector3(width + 0.02, radius * 1.1, 0.05)
	spoke.material = _accent
	var sm := MeshInstance3D.new()
	sm.mesh = spoke
	pivot.add_child(sm)
