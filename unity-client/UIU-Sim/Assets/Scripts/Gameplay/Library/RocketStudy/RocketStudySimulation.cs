using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Library.RocketStudy
{
    [Serializable]
    public sealed class RocketStudySettings
    {
        [Range(1f, 30f)] public float sessionDuration = 30f;
        public float gravity = -360f;
        [Min(0f)] public float thrust = 720f;
        [Min(1f)] public float maxVerticalSpeed = 220f;
        [Min(1f)] public float obstacleSpeed = 180f;
        [Min(0.5f)] public float spawnInterval = 2.3f;
        [Min(80f)] public float gapSize = 280f;
        public Vector2 rocketSize = new Vector2(40f, 22f);
        [Min(0f)] public float verticalPadding = 36f;
    }

    /// <summary>Small local-coordinate simulation. No input, scene, physics, stats, or network side effects.</summary>
    public sealed class RocketStudySimulation
    {
        public enum RunState { Ready, Running, Finished, Cancelled }
        public const float Width = 1000f;
        public const float Height = 560f;
        public const float RocketX = -Width * 0.25f;
        public const float ObstacleWidth = 72f;
        private const double Step = 1d / 120d;

        public sealed class Obstacle
        {
            public int Id { get; internal set; }
            public float X { get; internal set; }
            public float GapCenter { get; internal set; }
            public Rect Bottom(float gap) => new Rect(X - ObstacleWidth / 2f, -Height / 2f,
                ObstacleWidth, GapCenter - gap / 2f + Height / 2f);
            public Rect Top(float gap) => new Rect(X - ObstacleWidth / 2f, GapCenter + gap / 2f,
                ObstacleWidth, Height / 2f - GapCenter - gap / 2f);
        }

        private readonly List<Obstacle> obstacles = new List<Obstacle>();
        private readonly System.Random random;
        private readonly float gravity, thrust, maxSpeed, obstacleSpeed, interval, padding;
        private double elapsed;
        private double untilSpawn = 1d;
        private float previousGap;
        private int nextId;

        public RunState State { get; private set; } = RunState.Ready;
        public float Duration { get; }
        public float Elapsed => (float)elapsed;
        public float RocketY { get; private set; }
        public float VerticalVelocity { get; private set; }
        public Vector2 RocketSize { get; }
        public float GapSize { get; }
        public int Score => CalculateScore(Elapsed, Duration);
        public IReadOnlyList<Obstacle> Obstacles => obstacles;
        public Rect RocketBounds => new Rect(RocketX - RocketSize.x / 2f, RocketY - RocketSize.y / 2f,
            RocketSize.x, RocketSize.y);
        public event Action<LibraryStudyMinigameResult> Ended;

        public RocketStudySimulation(RocketStudySettings settings, int seed)
        {
            Duration = Mathf.Clamp(settings.sessionDuration, 1f, 30f);
            gravity = Mathf.Min(0f, settings.gravity);
            thrust = Mathf.Max(0f, settings.thrust);
            maxSpeed = Mathf.Clamp(settings.maxVerticalSpeed, 1f, 500f);
            obstacleSpeed = Mathf.Clamp(settings.obstacleSpeed, 1f, 400f);
            interval = Mathf.Max(0.5f, settings.spawnInterval);
            RocketSize = new Vector2(Mathf.Clamp(settings.rocketSize.x, 8f, 80f),
                Mathf.Clamp(settings.rocketSize.y, 8f, 60f));
            GapSize = Mathf.Clamp(settings.gapSize, RocketSize.y + 80f, Height - 40f);
            padding = Mathf.Clamp(settings.verticalPadding, 0f, (Height - GapSize) / 2f);
            random = new System.Random(seed);
        }

        public static int CalculateScore(float seconds, float duration)
        {
            if (float.IsNaN(seconds) || float.IsNaN(duration) || duration <= 0f) return 0;
            return Mathf.RoundToInt(Mathf.Clamp01(seconds / duration) * 100f);
        }

        public void Begin()
        {
            if (State == RunState.Ready) State = RunState.Running;
        }

        public void Advance(float realDeltaTime, bool thrustHeld)
        {
            if (State != RunState.Running || float.IsNaN(realDeltaTime) || realDeltaTime <= 0f) return;
            double remaining = Math.Min(realDeltaTime, Duration - elapsed);
            // Substeps prevent tunnelling on a slow frame, without discarding real survival time.
            while (remaining > 0d && State == RunState.Running)
            {
                double step = Math.Min(Step, remaining);
                float dt = (float)step;
                elapsed = Math.Min(Duration, elapsed + step);
                if (Duration - elapsed < 0.0000001d) elapsed = Duration;
                remaining -= step;
                VerticalVelocity = Mathf.Clamp(VerticalVelocity + (gravity + (thrustHeld ? thrust : 0f)) * dt,
                    -maxSpeed, maxSpeed);
                RocketY += VerticalVelocity * dt;

                untilSpawn -= step;
                if (untilSpawn <= 0d)
                {
                    Spawn();
                    untilSpawn += interval;
                }
                for (int i = obstacles.Count - 1; i >= 0; i--)
                {
                    obstacles[i].X -= obstacleSpeed * dt;
                    if (obstacles[i].X + ObstacleWidth / 2f < -Width / 2f) obstacles.RemoveAt(i);
                }

                float limit = (Height - RocketSize.y) / 2f;
                if (RocketY <= -limit || RocketY >= limit)
                {
                    RocketY = Mathf.Clamp(RocketY, -limit, limit);
                    Finish();
                    break;
                }
                Rect rocket = RocketBounds;
                foreach (Obstacle obstacle in obstacles)
                {
                    if (Touches(rocket, obstacle.Top(GapSize)) || Touches(rocket, obstacle.Bottom(GapSize)))
                    {
                        Finish();
                        break;
                    }
                }
                if (elapsed >= Duration) Finish();
            }
        }

        public void Cancel()
        {
            if (State != RunState.Ready && State != RunState.Running) return;
            State = RunState.Cancelled;
            obstacles.Clear();
            Ended?.Invoke(LibraryStudyMinigameResult.Cancelled());
        }

        private void Finish()
        {
            if (State != RunState.Running) return;
            State = RunState.Finished;
            Ended?.Invoke(LibraryStudyMinigameResult.Finished(Score));
        }

        private void Spawn()
        {
            float limit = (Height - GapSize) / 2f - padding;
            // Adjacent gaps overlap generously; the first one is centred for a forgiving introduction.
            float shift = Mathf.Min(80f, (GapSize - RocketSize.y) * 0.35f);
            float center = nextId == 0 ? 0f : Mathf.Clamp(previousGap + ((float)random.NextDouble() * 2f - 1f) * shift,
                -limit, limit);
            obstacles.Add(new Obstacle { Id = nextId++, X = Width / 2f + ObstacleWidth / 2f, GapCenter = center });
            previousGap = center;
        }

        private static bool Touches(Rect a, Rect b) =>
            a.xMin <= b.xMax && a.xMax >= b.xMin && a.yMin <= b.yMax && a.yMax >= b.yMin;
    }
}
