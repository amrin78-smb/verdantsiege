using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Data;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Milestone 3 HUD additions, kept out of BattleHud: the boss health bar (phase pips, Core-exposed state),
    /// the persistent "Herald's Command" banner, elite and boss arrival banners, phase-change banners, and a
    /// one-time "new foe" callout with the counter hint the first time each special enemy appears.
    /// Display only: reads simulation state and events, raises nothing.
    /// </summary>
    public sealed class EncounterAlertsHud : MonoBehaviour
    {
        /// <summary>Extra top space (reference pixels) the boss bar takes below the top bar; the camera fit excludes it.</summary>
        public const float BarHeight = 74f;

        private BattleSimulation _sim;
        private BattleHud _hud;
        private RectTransform _bossBar;
        private Image _bossFill, _bossTrail;
        private Image _bossTimer; // boss polish: wind-up countdown strip
        private bool _podsHinted;
        private TextMeshProUGUI _bossName, _bossState;
        private readonly List<Image> _phasePips = new List<Image>();
        private RectTransform _heraldPill;
        private CanvasGroup _heraldGroup;
        private TextMeshProUGUI _heraldText;
        private RectTransform _intel;
        private CanvasGroup _intelGroup;
        private TextMeshProUGUI _intelTitle, _intelBody;
        private float _intelTimer;
        private float _trail = 1f;
        private float _bossShow;
        private bool _eruptionHinted, _uprootHinted, _coreHinted;
        private readonly HashSet<EnemyDefinition> _seen = new HashSet<EnemyDefinition>();

        public void Build(BattleSimulation sim, BattleHud hud)
        {
            _sim = sim;
            _hud = hud;
            RectTransform root = hud.Root;
            if (sim.Boss != null) BuildBossBar(root, sim.Boss);
            BuildHeraldPill(root);
            BuildIntel(root);
            sim.EnemySpawned += OnEnemySpawned;
            sim.HeraldCommandChanged += OnHerald;
            sim.BossArrived += OnBossArrived;
            sim.BossPhaseChanged += OnBossPhase;
            sim.BossDefeated += OnBossDefeated;
            sim.BossTelegraphed += OnBossTelegraph;
            sim.BossCoreExposedChanged += OnCoreExposed;
            sim.DefenderUprooted += OnUprooted;
            sim.BossStaggered += OnBossStaggered;
            sim.BossWeakPointsExposed += OnWeakPoints;
        }

        private void OnDestroy()
        {
            if (_sim == null) return;
            _sim.EnemySpawned -= OnEnemySpawned;
            _sim.HeraldCommandChanged -= OnHerald;
            _sim.BossArrived -= OnBossArrived;
            _sim.BossPhaseChanged -= OnBossPhase;
            _sim.BossDefeated -= OnBossDefeated;
            _sim.BossTelegraphed -= OnBossTelegraph;
            _sim.BossCoreExposedChanged -= OnCoreExposed;
            _sim.DefenderUprooted -= OnUprooted;
            _sim.BossStaggered -= OnBossStaggered;
            _sim.BossWeakPointsExposed -= OnWeakPoints;
        }

        // ---------------------------------------------------------------- building
        private void BuildBossBar(RectTransform root, BossRuntime boss)
        {
            Image panel = UIKit.Panel("BossBar", root, new Color(0.12f, 0.06f, 0.1f, 0.92f), true, true);
            panel.raycastTarget = false;
            _bossBar = panel.rectTransform;
            UIKit.Place(_bossBar, new Vector2(0.5f, 1f), new Vector2(BattleHud.RailWidth * 0.5f, -112f), new Vector2(820f, BarHeight));
            _hud.SetFeedTop(-112f - BarHeight - 10f); // compact feed sits under the boss bar
            _bossName = UIKit.Text("Name", panel.transform, boss.Definition.displayName, 32f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(_bossName.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -4f), new Vector2(460f, 38f));
            _bossState = UIKit.Text("State", panel.transform, "", 28f, UIPalette.Gold, TextAlignmentOptions.TopRight, true);
            UIKit.Place(_bossState.rectTransform, new Vector2(1f, 1f), new Vector2(-22f, -7f), new Vector2(330f, 34f));
            Image track = UIKit.Image("Track", panel.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.55f));
            RectTransform tr = track.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.offsetMin = new Vector2(22f, 10f);
            tr.offsetMax = new Vector2(-22f, 32f);
            _bossTrail = FillImage("Trail", track.transform, new Color(1f, 0.85f, 0.85f, 0.6f));
            _bossFill = FillImage("Fill", track.transform, new Color(0.75f, 0.3f, 0.65f));
            // ---- boss polish: thin countdown strip along the bottom edge while an attack winds up.
            _bossTimer = UIKit.Image("WindupTimer", panel.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, UIPalette.Danger);
            RectTransform wt = _bossTimer.rectTransform;
            wt.anchorMin = new Vector2(0f, 0f);
            wt.anchorMax = new Vector2(1f, 0f);
            wt.pivot = new Vector2(0.5f, 0f);
            wt.offsetMin = new Vector2(22f, 2f);
            wt.offsetMax = new Vector2(-22f, 8f);
            _bossTimer.type = Image.Type.Filled;
            _bossTimer.fillMethod = Image.FillMethod.Horizontal;
            _bossTimer.enabled = false;
            // Phase pips at each threshold (a pip turns dark once that phase has begun).
            for (int i = 1; i < boss.PhaseCount; i++)
            {
                float at = boss.Definition.phases[i].startsAtHealthFraction;
                Image pip = UIKit.Image("Pip" + i, track.transform, UIKit.Lib != null ? UIKit.Lib.uiCircle : null, UIPalette.Gold);
                pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(at, 0.5f);
                pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                pip.rectTransform.sizeDelta = new Vector2(28f, 28f);
                pip.rectTransform.anchoredPosition = Vector2.zero;
                _phasePips.Add(pip);
            }
            _bossBar.gameObject.SetActive(false);
        }

        private static Image FillImage(string name, Transform parent, Color c)
        {
            Image img = UIKit.Image(name, parent, UIKit.Lib != null ? UIKit.Lib.uiRound : null, c);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            UIKit.Stretch(img.rectTransform, 3f);
            return img;
        }

        private void BuildHeraldPill(RectTransform root)
        {
            Image pill = UIKit.Panel("HeraldCommand", root, new Color(0.35f, 0.08f, 0.25f, 0.92f), true, true);
            pill.raycastTarget = false;
            _heraldPill = pill.rectTransform;
            // Persistent state pill lives in the top-right scenery corner, clear of the feed and the lanes.
            UIKit.Place(_heraldPill, new Vector2(1f, 1f), new Vector2(-16f, -140f), new Vector2(560f, 58f));
            Image flag = UIKit.Image("Flag", pill.transform, UIKit.Lib != null ? UIKit.Lib.iconFlag : null, UIPalette.Danger);
            UIKit.Place(flag.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(44f, 44f));
            _heraldText = UIKit.Text("Text", pill.transform, "", 28f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Stretch(_heraldText.rectTransform, 6f);
            _heraldText.rectTransform.offsetMin = new Vector2(64f, 6f);
            _heraldGroup = UIKit.Group(pill.gameObject);
            _heraldGroup.alpha = 0f;
            _heraldGroup.blocksRaycasts = false;
            _heraldPill.gameObject.SetActive(false);
        }

        /// <summary>Two-line "new foe" card under the top bars: name + counter hint (the one-line toast is too short).</summary>
        private void BuildIntel(RectTransform root)
        {
            Image p = UIKit.Panel("FoeIntel", root, new Color(0.1f, 0.06f, 0.12f, 0.92f), true, true);
            p.raycastTarget = false;
            _intel = p.rectTransform;
            // Sits in the scenery band under the top bars (boss bar / herald slot) so it never covers a lane.
            float intelY = (_sim.Boss != null ? -112f - BarHeight - 10f : -112f) - 72f;
            UIKit.Place(_intel, new Vector2(0.5f, 1f), new Vector2(BattleHud.RailWidth * 0.5f, intelY), new Vector2(1100f, 124f));
            _intelTitle = UIKit.Text("Title", p.transform, "", 34f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(_intelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -8f), new Vector2(1010f, 42f));
            _intelBody = UIKit.Text("Body", p.transform, "", 27f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(_intelBody.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -50f), new Vector2(1050f, 70f));
            _intelBody.textWrappingMode = TextWrappingModes.Normal;
            _intelBody.enableAutoSizing = true;
            _intelBody.fontSizeMin = MenuKit.TextMin;
            _intelBody.fontSizeMax = 30f;
            _intelGroup = UIKit.Group(p.gameObject);
            _intelGroup.alpha = 0f;
            _intelGroup.blocksRaycasts = false;
        }

        // Compact HUD: intel goes through the shared notification strip (the old card stays hidden).
        private void ShowIntel(string title, string body, float seconds) => _hud.ShowIntel(title, body, seconds);

        // ---------------------------------------------------------------- events
        private void OnEnemySpawned(EnemyRuntime e)
        {
            EnemyDefinition d = e.Definition;
            if (!_seen.Add(d)) return;
            if (d.elite)
            {
                ShowIntel("Elite: " + d.displayName, d.counterHint, 5f);
                return;
            }
            bool special = d.ability != EnemyAbilityKind.None || d.movement != EnemyMovementKind.Ground;
            if (special && e.waveIndex >= 0) ShowIntel("New foe: " + d.displayName, d.counterHint, 5f);
        }

        private void OnHerald(bool active)
        {
            if (active) _heraldText.text = $"Herald's Command: all Blight {Mathf.RoundToInt(_sim.HeraldBonus * 100f)}% faster";
            if (active) _heraldPill.gameObject.SetActive(true);
        }

        private void OnBossArrived(BossRuntime b)
        {
            _bossShow = 0f;
            if (_bossBar != null) _bossBar.gameObject.SetActive(true);
            // Compact intro (boss polish): the entrance animation carries the moment.
            _hud.ShowBanner(b.Definition.displayName, "The ground splits open", UIPalette.Blight, 2.4f);
            ShowIntel(b.Definition.displayName, b.Definition.counterHint, 5f);
        }

        private void OnBossPhase(BossRuntime b, int phase)
        {
            BossPhase p = b.CurrentPhase;
            _hud.ShowBanner(p != null ? p.name : "The Maw Rages", $"Phase {phase + 1} of {b.PhaseCount}", UIPalette.Danger, 3f);
        }

        private void OnBossDefeated(BossRuntime b) => _hud.ShowBanner(b.Definition.displayName + " Falls!", "Its Blight withers away", UIPalette.Gold, 3f);

        // ---- boss polish: weak points and stagger (two notifications at most per special)
        private void OnWeakPoints(BossRuntime b)
        {
            if (_podsHinted) return;
            _podsHinted = true;
            _hud.ShowToast($"Break all {b.weakPoints.Count} glowing pods to stop it!", 3f);
        }

        private void OnBossStaggered(BossRuntime b) => _hud.ShowBanner("STAGGERED!", "Its Core is open: strike now!", UIPalette.Gold, 1.6f);

        private void OnBossTelegraph(BossRuntime b, BossAttack a)
        {
            if (a.kind != BossAttackKind.RootEruption || _eruptionHinted) return;
            _eruptionHinted = true;
            _hud.ShowAlert("Roots will erupt on the glowing cells!", UIPalette.Danger, 3f);
        }

        private void OnCoreExposed(BossRuntime b, bool exposed)
        {
            if (!exposed || _coreHinted) return;
            _coreHinted = true;
            _hud.ShowAlert("The Core is exposed: strike now!", UIPalette.Gold, 3f);
        }

        private void OnUprooted(DefenderRuntime d, int refund)
        {
            if (_uprootHinted) return;
            _uprootHinted = true;
            _hud.ShowToast($"Uprooted! {refund} Vitality returned", 2.5f);
        }

        // ---------------------------------------------------------------- frame
        private void Update()
        {
            if (_sim == null) return;
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            if (_intelGroup != null)
            {
                _intelTimer -= dt;
                _intelGroup.alpha = Mathf.Clamp01(_intelTimer / 0.4f);
            }
            if (_heraldPill != null && _heraldPill.gameObject.activeSelf)
            {
                bool on = _sim.HeraldBonus > 0f && _sim.State == BattleState.Running;
                _heraldGroup.alpha = Mathf.MoveTowards(_heraldGroup.alpha, on ? 0.75f + 0.25f * Mathf.Sin(time * 4f) : 0f, dt * 3f);
                if (!on && _heraldGroup.alpha <= 0f) _heraldPill.gameObject.SetActive(false);
            }
            BossRuntime b = _sim.Boss;
            if (b == null || _bossBar == null || !_bossBar.gameObject.activeSelf) return;
            _bossShow = Mathf.MoveTowards(_bossShow, 1f, dt * 2f);
            _bossBar.localScale = new Vector3(1f, _bossShow, 1f);
            float f = b.HealthFraction;
            _bossFill.fillAmount = f;
            _trail = Mathf.MoveTowards(_trail, f, dt * (_trail > f + 0.05f ? 0.6f : 0.25f));
            if (_trail < f) _trail = f;
            _bossTrail.fillAmount = _trail;
            bool exposed = b.CoreExposed;
            _bossFill.color = exposed ? Color.Lerp(new Color(1f, 0.45f, 0.85f), Color.white, 0.25f + 0.25f * Mathf.Sin(time * 10f)) : new Color(0.75f, 0.3f, 0.65f);
            for (int i = 0; i < _phasePips.Count; i++) _phasePips[i].color = b.phase >= i + 1 ? new Color(0.25f, 0.15f, 0.2f, 0.9f) : UIPalette.Gold;
            // ---- boss polish: countdown strip + special/stagger states
            bool winding = b.state == BossState.Telegraph || b.state == BossState.Staggered;
            _bossTimer.enabled = winding && b.stateDuration > 0f;
            if (_bossTimer.enabled)
            {
                _bossTimer.fillAmount = Mathf.Clamp01(b.stateTimer / b.stateDuration);
                _bossTimer.color = b.state == BossState.Staggered ? new Color(1f, 0.6f, 0.95f) : b.WeakPointsExposed ? UIPalette.Gold : UIPalette.Danger;
            }
            if (b.state == BossState.Staggered)
            {
                _bossState.text = $"STAGGERED {Mathf.CeilToInt(b.stateTimer)}";
                _bossState.color = new Color(1f, 0.7f, 0.95f);
                return;
            }
            if (b.state == BossState.Telegraph && b.WeakPointsExposed)
            {
                _bossState.text = $"BREAK PODS {b.weakPoints.Count - b.WeakPointsAlive}/{b.weakPoints.Count}  {Mathf.CeilToInt(b.stateTimer)}";
                _bossState.color = Color.Lerp(UIPalette.Danger, UIPalette.Gold, 0.5f + 0.5f * Mathf.Sin(time * 8f));
                return;
            }
            switch (b.state)
            {
                case BossState.Rising: _bossState.text = "RISING"; _bossState.color = UIPalette.TextDim; break;
                case BossState.Transition: _bossState.text = "ENRAGED"; _bossState.color = UIPalette.Danger; break;
                case BossState.Defeated: _bossState.text = "DEFEATED"; _bossState.color = UIPalette.Gold; break;
                default:
                    if (exposed) { _bossState.text = $"CORE EXPOSED {Mathf.CeilToInt(b.exposedRemaining)}"; _bossState.color = new Color(1f, 0.6f, 0.9f); }
                    else if (b.state == BossState.Telegraph)
                    {
                        string label = b.currentAttack != null && !string.IsNullOrEmpty(b.currentAttack.label) ? b.currentAttack.label.ToUpperInvariant() : "WINDING UP";
                        _bossState.text = label;
                        _bossState.color = UIPalette.Danger;
                    }
                    else { _bossState.text = $"Phase {b.phase + 1}/{b.PhaseCount}"; _bossState.color = UIPalette.Gold; }
                    break;
            }
        }
    }
}
