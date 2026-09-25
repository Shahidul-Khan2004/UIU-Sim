using UIU.Simulator.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Assessment
{
    /// <summary>Terminal beta modal. Sequential ownership transfers avoid disabled control snapshots.</summary>
    public sealed class BetaEndUI : AcademicModal
    {
        public static BetaEndUI Instance { get; private set; }
        public static BetaEndUI EnsureExists()
        {
            if (Instance == null) Instance = new GameObject("BetaEndUI").AddComponent<BetaEndUI>();
            return Instance;
        }
        protected override void Awake() { base.Awake(); Instance = this; }
        protected override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; }
        public void Show()
        {
            if (IsVisible || BlocksGameplay) return;
            OpenModal(); Clear();
            Label("SEMESTER COMPLETE", 28, UiTheme.BrightOrange);
            Label("Your time at UIU for this beta has concluded.\n\n" +
                "Your Semester 1 results are now available in your Report Card.\n\n" +
                "The next part of UIU-Sim is still being developed.\n\n" +
                "Thank you for playing the beta.", 24, null, 300);
            Button("VIEW REPORT CARD", ViewReportCard);
            Button("QUIT GAME", QuitGame, true);
        }
        private void ViewReportCard()
        {
            // Restore the baseline before the report captures it, in this same call/frame.
            CloseModal();
            ReportCardUI.EnsureExists().Show(() => { if (this != null) Show(); });
        }
        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Quit Game requested. Application.Quit does not stop the Unity Editor.");
#endif
            Application.Quit();
        }
    }
}
