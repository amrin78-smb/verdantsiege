using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Data;

namespace VerdantSiege.UI
{
    /// <summary>
    /// One defender card: portrait, name, cost chip, radial regrowth (cooldown) overlay with seconds, and
    /// selected / unaffordable / ready states. Tap to select, or drag straight onto the battlefield.
    /// </summary>
    public sealed class DefenderCardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int SlotIndex { get; private set; }
        public event Action<int> Clicked;
        public event Action<int, Vector2> DragBegan;
        public event Action<int, Vector2> Dragged;
        public event Action<int, Vector2> DragEnded;

        /// <summary>Card name and cost sizes (TD-36: battle text never below 28; costs read at a glance).</summary>
        public const float NameSize = 32f, CostSize = 36f;

        private DeckSlot _slot;
        private Image _bg, _edge, _portrait, _cooldown, _costChip;
        private TextMeshProUGUI _cost, _name, _cdText;
        private RectTransform _content;
        private bool _selected;
        private float _readyFlash;
        private bool _wasReady = true;
        private float _shake;
        private float _lift;
        private Color _monogramTint;

        private static string Monogram(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string[] words = name.Split(' ');
            return words.Length > 1 ? $"{words[0][0]}{words[1][0]}" : name.Substring(0, 1);
        }

        public static DefenderCardView Create(Transform parent, int slotIndex, DeckSlot slot, Vector2 size)
        {
            RectTransform root = UIKit.Rect("Card_" + slot.Definition.id, parent);
            root.sizeDelta = size;
            var v = root.gameObject.AddComponent<DefenderCardView>();
            v.Build(slotIndex, slot, size);
            return v;
        }

        private void Build(int slotIndex, DeckSlot slot, Vector2 size)
        {
            SlotIndex = slotIndex;
            _slot = slot;
            DefenderDefinition d = slot.Definition;
            _content = UIKit.Rect("Content", transform);
            UIKit.Stretch(_content);
            _bg = UIKit.Panel("Bg", _content, UIPalette.PanelLight, false, true);
            UIKit.Stretch(_bg.rectTransform);
            // Accent wash in the defender colour.
            Image wash = UIKit.Image("Wash", _bg.transform, UIKit.Lib != null ? UIKit.Lib.uiGradient : null,
                new Color(d.accentColor.r, d.accentColor.g, d.accentColor.b, 0.55f));
            UIKit.Stretch(wash.rectTransform, 4f);
            // Two shapes (TD-36): a wide card (single-column rail: portrait left, name top-right, cost bottom-right) and a
            // tall card (two-column rail for 6-8 cards: portrait top-left, cost column top-right, name across the bottom).
            bool tall = size.x < size.y * 1.2f;
            float pd = tall ? Mathf.Min(size.y - 96f, size.x - 84f) : size.y * 0.98f;
            _portrait = UIKit.Image("Portrait", _bg.transform, d.icon, Color.white);
            _portrait.preserveAspect = true;
            if (tall) UIKit.Place(_portrait.rectTransform, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(pd, pd));
            else UIKit.Place(_portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(2f, 6f), new Vector2(pd, pd));
            if (d.icon == null)
            {
                // No card art yet: an accent disc with the defender's initials stands in (portrait image kept for tinting).
                _portrait.sprite = UIKit.Lib != null ? UIKit.Lib.uiCircle : null;
                _portrait.rectTransform.sizeDelta = new Vector2(pd * 0.72f, pd * 0.72f);
                _portrait.rectTransform.anchoredPosition = tall ? new Vector2(14f, -14f) : new Vector2(12f, 0f);
                TextMeshProUGUI mono = UIKit.Text("Monogram", _portrait.transform, Monogram(d.displayName), pd * 0.3f,
                    new Color(0.1f, 0.12f, 0.1f, 0.9f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(mono.rectTransform);
                _monogramTint = new Color(d.accentColor.r, d.accentColor.g, d.accentColor.b, 1f);
            }
            _name = UIKit.Text("Name", _bg.transform, d.displayName, NameSize, UIPalette.Parchment, tall ? TextAlignmentOptions.Bottom : TextAlignmentOptions.TopRight, true);
            if (tall)
            {
                _name.rectTransform.anchorMin = new Vector2(0f, 0f);
                _name.rectTransform.anchorMax = new Vector2(1f, 0f);
                _name.rectTransform.pivot = new Vector2(0.5f, 0f);
                _name.rectTransform.offsetMin = new Vector2(8f, 8f);
                _name.rectTransform.offsetMax = new Vector2(-8f, 8f + Mathf.Min(78f, size.y - pd - 14f));
            }
            else
            {
                _name.rectTransform.anchorMin = new Vector2(0.45f, 0.5f);
                _name.rectTransform.anchorMax = new Vector2(1f, 1f);
                _name.rectTransform.offsetMin = new Vector2(0f, 0f);
                _name.rectTransform.offsetMax = new Vector2(-12f, -8f);
            }
            _name.textWrappingMode = TextWrappingModes.Normal;
            _name.lineSpacing = -14f;
            MenuKit.Fit(_name, NameSize, MenuKit.TextMin);
            _costChip = UIKit.Image("CostChip", _bg.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.5f));
            Image vIcon = UIKit.Image("VitalityIcon", _costChip.transform, UIKit.Lib != null ? UIKit.Lib.iconVitality : null, Color.white);
            vIcon.preserveAspect = true;
            _cost = UIKit.Text("Cost", _costChip.transform, d.cost.ToString(), CostSize, UIPalette.Vitality, TextAlignmentOptions.MidlineRight, true);
            UIKit.Stretch(_cost.rectTransform);
            if (tall)
            {
                // Cost column: Vitality icon over the number.
                UIKit.Place(_costChip.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -8f), new Vector2(Mathf.Min(84f, size.x - pd - 14f), 100f));
                UIKit.Place(vIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(40f, 40f));
                _cost.alignment = TextAlignmentOptions.Center;
                _cost.rectTransform.offsetMax = new Vector2(0f, -44f);
                _cost.textWrappingMode = TextWrappingModes.NoWrap;
                MenuKit.Fit(_cost, CostSize, 30f);
            }
            else
            {
                float chipH = Mathf.Clamp(size.y * 0.36f, 46f, 58f);
                UIKit.Place(_costChip.rectTransform, new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(chipH * 2.4f, chipH));
                UIKit.Place(vIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(chipH * 0.84f, chipH * 0.84f));
                _cost.fontSize = Mathf.Max(CostSize, chipH * 0.7f);
                _cost.rectTransform.offsetMax = new Vector2(-10f, 0f);
            }
            _cooldown = UIKit.Image("Cooldown", _bg.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0.02f, 0.06f, 0.05f, 0.72f));
            UIKit.Stretch(_cooldown.rectTransform);
            _cooldown.type = Image.Type.Filled;
            _cooldown.fillMethod = Image.FillMethod.Vertical;
            _cooldown.fillOrigin = (int)Image.OriginVertical.Top;
            _cdText = UIKit.Text("CooldownText", _bg.transform, "", 52f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Stretch(_cdText.rectTransform);
            _edge = UIKit.Image("Edge", _bg.transform, UIKit.Lib != null ? UIKit.Lib.uiRoundOutline : null, UIPalette.PanelEdge);
            UIKit.Stretch(_edge.rectTransform, -4f);
            _bg.raycastTarget = true;
        }

