using System;

namespace Apex.Environment
{
    /// <summary>
    /// Street-front details of the real city (OsmCity buildings): which ground-floor bays are shops and which sign
    /// they carry, and where Turkish flags hang vertically from balconies and windows. Pure decisions (deterministic
    /// hashes); the geometry is built in the runtime (EnvironmentGeometry.FacadeOrnaments).
    ///
    /// Shop signs are generic Turkish trade names (no real businesses, brands or logos), rendered into one texture
    /// atlas by tools/signs/build_sign_atlas.py from this catalog (Resources/Textures/Signs/ShopSigns.jpg, cell
    /// order = catalog order, manifest ShopSigns.json checked by a test). Around Ortaköy Square the mix follows the
    /// quarter: kumpir and waffle stalls, cafés, silver jewellery and souvenir shops.
    ///
    /// Vertically hung flags follow the flag regulation's rule for vertical display: the hoist with the crescent
    /// and star at the top, the cloth hanging down (the flag is symmetric about its centre line, so no side is
    /// mirrored).
    /// </summary>
    public static class StreetFronts
    {
        public enum Trade { Food, Cafe, Daily, Service, Souvenir }

        public sealed class Sign
        {
            public readonly string text, subtitle;
            public readonly Trade trade;
            /// <summary>Typical of Ortaköy's square and bazaar lanes (preferred within <see cref="OrtakoyRadius"/>).</summary>
            public readonly bool ortakoy;
            /// <summary>Board and lettering colours (sRGB).</summary>
            public readonly (byte r, byte g, byte b) board, letters;

            public Sign(string text, string subtitle, Trade trade, bool ortakoy, uint board, uint letters)
            {
                this.text = text;
                this.subtitle = subtitle;
                this.trade = trade;
                this.ortakoy = ortakoy;
                this.board = ((byte)(board >> 16), (byte)(board >> 8), (byte)board);
                this.letters = ((byte)(letters >> 16), (byte)(letters >> 8), (byte)letters);
            }
        }

        const uint Red = 0xB3141B, White = 0xF7F4EC, Green = 0x1E5B3A, Navy = 0x1B2A4A, Yellow = 0xF2C230, Black = 0x1A1A1A,
            Burgundy = 0x6E1423, Gold = 0xE2C27A, Turquoise = 0x0E7C86, Cream = 0xEFE3C8, Brown = 0x5A3A22, Orange = 0xD9661F;

        /// <summary>The signs in atlas order (4 columns x 16 rows, row 0 at the top of the image).</summary>
        public static readonly Sign[] Catalog =
        {
            new Sign("KUMPİR", "Patates · Peynir · Garnitür", Trade.Food, true, Orange, White),
            new Sign("WAFFLE", "Meyveli · Çikolatalı", Trade.Food, true, Brown, Cream),
            new Sign("KUMPİR & WAFFLE", "", Trade.Food, true, Red, Yellow),
            new Sign("BALIK EKMEK", "Taze Balık", Trade.Food, true, Navy, White),
            new Sign("SİMİTÇİ", "Taze Simit · Poğaça", Trade.Food, false, Burgundy, Gold),
            new Sign("FIRIN", "Ekmek · Pide · Pasta", Trade.Food, false, Cream, Brown),
            new Sign("BÖREKÇİ", "Su Böreği · Kol Böreği", Trade.Food, false, White, Red),
            new Sign("PASTANE", "Baklava · Tatlı · Kurabiye", Trade.Food, false, Burgundy, Cream),
            new Sign("DÖNER", "Tavuk · Et", Trade.Food, false, Red, White),
            new Sign("LOKANTA", "Ev Yemekleri", Trade.Food, false, Green, Cream),
            new Sign("PİDE SALONU", "Lahmacun · Pide", Trade.Food, false, Yellow, Red),
            new Sign("KEBAPÇI", "Adana · Urfa", Trade.Food, false, Black, Gold),
            new Sign("DONDURMA", "Maraş Usulü", Trade.Food, true, Turquoise, White),
            new Sign("KURUYEMİŞ", "Lokum · Kahve", Trade.Food, false, Yellow, Black),
            new Sign("MANAV", "Meyve · Sebze", Trade.Food, false, Green, Yellow),
            new Sign("KASAP", "Et · Tavuk", Trade.Food, false, White, Burgundy),
            new Sign("LOKUM & BAKLAVA", "", Trade.Food, true, Burgundy, Gold),
            new Sign("KAHVEHANE", "Türk Kahvesi · Çay", Trade.Cafe, false, Brown, Gold),
            new Sign("ÇAY BAHÇESİ", "", Trade.Cafe, true, Green, White),
            new Sign("KAFE", "Kahve · Tatlı", Trade.Cafe, true, Black, White),
            new Sign("ÇAY OCAĞI", "", Trade.Cafe, false, Red, White),
            new Sign("TÜRK KAHVESİ", "Taze Çekilmiş", Trade.Cafe, true, Cream, Brown),
            new Sign("BAKKAL", "Gıda · Şarküteri", Trade.Daily, false, Navy, Yellow),
            new Sign("BÜFE", "Gazete · İçecek", Trade.Daily, false, Yellow, Navy),
            new Sign("MARKET", "Gıda · Temizlik", Trade.Daily, false, Red, White),
            new Sign("ECZANE", "", Trade.Daily, false, White, Red),
            new Sign("KIRTASİYE", "Kitap · Defter · Kalem", Trade.Daily, false, Turquoise, White),
            new Sign("NALBUR", "Hırdavat · Boya", Trade.Daily, false, Orange, Black),
            new Sign("ÇİÇEKÇİ", "Çiçek · Saksı", Trade.Daily, false, Green, White),
            new Sign("KUAFÖR", "Bayan · Erkek", Trade.Service, false, Black, Gold),
            new Sign("BERBER", "", Trade.Service, false, Navy, White),
            new Sign("TERZİ", "Tadilat", Trade.Service, false, Cream, Navy),
            new Sign("KUNDURACI", "Ayakkabı Tamiri", Trade.Service, false, Brown, White),
            new Sign("SAATÇİ", "Tamir", Trade.Service, false, Black, White),
            new Sign("OPTİK", "Gözlük · Lens", Trade.Service, false, White, Navy),
            new Sign("EMLAK", "Satılık · Kiralık", Trade.Service, false, Red, White),
            new Sign("KUYUMCU", "Altın · Gümüş", Trade.Service, false, Burgundy, Gold),
            new Sign("HEDİYELİK EŞYA", "", Trade.Souvenir, true, Turquoise, White),
            new Sign("GÜMÜŞ TAKI", "El Yapımı", Trade.Souvenir, true, Navy, Gold),
            new Sign("ANTİKACI", "Eski Eşya", Trade.Souvenir, true, Brown, Gold),
            new Sign("SAHAF", "Eski Kitap", Trade.Souvenir, true, Green, Gold),
            new Sign("SERAMİK & ÇİNİ", "El Yapımı", Trade.Souvenir, true, White, Turquoise),
            new Sign("HALI · KİLİM", "", Trade.Souvenir, true, Red, Cream),
        };

