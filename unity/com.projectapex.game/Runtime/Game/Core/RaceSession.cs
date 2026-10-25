using System;
using Apex.AI;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Free-practice session: one car on one circuit with lap timing, track limits,
    /// wrong-way detection, reset-to-track and optional autopilot. Runs before the
    /// vehicle in FixedUpdate so inputs are set for the coming physics step;
    /// progress and timing use the state of the last completed step.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RaceSession : MonoBehaviour
    {
        /// <summary>Seconds with all four wheels outside track limits before the lap is voided.</summary>
        public const float TrackLimitGrace = 0.2f;

        public CircuitInstance Circuit { get; private set; }
        public VehicleController Car { get; private set; }
        public LapTimer Timer { get; private set; }
        public Autopilot Autopilot { get; private set; }
        public float LapDistance { get; private set; }
        public bool AutopilotEnabled { get; set; }
        /// <summary>When false the car receives zero input (grid hold, pause, calibration).</summary>
        public bool InputEnabled { get; set; } = true;
        public InputRouter Input { get; set; }

        public event Action<string, Color> Message;

        int trackIndex = -1;
        float offTrackTime;
        float wrongWayTime;

        public void Setup(CircuitInstance circuit, VehicleController car, InputRouter input, bool autopilot)
        {
            Circuit = circuit;
            Car = car;
            Input = input;
            AutopilotEnabled = autopilot;
            Timer = new LapTimer(circuit.Geometry.Length, circuit.SectorStarts);
            Autopilot = new Autopilot(circuit.Geometry, car.setup);
            PlaceOnGrid();
        }

        public void PlaceOnGrid()
        {
            var pose = Circuit.GridPose(0, Car.setup.cogHeight + 0.05f);
            Car.ResetTo(pose.position, pose.rotation);
            Timer.ResetPosition();
            Autopilot.ResetTracking();
            trackIndex = -1;
        }

        public void ResetCarToTrack()
        {
            var proj = Circuit.Geometry.Project(Car.transform.position.ToNum(), trackIndex);
            var pose = Circuit.PoseAt(proj.distance - 5f, 0f, Car.setup.cogHeight + 0.08f);
            Car.ResetTo(pose.position, pose.rotation);
            Timer.ResetPosition();
            Autopilot.ResetTracking();
            trackIndex = -1;
            Message?.Invoke("Auto auf die Strecke gesetzt", Color.white);
        }

        void FixedUpdate()
        {
            if (Car == null) return;
            var sim = Car.Sim;
            var geo = Circuit.Geometry;
            var proj = geo.Project(Car.transform.position.ToNum(), trackIndex);
            trackIndex = proj.index;
            LapDistance = Circuit.LapDistance(proj.distance);
            Timer.Update(LapDistance, sim.SimTime);
            CheckTrackLimits(Time.fixedDeltaTime);
            CheckWrongWay(proj.distance, Time.fixedDeltaTime);

            if (!InputEnabled)
            {
                sim.throttle = 0f;
                sim.brake = 1f;
                sim.steer = 0f;
                return;
            }
            if (AutopilotEnabled)
            {
                var c = Autopilot.Drive(Car.transform.position.ToNum(), Car.transform.forward.ToNum(), sim.ForwardSpeed,
                    Car.setup.wheelbase, Car.setup.maxSteerAngleDeg * ApexMath.Deg2Rad);
                sim.throttle = c.throttle;
                sim.brake = c.brake;
                sim.steer = c.steer;
            }
            else if (Input != null)
            {
                sim.throttle = Input.Throttle;
                sim.brake = Input.Brake;
                sim.steer = Input.Steer;
            }
        }

        void CheckTrackLimits(float dt)
        {
            if (Car.Sim.WheelsOnTrack() == 0)
            {
                offTrackTime += dt;
                if (offTrackTime > TrackLimitGrace) Timer.Invalidate("Tracklimits");
            }
            else
            {
                offTrackTime = 0f;
            }
        }

        void CheckWrongWay(float rawDistance, float dt)
        {
            var tangent = Circuit.Geometry.TangentAt(rawDistance).ToUnity();
            float heading = Vector3.Dot(RigidbodyCompat.GetVelocity(Car.Body), tangent);
            if (Car.Sim.Speed > 5f && heading < -3f)
            {
                wrongWayTime += dt;
                if (wrongWayTime > 1f)
                {
                    wrongWayTime = 0.5f;
                    Message?.Invoke("FALSCHE RICHTUNG", new Color(1f, 0.3f, 0.25f));
                }
            }
            else
            {
                wrongWayTime = 0f;
            }
        }
    }
}
