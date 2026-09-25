using System;

namespace UIU.Simulator.Gameplay.Library
{
    /// <summary>
    /// Test/development adapter that returns a controlled normalized score.
    /// Not a scene button and not placed in gameplay.
    /// </summary>
    public sealed class ScriptedLibraryStudyMinigame : ILibraryStudyMinigame
    {
        public bool IsAvailable { get; set; } = true;
        public int Score { get; set; }
        public bool Cancel { get; set; }

        public void Launch(Action<LibraryStudyMinigameResult> onFinished)
        {
            if (Cancel || !IsAvailable)
            {
                onFinished?.Invoke(LibraryStudyMinigameResult.Cancelled());
                return;
            }

            onFinished?.Invoke(LibraryStudyMinigameResult.Finished(Score));
        }
    }
}
