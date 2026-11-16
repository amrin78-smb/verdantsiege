using System.IO;
using NUnit.Framework;
using VerdantSiege.Save;

namespace VerdantSiege.Tests
{
    /// <summary>Essence (boss reward) in the save: v1 -> v2 migration, first/repeat awards, persistence.</summary>
    public sealed class BossRewardSaveTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vs_essence_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void CurrentVersion_IsAtLeastTwo()
        {
            Assert.GreaterOrEqual(SaveData.CurrentVersion, 2, "Essence bumped the save to v2 (Milestone 4: v3)");
            Assert.AreEqual(0, new SaveData().essence);
        }

        [Test]
        public void Migration_V1ToV2_KeepsProgress_StartsEssenceAtZero()
        {
            // A v1 save never had Essence; stray fields (hand edits) must not leak into v2.
            File.WriteAllText(_path, "{\"version\":1,\"seeds\":420,\"essence\":77," +
                                     "\"encounters\":[{\"encounterId\":\"vh_07\",\"completed\":true,\"attempts\":3,\"bossDefeats\":5}]," +
                                     "\"settings\":{\"reducedShake\":true}}");
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.Loaded, s.Load());
            Assert.AreEqual(SaveData.CurrentVersion, s.Data.version);
            Assert.AreEqual(420, s.Data.seeds, "progress kept");
            Assert.IsTrue(s.Data.IsCompleted("vh_07"));
            Assert.AreEqual(3, s.Data.GetOrCreate("vh_07").attempts);
            Assert.IsTrue(s.Data.settings.reducedShake, "settings kept");
            Assert.AreEqual(0, s.Data.essence, "Essence starts at zero");
            Assert.AreEqual(0, s.Data.GetOrCreate("vh_07").bossDefeats, "first v2 boss win still counts as the first");
        }

        [Test]
        public void AwardBossVictory_FirstThenRepeat()
        {
            var d = new SaveData();
            Assert.AreEqual(3, d.AwardBossVictory("vh_07", 3, 1));
            Assert.AreEqual(1, d.AwardBossVictory("vh_07", 3, 1));
            Assert.AreEqual(1, d.AwardBossVictory("vh_07", 3, 1));
            Assert.AreEqual(5, d.essence);
            Assert.AreEqual(3, d.GetOrCreate("vh_07").bossDefeats);
            Assert.AreEqual(3, d.AwardBossVictory("other_boss", 3, 1), "first defeat is per encounter");
            Assert.AreEqual(0, d.AwardBossVictory("x", -4, -1), "negative data never removes Essence");
            Assert.AreEqual(8, d.essence);
        }

        [Test]
        public void Essence_PersistsAcrossRestart()
        {
            var s = new SaveService(_path);
            s.Load();
            s.Data.AwardBossVictory("vh_07", 3, 1);
            Assert.IsTrue(s.Save());
            var s2 = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.Loaded, s2.Load());
            Assert.AreEqual(3, s2.Data.essence);
            Assert.AreEqual(1, s2.Data.GetOrCreate("vh_07").bossDefeats);
        }
    }
}
