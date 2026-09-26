using System.Text;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Shared behaviour for campus student NPCs. Concrete components only choose the
    /// <see cref="StudentNpcType"/>; conversations come from <see cref="StudentDialogueDatabase"/>.
    /// <para>
    /// Completing a conversation as a student resolves the <c>NPC_TALK_&lt;NPC ID&gt;</c> activity on the
    /// backend, which grants +2 Aura only for the first completion with this NPC each day.
    /// Faculty players and conversations left early never request a reward.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class StudentNPCBase : MonoBehaviour, IInteractable
    {
        public const string CompletedOutcome = "COMPLETED";
        private const int MaxNpcIdLength = 40;

        [Header("Identity")]
        [Tooltip("Unique, stable ID for this NPC (A-Z, 0-9, _; max 40). Used for daily reward memory. " +
                 "Empty = derived from the GameObject name.")]
        [SerializeField] private string npcId = string.Empty;

        [Tooltip("Name shown in the dialogue header. Empty = type default.")]
        [SerializeField] private string speakerName = string.Empty;

        [Tooltip("Text shown when looking at the NPC. Empty = \"Talk to <speaker>\".")]
        [SerializeField] private string prompt = string.Empty;

        [Header("Dialogue")]
        [Tooltip("Optional override. Empty = Resources/StudentDialogue/StudentDialogueDatabase.")]
        [SerializeField] private StudentDialogueDatabase dialogueDatabase;

        private IActivityProgressSync progressSync;
        private System.Random random;
        private StudentConversationRunner activeRunner;
        private PlayerSaveState subscribedSave;

        public abstract StudentNpcType NpcType { get; }
        protected abstract string DefaultSpeakerName { get; }

        public string NpcId => NormalizeNpcId(string.IsNullOrWhiteSpace(npcId) ? gameObject.name : npcId);
        public string ActivityId => ActivityIds.NpcConversationPrefix + NpcId;
        public string SpeakerName => string.IsNullOrWhiteSpace(speakerName) ? DefaultSpeakerName : speakerName.Trim();
        public StudentConversationRunner ActiveConversation => activeRunner;

        public string InteractionPrompt => string.IsNullOrWhiteSpace(prompt) ? $"Talk to {SpeakerName}" : prompt;

        // ── Test seams ───────────────────────────────────────────────────

        public void SetProgressSyncForTesting(IActivityProgressSync sync) => progressSync = sync;
        public void SetDatabaseForTesting(StudentDialogueDatabase database) => dialogueDatabase = database;
        public void SetRandomSeedForTesting(int seed) => random = new System.Random(seed);
        public void SetNpcIdForTesting(string id) => npcId = id;

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Reset()
        {
            EnsureInteractionCollider();
        }

        protected virtual void Awake()
        {
            EnsureInteractionCollider();
        }

        protected virtual void OnEnable()
        {
            subscribedSave = PlayerSaveState.Instance;
            if (subscribedSave != null)
            {
                subscribedSave.OnAdmissionCompleted += StudentConversationMemory.Clear;
            }
        }

        protected virtual void OnDisable()
        {
            if (subscribedSave != null)
            {
                subscribedSave.OnAdmissionCompleted -= StudentConversationMemory.Clear;
                subscribedSave = null;
            }

            activeRunner?.Abandon();
        }

        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                npcId = NormalizeNpcId(npcId);
            }
        }

        // ── IInteractable ────────────────────────────────────────────────

        public string Interact()
        {
            if (DialogueUI.IsOpen)
            {
                return null;
            }

            // Dialogue closed by something else mid-conversation: treat as left early.
            if (activeRunner != null && activeRunner.State == StudentConversationRunner.RunState.Running)
            {
                activeRunner.Abandon();
            }

            DialogueUI dialogueUI = DialogueUI.Instance;
            if (dialogueUI == null)
            {
                Debug.LogError($"[{GetType().Name}] DialogueUI.Instance is null. Add DialogueUI to the Player prefab root.", this);
                return null;
            }

            StudentDialogueContext context = BuildContext();
            if (context == null || context.PlayerRole == null)
            {
                return $"{SpeakerName} gives you a friendly nod.";
            }

            StudentDialogueDatabase database = dialogueDatabase != null ? dialogueDatabase : StudentDialogueDatabase.LoadDefault();
            if (database == null)
            {
                Debug.LogError($"[{GetType().Name}] No StudentDialogueDatabase assigned or found in Resources.", this);
                return $"{SpeakerName} gives you a friendly nod.";
            }

            StudentConversationMemory.Entry memory = StudentConversationMemory.For(NpcId, context);
            random ??= new System.Random();
            StudentDialogueTree tree = database.SelectTree(NpcType, context, memory.SeenTreeIds, random);
            if (tree == null)
            {
                return $"{SpeakerName}: \"{database.NoConversationLine}\"";
            }

            memory.SeenTreeIds.Add(tree.TreeId);
            activeRunner = new StudentConversationRunner(
                dialogueUI,
                tree,
                context,
                SpeakerName,
                database,
                () => OnConversationCompleted(context),
                null);
            activeRunner.Start();

            Debug.Log($"[{GetType().Name}] {NpcId} started '{tree.TreeId}' (role={context.Role}, dept={context.Department}, day={context.Day}).");
            return null;
        }

        // ── Reward ───────────────────────────────────────────────────────

        private void OnConversationCompleted(StudentDialogueContext context)
        {
            StudentConversationMemory.Entry memory = StudentConversationMemory.For(NpcId, context);
            memory.ConversationCompleted = true;

            if (!context.IsStudent)
            {
                return;
            }

            if (memory.RewardClaimed || memory.RewardRequestInFlight)
            {
                Debug.Log($"[{GetType().Name}] {NpcId} already rewarded today — no Aura.");
                return;
            }

            IActivityProgressSync sync = ResolveProgressSync();
            if (sync == null)
            {
                Debug.LogError($"[{GetType().Name}] PlayerProgressSync missing. Cannot record conversation reward.", this);
                return;
            }

            memory.RewardRequestInFlight = true;
            sync.RequestActivityResolve(
                ActivityId,
                CompletedOutcome,
                result =>
                {
                    memory.RewardRequestInFlight = false;
                    memory.RewardClaimed = true;
                    Debug.Log(
                        $"[{GetType().Name}] {ActivityId} resolved: auraDelta={result.Record.AuraDelta}, " +
                        $"alreadyResolved={result.AlreadyResolved}");
                },
                () =>
                {
                    memory.RewardRequestInFlight = false;
                    Debug.LogWarning($"[{GetType().Name}] {ActivityId} reward was not recorded; a later completion today may retry.");
                });
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private StudentDialogueContext BuildContext()
        {
            PlayerSaveState save = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();
            DailyActivityState activities = FindFirstObjectByType<DailyActivityState>();
            return StudentDialogueContext.FromGameState(save, activities);
        }

        private IActivityProgressSync ResolveProgressSync()
        {
            if (progressSync == null)
            {
                progressSync = FindFirstObjectByType<PlayerProgressSync>();
            }

            return progressSync;
        }

        private void EnsureInteractionCollider()
        {
            if (GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                capsule.radius = 0.3f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 scale = transform.lossyScale;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            capsule.center = transform.InverseTransformPoint(bounds.center);
            capsule.height = bounds.size.y / scaleY;
            capsule.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f, 0.2f, 0.6f) / scaleXZ;
        }

        public static string NormalizeNpcId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "STUDENT";
            }

            var builder = new StringBuilder(raw.Length);
            foreach (char c in raw.Trim().ToUpperInvariant())
            {
                bool allowed = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                builder.Append(allowed ? c : '_');
                if (builder.Length == MaxNpcIdLength)
                {
                    break;
                }
            }

            return builder.ToString();
        }
    }
}
