using System.Numerics;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    public class PodiumCeremonyTests
    {
        [Test]
        public void FiguresStandOnTheirStepsFacingTheTrack()
        {
            Assert.That(PodiumCeremony.Steps[0].Y, Is.GreaterThan(PodiumCeremony.Steps[1].Y));
            Assert.That(PodiumCeremony.Steps[1].Y, Is.GreaterThan(PodiumCeremony.Steps[2].Y));
            // Facing -z: a point ahead of the figure lies towards the track side.
            var ahead = PodiumCeremony.ToPodium(0, new Vector3(0f, 1f, 1f));
            Assert.That(ahead.Z, Is.LessThan(PodiumCeremony.Steps[0].Z));
            var frames = new PitCrewFigure.Frame[PitCrewFigure.PartCount];
            var pose = new PitCrewChoreography.Pose { position = PodiumCeremony.Steps[0], yaw = PodiumCeremony.Yaw, hold = PitCrewChoreography.Hold.Wheel };
            PitCrewFigure.Solve(pose, PodiumCeremony.WinnerHands(5f, false), 0f, frames);
            foreach (var f in frames)
                Assert.That(f.origin.Y, Is.GreaterThan(-0.01f), "no part below the step");
        }

        [Test]
        public void WinnerLiftsTheTrophyOverTheHead()
        {
            var low = PodiumCeremony.WinnerHands(0f, false);
            var high = PodiumCeremony.WinnerHands(3f, false);
            Assert.That(high.Y - low.Y, Is.GreaterThan(0.7f));
            Assert.That(PodiumCeremony.WinnerHands(0f, true).Y, Is.EqualTo(PodiumCeremony.WinnerHands(9f, true).Y), "reduced motion: still");
            Assert.That(high.Y, Is.LessThan(PitCrewFigure.HipHeight + PitCrewFigure.TorsoLength + PitCrewFigure.UpperArm + PitCrewFigure.Forearm + 0.1f), "reachable");
        }

        [Test]
        public void ConfettiFallsAndSettlesOnThePodium()
        {
            int launched = 0;
            for (int i = 0; i < PodiumCeremony.ConfettiCount; i++)
            {
                Assert.That(PodiumCeremony.Confetti(i, PodiumCeremony.ConfettiSeconds + 6f, out var end, out _), Is.True);
                Assert.That(end.Y, Is.LessThan(0.7f), "down by the end");
                Assert.That(System.MathF.Abs(end.X), Is.LessThan(9f));
                Assert.That(end.Z, Is.InRange(-3f, 9f));
                if (PodiumCeremony.Confetti(i, 1.5f, out var mid, out _)) { launched++; Assert.That(mid.Y, Is.GreaterThan(0.6f)); }
                Assert.That(PodiumCeremony.ConfettiColour(i), Is.InRange(0, 3));
            }
            Assert.That(launched, Is.GreaterThan(PodiumCeremony.ConfettiCount / 3));
            Assert.That(PodiumCeremony.BannerWave(0f, 0.5f, 1f, 0), Is.EqualTo(0f), "fixed at the pole");
        }
    }
}
