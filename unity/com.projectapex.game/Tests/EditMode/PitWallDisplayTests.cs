using Apex.UI;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Pit wall monitors (Apex.UI.PitWallDisplay) and the own pixel font.</summary>
    public class PitWallDisplayTests
    {
        static PitWallDisplay.Data Sample(float rain)
        {
            var d = new PitWallDisplay.Data { lap = 3, laps = 12, rain = rain, wetness = rain * 0.8f, time = 10f, throttle = 0.7f, brake = 0.1f };
            for (int i = 0; i < 8; i++)
                d.rows.Add(new PitWallDisplay.Row { code = "AB" + (char)('C' + i), gap = i == 0 ? "1:32.4" : "+" + i + ".2", team = PitWallDisplay.Rgba(200, 20, 30), compound = PitWallDisplay.Rgba(240, 200, 40), wear = i * 0.1f, s1 = 1, s2 = 2, s3 = 3, player = i == 2 });
            for (int i = 0; i < 120; i++) d.speedTrace.Add(30f + 50f * (i % 40) / 40f);
            return d;
        }

        static int Count(uint[] px, int x0, int y0, int w, int h, uint colour)
        {
            int n = 0;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (px[y * PitWallDisplay.Width + x] == colour) n++;
            return n;
        }

        [Test]
        public void EveryPanelShowsItsData()
        {
            var px = new uint[PitWallDisplay.Width * PitWallDisplay.Height];
            PitWallDisplay.Render(Sample(0.6f), px);
            uint text = PitWallDisplay.Rgba(220, 228, 236), cyan = PitWallDisplay.Rgba(0, 200, 230), team = PitWallDisplay.Rgba(200, 20, 30);
            var (tx, ty) = PitWallDisplay.Origin(PitWallDisplay.Panel.Timing);
            Assert.That(Count(px, tx, ty + 10, 128, 54, text), Is.GreaterThan(150), "codes and gaps written");
            Assert.That(Count(px, tx, ty + 10, 20, 54, team), Is.GreaterThanOrEqualTo(6 * 14), "team colour bars");
            var (sx, sy) = PitWallDisplay.Origin(PitWallDisplay.Panel.Telemetry);
            Assert.That(Count(px, sx, sy + 10, 128, 42, cyan), Is.GreaterThan(100), "speed trace");
            var (rx, ry) = PitWallDisplay.Origin(PitWallDisplay.Panel.Radar);
            int rainPx = Count(px, rx, ry + 10, 128, 54, PitWallDisplay.Rgba(40, 210, 90)) + Count(px, rx, ry + 10, 128, 54, PitWallDisplay.Rgba(240, 200, 40));
            Assert.That(rainPx, Is.GreaterThan(50), "rain cells on the radar");
            // Dry: no rain cells.
            PitWallDisplay.Render(Sample(0f), px);
            Assert.That(Count(px, rx, ry + 10, 128, 54, PitWallDisplay.Rgba(40, 210, 90)), Is.EqualTo(0));
        }

        [Test]
        public void ThePixelFontCoversTheBilingualSigns()
        {
            foreach (char c in "PIT LANE PİT YOLU PADDOCK PADOK ÇIKIŞ GİRİŞ 0123456789:+-.%") Assert.That(PixelFont.Has(c), Is.True, c.ToString());
            Assert.That(PixelFont.Width("80"), Is.EqualTo(11));
            int on = 0;
            PixelFont.Draw("8", 0, 0, 1, (x, y) => on++);
            Assert.That(on, Is.EqualTo(17));
            int runPx = 0;
            foreach (var r in PixelFont.Runs("80")) runPx += r.len;
            int drawPx = 0;
            PixelFont.Draw("80", 0, 0, 1, (x, y) => drawPx++);
            Assert.That(runPx, Is.EqualTo(drawPx), "runs cover exactly the set pixels");
        }
    }
}