        public void SetSelected(bool selected) => _selected = selected;

        public void Deny()
        {
            _shake = 0.35f;
        }

        /// <summary>Refresh from authoritative state (called each frame by the HUD; cheap).</summary>
        public void Refresh(int vitality, float dt)
        {
            if (_slot == null) return;
            bool ready = _slot.Ready;
            bool affordable = vitality >= _slot.Definition.cost;
            float frac = _slot.CooldownFraction;
            _cooldown.fillAmount = ready ? 0f : frac;
            _cdText.text = ready ? "" : Mathf.CeilToInt(_slot.cooldownRemaining).ToString();
            if (ready && !_wasReady) _readyFlash = 0.35f;
            _wasReady = ready;
            _readyFlash = Mathf.Max(0f, _readyFlash - dt);

            bool usable = ready && affordable;
            Color baseTint = _monogramTint.a > 0f ? _monogramTint : Color.white;
            _portrait.color = usable ? baseTint : baseTint * new Color(0.55f, 0.55f, 0.55f, 1f);
            _cost.color = affordable ? UIPalette.Vitality : UIPalette.Danger;
            Color edge = _selected ? UIPalette.Gold : new Color(UIPalette.PanelEdge.r, UIPalette.PanelEdge.g, UIPalette.PanelEdge.b, usable ? 0.55f : 0.25f);
            if (_readyFlash > 0f) edge = Color.Lerp(edge, Color.white, _readyFlash / 0.35f);
            _edge.color = edge;

            _lift = Mathf.MoveTowards(_lift, _selected ? 1f : 0f, dt * 8f);
            _shake = Mathf.Max(0f, _shake - dt);
            float sx = _shake > 0f ? Mathf.Sin(_shake * 70f) * 10f * (_shake / 0.35f) : 0f;
            _content.anchoredPosition = new Vector2(sx + _lift * 18f, 0f);
            float s = 1f + _lift * 0.04f;
            _content.localScale = new Vector3(s, s, 1f);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            Clicked?.Invoke(SlotIndex);
        }

        public void OnBeginDrag(PointerEventData e) => DragBegan?.Invoke(SlotIndex, e.position);
        public void OnDrag(PointerEventData e) => Dragged?.Invoke(SlotIndex, e.position);
        public void OnEndDrag(PointerEventData e) => DragEnded?.Invoke(SlotIndex, e.position);
    }
}
