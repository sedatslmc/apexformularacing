using System;
using System.IO;
using Apex.Core;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Versioned JSON persistence for settings and records. Writes go to a temp
    /// file first and the previous file is kept as .bak, so a crash mid-write or
    /// a corrupt file falls back to the last good copy, then to defaults.
    /// </summary>
    public static class SaveService
    {
        static string Dir => Application.persistentDataPath;
        static string SettingsPath => Path.Combine(Dir, "settings.json");

        public static SettingsData LoadSettings()
        {
            foreach (var path in new[] { SettingsPath, SettingsPath + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var data = JsonUtility.FromJson<SettingsData>(File.ReadAllText(path));
                    if (data == null) continue;
                    if (data.MigrateAndValidate()) SaveSettings(data);
                    return data;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Apex] Could not read {path}: {e.Message}");
                }
            }
            return new SettingsData();
        }

        public static bool SaveSettings(SettingsData data)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = SettingsPath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(SettingsPath)) File.Copy(SettingsPath, SettingsPath + ".bak", true);
                File.Copy(tmp, SettingsPath, true);
                File.Delete(tmp);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Apex] Saving settings failed: {e.Message}");
                return false;
            }
        }

        [Serializable]
        class BestLapTable
        {
            public string[] tracks = new string[0];
            public double[] times = new double[0];
        }

        public static double BestLap(SettingsData s, string trackId)
        {
            var t = Table(s);
            int i = Array.IndexOf(t.tracks, trackId);
            return i >= 0 ? t.times[i] : 0.0;
        }

        /// <summary>Stores the lap if it beats the record; returns true if it did.</summary>
        public static bool SubmitLap(SettingsData s, string trackId, double lapTime)
        {
            var t = Table(s);
            int i = Array.IndexOf(t.tracks, trackId);
            if (i >= 0 && t.times[i] > 0 && lapTime >= t.times[i]) return false;
            if (i < 0)
            {
                Array.Resize(ref t.tracks, t.tracks.Length + 1);
                Array.Resize(ref t.times, t.times.Length + 1);
                i = t.tracks.Length - 1;
                t.tracks[i] = trackId;
            }
            t.times[i] = lapTime;
            s.bestLapsJson = JsonUtility.ToJson(t);
            SaveSettings(s);
            return true;
        }

        static BestLapTable Table(SettingsData s)
        {
            if (string.IsNullOrEmpty(s.bestLapsJson)) return new BestLapTable();
            try
            {
                return JsonUtility.FromJson<BestLapTable>(s.bestLapsJson) ?? new BestLapTable();
            }
            catch (Exception)
            {
                return new BestLapTable();
            }
        }
    }
}
