using System.Collections.Generic;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    public class FlagPanelTests
    {
        const float L = 5000f;

        static CarReport Car(int id, double progress, float speed, bool onTrack = true) =>
            new CarReport { id = id, progress = progress, distance = (float)(progress % L), speed = speed, onRacingSurface = onTrack };

        [Test]
        public void PanelsFollowTheIncidentZone()
        {
            var rc = new RaceControl(new RaceControlRules { safetyCar = false, virtualSafetyCar = false }, L);
            var logic = new FlagPanelLogic();
            var cars = new List<CarReport> { Car(1, 1000, 0f) };
            double t = 0;
            for (int i = 0; i < 50; i++, t += 0.1) { rc.Update(t, cars, 0); logic.Update(rc, t); }
            Assert.That(rc.Status, Is.EqualTo(TrackStatus.Yellow));
            Assert.That(logic.Evaluate(900f, L, rc, cars, t), Is.EqualTo(PanelSignal.DoubleYellow), "car stopped on track 100 m ahead");
            Assert.That(logic.Evaluate(800f, L, rc, cars, t), Is.EqualTo(PanelSignal.Yellow), "further up the yellow zone");
            Assert.That(logic.Evaluate(1150f, L, rc, cars, t), Is.EqualTo(PanelSignal.Green), "end of the incident zone");
            Assert.That(logic.Evaluate(3000f, L, rc, cars, t), Is.EqualTo(PanelSignal.Off), "rest of the lap stays dark");
        }

        [Test]
        public void NeutralisationShowsBoardsAndGreenAfterwards()
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            var logic = new FlagPanelLogic();
            double t = 0;
            bool stranded = true;
            rc.CarRecovered += id => stranded = false;
            var seen = new HashSet<PanelSignal>();
            for (int i = 0; i < 900; i++, t += 0.1)
            {
                var cars = new List<CarReport> { Car(0, 10000 + t * 30, 30f), stranded ? Car(1, 2500, 0f, false) : new CarReport { id = 1, ignore = true } };
                rc.Update(t, cars, 2);
                logic.Update(rc, t);
                seen.Add(logic.Evaluate(4000f, L, rc, cars, t));
            }
            // Plain bool asserts: Unity's NUnit (3.5) has no collection overloads of Does.Contain.
            Assert.That(seen.Contains(PanelSignal.VirtualSafetyCar), Is.True, "VSC board");
            Assert.That(seen.Contains(PanelSignal.Green), Is.True, "green after the VSC ends");
            Assert.That(seen.Contains(PanelSignal.SafetyCar), Is.False, "no safety car for an off-track car");
        }

        [Test]
        public void BlueForALappedCarWithTheLeaderCloseBehind()
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            var logic = new FlagPanelLogic();
            var cars = new List<CarReport> { Car(0, 2 * L + 1960, 70f), Car(5, L + 2000, 60f) };
            Assert.That(logic.Evaluate(2050f, L, rc, cars, 0), Is.EqualTo(PanelSignal.Blue));
            // Same gap but on the same lap: just racing, no blue.
            cars[0] = Car(0, L + 1960, 70f);
            Assert.That(logic.Evaluate(2050f, L, rc, cars, 0), Is.EqualTo(PanelSignal.Off));
        }
    }
}
