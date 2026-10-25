using System.Collections.Generic;
using Apex.Career;
using Apex.Core;
using NUnit.Framework;

namespace Apex.Tests
{
    public class CareerTests
    {
        [Test]
        public void HundredLevelsEachWithAUniqueReward()
        {
            var rewards = Progression.Rewards;
            Assert.That(rewards.Count, Is.EqualTo(100));
            var names = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                Assert.That(rewards[i].level, Is.EqualTo(i + 1));
                Assert.That(rewards[i].cosmetic, Is.True, "no performance rewards in the level table");
                Assert.That(names.Add(rewards[i].name), Is.True, $"duplicate reward name at level {i + 1}: {rewards[i].name}");
            }
        }

        [Test]
        public void XpCurveIsMonotonicAndLevelLookupMatches()
        {
            for (int l = 2; l <= 100; l++)
                Assert.That(Progression.XpForLevel(l), Is.GreaterThan(Progression.XpForLevel(l - 1)));
            Assert.That(Progression.LevelForXp(0), Is.EqualTo(1));
            Assert.That(Progression.LevelForXp(Progression.XpForLevel(37)), Is.EqualTo(37));
            Assert.That(Progression.LevelForXp(long.MaxValue / 4), Is.EqualTo(100));
        }

        [Test]
        public void RaceXpRewardsResultAndCleanDriving()
        {
            int win = Progression.RaceXp(1, 12, 5, true, true, 3);
            int mid = Progression.RaceXp(6, 12, 5, false, true, 0);
            int dnf = Progression.RaceXp(12, 12, 1, false, false, 0);
            Assert.That(win, Is.GreaterThan(mid));
            Assert.That(mid, Is.GreaterThan(dnf));
        }

        [Test]
        public void ChampionshipStateAndMigration()
        {
            var c = new CareerData();
            Assert.That(c.ChampionshipActive, Is.False);
            c.StartChampionship(new[] { "test_loop", "bosphorus_gp" }, 5, 11, 0.5f);
            Assert.That(c.ChampionshipActive, Is.True);
            Assert.That(c.points.Length, Is.EqualTo(Roster.Drivers.Length + 1));
            c.nextRound = 2;
            Assert.That(c.ChampionshipActive, Is.False, "season complete");
            var old = new CareerData { version = 0, points = null, calendar = null, xp = -5 };
            Assert.That(old.MigrateAndValidate(), Is.True);
            Assert.That(old.xp, Is.EqualTo(0));
        }

        [Test]
        public void RosterIsFictionalAndComplete()
        {
            Assert.That(Roster.Teams.Length, Is.EqualTo(10));
            Assert.That(Roster.Drivers.Length, Is.EqualTo(20));
            var codes = new HashSet<string>();
            foreach (var d in Roster.Drivers) Assert.That(codes.Add(d.code), Is.True);
            Assert.That(Roster.PickField(19, 0, 0).Count, Is.EqualTo(19));
        }
    }
}
