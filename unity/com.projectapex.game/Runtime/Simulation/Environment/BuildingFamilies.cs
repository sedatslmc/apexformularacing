using System;

namespace Apex.Environment
{
    /// <summary>
    /// Building families of the real city (docs/ISTANBUL_OVERHAUL.md, Phase 4): the building stock of Beşiktaş,
    /// Akaretler and Ortaköy is not one kind of block. Each OSM building gets the family that fits its height,
    /// footprint, use and quarter, and the family decides its colours, roof and street-front relief
    /// (EnvironmentGeometry): wooden houses with bay windows, 19th-century masonry with string courses, the
    /// 1960s–80s apartment blocks with continuous balconies that make up most of the district, contemporary
    /// buildings with glass balustrades, and office or shop buildings. Deterministic per building.
    /// </summary>
    public static class BuildingFamilies
    {
        public enum Family
        {
            /// <summary>Late-Ottoman timber house: 2–3 storeys, painted weatherboarding, bay window (cumba), hip roof.</summary>
            OttomanTimber,
            /// <summary>19th / early 20th-century masonry apartment (Beşiktaş, Akaretler surroundings): string courses, plinth, cornice.</summary>
            Masonry19th,
            /// <summary>1960s–80s apartment block (apartman): continuous concrete balconies on every floor, flat roof with tanks.</summary>
            Apartment1970s,
            /// <summary>Contemporary residence (after 2000): glass balustrades, vertical fins, cooler colours.</summary>
            Contemporary,
            /// <summary>Office, hotel or shop building: shopfronts, banded windows.</summary>
            Commercial,
        }

        /// <summary>Facade colour slots: 0–5 Istanbul plaster (CityLayout styles), 6–8 painted timber, 9 contemporary grey.</summary>
        public const int FacadeStyles = 10;
        public const int TimberStyle0 = 6, TimberStyles = 3, ContemporaryStyle = 9, PlasterStyles = 6;

        /// <param name="type">OSM building type (apartments, house, commercial, office, hotel, retail, yes …)</param>
        /// <param name="historic">OSM historic tag or a listed kind (palace, place of worship)</param>
        /// <param name="ortakoy">within Ortaköy's quarter (most of the surviving timber houses near the circuit)</param>
        public static Family Classify(string type, float height, float area, bool historic, bool ortakoy, int hash)
        {
            hash &= 0x7fffffff;
            type ??= "";
            if (type == "office" || type == "commercial" || type == "retail" || type == "hotel")
                return height > 18f && hash % 3 == 0 ? Family.Contemporary : Family.Commercial;
            // Timber houses: low and small; more of them in Ortaköy.
            if (height <= 11f && area < 220f && (type == "house" || type == "detached" || hash % (ortakoy ? 2 : 5) == 0))
                return Family.OttomanTimber;
            if (historic) return Family.Masonry19th;
            if (height > 27f) return hash % 3 == 0 ? Family.Commercial : Family.Contemporary;
            // Mid-rise: mostly the post-war blocks, some older masonry, a few new buildings.
            int r = hash % 20;
            if (r < 4) return Family.Masonry19th;
            if (r < 6) return Family.Contemporary;
            return Family.Apartment1970s;
        }

        /// <summary>Facade colour slot for a building of a family.</summary>
        public static int Style(Family f, bool historic, int hash)
        {
            hash &= 0x7fffffff;
            switch (f)
            {
                case Family.OttomanTimber: return TimberStyle0 + hash % TimberStyles;
                case Family.Contemporary: return hash % 2 == 0 ? ContemporaryStyle : 3;   // grey, or light grey plaster
                case Family.Masonry19th: return historic ? PlasterStyles - 1 : (hash % 2 == 0 ? 0 : 4);   // cream / pale yellow stucco
                default: return hash % PlasterStyles;
            }
        }

        /// <summary>Tiled hip roof (four-cornered footprints): always for timber houses, often for old masonry, never for blocks.</summary>
        public static bool HipRoof(Family f, int corners, int hash) =>
            corners == 4 && (f == Family.OttomanTimber || (f == Family.Masonry19th && (hash & 0x7fffffff) % 2 == 0));
    }
}