        public const int AtlasColumns = 4, AtlasRows = 16;
        /// <summary>Sign board (m): width and height match the atlas cell's 4:1 aspect.</summary>
        public const float BoardWidth = 3.1f, BoardHeight = 0.775f;
        /// <summary>Ortaköy quarter: distance (m) from the mosque within which shops follow the square's mix and
        /// more flags hang (the reference section of the city).</summary>
        public const float OrtakoyRadius = 320f;

        /// <summary>Atlas UVs of a sign cell (u0, v0, u1, v1), v up (row 0 is the top of the image).</summary>
        public static (float u0, float v0, float u1, float v1) Uv(int sign)
        {
            int col = sign % AtlasColumns, row = sign / AtlasColumns;
            float du = 1f / AtlasColumns, dv = 1f / AtlasRows;
            // Inset against bleeding at mip levels (512 x 128 cells of a 2048 x 2048 image), 4:1 like the cell.
            const float iv = 1f / 2048f, iu = 4f * iv;
            return (col * du + iu, 1f - (row + 1) * dv + iv, (col + 1) * du - iu, 1f - row * dv - iv);
        }

        public static int Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) & 0x7fffffff);
            }
        }

        /// <summary>Ground-floor bay used as a shop: every bay of a commercial building, otherwise some street-front
        /// bays (more in Ortaköy, where nearly every ground floor is a shop or café).</summary>
        public static bool IsShopBay(int seed, int edge, int bay, bool shopBuilding, bool ortakoy)
        {
            if (shopBuilding) return true;
            int h = Hash(seed + edge * 131, bay * 7 + 3);
            return h % 100 < (ortakoy ? 70 : 20);
        }

        /// <summary>Sign of a shop bay; neighbouring bays of one shop front share a sign every second bay.</summary>
        public static int SignFor(int seed, int edge, int bay, bool ortakoy)
        {
            int h = Hash(seed * 31 + edge, bay / 2 + 11);
            if (ortakoy && h % 100 < 70)
            {
                int n = 0;
                foreach (var s in Catalog) if (s.ortakoy) n++;
                int k = (h / 100) % n;
                for (int i = 0; i < Catalog.Length; i++)
                    if (Catalog[i].ortakoy && k-- == 0) return i;
            }
            int general = 0;
            foreach (var s in Catalog) if (!s.ortakoy) general++;
            int m = (h / 100) % general;
            for (int i = 0; i < Catalog.Length; i++)
                if (!Catalog[i].ortakoy && m-- == 0) return i;
            return 0;
        }

        /// <summary>
        /// Building front that shows Turkish flags hung vertically (from balcony railings or window sills): a
        /// quarter of the fronts in Ortaköy, one in ten elsewhere; one or two flags per front.
        /// </summary>
        public static bool FlagFront(int seed, int edge, bool ortakoy) => Hash(seed + 97, edge) % 100 < (ortakoy ? 25 : 10);

        /// <summary>Bay and storey (1-based, above the ground floor) of the flags on a flag front.</summary>
        public static (int bay, int floor) FlagSpot(int seed, int edge, int k, int bays, int floors)
        {
            int h = Hash(seed + edge * 17, k + 5);
            int floor = floors <= 2 ? 1 : 1 + h % Math.Min(3, floors - 1);
            return (Hash(h, 7) % Math.Max(1, bays), floor);
        }
    }
}
