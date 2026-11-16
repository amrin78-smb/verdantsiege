using System.IO;
using NUnit.Framework;
using VerdantSiege.Save;

namespace VerdantSiege.Tests
{
    public sealed class SaveTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vs_save_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void NewGame_WhenNoFile()
        {
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.NewGame, s.Load());
            Assert.AreEqual(0, s.Data.seeds);
        }

        [Test]
        public void RoundTrip_PersistsProgressAndSettings()
        {
            var s = new SaveService(_path);
            s.Load();
            s.Data.seeds = 75;
            s.Data.GetOrCreate("vh_01").completed = true;
            s.Data.settings.autoCollect = true;
            Assert.IsTrue(s.Save());

            var s2 = new SaveService(_path); // simulates app restart
            Assert.AreEqual(SaveLoadOutcome.Loaded, s2.Load());
            Assert.AreEqual(75, s2.Data.seeds);
            Assert.IsTrue(s2.Data.IsCompleted("vh_01"));
            Assert.IsTrue(s2.Data.settings.autoCollect);
        }

        [Test]
        public void CorruptPrimary_RecoversFromBackup()
        {
            var s = new SaveService(_path);
            s.Load();
            s.Data.seeds = 10; s.Save();
            s.Data.seeds = 20; s.Save(); // backup now holds 10
            File.WriteAllText(_path, "{ not json !!!");
            var s2 = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.RecoveredFromBackup, s2.Load());
            Assert.AreEqual(10, s2.Data.seeds);
        }

        [Test]
        public void CorruptEverything_ResetsAndKeepsCopy()
        {
            File.WriteAllText(_path, "garbage");
            File.WriteAllText(_path + ".bak", "");
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.CorruptReset, s.Load());
            Assert.IsTrue(File.Exists(_path + ".corrupt"));
            Assert.AreEqual(0, s.Data.seeds);
        }

        [Test]
        public void Migration_Version0WithoutField_Loads()
        {
            File.WriteAllText(_path, "{\"seeds\":33,\"encounters\":[{\"encounterId\":\"vh_01\",\"completed\":true}]}");
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.Loaded, s.Load());
            Assert.AreEqual(33, s.Data.seeds);
            Assert.AreEqual(SaveData.CurrentVersion, s.Data.version);
            Assert.IsNotNull(s.Data.settings);
        }

        [Test]
        public void NewerVersion_IsNotOverwritten()
        {
            string json = "{\"version\":999,\"seeds\":5}";
            File.WriteAllText(_path, json);
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.NewerVersion, s.Load());
            Assert.IsFalse(s.Save());
            Assert.AreEqual(json, File.ReadAllText(_path));
        }

        [Test]
        public void Reset_ClearsProgress_KeepsSettings()
        {
            var s = new SaveService(_path);
            s.Load();
            s.Data.seeds = 99; s.Data.settings.sfxVolume = 0.1f;
            s.ResetProgress();
            Assert.AreEqual(0, s.Data.seeds);
            Assert.AreEqual(0.1f, s.Data.settings.sfxVolume, 1e-5f);
        }
    }
}
