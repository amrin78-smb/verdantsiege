using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;
using VerdantSiege.Combat;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Floating damage numbers (Settings ▸ damage numbers). Hits on the same enemy within a short window merge
    /// into one rising number, so fast fire never floods the screen. Coloured by element; armour hits are pale.
    /// Pooled TMP labels, screen space, no allocations per hit after warm-up.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private sealed class Label
        {
            public TextMeshProUGUI text;
            public int enemyId;
            public float amount;
            public float age;
            public Vector3 world;
            public bool active;
        }

        private const float Life = 0.75f;
        private const float MergeWindow = 0.25f;
        private BattleSimulation _sim;
        private BattleLayout _layout;
        private Camera _cam;
        private RectTransform _root;
        private readonly List<Label> _labels = new List<Label>();

        public void Init(BattleSimulation sim, BattleLayout layout, Camera cam, Transform canvas)
        {
            _sim = sim;
            _layout = layout;
            _cam = cam;
            _root = UIKit.Rect("DamageNumbers", canvas);
            UIKit.Stretch(_root);
            _root.SetSiblingIndex(1);
            sim.EnemyDamaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_sim != null) _sim.EnemyDamaged -= OnDamaged;
        }

        private static Color ColorFor(DamageType t, bool armor)
        {
            if (armor) return new Color(0.92f, 0.88f, 0.76f);
            switch (t)
            {
                case DamageType.Fire: return new Color(1f, 0.55f, 0.2f);
                case DamageType.Frost: return new Color(0.55f, 0.88f, 1f);
                case DamageType.Electric: return new Color(0.82f, 0.6f, 1f);
                case DamageType.Toxic: return new Color(0.6f, 1f, 0.35f);
                case DamageType.Piercing: return new Color(1f, 1f, 1f);
                default: return new Color(1f, 0.92f, 0.55f);
            }
        }

        private void OnDamaged(EnemyRuntime e, DamageResult r, DamageType type)
        {
            float total = r.Total;
            if (total < 1f) return;
            Vector3 world = _layout.ToWorld(e.x + 0.2f, e.VisualLane) + Vector3.up * (e.IsFlying ? 1.6f : 1.25f);
            // Merge into a recent label for the same enemy.
            foreach (Label l in _labels)
            {
                if (!l.active || l.enemyId != e.Id || l.age > MergeWindow) continue;
                l.amount += total;
                l.age = 0f;
                l.world = world;
                l.text.text = Mathf.RoundToInt(l.amount).ToString();
                l.text.color = ColorFor(type, r.toArmor > 0f && r.toHealth <= 0f);
                return;
            }
            Label label = null;
            foreach (Label l in _labels) if (!l.active) { label = l; break; }
            if (label == null)
            {
                if (_labels.Count >= 40) return; // budget
                TextMeshProUGUI t = UIKit.Text("Dmg", _root, "", 34f, Color.white, TextAlignmentOptions.Center, true);
                t.rectTransform.sizeDelta = new Vector2(160f, 50f);
                t.outlineWidth = 0.22f;
                t.outlineColor = new Color32(20, 30, 20, 220);
                label = new Label { text = t };
                _labels.Add(label);
            }
            label.active = true;
            label.enemyId = e.Id;
            label.amount = total;
            label.age = 0f;
            label.world = world;
            label.text.text = Mathf.RoundToInt(total).ToString();
            label.text.color = ColorFor(type, r.toArmor > 0f && r.toHealth <= 0f);
            label.text.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (Label l in _labels)
            {
                if (!l.active) continue;
                l.age += dt;
                if (l.age >= Life)
                {
                    l.active = false;
                    l.text.gameObject.SetActive(false);
                    continue;
                }
                float k = l.age / Life;
                Vector3 sp = _cam.WorldToScreenPoint(l.world);
                l.text.rectTransform.position = new Vector3(sp.x, sp.y + k * 60f, 0f);
                float pop = k < 0.15f ? 1f + (0.15f - k) * 3f : 1f;
                l.text.rectTransform.localScale = new Vector3(pop, pop, 1f);
                Color c = l.text.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                l.text.color = c;
            }
        }
    }
}
