using Action = System.Action;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NoModBar.Core;

namespace NoModBar
{
    internal class ModBarCanvas
    {
        // Layout rhythm — 8 pt outer padding, 6 pt between tiles.
        private const float PadH = 8f;
        private const float PadV = 8f;
        private const float Spacing = 6f;
        private const float TilePadH = 6f;
        private const float TooltipGap = 6f;

        private const float CollapseAnimTime = 0.12f;
        private const float IdleDelay = 4f;
        private const float IdleFadeTime = 0.5f;
        private const float IdleAlpha = 0.4f;
        private const float EdgeSnapPx = 16f;

        private GameObject _root;
        private RectTransform _canvasRt;
        private RectTransform _stripRt;
        private HorizontalLayoutGroup _stripLayout;
        private CanvasGroup _stripGroup;
        private GameObject _divider;
        private GameObject _content;
        private LayoutElement _contentLe;
        private CanvasGroup _contentGroup;
        private GameObject _tooltip;
        private RectTransform _tooltipRt;
        private TextMeshProUGUI _tooltipLabel;
        private TextMeshProUGUI _collapseLabel;
        private Image _collapseBg;
        private LayoutElement _collapseLe;

        private class BtnState { public bool Hover; public bool Press; }

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Image> _buttonBgs = new List<Image>();
        private readonly List<TextMeshProUGUI> _buttonLabels = new List<TextMeshProUGUI>();
        private readonly List<BtnState> _states = new List<BtnState>();
        private readonly List<ModBarEntry> _entries = new List<ModBarEntry>();

        private bool _collapsed;
        private bool _visible = true;
        private float _collapseT = 1f; // 1 = expanded, 0 = collapsed
        private float _idleTimer;
        private bool _barHovered;
        private bool _collapseHover;
        private bool _collapsePress;
        private bool _dragging;
        private Vector2 _dragGrab; // strip top-left minus pointer, in canvas-local units
        private float _appliedSize = -1f;

        private float ButtonSize => Plugin.ButtonSize.Value;
        private float TileWidth => ButtonSize + 2f * TilePadH;
        private float BarHeight => ButtonSize + 2f * PadV;

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
            scaler.matchWidthOrHeight = 1f; // scale with screen height only — bar size is aspect-independent

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("NoModBarEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Object.DontDestroyOnLoad(esGo);
            }

            _canvasRt = _root.GetComponent<RectTransform>();
            _canvasRt.anchorMin = Vector2.zero;
            _canvasRt.anchorMax = Vector2.one;
            _canvasRt.offsetMin = Vector2.zero;
            _canvasRt.offsetMax = Vector2.zero;

            BuildStrip(_canvasRt);
            BuildTooltip(_canvasRt);

            SetVisible(false);
        }

