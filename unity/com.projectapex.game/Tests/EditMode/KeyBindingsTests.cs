using Apex.InputLogic;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Free key assignment for the PC keyboard.</summary>
    public class KeyBindingsTests
    {
        [Test]
        public void DefaultsAreTheClassicKeys()
        {
            var b = new KeyBindings();
            Assert.That(b.Describe(KeyBindings.Action.Throttle), Is.EqualTo("W / ↑"));
            Assert.That(b.Describe(KeyBindings.Action.SteerLeft), Is.EqualTo("A / ←"));
            Assert.That(b.Get(KeyBindings.Action.ResetCar, 0), Is.EqualTo("Backspace"));
            Assert.That(KeyBindings.KeyName("Backspace"), Is.EqualTo("Rücktaste"));
            foreach (var a in KeyBindings.All) Assert.That(KeyBindings.ActionName(a), Is.Not.Empty);
        }

        [Test]
        public void AssigningAUsedKeySwapsInsteadOfDoublingUp()
        {
            var b = new KeyBindings();
            // Throttle on Space: nothing else had Space.
            Assert.That(b.Assign(KeyBindings.Action.Throttle, 0, "Space"), Is.True);
            Assert.That(b.Describe(KeyBindings.Action.Throttle), Is.EqualTo("Leertaste / ↑"));
            // Brake onto "D" (steer right's key): steer right gets the brake's old key S.
            b.Assign(KeyBindings.Action.Brake, 0, "D");
            Assert.That(b.Get(KeyBindings.Action.Brake, 0), Is.EqualTo("D"));
            Assert.That(b.Get(KeyBindings.Action.SteerRight, 0), Is.EqualTo("S"));
            // No key is used twice.
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var a in KeyBindings.All)
                foreach (var k in b.KeysOf(a)) Assert.That(seen.Add(k), Is.True, k);
        }

        [Test]
        public void FixedKeysCannotBeTaken()
        {
            var b = new KeyBindings();
            foreach (var k in new[] { "Escape", "Enter", "F1", "F6", "F12", "Digit3", "" })
            {
                Assert.That(KeyBindings.IsReserved(k), Is.True, k);
                Assert.That(b.Assign(KeyBindings.Action.Camera, 0, k), Is.False, k);
            }
            Assert.That(b.Get(KeyBindings.Action.Camera, 0), Is.EqualTo("C"));
            Assert.That(KeyBindings.IsReserved("F"), Is.False, "the letter F is free");
        }

        [Test]
        public void SavedAssignmentSurvivesAndBadFilesAreSafe()
        {
            var b = new KeyBindings();
            b.Assign(KeyBindings.Action.ShiftUp, 1, "LeftShift");
            b.Clear(KeyBindings.Action.Throttle, 1);
            var back = KeyBindings.Parse(b.Serialize());
            Assert.That(back.Serialize(), Is.EqualTo(b.Serialize()));
            Assert.That(back.Describe(KeyBindings.Action.ShiftUp), Is.EqualTo("E / Umschalt links"));
            Assert.That(back.Describe(KeyBindings.Action.Throttle), Is.EqualTo("W"));
            // Empty, garbage, reserved and doubled keys: defaults where needed, nothing twice.
            Assert.That(KeyBindings.Parse("").Serialize(), Is.EqualTo(new KeyBindings().Serialize()));
            var bad = KeyBindings.Parse("Nonsense;Throttle=Escape,X;Brake=X,Y;Camera");
            Assert.That(bad.Get(KeyBindings.Action.Throttle, 0), Is.EqualTo(""), "reserved key dropped");
            Assert.That(bad.Get(KeyBindings.Action.Throttle, 1), Is.EqualTo("X"));
            Assert.That(bad.Get(KeyBindings.Action.Brake, 0), Is.EqualTo(""), "X is already throttle's");
            Assert.That(bad.Get(KeyBindings.Action.Camera, 0), Is.EqualTo("C"), "missing actions keep defaults");
        }
    }
}
