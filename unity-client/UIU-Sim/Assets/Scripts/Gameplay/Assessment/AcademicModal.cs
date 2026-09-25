using System;
using System.Collections.Generic;
using TMPro;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;
namespace UIU.Simulator.Gameplay.Assessment
{
    /// <summary>Shared assessment/report modal shell. Captures controls only on acquisition, never on page changes.</summary>
    public abstract class AcademicModal : MonoBehaviour
    {
        private static readonly HashSet<AcademicModal> owners = new HashSet<AcademicModal>();
        public static bool BlocksGameplay => owners.Count > 0;
        protected GameObject root;
        protected Transform content;
        private readonly List<Behaviour> controls = new List<Behaviour>();
        private readonly List<bool> previous = new List<bool>();
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;
        protected bool IsVisible => root != null && root.activeSelf;
        protected virtual void Awake() { Build(); root.SetActive(false); }
        protected void OpenModal()
        {
            if (IsVisible) return;
            if (root == null) Build();
            AuthUiUtility.EnsureInputSystemEventSystem();
            controls.Clear(); previous.Clear();
            var active = PlayerProgressSync.Active;
            Capture(active != null ? active.GetComponent<PlayerMovement>() : FindFirstObjectByType<PlayerMovement>());
            Capture(FindFirstObjectByType<FirstPersonLook>());
            Capture(active != null ? active.GetComponent<InteractionController>() : FindFirstObjectByType<InteractionController>());
            Capture(FindFirstObjectByType<CameraFollow>());
            previousCursor = Cursor.lockState; previousCursorVisible = Cursor.visible;
            owners.Add(this); root.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void Capture(Behaviour control)
        {
            if (control == null) return;
            controls.Add(control); previous.Add(control.enabled); control.enabled = false;
        }
        public virtual void CloseModal()
        {
            if (!owners.Remove(this)) return;
            if (root != null) root.SetActive(false);
            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i] == null) continue;
                if (controls[i] is FirstPersonLook look) look.SuppressEscapeThisFrame();
                controls[i].enabled = previous[i];
            }
            Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible;
            controls.Clear(); previous.Clear();
        }
        protected virtual void OnDestroy() { CloseModal(); }
        protected void Clear()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
        protected TextMeshProUGUI Label(string text, int size = 22, Color? color = null, float height = 48)
        {
            var go = new GameObject("Label", typeof(RectTransform)); go.transform.SetParent(content, false);
            var label = go.AddComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
            label.color = color ?? UiTheme.White; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
            label.richText = false;
            go.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(height, label.GetPreferredValues(text, 664, float.PositiveInfinity).y + 8);
            return label;
        }
        protected Button Button(string caption, Action clicked, bool secondary = false)
        {
            var go = new GameObject(caption, typeof(RectTransform)); go.transform.SetParent(content, false);
            var image = go.AddComponent<Image>(); image.color = secondary ? new Color(.18f,.18f,.18f) : UiTheme.BrightOrange;
            var button = go.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => clicked());
            var element = go.AddComponent<LayoutElement>(); element.preferredHeight = 46;
            var labelGo = new GameObject("Caption", typeof(RectTransform)); labelGo.transform.SetParent(go.transform, false);
            var rect = (RectTransform)labelGo.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12,0); rect.offsetMax = new Vector2(-12,0);
            var label = labelGo.AddComponent<TextMeshProUGUI>(); label.text = caption; label.fontSize = 20;
            label.color = secondary ? UiTheme.White : UiTheme.Black; label.alignment = TextAlignmentOptions.Center;
            label.richText = false; label.raycastTarget = false;
            element.preferredHeight = Mathf.Max(46, label.GetPreferredValues(caption, 640, float.PositiveInfinity).y + 16);
            return button;
        }
        protected void DisableButtons()
        { foreach (var button in content.GetComponentsInChildren<Button>()) button.interactable = false; }
        private void Build()
        {
            root = new GameObject("AcademicCanvas", typeof(RectTransform)); root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 260;
            var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();
            var shade = new GameObject("Backdrop", typeof(RectTransform)); shade.transform.SetParent(root.transform,false);
            var shadeRect = (RectTransform)shade.transform; shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.AddComponent<Image>().color = new Color(0,0,0,.65f);
            var panel = new GameObject("Panel", typeof(RectTransform)); panel.transform.SetParent(root.transform,false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(720,720);
            panel.AddComponent<Image>().color = UiTheme.Black;
            // A bounded scroll viewport keeps long real questions/report text reachable.
            var viewport = new GameObject("Viewport", typeof(RectTransform)); viewport.transform.SetParent(panel.transform,false);
            var viewRect = (RectTransform)viewport.transform; viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = new Vector2(28,24); viewRect.offsetMax = new Vector2(-28,-24);
            viewport.AddComponent<RectMask2D>();
            var body = new GameObject("Content", typeof(RectTransform)); body.transform.SetParent(viewport.transform,false);
            var bodyRect = (RectTransform)body.transform; bodyRect.anchorMin = new Vector2(0,1); bodyRect.anchorMax = Vector2.one;
            bodyRect.pivot = new Vector2(.5f,1); bodyRect.sizeDelta = Vector2.zero;
            var layout = body.AddComponent<VerticalLayoutGroup>(); layout.spacing = 10; layout.childControlWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            body.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = panel.AddComponent<ScrollRect>(); scroll.viewport = viewRect; scroll.content = bodyRect;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            content = body.transform;
        }
    }
}
