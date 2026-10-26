using System;
using System.IO;
using Apex.Tracks;

namespace Apex.Tests
{
    /// <summary>Locates package data from both the Unity editor (cwd = project root
    /// inside the repo) and the .NET test runner (bin folder inside the repo).</summary>
    public static class TestData
    {
        const string Relative = "com.projectapex.game/Runtime/Resources/Tracks";

        public static string TracksDir()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    foreach (var candidate in new[] { Path.Combine(dir.FullName, Relative), Path.Combine(dir.FullName, "unity", Relative) })
                        if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Could not locate " + Relative);
        }

        public static CircuitDefinition Circuit(string id) =>
            CircuitDefinition.FromJson(File.ReadAllText(Path.Combine(TracksDir(), id + ".json")));

        public static TrackGeometry Geometry(string id) => TrackGeometry.FromDefinition(Circuit(id));

        /// <summary>The OSM city data next to the circuits (Resources/City/&lt;id&gt;.json).</summary>
        public static Apex.Environment.CityData City(string id) =>
            Apex.Environment.CityData.FromJson(File.ReadAllText(Path.Combine(TracksDir(), "..", "City", id + ".json")));
    }
}
