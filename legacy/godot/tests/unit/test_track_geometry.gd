extends TestCase


func _load() -> TrackGeometry:
	var data := Circuit.load_layout("res://data/tracks/kestrel_ridge.json")
	return TrackGeometry.from_points(data["points"], 2.0)


func test_layout_loads_and_has_plausible_length() -> void:
	var g := _load()
	check_range(g.length, 3500.0, 5500.0, "circuit length")
	check(g.sample_count() > 1000, "dense sampling")


func test_closed_loop_is_continuous() -> void:
	var g := _load()
	var n := g.sample_count()
	var gap := g.positions[n - 1].distance_to(g.positions[0])
	check_near(gap, g.spacing, 0.2, "last sample connects to first")
	for i in n:
		var step := g.positions[i].distance_to(g.positions[(i + 1) % n])
		if absf(step - g.spacing) > 0.3:
			check(false, "uniform spacing broken at %d (%.2f)" % [i, step])
			break


func test_min_radius_and_clearance() -> void:
	var g := _load()
	var max_k := 0.0
	for k in g.curvatures:
		max_k = maxf(max_k, absf(k))
	check(1.0 / max_k > 25.0, "tightest corner radius > 25 m (got %.1f)" % (1.0 / max_k))
	# Non-adjacent parts of the track must not overlap, including run-off.
	var n := g.sample_count()
	var min_clear := INF
	for i in range(0, n, 5):
		for j in range(i + 5, n, 5):
			var along := minf(j - i, n - (j - i)) * g.spacing
			if along < 150.0:
				continue
			min_clear = minf(min_clear, TrackGeometry._flat_dist2(g.positions[i], g.positions[j]))
	check(sqrt(min_clear) > 2.0 * (7.0 + Circuit.KERB_WIDTH + Circuit.RUNOFF_WIDTH) * 0.95,
			"track sections keep clear of each other (%.1f m)" % sqrt(min_clear))


func test_projection_roundtrip() -> void:
	var g := _load()
	for d in [0.0, 123.4, 1500.0, g.length - 3.0]:
		var p := g.position_at(d) + g.tangent_at(d).cross(Vector3.UP).normalized() * 3.0
		var proj := g.project(p)
		var err := absf(proj.x - d)
		err = minf(err, g.length - err)
		check(err < 1.0, "projected distance matches at %s (err %.2f)" % [d, err])
		check_near(proj.y, 3.0, 0.3, "lateral offset is +right at %s" % d)
