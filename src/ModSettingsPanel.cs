using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using BepInEx.Configuration;

namespace NoModBar
{
    /// <summary>
    /// The mod's own settings panel, registered on the bar as "CFG".
    /// Currently hosts a single option: the free-cursor hotkey (click to rebind).
    /// While open it holds the mod-ui cursor flag so the panel stays clickable.
    /// Visuals match the bar: 9-slice framed chrome, explicit hover/press button
    /// states driven from the palette via EventTrigger (no ColorBlock tints).
    /// </summary>
    internal class ModSettingsPanel
    {
        public const string EntryId = "nomodbar.settings";

        private const float PanelWidth = 480f;
        private const float PanelHeight = 210f;
        private const float Pad = 18f;

        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftAlt, KeyCode.RightAlt
        };

        private class BtnState { public bool Hover; public bool Press; public bool Active; }

        private readonly Dictionary<Button, BtnState> _states = new Dictionary<Button, BtnState>();

        private GameObject _root;
        private GameObject _panel;
        private TextMeshProUGUI _hotkeyValue;
        private Image _hotkeyBg;
        private BtnState _hotkeyState;
        private bool _open;
        private bool _capturing;

        public bool IsOpen => _open;

        public void Create()
        {
            _root = new GameObject("NoModBarSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1010; // above the bar (1000)
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f; // scale with screen height only, like the bar

            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            BuildPanel(rootRt);
            SetOpen(false);
        }

        private void BuildPanel(RectTransform canvasRt)
        {
            _panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasRt, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rt.anchoredPosition = Vector2.zero;

            // 9-slice frame like the bar strip — border stays 1 canvas unit thick
            // at any panel size instead of stretching inward like a RawImage.
            var bg = _panel.GetComponent<Image>();
            bg.sprite = TextureFactory.CreateFramedSprite(UiColors.BgPanel, UiColors.BorderPanel, 1);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = TextureFactory.FramedPpuMultiplier;
            bg.color = Color.white;
            bg.raycastTarget = true; // block clicks passing through the panel

            // Title
            MakeLabel(_panel.transform, "Title", "NO MOD BAR — SETTINGS",
                new Vector2(Pad, -Pad), new Vector2(0, 1), 14, FontStyles.Bold, UiColors.HudGreen);

            // Close button
            var close = MakeButton(_panel.transform, "Close", "X", new Vector2(26, 26), null);
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1, 1);
            closeRt.anchorMax = new Vector2(1, 1);
            closeRt.pivot = new Vector2(1, 1);
            closeRt.anchoredPosition = new Vector2(-10, -10);
            close.GetComponent<Button>().onClick.AddListener(() => SetOpen(false));

            // Hotkey row
            MakeLabel(_panel.transform, "HotkeyLabel", "Free-cursor hotkey",
                new Vector2(Pad, -70), new Vector2(0, 1), 13, FontStyles.Normal, UiColors.TextPrimary);

            var rebind = MakeButton(_panel.transform, "HotkeyRebind", "", new Vector2(190, 32), null);
            _hotkeyState = _states[rebind];
            var rebindRt = rebind.GetComponent<RectTransform>();
            rebindRt.anchorMin = new Vector2(1, 1);
            rebindRt.anchorMax = new Vector2(1, 1);
            rebindRt.pivot = new Vector2(1, 1);
            rebindRt.anchoredPosition = new Vector2(-Pad, -62);
            _hotkeyBg = rebind.GetComponent<Image>();
            rebind.GetComponent<Button>().onClick.AddListener(StartCapture);
            _hotkeyValue = rebind.GetComponentInChildren<TextMeshProUGUI>();
            _hotkeyValue.fontStyle = FontStyles.Normal;
            RefreshHotkeyLabel();

            // Hint
            MakeLabel(_panel.transform, "Hint",
                "Hold to unlock the cursor from mouse-look while flying.\nClick the binding to rebind (Esc cancels).",
                new Vector2(Pad, -116), new Vector2(0, 1), 11, FontStyles.Normal, UiColors.TextSecondary,
                TextAlignmentOptions.TopLeft, true);
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, Vector2 pos,
            Vector2 pivot, int size, FontStyles style, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(PanelWidth - 2 * Pad, wrap ? 60f : 22f);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.text = text;
            tmp.alignment = align;
            tmp.enableWordWrapping = wrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Bar-style button: flat chrome base with explicit hover/press states from
        /// the palette, driven via EventTrigger (Transition.None — no tinting).
        /// Returns the Button; the BtnState component drives UpdateButtonVisual.
        /// </summary>
        private Button MakeButton(Transform parent, string name, string text, Vector2 size, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(EventTrigger));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(Pad, -Pad);
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = UiColors.BgPanelRaised;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var trig = go.GetComponent<EventTrigger>();
            var state = new BtnState();
            _states[btn] = state;
            AddTrigger(trig, EventTriggerType.PointerEnter, d => { state.Hover = true; UpdateButtonVisual(btn, state); });
            AddTrigger(trig, EventTriggerType.PointerExit, d => { state.Hover = false; state.Press = false; UpdateButtonVisual(btn, state); });
            AddTrigger(trig, EventTriggerType.PointerDown, d => { state.Press = true; UpdateButtonVisual(btn, state); });
            AddTrigger(trig, EventTriggerType.PointerUp, d => { state.Press = false; UpdateButtonVisual(btn, state); });

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

        /// <summary>Bar-style state painting: amber for active (capturing), neutral otherwise.</summary>
        private static void UpdateButtonVisual(Button btn, BtnState state)
        {
            var img = btn.GetComponent<Image>();
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (state.Active)
            {
                img.color = state.Press ? UiColors.AmberBgPressed
                    : state.Hover ? UiColors.AmberBgHover
                    : UiColors.AmberBg;
                if (label != null) label.color = UiColors.Amber;
            }
            else
            {
                img.color = state.Press ? UiColors.BgPressed
                    : state.Hover ? UiColors.BgHover
                    : UiColors.BgPanelRaised;
                if (label != null) label.color = UiColors.TextPrimary;
            }
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            if (_open == open && _panel != null && _panel.activeSelf == open) return;
            _open = open;
            if (!open && _capturing) StopCapture();
            if (_panel != null) _panel.SetActive(open);
            CursorOverride.SetModUi(open);
        }

        /// <summary>Per-frame input handling: key capture + Esc to close.</summary>
        public void Tick()
        {
            if (!_open) return;
            if (_capturing)
            {
                CaptureTick();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetOpen(false);
            }
        }

        private void StartCapture()
        {
            _capturing = true;
            if (_hotkeyState != null) _hotkeyState.Active = true;
            if (_hotkeyBg != null) UpdateButtonVisual(_hotkeyBg.GetComponent<Button>(), _hotkeyState);
            if (_hotkeyValue != null)
            {
                _hotkeyValue.text = "Press keys…";
                _hotkeyValue.color = UiColors.Amber;
            }
        }

        private void StopCapture()
        {
            _capturing = false;
            if (_hotkeyState != null) _hotkeyState.Active = false;
            if (_hotkeyBg != null) UpdateButtonVisual(_hotkeyBg.GetComponent<Button>(), _hotkeyState);
            RefreshHotkeyLabel();
        }

        private void CaptureTick()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopCapture();
                return;
            }

            foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
            {
                if (kc == KeyCode.None || kc == KeyCode.Escape) continue;
                if (kc >= KeyCode.Mouse0 && kc <= KeyCode.Mouse6) continue; // mouse buttons are for clicking the UI
                if (!Input.GetKeyDown(kc)) continue;

                var mods = new List<KeyCode>();
                foreach (KeyCode m in ModifierKeys)
                {
                    if (m != kc && Input.GetKey(m)) mods.Add(m);
                }

                Plugin.FreeCursorKey.Value = mods.Count == 0
                    ? new KeyboardShortcut(kc)
                    : new KeyboardShortcut(kc, mods.ToArray());
                Plugin.Log?.LogInfo($"ModBar: free-cursor hotkey bound to {Plugin.FreeCursorKey.Value}");
                StopCapture();
                return;
            }
        }

        private void RefreshHotkeyLabel()
        {
            if (_hotkeyValue == null) return;
            KeyboardShortcut ks = Plugin.FreeCursorKey.Value;
            _hotkeyValue.text = ks.MainKey == KeyCode.None ? "<not bound>" : ks.ToString();
            _hotkeyValue.color = UiColors.TextPrimary;
        }

        public void Destroy()
        {
            CursorOverride.SetModUi(false);
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }
}
