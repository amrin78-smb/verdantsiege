using System;
using System.IO;
using UnityEngine;

namespace VerdantSiege.Save
{
    /// <summary>Upgrades older save JSON to the current shape. One step per version.</summary>
    public static class SaveMigrator
    {
        /// <summary>Returns the migrated data, or null if the version is unknown/newer than this build.</summary>
        public static SaveData Migrate(string json, int fromVersion)
        {
            if (fromVersion > SaveData.CurrentVersion) return null; // written by a newer build: do not clobber
            if (fromVersion < 0) return null;
            // Version 0 = pre-release prototype saves without a version field; same shape as v1.
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return null;
            if (fromVersion < 2)
            {
                // v1 -> v2: Essence (boss reward) and per-encounter boss defeat counts did not exist yet. Start both at
                // zero explicitly so a v1 save never inherits garbage, and the first boss win still counts as the first.
                data.essence = 0;
                if (data.encounters != null) foreach (EncounterRecord r in data.encounters) if (r != null) r.bossDefeats = 0;
            }
            if (data.settings == null) data.settings = new SettingsData();
            if (data.encounters == null) data.encounters = new System.Collections.Generic.List<EncounterRecord>();
            if (data.unlockedDefenders == null) data.unlockedDefenders = new System.Collections.Generic.List<string>();
            if (fromVersion < 3)
            {
                // v2 -> v3 (Milestone 4): stars from the old completed/flawless flags; no mastery, research or
                // loadouts yet (stray fields from hand edits are dropped). Earlier progress is honoured with mastery
                // once the content is known (CampaignRules.Reconcile reads legacyGrantPending).
                data.mastery = new System.Collections.Generic.List<MasteryRecord>();
                data.research = new System.Collections.Generic.List<string>();
                data.lastLoadout = new System.Collections.Generic.List<string>();
                data.loadouts = new System.Collections.Generic.List<LoadoutRecord>();
                data.sanctuaryTierSeen = -1;
                bool any = false;
                foreach (EncounterRecord r in data.encounters)
                {
                    if (r == null) continue;
                    r.stars = (r.completed ? EncounterRecord.StarWin : 0) | (r.completed && r.flawless ? EncounterRecord.StarFlawless : 0);
                    r.wins = r.completed ? 1 : 0;
                    any |= r.completed;
                }
                data.legacyGrantPending = any;
            }
            data.encounters.RemoveAll(r => r == null);
            if (data.mastery == null) data.mastery = new System.Collections.Generic.List<MasteryRecord>();
            if (data.research == null) data.research = new System.Collections.Generic.List<string>();
            if (data.lastLoadout == null) data.lastLoadout = new System.Collections.Generic.List<string>();
            if (data.loadouts == null) data.loadouts = new System.Collections.Generic.List<LoadoutRecord>();
            data.version = SaveData.CurrentVersion;
            return data;
        }
    }

    public enum SaveLoadOutcome
    {
        Loaded = 0,
        NewGame,           // no save present
        RecoveredFromBackup,
        CorruptReset,      // primary and backup unreadable; started fresh (corrupt file kept aside)
        NewerVersion       // save from a newer build; loaded defaults without overwriting
    }

    /// <summary>
    /// Local, offline, versioned JSON save. Writes go to a temp file, the previous good file is kept as a
    /// backup, then the temp file replaces the primary, so an interrupted write never destroys progress.
    /// </summary>
    public sealed class SaveService
    {
        [Serializable]
        private sealed class VersionProbe { public int version = -1; }

        private readonly string _path;
        private string BackupPath => _path + ".bak";
        private string TempPath => _path + ".tmp";

        public SaveData Data { get; private set; } = new SaveData();
        public SaveLoadOutcome LastOutcome { get; private set; }
        /// <summary>When false (newer-version save detected) writes are refused to protect the newer file.</summary>
        public bool WritesAllowed { get; private set; } = true;

        public SaveService(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "verdant_siege_save.json");

        public SaveLoadOutcome Load()
        {
            WritesAllowed = true;
            if (!File.Exists(_path) && !File.Exists(BackupPath))
            {
                Data = new SaveData();
                return LastOutcome = SaveLoadOutcome.NewGame;
            }
            if (TryRead(_path, out SaveData d, out bool newer))
            {
                Data = d;
                return LastOutcome = SaveLoadOutcome.Loaded;
            }
            if (newer)
            {
                Data = new SaveData();
                WritesAllowed = false;
                return LastOutcome = SaveLoadOutcome.NewerVersion;
            }
            if (TryRead(BackupPath, out d, out newer))
            {
                Data = d;
                Debug.LogWarning("[VS Save] Primary save unreadable; recovered from backup.");
                return LastOutcome = SaveLoadOutcome.RecoveredFromBackup;
            }
            // Keep the corrupt file for diagnosis, start fresh.
            try { if (File.Exists(_path)) File.Copy(_path, _path + ".corrupt", true); } catch (Exception) { }
            Debug.LogWarning("[VS Save] Save data corrupt; starting a new game (copy kept as .corrupt).");
            Data = new SaveData();
            return LastOutcome = SaveLoadOutcome.CorruptReset;
        }

        private static bool TryRead(string path, out SaveData data, out bool newerVersion)
        {
            data = null;
            newerVersion = false;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;
                VersionProbe probe = JsonUtility.FromJson<VersionProbe>(json);
                int version = probe != null && probe.version >= 0 ? probe.version : 0;
                if (version > SaveData.CurrentVersion) { newerVersion = true; return false; }
                data = SaveMigrator.Migrate(json, version);
                return data != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VS Save] Could not read {Path.GetFileName(path)}: {ex.Message}");
                return false;
            }
        }

        public bool Save()
        {
            if (!WritesAllowed) return false;
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                Data.version = SaveData.CurrentVersion;
                Data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                File.WriteAllText(TempPath, JsonUtility.ToJson(Data, true));
                if (File.Exists(_path)) File.Copy(_path, BackupPath, true);
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(TempPath, _path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VS Save] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>New game: wipes progression (settings kept) after the UI has confirmed with the player.</summary>
        public void ResetProgress(bool keepSettings = true)
        {
            SettingsData settings = keepSettings ? Data.settings : new SettingsData();
            Data = new SaveData { settings = settings };
            WritesAllowed = true;
            Save();
        }
    }
}
