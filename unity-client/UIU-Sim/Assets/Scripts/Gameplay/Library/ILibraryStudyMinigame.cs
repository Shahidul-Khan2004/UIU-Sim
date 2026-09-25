using System;

namespace UIU.Simulator.Gameplay.Library
{
    /// <summary>
    /// Boundary between the library desk and an arcade minigame.
    /// The library interaction only starts a session, launches this adapter, and submits the score.
    /// </summary>
    public interface ILibraryStudyMinigame
    {
        /// <summary>
        /// False when no real minigame is installed. The interaction must not consume today's attempt.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Runs the minigame. Cancellation or failure must report <see cref="LibraryStudyMinigameResult.Cancelled"/>.
        /// A finished attempt reports a normalized score from 0 to 100.
        /// </summary>
        void Launch(Action<LibraryStudyMinigameResult> onFinished);
    }

    /// <summary>
    /// Optional lifecycle for adapters that retain their result screen while the server saves.
    /// Close must invoke onClosed only after scene cleanup. The existing Launch contract is unchanged.
    /// </summary>
    public interface ILibraryStudyMinigameSession
    {
        void Close(Action onClosed);
    }

    public readonly struct LibraryStudyMinigameResult
    {
        public bool Completed { get; }
        public int Score { get; }

        public LibraryStudyMinigameResult(bool completed, int score)
        {
            Completed = completed;
            Score = score;
        }

        public static LibraryStudyMinigameResult Finished(int score)
        {
            return new LibraryStudyMinigameResult(true, score);
        }

        public static LibraryStudyMinigameResult Cancelled()
        {
            return new LibraryStudyMinigameResult(false, 0);
        }
    }

    /// <summary>
    /// Default adapter used until a real minigame is assigned. It never starts a backend attempt.
    /// </summary>
    public sealed class UnavailableLibraryStudyMinigame : ILibraryStudyMinigame
    {
        public static readonly UnavailableLibraryStudyMinigame Instance = new UnavailableLibraryStudyMinigame();

        public bool IsAvailable => false;

        public void Launch(Action<LibraryStudyMinigameResult> onFinished)
        {
            onFinished?.Invoke(LibraryStudyMinigameResult.Cancelled());
        }
    }
}
