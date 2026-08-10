using Action = System.Action;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NoModBar
{
    internal class ModBarCanvas
    {
        private const float HeaderHeight = 34f;
        private const float TooltipGap = 6f;

        private GameObject _root;
        private RectTransform _stripRt;
        private HorizontalLayoutGroup _stripLayout;
        private GameObject _tooltip;
        private TextMeshProUGUI _tooltipLabel;
        private TextMeshProUGUI _collapseLabel;

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Image> _buttonBgs = new List<Image>();
        private readonly List<TextMeshProUGUI> _buttonLabels = new List<TextMeshProUGUI>();
        private readonly List<ModBarEntry> _entries = new List<ModBarEntry>();

        private bool _collapsed;
        private bool _visible = true;

        public bool Visible => _visible;

        public void Create()
        {
            _root = new GameObject("NoModBarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            Object.DontDestroyOnLoad(_root);

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("NoModBarEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Object.DontDestroyOnLoad(esGo);
            }

            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            BuildStrip(rootRt);
            BuildTooltip(rootRt);

            SetVisible(false);
        }

        private void BuildStrip(RectTransform canvasRt)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(RawImage),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(canvasRt, false);
            _stripRt = go.GetComponent<RectTransform>();
            _stripRt.anchorMin = new Vector2(0, 1);
            _stripRt.anchorMax = new Vector2(0, 1);
            _stripRt.pivot = new Vector2(0, 1);
            _stripRt.sizeDelta = new Vector2(120, HeaderHeight);

            var bg = go.GetComponent<RawImage>();
            bg.texture = TextureFactory.CreatePanelBackground(64, 64, UiColors.BgPanel, UiColors.BorderPanel, 2f);
            bg.color = Color.white;
            bg.raycastTarget = false;

            _stripLayout = go.GetComponent<HorizontalLayoutGroup>();
            _stripLayout.padding = new RectOffset(4, 4, 4, 4);
            _stripLayout.spacing = 4f;
            _stripLayout.childAlignment = TextAnchor.MiddleLeft;
            _stripLayout.childControlWidth = false;
            _stripLayout.childControlHeight = true;
            _stripLayout.childForceExpandWidth = false;
            _stripLayout.childForceExpandHeight = true;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            BuildCollapseButton();
            Rebuild(ModBarApi.Snapshot());
        }

        private void BuildCollapseButton()
        {
            float size = Plugin.ButtonSize.Value;
            var btn = MakeButton(_stripLayout.transform, "Collapse", _collapsed ? ">>" : "<<", size, ToggleCollapsed);
            _collapseLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            _collapseLabel.color = UiColors.TextSecondary;
        }

        private void BuildTooltip(RectTransform canvasRt)
        {
            var go = new GameObject("Tooltip", typeof(RectTransform), typeof(RawImage),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(canvasRt, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);

            var img = go.GetComponent<RawImage>();
            img.texture = TextureFactory.CreatePanelBackground(64, 24, UiColors.BgPanelRaised, UiColors.BorderPanel, 1f);
            img.color = Color.white;
            img.raycastTarget = false;

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 3, 3);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.GetComponent<LayoutElement>().preferredHeight = 18f;
            _tooltipLabel = labelGo.GetComponent<TextMeshProUGUI>();
            _tooltipLabel.font = FontLoader.GetDefaultFont();
            _tooltipLabel.fontSize = 11;
            _tooltipLabel.color = UiColors.TextPrimary;
            _tooltipLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _tooltipLabel.enableWordWrapping = false;
            _tooltipLabel.raycastTarget = false;

            _tooltip = go;
            _tooltip.SetActive(false);
        }

        private Button MakeButton(Transform parent, string name, string text, float size, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
            le.minHeight = size;
            var img = go.GetComponent<Image>();
            img.color = UiColors.BgPanelRaised;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            cb.pressedColor = new Color(2f, 2f, 2f, 1f);
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            var tmpGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tmpGo.transform.SetParent(go.transform, false);
            var tmpRt = tmpGo.GetComponent<RectTransform>();
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.offsetMin = Vector2.zero;
            tmpRt.offsetMax = Vector2.zero;
            var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = UiColors.TextPrimary;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return btn;
        }

        private void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].gameObject.SetActive(!collapsed);
            if (_collapseLabel != null)
                _collapseLabel.text = collapsed ? ">>" : "<<";
            if (collapsed) HideTooltip();
        }

        public void Rebuild(List<ModBarEntry> entries)
        {
            for (int i = 0; i < _buttons.Count; i++)
                Object.Destroy(_buttons[i].gameObject);
            _buttons.Clear();
            _buttonBgs.Clear();
            _buttonLabels.Clear();
            _entries.Clear();
            _entries.AddRange(entries);
            if (_collapsed) return;

            float size = Plugin.ButtonSize.Value;
            for (int i = 0; i < _entries.Count; i++)
            {
                int idx = i;
                ModBarEntry entry = _entries[i];
                var btn = MakeButton(_stripLayout.transform, "Btn_" + entry.Name, entry.Name, size,
                    () => ToggleEntry(idx));
                AddTooltip(btn, entry.Tooltip);
                _buttons.Add(btn);
                _buttonBgs.Add(btn.GetComponent<Image>());
                _buttonLabels.Add(btn.GetComponentInChildren<TextMeshProUGUI>());
            }
            RefreshActiveStates();
        }

        private void ToggleEntry(int idx)
        {
            if (idx < 0 || idx >= _entries.Count) return;
            try
            {
                Action toggle = _entries[idx].Toggle;
                if (toggle != null) toggle();
            }
            catch
            {
            }
        }

        public void RefreshActiveStates()
        {
            for (int i = 0; i < _buttons.Count && i < _entries.Count; i++)
            {
                ModBarEntry entry = _entries[i];
                bool active = false;
                if (entry.IsVisible != null)
                {
                    try { active = entry.IsVisible(); } catch { }
                }
                _buttonBgs[i].color = active ? UiColors.HudGreenDim : UiColors.BgPanelRaised;
                _buttonLabels[i].color = active ? UiColors.HudGreen : UiColors.TextPrimary;
            }
        }

        private void AddTooltip(Button btn, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var trigger = btn.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(new UnityAction<BaseEventData>(delegate { ShowTooltip(text); }));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(new UnityAction<BaseEventData>(delegate { HideTooltip(); }));
            trigger.triggers.Add(exit);
        }

        private void ShowTooltip(string text)
        {
            if (_tooltip == null || string.IsNullOrEmpty(text)) return;
            _tooltipLabel.text = text;
            _tooltip.SetActive(true);
        }

        private void HideTooltip()
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        public void ApplyOffsets()
        {
            if (_stripRt == null) return;
            _stripRt.anchoredPosition = new Vector2(Plugin.OffsetX.Value, -Plugin.OffsetY.Value);
            if (_tooltip != null)
            {
                var ttRt = _tooltip.GetComponent<RectTransform>();
                ttRt.anchoredPosition = new Vector2(Plugin.OffsetX.Value, -Plugin.OffsetY.Value - HeaderHeight - TooltipGap);
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.SetActive(visible);
            if (!visible) HideTooltip();
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
        }
    }
}
