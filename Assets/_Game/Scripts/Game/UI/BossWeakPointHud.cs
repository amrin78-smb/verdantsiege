using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Screen-space health bars over the boss's weak-point pods while a special attack winds up, each with a
    /// countdown ring segment so the deadline reads at the pod itself. Display only; under the HUD widgets.
    /// </summary>
    public sealed class BossWeakPointHud : MonoBehaviour
    {
        private sealed class Bar
        {
            public RectTransform root;
            public Image fill, ring;
            public CanvasGroup group;
        }

        private BattleSimulation _sim;
        private BossView _view;
        private Camera _cam;
        private RectTransform _parent;
        private readonly List<Bar> _bars = new List<Bar>();

        public static BossWeakPointHud Create(BattleHud hud, BattleSimulation sim, BossView view, Camera cam)
        {
            if (sim.Boss == null || view == null || view.WeakPoints == null) return null;
            var go = new GameObject("BossWeakPointHud", typeof(RectTransform));
            go.transform.SetParent(hud.Canvas.transform, false);
            go.transform.SetSiblingIndex(1); // above the tap surface, below the HUD panels
            UIKit.Stretch((RectTransform)go.transform);
            var h = go.AddComponent<BossWeakPointHud>();
            h._sim = sim;
            h._view = view;
            h._cam = cam;
            h._parent = (RectTransform)go.transform;
            for (int i = 0; i < view.WeakPoints.Count; i++) h._bars.Add(h.BuildBar(i));
            return h;
        }

        private Bar BuildBar(int i)
        {
            var b = new Bar();
            Sprite round = UIKit.Lib != null ? UIKit.Lib.uiRound : null;
            Sprite circle = UIKit.Lib != null ? UIKit.Lib.uiCircle : null;
            b.root = UIKit.Rect("Pod" + i, _parent);
            b.root.anchorMin = b.root.anchorMax = Vector2.zero;
            b.root.pivot = new Vector2(0.5f, 0f);
            b.root.sizeDelta = new Vector2(120f, 64f);
            b.group = UIKit.Group(b.root.gameObject);
            b.group.blocksRaycasts = false;
            b.group.interactable = false;
            // Countdown ring (drains as the wind-up runs out).
            Image ringBack = UIKit.Image("RingBack", b.root, circle, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(ringBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(40f, 40f));
            b.ring = UIKit.Image("Ring", ringBack.transform, circle, new Color(1f, 0.35f, 0.3f));
            UIKit.Stretch(b.ring.rectTransform, 4f);
            b.ring.type = Image.Type.Filled;
            b.ring.fillMethod = Image.FillMethod.Radial360;
            b.ring.fillOrigin = (int)Image.Origin360.Top;
            b.ring.fillClockwise = false;
            // Health bar.
            Image track = UIKit.Image("Track", b.root, round, new Color(0.05f, 0.02f, 0.05f, 0.85f));
            UIKit.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(112f, 20f));
            b.fill = UIKit.Image("Fill", track.transform, round, new Color(1f, 0.82f, 0.32f));
            UIKit.Stretch(b.fill.rectTransform, 3f);
            b.fill.type = Image.Type.Filled;
            b.fill.fillMethod = Image.FillMethod.Horizontal;
            b.group.alpha = 0f;
            return b;
        }

        private void LateUpdate()
        {
            if (_sim == null || _view == null || _cam == null) return;
            BossRuntime boss = _sim.Boss;
            float scale = _parent.rect.height > 0f ? _parent.rect.height / Mathf.Max(1, Screen.height) : 1f;
            float left = boss.stateDuration > 0f ? Mathf.Clamp01(boss.stateTimer / boss.stateDuration) : 0f;
            for (int i = 0; i < _bars.Count; i++)
            {
                Bar b = _bars[i];
                BossWeakPoint w = i < boss.weakPoints.Count ? boss.weakPoints[i] : null;
                bool on = w != null && w.alive && boss.state == BossState.Telegraph && _view.WeakPoints.PodVisible(i);
                b.group.alpha = Mathf.MoveTowards(b.group.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 6f);
                if (!on) continue;
                Vector3 sp = _cam.WorldToScreenPoint(_view.WeakPoints.PodTop(i));
                b.root.anchoredPosition = new Vector2(sp.x, sp.y) * scale;
                b.fill.fillAmount = w.HealthFraction;
                b.ring.fillAmount = left;
                b.ring.color = Color.Lerp(new Color(1f, 0.2f, 0.25f), new Color(1f, 0.8f, 0.3f), left);
            }
        }
    }
}