        private void BuildStrip(RectTransform canvasRt)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(CanvasGroup),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(EventTrigger));
            go.transform.SetParent(canvasRt, false);
            _stripRt = go.GetComponent<RectTransform>();
            _stripRt.anchorMin = new Vector2(0, 1);
            _stripRt.anchorMax = new Vector2(0, 1);
            _stripRt.pivot = new Vector2(0, 1);
            _stripRt.sizeDelta = new Vector2(120, BarHeight);

            var bg = go.GetComponent<Image>();
            bg.sprite = TextureFactory.CreateFramedSprite(UiColors.BgPanel, UiColors.BorderPanel, 1);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = TextureFactory.FramedPpuMultiplier;
            bg.color = Color.white;
            bg.raycastTarget = true; // drag grip (padding areas) + hover zone for idle fade

            _stripGroup = go.GetComponent<CanvasGroup>();

            _stripLayout = go.GetComponent<HorizontalLayoutGroup>();
            _stripLayout.padding = new RectOffset((int)PadH, (int)PadH, (int)PadV, (int)PadV);
            _stripLayout.spacing = Spacing;
            _stripLayout.childAlignment = TextAnchor.MiddleLeft;
            _stripLayout.childControlWidth = true;
            _stripLayout.childControlHeight = true;
            _stripLayout.childForceExpandWidth = false;
            _stripLayout.childForceExpandHeight = true;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Pointer enter/exit bubble up from child buttons, so one trigger on the
            // strip tracks hover for the whole bar (idle fade). Drag events only fire
            // when the press starts on the strip background itself — i.e. the padding
            // grips — which keeps button clicks drag-free.
            var trigger = go.GetComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, d => { _barHovered = true; _idleTimer = 0f; });
            AddTrigger(trigger, EventTriggerType.PointerExit, d => { _barHovered = false; });
            AddTrigger(trigger, EventTriggerType.BeginDrag, d => BeginDrag((PointerEventData)d));
            AddTrigger(trigger, EventTriggerType.Drag, d => OnDrag((PointerEventData)d));
            AddTrigger(trigger, EventTriggerType.EndDrag, d => EndDrag());

            BuildCollapseButton();
            BuildDivider();
            BuildContent();
            Rebuild(ModBarApi.Snapshot());
        }

        private void BuildCollapseButton()
        {
            var btn = MakeButton(_stripLayout.transform, "Collapse", "<<", ButtonSize, ButtonSize,
                ToggleCollapsed);
            _collapseLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            _collapseBg = btn.GetComponent<Image>();
            _collapseLe = btn.GetComponent<LayoutElement>();
            _collapseBg.color = new Color(0f, 0f, 0f, 0f); // flat chrome, distinct from mod tiles

            var trig = btn.GetComponent<EventTrigger>();
            var rt = btn.GetComponent<RectTransform>();
            AddTrigger(trig, EventTriggerType.PointerEnter, d =>
            {
                _collapseHover = true;
                ShowTooltip(_collapsed ? "Expand mod bar" : "Collapse bar — drag the bar's edge to reposition", rt);
                UpdateCollapseVisual();
            });
            AddTrigger(trig, EventTriggerType.PointerExit, d =>
            {
                _collapseHover = false;
                _collapsePress = false;
                HideTooltip();
                UpdateCollapseVisual();
            });
            AddTrigger(trig, EventTriggerType.PointerDown, d => { _collapsePress = true; UpdateCollapseVisual(); });
            AddTrigger(trig, EventTriggerType.PointerUp, d => { _collapsePress = false; UpdateCollapseVisual(); });
            UpdateCollapseVisual();
        }

        private void UpdateCollapseVisual()
        {
            if (_collapseBg == null) return;
            _collapseBg.color = _collapsePress ? UiColors.BgPressed
                : _collapseHover ? UiColors.BgHover
                : new Color(0f, 0f, 0f, 0f);
            _collapseLabel.color = _collapseHover ? UiColors.TextSecondary : UiColors.TextMuted;
        }

        private void BuildDivider()
        {
            _divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _divider.transform.SetParent(_stripLayout.transform, false);
            var le = _divider.GetComponent<LayoutElement>();
            le.preferredWidth = 1f;
            le.minWidth = 1f;
            var img = _divider.GetComponent<Image>();
            img.color = UiColors.BorderSubtle;
            img.raycastTarget = false;
        }

        private void BuildContent()
        {
            _content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement), typeof(CanvasGroup), typeof(RectMask2D));
            _content.transform.SetParent(_stripLayout.transform, false);
            _contentLe = _content.GetComponent<LayoutElement>();
            _contentGroup = _content.GetComponent<CanvasGroup>();

            var hlg = _content.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset();
            hlg.spacing = Spacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
        }

        private void BuildTooltip(RectTransform canvasRt)
        {
            var go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasRt, false);
            _tooltipRt = go.GetComponent<RectTransform>();
            _tooltipRt.anchorMin = new Vector2(0, 1);
            _tooltipRt.anchorMax = new Vector2(0, 1);
            _tooltipRt.pivot = new Vector2(0, 1);

            var img = go.GetComponent<Image>();
            img.sprite = TextureFactory.CreateFramedSprite(UiColors.BgPanelRaised, UiColors.BorderPanel, 1);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = TextureFactory.FramedPpuMultiplier;
            img.color = Color.white;
            img.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 3f);
            labelRt.offsetMax = new Vector2(-8f, -3f);
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

        private Button MakeButton(Transform parent, string name, string text, float width, float height,
            UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement), typeof(EventTrigger));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
            var img = go.GetComponent<Image>();
            img.color = UiColors.BgPanelRaised;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            // Transition.None: hover/pressed/active are driven explicitly from the
            // palette via EventTrigger, so states stay exact and tunable.
            btn.transition = Selectable.Transition.None;
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
            if (_collapsed == collapsed) return;
            _collapsed = collapsed;
            if (_collapseLabel != null)
                _collapseLabel.text = collapsed ? ">>" : "<<";
            if (collapsed) HideTooltip();
            // The actual width/alpha tween runs in Tick via ApplyCollapse.
        }

        /// <summary>Per-frame driver: collapse tween + idle fade. Called by ModBarController.</summary>
        public void Tick(float dt)
        {
            if (!_visible || _stripRt == null) return;

            float target = _collapsed ? 0f : 1f;
            if (!Mathf.Approximately(_collapseT, target))
            {
                _collapseT = Mathf.MoveTowards(_collapseT, target, dt / CollapseAnimTime);
                ApplyCollapse(_collapseT);
            }

            if (_barHovered || _dragging)
                _idleTimer = 0f;
            else
                _idleTimer += dt;
            float alphaTarget = _idleTimer > IdleDelay ? IdleAlpha : 1f;
            _stripGroup.alpha = Mathf.MoveTowards(_stripGroup.alpha, alphaTarget, dt / IdleFadeTime);
        }

        private void ApplyCollapse(float t)
        {
            bool show = t > 0.001f;
            if (_content != null && _content.activeSelf != show) _content.SetActive(show);
            if (_divider != null && _divider.activeSelf != show) _divider.SetActive(show);
            if (_contentLe != null)
            {
                _contentLe.preferredWidth = ContentNaturalWidth() * t;
                _contentLe.minWidth = 0f;
            }
            if (_contentGroup != null) _contentGroup.alpha = t;
        }

        private float ContentNaturalWidth()
        {
            int n = _buttons.Count;
            if (n == 0) return 0f;
            return n * TileWidth + (n - 1) * Spacing;
        }

        public void Rebuild(List<ModBarEntry> entries)
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) Object.Destroy(_buttons[i].gameObject);
            _buttons.Clear();
            _buttonBgs.Clear();
            _buttonLabels.Clear();
            _states.Clear();
            _entries.Clear();
            _entries.AddRange(entries);

            float height = ButtonSize;
            float width = TileWidth;
            for (int i = 0; i < _entries.Count; i++)
            {
                int idx = i;
                ModBarEntry entry = _entries[i];
                var st = new BtnState();
                var btn = MakeButton(_content.transform, "Btn_" + entry.Name, entry.Name, width, height,
                    () => ToggleEntry(idx));
                var btnRt = btn.GetComponent<RectTransform>();
                string tip = entry.Tooltip;

                var trig = btn.GetComponent<EventTrigger>();
                AddTrigger(trig, EventTriggerType.PointerEnter, d =>
                {
                    st.Hover = true;
                    ShowTooltip(tip, btnRt);
                    UpdateButtonVisual(idx);
                });
                AddTrigger(trig, EventTriggerType.PointerExit, d =>
                {
                    st.Hover = false;
                    st.Press = false;
                    HideTooltip();
                    UpdateButtonVisual(idx);
                });
                AddTrigger(trig, EventTriggerType.PointerDown, d => { st.Press = true; UpdateButtonVisual(idx); });
                AddTrigger(trig, EventTriggerType.PointerUp, d => { st.Press = false; UpdateButtonVisual(idx); });

                _buttons.Add(btn);
                _states.Add(st);
                _buttonBgs.Add(btn.GetComponent<Image>());
                _buttonLabels.Add(btn.GetComponentInChildren<TextMeshProUGUI>());
            }

            // Rebuilding while collapsed previously destroyed all buttons and left an
            // empty bar on expand — buttons now always rebuild into the (possibly
            // hidden) content container, and the collapse state is re-applied.
            ApplyCollapse(_collapseT);
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
            for (int i = 0; i < _buttons.Count; i++)
                UpdateButtonVisual(i);
        }

        private bool IsEntryActive(int idx)
        {
            if (idx < 0 || idx >= _entries.Count) return false;
            ModBarEntry entry = _entries[idx];
            if (entry.IsVisible == null) return false;
            try { return entry.IsVisible(); } catch { return false; }
        }

        private void UpdateButtonVisual(int idx)
        {
            if (idx < 0 || idx >= _buttons.Count) return;
            bool active = IsEntryActive(idx);
            BtnState st = _states[idx];
            Image bg = _buttonBgs[idx];
            TextMeshProUGUI label = _buttonLabels[idx];

            if (active)
            {
                bg.color = st.Press ? UiColors.AmberBgPressed
                    : st.Hover ? UiColors.AmberBgHover
                    : UiColors.AmberBg;
                label.color = UiColors.Amber;
            }
            else
            {
                bg.color = st.Press ? UiColors.BgPressed
                    : st.Hover ? UiColors.BgHover
                    : UiColors.BgPanelRaised;
                label.color = UiColors.TextPrimary;
            }
        }

        private void ShowTooltip(string text, RectTransform anchor)
        {
            if (_tooltip == null || string.IsNullOrEmpty(text) || anchor == null) return;
            _tooltipLabel.text = text;
            Vector2 preferred = _tooltipLabel.GetPreferredValues(text);
            _tooltipRt.sizeDelta = new Vector2(
                Mathf.Ceil(preferred.x) + 16f,
                Mathf.Max(18f, Mathf.Ceil(preferred.y)) + 6f);
            _tooltip.SetActive(true);
            Canvas.ForceUpdateCanvases();

            // Anchor the tooltip's top-left just below the hovered button's bottom-left,
            // then clamp inside the canvas so long tooltips don't leave the screen.
            var btnCorners = new Vector3[4];
            anchor.GetWorldCorners(btnCorners); // 0=BL 1=TL 2=TR 3=BR
            Vector3 pos = btnCorners[0] + new Vector3(0f, -TooltipGap * _canvasRt.lossyScale.y, 0f);

            var canvasCorners = new Vector3[4];
            _canvasRt.GetWorldCorners(canvasCorners);
            float tipWorldWidth = _tooltipRt.rect.width * _tooltipRt.lossyScale.x;
            float maxX = canvasCorners[3].x - tipWorldWidth;
            if (pos.x > maxX) pos.x = maxX;
            if (pos.x < canvasCorners[0].x) pos.x = canvasCorners[0].x;

            _tooltipRt.position = pos;
        }

        private void HideTooltip()
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        // ---- Drag-to-reposition (press must start on the strip's padding grips) ----

        private Vector2 StripTopLeftLocal()
        {
            Vector2 ap = _stripRt.anchoredPosition;
            return new Vector2(-_canvasRt.rect.width / 2f + ap.x, _canvasRt.rect.height / 2f + ap.y);
        }

        private void BeginDrag(PointerEventData d)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, d.position, null, out local))
                return;
            _dragging = true;
            _idleTimer = 0f;
            _dragGrab = StripTopLeftLocal() - local;
            HideTooltip();
        }

        private void OnDrag(PointerEventData d)
        {
            if (!_dragging) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, d.position, null, out local))
                return;

            float w = _canvasRt.rect.width;
            float h = _canvasRt.rect.height;
            float sw = _stripRt.rect.width;
            float sh = _stripRt.rect.height;
            if (sw <= 0f || sh <= 0f) return;

            Vector2 topLeft = local + _dragGrab;
            float minX = -w / 2f, maxX = w / 2f - sw;
            float minY = -h / 2f + sh, maxY = h / 2f;
            float x = Mathf.Clamp(topLeft.x, minX, maxX);
            float y = Mathf.Clamp(topLeft.y, minY, maxY);

            // Snap flush to screen edges.
            if (x < minX + EdgeSnapPx) x = minX;
            if (x > maxX - EdgeSnapPx) x = maxX;
            if (y > maxY - EdgeSnapPx) y = maxY;
            if (y < minY + EdgeSnapPx) y = minY;

            // Config stays the single source of truth; ApplyOffsets picks this up.
            Plugin.OffsetX.Value = x + w / 2f;
            Plugin.OffsetY.Value = h / 2f - y;
        }

        private void EndDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            if (Plugin.Instance != null)
                Plugin.Instance.Config.Save();
        }

        public void ApplyOffsets()
        {
            if (_stripRt == null) return;
            _stripRt.anchoredPosition = new Vector2(Plugin.OffsetX.Value, -Plugin.OffsetY.Value);

            // Live-apply ButtonSize: strip height, handle size, and content width.
            float size = ButtonSize;
            if (!Mathf.Approximately(size, _appliedSize))
            {
                _appliedSize = size;
                _stripRt.sizeDelta = new Vector2(_stripRt.sizeDelta.x, BarHeight);
                if (_collapseLe != null)
                {
                    _collapseLe.preferredWidth = size;
                    _collapseLe.preferredHeight = size;
                    _collapseLe.minWidth = size;
                    _collapseLe.minHeight = size;
                }
                for (int i = 0; i < _buttons.Count; i++)
                {
                    var le = _buttons[i].GetComponent<LayoutElement>();
                    le.preferredWidth = TileWidth;
                    le.preferredHeight = size;
                    le.minWidth = TileWidth;
                    le.minHeight = size;
                }
                ApplyCollapse(_collapseT);
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.SetActive(visible);
            if (visible)
            {
                _idleTimer = 0f;
                if (_stripGroup != null) _stripGroup.alpha = 1f;
            }
            else
            {
                HideTooltip();
            }
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }
}
