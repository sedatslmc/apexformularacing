class_name SurfaceTypes
extends RefCounted
## Physical properties of drivable surfaces. Static bodies carry a "surface"
## metadata string that wheels read from their raycast hit.

const ASPHALT := &"asphalt"
const KERB := &"kerb"
const GRASS := &"grass"
const GRAVEL := &"gravel"
const WALL := &"wall"

## grip: tire friction multiplier. drag: rolling-resistance coefficient
## (fraction of load). rumble: amplitude of the vibration/noise cue.
## on_track: whether a wheel here counts as inside track limits.
const PROPERTIES := {
	ASPHALT: {"grip": 1.0, "drag": 0.012, "rumble": 0.0, "on_track": true},
	KERB: {"grip": 0.93, "drag": 0.02, "rumble": 1.0, "on_track": true},
	GRASS: {"grip": 0.55, "drag": 0.09, "rumble": 0.35, "on_track": false},
	GRAVEL: {"grip": 0.42, "drag": 0.35, "rumble": 0.8, "on_track": false},
	WALL: {"grip": 0.5, "drag": 0.05, "rumble": 0.0, "on_track": false},
}


static func get_props(surface: StringName) -> Dictionary:
	return PROPERTIES.get(surface, PROPERTIES[ASPHALT])
