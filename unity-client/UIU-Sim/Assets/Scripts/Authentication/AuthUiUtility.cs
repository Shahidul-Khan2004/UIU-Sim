using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Authentication
{
    public static class AuthUiUtility
    {
        /// <summary>
        /// Login / menu screens need a free visible cursor. Gameplay (CameraFollow) locks it.
        /// </summary>
        public static void ShowUiCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void EnsureInputSystemEventSystem()
        {
            EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    Object.Destroy(legacy);
                    Debug.Log("[AuthUiUtility] Removed StandaloneInputModule (incompatible with Input System Package).");
                }

                if (existing.GetComponent<InputSystemUIInputModule>() == null)
                {
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
                    Debug.Log("[AuthUiUtility] Added InputSystemUIInputModule.");
                }

                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[AuthUiUtility] Created EventSystem with InputSystemUIInputModule.");
        }

        public static Font ResolveUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor)
        {
            GameObject go = CreateRect(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = ResolveUiFont();
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = content;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = CreateRect(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.56f, 0.42f, 1f);
            Button button = buttonObject.AddComponent<Button>();

            Text text = CreateText(buttonObject.transform, "Label", label, 20, TextAnchor.MiddleCenter);
            text.raycastTarget = false;

            return button;
        }
    }
}
