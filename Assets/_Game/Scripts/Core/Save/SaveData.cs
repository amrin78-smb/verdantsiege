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
        /// <summary>Star bits (v3): win, flawless Heartroot, challenge objective.</summary>
        public const int StarWin = 1, StarFlawless = 2, StarChallenge = 4;

        public string encounterId;
        public bool completed;
        public bool flawless;
        public int bestHeartroot;
        public int attempts;
        /// <summary>Times this encounter's boss was defeated (v2; 0 for non-boss encounters).</summary>
        public int bossDefeats;
        /// <summary>Earned star bits (v3, see StarWin/StarFlawless/StarChallenge). Each star's reward is paid once.</summary>
        public int stars;
        /// <summary>Wins (v3; the first is the full-reward clear, later ones are replays).</summary>
        public int wins;

        public int StarCount => ((stars & StarWin) != 0 ? 1 : 0) + ((stars & StarFlawless) != 0 ? 1 : 0) + ((stars & StarChallenge) != 0 ? 1 : 0);
        public bool HasStar(int bit) => (stars & bit) != 0;
    }

    /// <summary>Defender mastery (v3): XP earned by using a defender in battles.</summary>
    [Serializable]
    public sealed class MasteryRecord
    {
        public string defenderId;
        public int xp;
    }

    /// <summary>The deck last used for a mission (v3).</summary>
    [Serializable]
    public sealed class LoadoutRecord
    {
        public string encounterId;
        public List<string> defenders = new List<string>();
    }

    /// <summary>
    /// Persistent progression. Plain serialisable data only (JsonUtility); never secrets or credentials.
    /// Bump <see cref="CurrentVersion"/> and add a step to <see cref="SaveMigrator"/> whenever the shape changes.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public long savedAtUnix;
        public int seeds;
        /// <summary>Rare meta currency earned by defeating bosses and completing challenges (v2).</summary>
        public int essence;
        public List<EncounterRecord> encounters = new List<EncounterRecord>();
        /// <summary>Defender collection (campaign unlocks; v3 fills it from the campaign's starting defenders).</summary>
        public List<string> unlockedDefenders = new List<string>();
        public SettingsData settings = new SettingsData();

        // ---- v3: Milestone 4 campaign and progression
        public List<MasteryRecord> mastery = new List<MasteryRecord>();
        /// <summary>Research node ids bought in the Seed Conservatory.</summary>
        public List<string> research = new List<string>();
        /// <summary>The most recent deck (fills new missions' defaults).</summary>
        public List<string> lastLoadout = new List<string>();
        public List<LoadoutRecord> loadouts = new List<LoadoutRecord>();
        /// <summary>Sanctuary restoration tier the player has already seen celebrated (-1 = not set yet).</summary>
        public int sanctuaryTierSeen = -1;
        /// <summary>Set by the v2->v3 migration: honour earlier progress with mastery once content is known.</summary>
        public bool legacyGrantPending;

        public EncounterRecord GetOrCreate(string encounterId)
        {
            foreach (EncounterRecord r in encounters) if (r.encounterId == encounterId) return r;
            var rec = new EncounterRecord { encounterId = encounterId };
            encounters.Add(rec);
            return rec;
        }

        public EncounterRecord Find(string encounterId)
        {
            foreach (EncounterRecord r in encounters) if (r != null && r.encounterId == encounterId) return r;
            return null;
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

        public bool IsUnlocked(string defenderId) => unlockedDefenders.Contains(defenderId);

        public bool Unlock(string defenderId)
        {
            if (string.IsNullOrEmpty(defenderId) || unlockedDefenders.Contains(defenderId)) return false;
            unlockedDefenders.Add(defenderId);
            return true;
        }

        public int MasteryXp(string defenderId)
        {
            foreach (MasteryRecord m in mastery) if (m != null && m.defenderId == defenderId) return m.xp;
            return 0;
        }

        public void AddMasteryXp(string defenderId, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(defenderId)) return;
            foreach (MasteryRecord m in mastery)
                if (m != null && m.defenderId == defenderId) { m.xp += amount; return; }
            mastery.Add(new MasteryRecord { defenderId = defenderId, xp = amount });
        }

        public bool HasResearch(string id) => research.Contains(id);

        public List<string> LoadoutFor(string encounterId)
        {
            foreach (LoadoutRecord l in loadouts) if (l != null && l.encounterId == encounterId) return l.defenders;
            return null;
        }

        public void SetLoadout(string encounterId, List<string> defenders)
        {
            var copy = new List<string>(defenders);
            lastLoadout = new List<string>(defenders);
            foreach (LoadoutRecord l in loadouts)
                if (l != null && l.encounterId == encounterId) { l.defenders = copy; return; }
            loadouts.Add(new LoadoutRecord { encounterId = encounterId, defenders = copy });
        }
    }
}
