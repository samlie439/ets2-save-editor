using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Ets2Save
{
    public sealed class SaveSlot
    {
        public string ProfileDir;
        public string ProfileName;
        public string Slot;
        public string GamePath;
        public string InfoPath;
        public DateTime Modified;
        public string Name;
        public long Money;
        public long Experience;
        public long PlayTimeSeconds;

        public string Display
        {
            get
            {
                return string.Format("{0}  ·  {1}  ·  {2:yyyy-MM-dd HH:mm}", Name, Slot, Modified);
            }
        }

        public override string ToString()
        {
            return string.Format("{0}   ({1:MM-dd HH:mm})", Name, Modified);
        }
    }

    public static class SaveLibrary
    {
        public static string DocumentsRoot
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(Path.Combine(docs, "Euro Truck Simulator 2"));
            }
        }

        public static List<SaveSlot> Discover()
        {
            List<SaveSlot> result = new List<SaveSlot>();
            string root = Path.Combine(DocumentsRoot, "profiles");
            if (!Directory.Exists(root)) return result;

            foreach (string profileDir in Directory.GetDirectories(root))
            {
                string profileKey = Path.GetFileName(profileDir);
                string profileName = ReadProfileName(profileDir);
                string saveDir = Path.Combine(profileDir, "save");
                if (!Directory.Exists(saveDir)) continue;

                foreach (string slotDir in Directory.GetDirectories(saveDir))
                {
                    string game = Path.Combine(slotDir, "game.sii");
                    if (!File.Exists(game)) continue;
                    SaveSlot slot = new SaveSlot();
                    slot.ProfileDir = profileDir;
                    slot.ProfileName = profileName;
                    slot.Slot = Path.GetFileName(slotDir);
                    slot.GamePath = game;
                    slot.InfoPath = Path.Combine(slotDir, "info.sii");
                    slot.Modified = File.GetLastWriteTime(game);
                    slot.Name = slot.Slot;
                    slot.PlayTimeSeconds = -1;
                    TryReadInfo(slot);
                    result.Add(slot);
                }
            }
            result.Sort(delegate (SaveSlot a, SaveSlot b) { return b.Modified.CompareTo(a.Modified); });
            return result;
        }

        static void TryReadInfo(SaveSlot slot)
        {
            if (!File.Exists(slot.InfoPath)) return;
            try
            {
                string container;
                byte[] payload = Scsc.LoadPayload(slot.InfoPath, out container);
                if (Scsc.DetectFormat(payload) != "text")
                {
                    slot.Name = slot.Slot;
                    return;
                }
                string text = System.Text.Encoding.UTF8.GetString(payload);
                Dictionary<string, SiiTextBlock> blocks = SiiTextParser.Parse(text);
                string saveName = SiiTextParser.Get(blocks, "save_container", "name", null);
                slot.Name = string.IsNullOrEmpty(saveName) ? slot.Slot : saveName;
                slot.Money = SiiTextParser.GetLong(blocks, "save_container", "info_money_account", 0);
                slot.Experience = SiiTextParser.GetLong(blocks, "save_container", "info_players_experience", 0);
                slot.PlayTimeSeconds = SiiTextParser.GetLong(blocks, "save_container", "time", -1);
            }
            catch
            {
                slot.Name = slot.Slot;
            }
        }

        static string ReadProfileName(string profileDir)
        {
            string key = Path.GetFileName(profileDir);
            try
            {
                string file = Path.Combine(profileDir, "profile.sii");
                if (File.Exists(file))
                {
                    Dictionary<string, SiiTextBlock> blocks = SiiTextParser.Parse(File.ReadAllText(file, System.Text.Encoding.UTF8));
                    string name = SiiTextParser.Get(blocks, "user_profile", "company_name", null);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch
            {
            }
            if (key.Length > 12) key = key.Substring(0, 12) + "…";
            return "配置 " + key;
        }

        public static string FormatPlayTime(long seconds)
        {
            if (seconds < 0) return "-";
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return string.Format("{0} 小时 {1} 分", (int)t.TotalHours, t.Minutes);
        }

        // ------------------------------------------------------------------
        /// <summary>Copies every save slot into <paramref name="root"/> and prunes old copies.</summary>
        public static List<string> BackupAll(string root, int keepPerSlot)
        {
            List<string> written = new List<string>();
            List<SaveSlot> slots = Discover();
            Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();
            foreach (SaveSlot slot in slots)
            {
                string profileDir = Path.Combine(root, Sanitize(slot.ProfileName));
                Directory.CreateDirectory(profileDir);
                string fileName = string.Format("{0}_{1:yyyyMMdd_HHmmss}.sii", Sanitize(slot.Slot), DateTime.Now);
                string target = Path.Combine(profileDir, fileName);
                File.Copy(slot.GamePath, target, true);
                written.Add(target);

                string prefix = Sanitize(slot.Slot) + "_";
                List<string> list;
                if (!groups.TryGetValue(profileDir + "|" + prefix, out list))
                {
                    list = new List<string>();
                    groups[profileDir + "|" + prefix] = list;
                }
                list.Add(target);
            }

            foreach (KeyValuePair<string, List<string>> kv in groups)
            {
                string[] parts = kv.Key.Split('|');
                string dir = parts[0];
                string prefix = parts[1];
                List<string> mine = new List<string>();
                foreach (string file in Directory.GetFiles(dir, prefix + "*.sii"))
                {
                    string rest = Path.GetFileName(file).Substring(prefix.Length);
                    if (System.Text.RegularExpressions.Regex.IsMatch(rest, @"^\d{8}_\d{6}\.sii$"))
                        mine.Add(file);
                }
                mine.Sort(StringComparer.OrdinalIgnoreCase);
                int excess = mine.Count - keepPerSlot;
                for (int i = 0; i < excess; i++)
                {
                    try { File.Delete(mine[i]); } catch { }
                }
            }
            return written;
        }

        static string Sanitize(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid) name = name.Replace(c, '_');
            if (name.Length == 0) name = "save";
            if (name.Length > 40) name = name.Substring(0, 40);
            return name;
        }
    }
}
