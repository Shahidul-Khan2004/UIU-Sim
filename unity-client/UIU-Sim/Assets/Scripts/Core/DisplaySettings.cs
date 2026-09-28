using UnityEngine;

namespace UIU.Simulator.Core
{
    /// <summary>
    /// Locks the standalone player to 1280x720 Fullscreen Window (borderless).
    /// Rendering stays at 720p on higher-resolution monitors; the OS compositor scales
    /// the backbuffer to fill the display. URP render scale should remain 1.0 so the
    /// pipeline does not upsample on top of this.
    ///
    /// Owned by <see cref="GameManager"/> — do not place this on scene objects.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-990)]
    public sealed class DisplaySettings : MonoBehaviour
    {
        public const int Width = 1280;
        public const int Height = 720;
        public const int TargetFrameRate = 60;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Apply();
        }

        private void Start()
        {
            // Some window managers override the initial mode; re-assert after the first frame setup.
            Apply();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                Apply();
            }
        }

        /// <summary>
        /// Applies the locked display mode. Safe to call more than once.
        /// </summary>
        public static void Apply()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            Screen.SetResolution(Width, Height, FullScreenMode.FullScreenWindow);
        }
    }
}
