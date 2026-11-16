using System;
using System.Collections.Generic;

namespace VerdantSiege.Save
{
    [Serializable]
    public sealed class SettingsData
    {
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.9f;
        public float ambienceVolume = 0.6f;
        public bool autoCollect = false;
        public bool reducedFlashes = false;
        public bool reducedShake = false;
        public bool showDamageNumbers = true;
        /// <summary>-1 = automatic (device tier), else 0 Low, 1 Medium, 2 High.</summary>
        public int qualityLevel = -1;
        public bool tutorialSeen = false;
    }

    [Serializable]
    public sealed class EncounterRecord
    {
        public string encounterId;
        public bool completed;
        public bool flawless;
        public int bestHeartroot;
        public int attempts;
        /// <summary>Times this encounter's boss was defeated (v2; 0 for non-boss encounters).</summary>
        public int bossDefeats;
    }

    /// <summary>
    /// Persistent progression. Plain serialisable data only (JsonUtility); never secrets or credentials.
    /// Bump <see cref="CurrentVersion"/> and add a step to <see cref="SaveMigrator"/> whenever the shape changes.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public long savedAtUnix;
        public int seeds;
        /// <summary>Rare meta currency earned by defeating bosses (v2).</summary>
        public int essence;
        public List<EncounterRecord> encounters = new List<EncounterRecord>();
        public List<string> unlockedDefenders = new List<string>();
        public SettingsData settings = new SettingsData();

        public EncounterRecord GetOrCreate(string encounterId)
        {
            foreach (EncounterRecord r in encounters) if (r.encounterId == encounterId) return r;
            var rec = new EncounterRecord { encounterId = encounterId };
            encounters.Add(rec);
            return rec;
        }

        /// <summary>
        /// Records a boss defeat in an encounter and adds its Essence: <paramref name="firstEssence"/> the first time,
        /// <paramref name="repeatEssence"/> afterwards. Returns the Essence awarded.
        /// </summary>
        public int AwardBossVictory(string encounterId, int firstEssence, int repeatEssence)
        {
            EncounterRecord rec = GetOrCreate(encounterId);
            int award = rec.bossDefeats == 0 ? firstEssence : repeatEssence;
            rec.bossDefeats++;
            essence += Math.Max(0, award);
            return Math.Max(0, award);
        }

        public bool IsCompleted(string encounterId)
        {
            foreach (EncounterRecord r in encounters) if (r.encounterId == encounterId) return r.completed;
            return false;
        }
    }
}
