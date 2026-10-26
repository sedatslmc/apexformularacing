using System.Collections.Generic;
using Apex.Environment;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    public class EventIdentityTests
    {
        [Test]
        public void StartLampsFollowTheLightsSequenceOnly()
        {
            Assert.That(EventIdentity.LitStartLamps(RaceDirector.Phase.Waiting, 3), Is.EqualTo(0));
            Assert.That(EventIdentity.LitStartLamps(RaceDirector.Phase.Lights, 3), Is.EqualTo(3));
            Assert.That(EventIdentity.LitStartLamps(RaceDirector.Phase.Lights, 9), Is.EqualTo(5));
            Assert.That(EventIdentity.LitStartLamps(RaceDirector.Phase.Racing, 5), Is.EqualTo(0), "lights out = race start");
        }

        [Test]
        public void ScreensPrioritiseTrackStatusThenRaceState()
        {
            Assert.That(EventIdentity.ScreenMessage(RaceDirector.Phase.Racing, TrackStatus.SafetyCar, 3), Is.EqualTo("GÜVENLİK ARACI"));
            Assert.That(EventIdentity.ScreenMessage(RaceDirector.Phase.Racing, TrackStatus.VirtualSafetyCarEnding, 3), Is.EqualTo("SANAL GÜVENLİK ARACI"));
            Assert.That(EventIdentity.ScreenMessage(RaceDirector.Phase.Lights, TrackStatus.Green, 3), Is.EqualTo("YARIŞ BAŞLIYOR"));
            Assert.That(EventIdentity.ScreenMessage(RaceDirector.Phase.Finished, TrackStatus.Green, 3), Is.EqualTo("DAMALI BAYRAK"));
            // A local yellow does not take over the screens.
            Assert.That(EventIdentity.ScreenMessage(RaceDirector.Phase.Racing, TrackStatus.Yellow, 1, "APX", 2, 5), Is.EqualTo("BOĞAZ GRAND PRIX"));
            Assert.That(EventIdentity.ScreenMessage(RaceDirector.Phase.Racing, TrackStatus.Green, EventIdentity.MessageSeconds + 1, "APX", 2, 5), Is.EqualTo("LİDER APX · TUR 2/5"));
            // Before the race the event messages rotate.
            var seen = new HashSet<string>();
            for (int s = 0; s < 6; s++) seen.Add(EventIdentity.ScreenMessage(RaceDirector.Phase.Waiting, TrackStatus.Green, s * EventIdentity.MessageSeconds + 0.5));
            Assert.That(seen.Count, Is.EqualTo(EventIdentity.IdleMessages.Length));
        }

        [Test]
        public void TowerRowsListTheOrderWithThreeLetterCodes()
        {
            var standings = new List<RaceDirector.Entry>
            {
                new RaceDirector.Entry { code = "APX", name = "Player" },
                new RaceDirector.Entry { code = "", name = "Demir" },
                new RaceDirector.Entry { code = "KAY", name = "Kaya" },
            };
            var rows = EventIdentity.TowerRows(standings, 2);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0], Is.EqualTo(" 1 APX"));
            Assert.That(rows[1], Is.EqualTo(" 2 DEM"));
            Assert.That(EventIdentity.TowerRows(null, 10).Count, Is.EqualTo(0));
        }

        static bool NearPalette(float r, float g, float b)
        {
            foreach (var c in new[] { EventArt.Red, EventArt.Turquoise, EventArt.Gold, EventArt.Navy, EventArt.Ivory })
                if (System.Math.Abs(r - c[0]) + System.Math.Abs(g - c[1]) + System.Math.Abs(b - c[2]) < 0.02f) return true;
            return false;
        }

        [Test]
        public void EmblemIsRoundAndUsesTheEventPalette()
        {
            const int n = 128;
            var px = EventArt.Emblem(n);
            Assert.That(px[3], Is.EqualTo(0f), "corner transparent");
            int centre = (n / 2 * n + n / 2) * 4;
            Assert.That(px[centre + 3], Is.EqualTo(1f).Within(1e-4f), "centre opaque");
            int opaque = 0, onPalette = 0;
            for (int k = 0; k < px.Length; k += 4)
            {
                if (px[k + 3] < 0.999f) continue;
                opaque++;
                if (NearPalette(px[k], px[k + 1], px[k + 2])) onPalette++;
            }
            // Anti-aliased edges of the thin lines blend colours (many at 128 px); the rest is palette.
            Assert.That(onPalette, Is.GreaterThan(opaque * 0.7f));
            // Red, turquoise and gold all appear.
            bool red = false, tq = false, gold = false;
            for (int k = 0; k < px.Length; k += 4)
            {
                red |= NearPalette(px[k], px[k + 1], px[k + 2]) && px[k] > 0.7f && px[k + 1] < 0.1f;
                tq |= px[k] < 0.05f && px[k + 1] > 0.45f && px[k + 2] > 0.5f;
                gold |= px[k] > 0.75f && px[k + 1] > 0.6f && px[k + 2] < 0.45f;
            }
            Assert.That(red && tq && gold, Is.True);
        }

        [Test]
        public void IznikTileIsSeamless()
        {
            const int n = 64;
            var px = EventArt.IznikTile(n);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = (j * n + i) * 4, b = (j * n + (n - 1 - i)) * 4, c = ((n - 1 - j) * n + i) * 4;
                    for (int ch = 0; ch < 4; ch++)
                    {
                        Assert.That(px[a + ch], Is.EqualTo(px[b + ch]).Within(1e-4f), "mirror in x: opposite edges match");
                        Assert.That(px[a + ch], Is.EqualTo(px[c + ch]).Within(1e-4f), "mirror in y");
                    }
                }
        }
    }
}
