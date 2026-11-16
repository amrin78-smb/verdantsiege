using NUnit.Framework;
using VerdantSiege.Battle.View;
using VerdantSiege.Grid;
using VerdantSiege.Visual;

namespace VerdantSiege.Tests
{
    public sealed class PresentationTests
    {
        [TestCase(0.016f)] [TestCase(0.066f)] [TestCase(0.2f)] [TestCase(0.5f)]
        public void Spring_IsStableAtAnyFrameTime(float dt)
        {
            var s = new Spring { value = -0.6f };
            for (int i = 0; i < 400; i++) s.Tick(dt, 260f, 16f);
            Assert.IsFalse(float.IsNaN(s.value));
            Assert.Less(System.Math.Abs(s.value), 0.01f, "spring must settle");
        }

        [Test]
        public void Layout_WorldCoordRoundTrip()
        {
            var layout = new BattleLayout(5, 9, 1f, 1f, UnityEngine.Vector3.zero);
            for (int l = 0; l < 5; l++)
            for (int c = 0; c < 9; c++)
            {
                var coord = new GridCoord(l, c);
                Assert.AreEqual(coord, layout.WorldToCoord(layout.CellCenter(coord)));
            }
            Assert.IsFalse(layout.InBounds(layout.WorldToCoord(new UnityEngine.Vector3(-0.2f, 0f, 2f))));
        }
    }
}
