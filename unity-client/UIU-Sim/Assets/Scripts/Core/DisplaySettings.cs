using UnityEngine;

namespace UIU.Simulator.Core
{
    /// <summary>
    /// Opens the standalone player as borderless fullscreen at the monitor's native
    /// resolution (the display the window is on). Re-applies on focus so moving the
    /// window between monitors picks up the new display size.
    ///
    /// Owned by <see cref="GameManager"/> — do not place this on scene objects.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-990)]
    public sealed class DisplaySettings : MonoBehaviour
    {
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
        /// Applies native-resolution borderless fullscreen. Safe to call more than once.
        /// </summary>
        public static void Apply()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;

            var info = Screen.mainWindowDisplayInfo;
            int width = info.width;
            int height = info.height;
            if (width <= 0 || height <= 0)
            {
                width = Display.main.systemWidth;
                height = Display.main.systemHeight;
            }

            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }
    }
}
