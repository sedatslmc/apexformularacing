extends SceneTree
## Headless test runner. Usage:
##   godot --headless --path . -s res://tests/run_tests.gd [-- filter]
## Runs every test_* method in tests/unit and tests/integration. Methods may
## be coroutines (await). Exits with code 1 if any test fails.

const DIRS := ["res://tests/unit", "res://tests/integration"]


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var filter := ""
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		filter = args[0]
	var passed := 0
	var failed := 0
	var t0 := Time.get_ticks_msec()
	for dir in DIRS:
		for file in DirAccess.get_files_at(dir):
			if not file.begins_with("test_") or not file.ends_with(".gd"):
				continue
			var script: GDScript = load(dir + "/" + file)
			for m in script.get_script_method_list():
				var method: String = m["name"]
				if not method.begins_with("test_"):
					continue
				var full := file.get_basename() + "." + method
				if filter != "" and not full.contains(filter):
					continue
				var tc: TestCase = script.new()
				tc.tree = self
				var start := Time.get_ticks_msec()
				await tc.call(method)
				var ms := Time.get_ticks_msec() - start
				if tc.failures.is_empty():
					passed += 1
					print("  PASS  %s (%d ms)" % [full, ms])
				else:
					failed += 1
					print("  FAIL  %s (%d ms)" % [full, ms])
					for f in tc.failures:
						print("        - " + f)
	print("\n%d passed, %d failed in %.1f s" % [passed, failed, (Time.get_ticks_msec() - t0) / 1000.0])
	quit(1 if failed > 0 else 0)
