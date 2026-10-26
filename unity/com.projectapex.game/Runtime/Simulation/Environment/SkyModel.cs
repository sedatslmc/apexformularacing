using System;
using Apex.Core;
using Apex.Weather;

namespace Apex.Environment
{
    /// <summary>
    /// Sun position for a place and UTC time (NOAA solar calculator: mean anomaly, equation of
    /// centre, obliquity, equation of time, atmospheric refraction). Accurate to well under a
    /// degree for 1900–2100, which is all the lighting needs.
    /// </summary>
    public static class SolarPosition
    {
        /// <summary>Beşiktaş/Ortaköy waterfront, the centre of the Bosphorus circuit.</summary>
        public const double IstanbulLatitude = 41.045, IstanbulLongitude = 29.01;
        /// <summary>Turkey keeps UTC+3 all year (no daylight saving since 2016).</summary>
        public const double IstanbulUtcOffset = 3.0;

        /// <param name="utcHours">hours since 00:00 UTC of the given date</param>
        /// <param name="elevation">apparent elevation in degrees (refraction included)</param>
        /// <param name="azimuth">true azimuth in degrees, from north clockwise</param>
        public static void Compute(double latitude, double longitude, int year, int month, int day, double utcHours,
            out float elevation, out float azimuth)
        {
            int y = year, m = month;
            if (m <= 2) { y -= 1; m += 12; }
            int a = y / 100, b = 2 - a + a / 4;
            double jd = Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + day + b - 1524.5 + utcHours / 24.0;
            double t = (jd - 2451545.0) / 36525.0;
            double l0 = PosMod(280.46646 + t * (36000.76983 + t * 0.0003032), 360.0);
            double ma = 357.52911 + t * (35999.05029 - 0.0001537 * t);
            double e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
            double mr = Rad(ma);
            double c = Math.Sin(mr) * (1.914602 - t * (0.004817 + 0.000014 * t)) + Math.Sin(2 * mr) * (0.019993 - 0.000101 * t) + Math.Sin(3 * mr) * 0.000289;
            double omega = 125.04 - 1934.136 * t;
            double lambda = l0 + c - 0.00569 - 0.00478 * Math.Sin(Rad(omega));
            double eps0 = 23.0 + (26.0 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60.0) / 60.0;
            double eps = eps0 + 0.00256 * Math.Cos(Rad(omega));
            double decl = Math.Asin(Math.Sin(Rad(eps)) * Math.Sin(Rad(lambda)));
            double yv = Math.Tan(Rad(eps / 2.0));
            yv *= yv;
            double l0r = Rad(l0);
            double eqTime = 4.0 * Deg(yv * Math.Sin(2 * l0r) - 2 * e * Math.Sin(mr) + 4 * e * yv * Math.Sin(mr) * Math.Cos(2 * l0r)
                - 0.5 * yv * yv * Math.Sin(4 * l0r) - 1.25 * e * e * Math.Sin(2 * mr));
            double trueSolarMinutes = PosMod(utcHours * 60.0 + eqTime + 4.0 * longitude, 1440.0);
            double hourAngle = Rad(trueSolarMinutes / 4.0 - 180.0);
            double lat = Rad(latitude);
            double cosZenith = Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(hourAngle);
            double geometric = 90.0 - Deg(Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosZenith))));
            elevation = (float)(geometric + Refraction(geometric));
            double az = Deg(Math.Atan2(Math.Sin(hourAngle), Math.Cos(hourAngle) * Math.Sin(lat) - Math.Tan(decl) * Math.Cos(lat))) + 180.0;
            azimuth = (float)PosMod(az, 360.0);
        }

        /// <summary>Istanbul, local clock time (UTC+3) on the given date.</summary>
        public static void Istanbul(int year, int month, int day, double localHours, out float elevation, out float azimuth) =>
            Compute(IstanbulLatitude, IstanbulLongitude, year, month, day, localHours - IstanbulUtcOffset, out elevation, out azimuth);

        /// <summary>Local clock time of sunset in Istanbul (upper limb at the horizon, apparent elevation −0.833°).</summary>
        public static double IstanbulSunset(int year, int month, int day)
        {
            double lo = 12.0, hi = 23.99;
            for (int i = 0; i < 40; i++)
            {
                double mid = 0.5 * (lo + hi);
                Istanbul(year, month, day, mid, out float el, out _);
                if (el > -0.833f) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Refraction in degrees for a geometric elevation (NOAA approximation).</summary>
        static double Refraction(double el)
        {
            if (el > 85.0) return 0.0;
            double tn = Math.Tan(Rad(el));
            if (el > 5.0) return (58.1 / tn - 0.07 / (tn * tn * tn) + 0.000086 / Math.Pow(tn, 5)) / 3600.0;
            if (el > -0.575) return (1735.0 + el * (-518.2 + el * (103.4 + el * (-12.79 + el * 0.711)))) / 3600.0;
            return -20.772 / tn / 3600.0;
        }

        static double Rad(double d) => d * Math.PI / 180.0;
        static double Deg(double r) => r * 180.0 / Math.PI;
        static double PosMod(double v, double m) => ((v % m) + m) % m;
    }

    /// <summary>
    /// Key and ambient light for the time of day and cloud cover, from the real sun position over
    /// Istanbul on the race weekend and a clear-sky atmosphere:
    /// <list type="bullet">
    /// <item>Direct sun: Rayleigh plus urban aerosol extinction along the air mass (Kasten–Young), per
    /// colour channel; the colour shifts from neutral at midday to golden near the horizon on its own.</item>
    /// <item>Sky: diffuse illuminance fitted to clear-sky measurements (about 18 klux at 50°, 7 klux at 10°,
    /// under 1 klux at sunset, civil twilight 3 lux at −6°).</item>
    /// <item>Exposure: a camera adapts, so the scene is scaled by (reference key / key)^0.6. The ratio of
    /// sun to sky stays physical, which gives low-sun scenes their bright, blue-tinted shadows.</item>
    /// </list>
    /// Night is art-directed (a cool moon and city glow, no physical moonlight levels).
    /// Colours are sRGB-encoded as Unity expects for Light.color and the Trilight ambient.
    /// </summary>
    public static class SkyModel
    {
        /// <summary>Race weekend: early autumn (sunset about 19:07 local).</summary>
        public const int Year = 2026, Month = 9, Day = 20;
        /// <summary>Aerosol optical depth at 550 nm: a typical clear day over Istanbul (AERONET 0.15–0.3).</summary>
        public const float AerosolDepth = 0.2f;
        /// <summary>How strongly the virtual camera compensates exposure (0 none, 1 full).</summary>
        public const float Adaptation = 0.6f;
        /// <summary>Unity intensity of the direct sun at the reference midday (elevation 50°).</summary>
        public const float ReferenceSunIntensity = 1.35f;
        const float ExtraterrestrialKlux = 128f;

        public struct Rgb
        {
            public float r, g, b;
            public Rgb(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
            public float Luminance => 0.2126f * r + 0.7152f * g + 0.0722f * b;
            public static Rgb operator *(Rgb c, float k) => new Rgb(c.r * k, c.g * k, c.b * k);
            public static Rgb operator +(Rgb a, Rgb b) => new Rgb(a.r + b.r, a.g + b.g, a.b + b.b);
            public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(ApexMath.Lerp(a.r, b.r, t), ApexMath.Lerp(a.g, b.g, t), ApexMath.Lerp(a.b, b.b, t));
            public override string ToString() => $"({r:0.000}, {g:0.000}, {b:0.000})";
        }

        public struct Lighting
        {
            /// <summary>True azimuth / elevation of the key light (sun, or moon at night).</summary>
            public float azimuthTrue, elevation;
            /// <summary>Key light colour (sRGB, max channel 1) and Unity intensity.</summary>
            public Rgb sunColor;
            public float sunIntensity;
            public float shadowStrength;
            /// <summary>Trilight ambient (sRGB, already scaled to the exposure).</summary>
            public Rgb ambientSky, ambientEquator, ambientGround;
            /// <summary>Distance fog / haze colour (sRGB).</summary>
            public Rgb fog;
            /// <summary>Procedural skybox: exposure and atmosphere thickness (Unity's Skybox/Procedural).</summary>
            public float skyExposure, atmosphereThickness;
            /// <summary>Street and window lights, 0..1.</summary>
            public float artificial;
            /// <summary>Exposure compensation the virtual camera applied, in EV (information and tests).</summary>
            public float exposureEv;
            /// <summary>Linear direct and diffuse illuminance on the ground in klux before exposure (tests, tuning).</summary>
            public float directKlux, skyKlux;
        }

        /// <summary>Local clock time of each preset on the race weekend.</summary>
        public static double ClockHours(TimeOfDay time)
        {
            double sunset = SolarPosition.IstanbulSunset(Year, Month, Day);
            switch (time)
            {
                case TimeOfDay.Morning: return 8.75;
                case TimeOfDay.Day: return 13.0;
                case TimeOfDay.Dusk: return sunset - 0.3;           // red sun just above the hills, about 3°
                case TimeOfDay.Night: return 22.5;
                default: return sunset - 0.9;                       // golden hour, sun about 9° high
            }
        }

        public static Lighting Get(TimeOfDay time, float cloudCover = 0f)
        {
            float cloud = ApexMath.Clamp01(cloudCover);
            if (time == TimeOfDay.Night) return Night(cloud);
            SolarPosition.Istanbul(Year, Month, Day, ClockHours(time), out float el, out float az);
            return ForSun(el, az, cloud);
        }

        /// <summary>Daylight for an arbitrary sun position (elevation above about −6°).</summary>
        public static Lighting ForSun(float elevation, float azimuthTrue, float cloudCover)
        {
            float cloud = ApexMath.Clamp01(cloudCover);
            var beam = Transmittance(elevation, AerosolDepth);
            // The disc sets over about one degree; below it there is no direct light.
            float disc = ApexMath.Clamp01((elevation + 0.8f) / 1.6f);
            float sinH = (float)Math.Sin(Math.Max(0.0, elevation) * Math.PI / 180.0);
            float directNormal = ExtraterrestrialKlux * beam.Luminance * disc * (1f - 0.8f * cloud);
            float direct = directNormal * sinH;
            float sky = SkyKlux(elevation) * (1f + 0.25f * cloud);   // overcast: brighter, flatter diffuse
            float key = direct + sky;
            float refKey = ReferenceKey;
            float gain = (float)Math.Pow(refKey / Math.Max(key, 1e-4f), Adaptation);
            gain = Math.Min(gain, 40f);

            var l = new Lighting { azimuthTrue = azimuthTrue, elevation = elevation, directKlux = direct, skyKlux = sky };
            l.exposureEv = (float)(Math.Log(gain) / Math.Log(2.0));
            // Key light: colour from the beam's spectrum, intensity relative to the reference midday beam.
            var tint = Normalise(beam);
            var overcast = new Rgb(0.8f, 0.84f, 0.9f);
            l.sunColor = Encode(Rgb.Lerp(tint, Linear(overcast), cloud * 0.8f));
            l.sunIntensity = ReferenceSunIntensity * directNormal / ReferenceDirectNormal * gain;
            l.shadowStrength = ApexMath.Lerp(0.9f, 0.3f, cloud);

            // Ambient: overhead sky stays blue; the horizon takes the low sun's colour; the ground bounces both.
            float warmth = 1f - ApexMath.SmoothStep01((elevation - 2f) / 23f);
            float twilight = 1f - ApexMath.SmoothStep01((elevation + 2f) / 8f);
            var skyDay = Linear(new Rgb(0.55f, 0.65f, 0.78f));
            var skyTwilight = Linear(new Rgb(0.42f, 0.47f, 0.66f));
            var horizonDay = Linear(new Rgb(0.55f, 0.55f, 0.52f));
            var horizonWarm = Linear(new Rgb(0.74f, 0.6f, 0.5f));
            var grey = Linear(new Rgb(0.6f, 0.62f, 0.64f));
            var skyCol = Rgb.Lerp(Rgb.Lerp(skyDay, skyTwilight, twilight), grey, cloud);
            var horizonCol = Rgb.Lerp(Rgb.Lerp(horizonDay, horizonWarm, warmth), grey * 0.95f, cloud);
            float ambientScale = sky / ReferenceSkyKlux * gain;
            l.ambientSky = Encode(skyCol * ambientScale);
            l.ambientEquator = Encode(horizonCol * ambientScale);
            // Ground bounce: asphalt, stone and water (albedo ~0.1) lit by sun and sky. Its radiance relative
            // to the sky's is albedo × ground illuminance / sky illuminance.
            var groundLight = (tint * direct + Normalise(skyCol) * sky) * (0.1f / Math.Max(sky, 1e-4f) * skyCol.Luminance * ambientScale);
            l.ambientGround = Encode(groundLight);

            // Haze: daylight blue-grey, golden near the horizon sun, flattened by cloud.
            var fogDay = new Rgb(0.72f, 0.78f, 0.84f);
            var fogWarm = new Rgb(0.8f, 0.74f, 0.66f);
            var fogTwilight = new Rgb(0.6f, 0.56f, 0.58f);
            var fog = Rgb.Lerp(Rgb.Lerp(fogDay, fogWarm, warmth), fogTwilight, twilight);
            l.fog = Rgb.Lerp(fog, fog * 0.75f + new Rgb(0.14f, 0.15f, 0.16f), cloud);
            l.skyExposure = ApexMath.Lerp(1.3f, 0.7f, twilight);
            l.atmosphereThickness = ApexMath.Lerp(0.9f, 1.25f, warmth);
            l.artificial = ApexMath.Clamp01(twilight * 0.6f + (elevation < 12f ? 0.15f * warmth : 0f));
            return l;
        }

        static Lighting Night(float cloud)
        {
            // Art-directed: a dim, cool moon high in the south-east and a warm city sky glow at the horizon.
            var l = new Lighting
            {
                azimuthTrue = 140f,
                elevation = 38f,
                sunColor = new Rgb(0.55f, 0.65f, 0.9f),
                sunIntensity = 0.16f * (1f - 0.65f * cloud),
                shadowStrength = ApexMath.Lerp(0.8f, 0.2f, cloud),
                ambientSky = new Rgb(0.09f, 0.11f, 0.17f),
                ambientEquator = Rgb.Lerp(new Rgb(0.14f, 0.12f, 0.11f), new Rgb(0.18f, 0.15f, 0.13f), cloud),  // clouds reflect the city
                ambientGround = new Rgb(0.05f, 0.05f, 0.05f),
                fog = Rgb.Lerp(new Rgb(0.05f, 0.07f, 0.11f), new Rgb(0.1f, 0.1f, 0.11f), cloud),
                skyExposure = 0.12f,
                atmosphereThickness = 1f,
                artificial = 1f,
            };
            return l;
        }

        /// <summary>Direct-beam transmittance (linear) for the R, G, B primaries (610, 550, 465 nm).</summary>
        public static Rgb Transmittance(float elevation, float aerosol550)
        {
            float h = Math.Max(elevation, -0.5f);
            double m = 1.0 / (Math.Sin(h * Math.PI / 180.0) + 0.50572 * Math.Pow(h + 6.07995, -1.6364));
            return new Rgb(Channel(0.61, m, aerosol550), Channel(0.55, m, aerosol550), Channel(0.465, m, aerosol550));
        }

        static float Channel(double lambda, double airMass, float aerosol550)
        {
            // Rayleigh (Hansen & Travis) and Ångström aerosol (alpha 1.3) optical depths.
            double l2 = lambda * lambda, l4 = l2 * l2;
            double rayleigh = 0.008569 / l4 * (1.0 + 0.0113 / l2 + 0.00013 / l4);
            double aero = aerosol550 * Math.Pow(lambda / 0.55, -1.3);
            return (float)Math.Exp(-airMass * (rayleigh + aero));
        }

        /// <summary>Clear-sky diffuse illuminance on the ground (klux).</summary>
        public static float SkyKlux(float elevation)
        {
            if (elevation >= 0f)
                return 0.8f + 21f * (float)Math.Pow(Math.Sin(elevation * Math.PI / 180.0), 0.65);
            return 0.8f * (float)Math.Pow(10.0, 0.395 * elevation);
        }

        const float ReferenceElevation = 50f;
        static float ReferenceDirectNormal => ExtraterrestrialKlux * Transmittance(ReferenceElevation, AerosolDepth).Luminance;
        static float ReferenceSkyKlux => SkyKlux(ReferenceElevation);
        static float ReferenceKey => ReferenceDirectNormal * (float)Math.Sin(ReferenceElevation * Math.PI / 180.0) + ReferenceSkyKlux;

        static Rgb Normalise(Rgb c)
        {
            float m = Math.Max(c.r, Math.Max(c.g, c.b));
            return m <= 1e-6f ? new Rgb(1f, 0.4f, 0.1f) : c * (1f / m);
        }

        public static float ToLinear(float s) => s <= 0.04045f ? s / 12.92f : (float)Math.Pow((s + 0.055f) / 1.055f, 2.4);
        public static float ToSrgb(float l) => l <= 0.0031308f ? l * 12.92f : 1.055f * (float)Math.Pow(l, 1.0 / 2.4) - 0.055f;
        static Rgb Linear(Rgb s) => new Rgb(ToLinear(s.r), ToLinear(s.g), ToLinear(s.b));
        static Rgb Encode(Rgb l) => new Rgb(ToSrgb(Math.Max(0f, l.r)), ToSrgb(Math.Max(0f, l.g)), ToSrgb(Math.Max(0f, l.b)));
    }
}
