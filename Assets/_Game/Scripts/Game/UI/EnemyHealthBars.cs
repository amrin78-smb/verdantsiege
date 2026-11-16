using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Compact health bars over damaged enemies (screen-space, pooled), with a pale armour segment for plated
    /// enemies. Only shown once an enemy has taken damage, keeping the field clean.
    /// </summary>
    public sealed class EnemyHealthBars : MonoBehaviour
    {
        private sealed class Bar
        {
            public RectTransform root;
            public Image health;
            public Image armor;
        }

        private BattleSimulation _sim;
        private BattleLayout _layout;
        private Camera _cam;
        private RectTransform _canvasRect;
        private RectTransform _root;
        private readonly List<Bar> _pool = new List<Bar>();

        public void Init(BattleSimulation sim, BattleLayout layout, Camera cam, Transform canvas)
        {
            _sim = sim;
            _layout = layout;
            _cam = cam;
            RectTransform rt = UIKit.Rect("EnemyHealthBars", canvas);
            UIKit.Stretch(rt);
            rt.SetSiblingIndex(1); // above the tap surface, below the HUD widgets
            _canvasRect = (RectTransform)canvas;
            _root = rt;
        }

        private Bar Get(int i)
        {
            while (_pool.Count <= i)
            {
                Image bg = UIKit.Image("Bar", _root, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.6f));
                bg.rectTransform.sizeDelta = new Vector2(84f, 14f);
                Image hp = UIKit.Image("Hp", bg.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, UIPalette.Danger);
                hp.type = Image.Type.Filled;
                hp.fillMethod = Image.FillMethod.Horizontal;
                UIKit.Stretch(hp.rectTransform, 2f);
                Image ar = UIKit.Image("Armor", bg.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0.9f, 0.86f, 0.74f));
                ar.type = Image.Type.Filled;
                ar.fillMethod = Image.FillMethod.Horizontal;
                ar.rectTransform.anchorMin = new Vector2(0f, 1f);
                ar.rectTransform.anchorMax = new Vector2(1f, 1f);
                ar.rectTransform.pivot = new Vector2(0.5f, 0f);
                ar.rectTransform.offsetMin = new Vector2(2f, 1f);
                ar.rectTransform.offsetMax = new Vector2(-2f, 8f);
                _pool.Add(new Bar { root = bg.rectTransform, health = hp, armor = ar });
            }
            return _pool[i];
        }

        private void LateUpdate()
        {
            if (_sim == null || _cam == null) return;
            int used = 0;
            float scale = _canvasRect.localScale.x > 0f ? _canvasRect.localScale.x : 1f;
            float interp = Mathf.Clamp01(_sim.Interpolation);
            foreach (EnemyRuntime e in _sim.Enemies)
            {
                if (!e.alive) continue;
                bool hurt = e.health < e.MaxHealth || (e.Definition.armorHealth > 0f && e.armor < e.Definition.armorHealth);
                if (!hurt) continue;
                float x = Mathf.LerpUnclamped(e.prevX, e.x, interp) + e.Definition.bodyLength * 0.5f;
                float h = e.Definition.armorHealth > 0f ? 1.75f : 1.45f;
                Vector3 sp = _cam.WorldToScreenPoint(_layout.ToWorld(x, e.lane) + Vector3.up * h);
                if (sp.z <= 0f) continue;
                Bar b = Get(used++);
                if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
                b.root.position = new Vector3(sp.x, sp.y, 0f);
                b.root.localScale = Vector3.one;
                b.health.fillAmount = e.health / e.MaxHealth;
                bool armored = e.Definition.armorHealth > 0f;
                b.armor.enabled = armored && e.armor > 0f;
                if (armored) b.armor.fillAmount = e.armor / e.Definition.armorHealth;
            }
            for (int i = used; i < _pool.Count; i++) if (_pool[i].root.gameObject.activeSelf) _pool[i].root.gameObject.SetActive(false);
        }
    }
}
