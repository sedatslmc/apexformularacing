class_name TestCase
extends RefCounted
## Minimal assertion helpers for the headless test runner (tests/run_tests.gd).

var failures: PackedStringArray = PackedStringArray()
var tree: SceneTree


func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func check_near(actual: float, expected: float, tolerance: float, message: String) -> void:
	if absf(actual - expected) > tolerance:
		failures.append("%s (expected %s ± %s, got %s)" % [message, expected, tolerance, actual])


func check_range(actual: float, lo: float, hi: float, message: String) -> void:
	if actual < lo or actual > hi:
		failures.append("%s (expected in [%s, %s], got %s)" % [message, lo, hi, actual])
