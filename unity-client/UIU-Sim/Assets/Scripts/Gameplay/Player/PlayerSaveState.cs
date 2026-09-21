using System;
using System.Collections;
using UIU.Simulator.Authentication;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Cached GET /api/players/me/save state for admission and ID-scanner gating.
    /// ID card ownership is a boolean (<see cref="IdCardIssued"/>), not an inventory item.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSaveState : MonoBehaviour
    {
        private const string SavePath = "api/players/me/save";

        public static PlayerSaveState Instance { get; private set; }

        private ApiClient apiClient;
        private UserSession userSession;

        private bool isHydrated;
        private bool isHydrating;
        private bool hasSave;
        private bool idCardIssued;
        private bool admissionCompleted;
        private int semester;
        private int currentDay;
        private string playerName;
        private string role;
        private string department;
        private string universityId;

        public bool IsHydrated => isHydrated;
        public bool IsHydrating => isHydrating;
        public bool HasSave => hasSave;
        public bool IdCardIssued => idCardIssued;
        public bool AdmissionCompleted => admissionCompleted;
        public int Semester => semester;
        public int CurrentDay => currentDay;
        public string PlayerName => playerName;
        public string Role => role;
        public string Department => department;
        public string UniversityId => universityId;

        /// <summary>True when the player still needs receptionist admission / ID issuance.</summary>
        public bool NeedsAdmission => isHydrated && (!hasSave || !idCardIssued);

        /// <summary>True once admission created an active university journey day.</summary>
        public bool HasActiveUniversityDay => isHydrated && hasSave && admissionCompleted;

        public event Action OnHydrated;
        public event Action OnAdmissionCompleted;
        public event Action OnDayProgressChanged;

        public static PlayerSaveState EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            PlayerSaveState existing = FindFirstObjectByType<PlayerSaveState>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("PlayerSaveState");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<PlayerSaveState>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            EnsureDependencies();
            RefreshFromServer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RefreshFromServer()
        {
            if (!isHydrating)
            {
                StartCoroutine(HydrateRoutine());
            }
        }

        /// <summary>
        /// Applies a successful POST /api/players/me/save response locally.
        /// </summary>
        public void ApplyCreatedSave(ApiClient.PlayerSaveStatusDto status)
        {
            if (status == null || !status.hasSave || status.save == null)
            {
                ClearLocalState();
                return;
            }

            ApplySaveDto(status.save, hasSaveValue: true);
            isHydrated = true;
            OnAdmissionCompleted?.Invoke();
            OnDayProgressChanged?.Invoke();
        }

        /// <summary>Test / editor seam to inject admission state without networking.</summary>
        public void SetStateForTesting(bool hasSaveValue, bool idCardIssuedValue, bool markHydrated = true)
        {
            hasSave = hasSaveValue;
            idCardIssued = idCardIssuedValue;
            admissionCompleted = hasSaveValue && idCardIssuedValue;
            semester = hasSaveValue ? Mathf.Max(1, semester) : 0;
            currentDay = hasSaveValue ? Mathf.Max(1, currentDay) : 0;
            isHydrated = markHydrated;
            OnDayProgressChanged?.Invoke();
        }

        /// <summary>Test / editor seam for semester and day display.</summary>
        public void SetDayProgressForTesting(int semesterValue, int dayValue, bool markHydrated = true)
        {
            semester = Mathf.Max(0, semesterValue);
            currentDay = Mathf.Max(0, dayValue);
            if (semester > 0 && currentDay > 0)
            {
                hasSave = true;
                admissionCompleted = true;
            }

            isHydrated = markHydrated;
            OnDayProgressChanged?.Invoke();
        }

        /// <summary>Test seam for role / department eligibility checks.</summary>
        public void SetIdentityForTesting(string roleValue, string departmentValue, bool idCardIssuedValue = true)
        {
            role = roleValue;
            department = departmentValue;
            idCardIssued = idCardIssuedValue;
            hasSave = true;
            admissionCompleted = true;
            isHydrated = true;
            OnDayProgressChanged?.Invoke();
        }

        /// <summary>Applies confirmed server day advancement without re-fetching the save.</summary>
        public void ApplyAdvancedDay(int semesterValue, int dayValue, bool idCardIssuedValue)
        {
            hasSave = true;
            semester = Mathf.Max(1, semesterValue);
            currentDay = Mathf.Max(1, dayValue);
            idCardIssued = idCardIssuedValue;
            admissionCompleted = true;
            OnDayProgressChanged?.Invoke();
        }

        private IEnumerator HydrateRoutine()
        {
            isHydrating = true;
            EnsureDependencies();

            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                Debug.LogWarning("[PlayerSaveState] Cannot hydrate save yet: missing auth. Will retry when RefreshFromServer is called.");
                isHydrating = false;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;

            yield return apiClient.Get(
                SavePath,
                userSession.JwtToken,
                body =>
                {
                    succeeded = true;
                    responseBody = body;
                },
                (error, code) =>
                {
                    Debug.LogWarning($"[PlayerSaveState] GET save failed: {error} (HTTP {code})");
                });

            if (succeeded)
            {
                try
                {
                    ApiClient.PlayerSaveStatusDto status =
                        JsonUtility.FromJson<ApiClient.PlayerSaveStatusDto>(responseBody);
                    if (status != null && status.hasSave && status.save != null)
                    {
                        ApplySaveDto(status.save, hasSaveValue: true);
                    }
                    else
                    {
                        ClearLocalState();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayerSaveState] Failed to parse save status: {ex.Message}");
                    ClearLocalState();
                }
            }
            else
            {
                ClearLocalState();
            }

            isHydrated = true;
            isHydrating = false;
            OnHydrated?.Invoke();
            OnDayProgressChanged?.Invoke();
            Debug.Log(
                $"[PlayerSaveState] Hydrated: hasSave={hasSave}, idCardIssued={idCardIssued}, " +
                $"semester={semester}, currentDay={currentDay}, playerName={playerName}");
        }

        private void ApplySaveDto(ApiClient.PlayerSaveDto save, bool hasSaveValue)
        {
            hasSave = hasSaveValue;
            playerName = save.playerName;
            role = save.role;
            department = save.department;
            universityId = save.universityId;
            admissionCompleted = save.admissionCompleted;
            idCardIssued = save.idCardIssued;
            semester = Mathf.Max(1, save.semester);
            currentDay = Mathf.Max(1, save.currentDay);
        }

        private void ClearLocalState()
        {
            hasSave = false;
            idCardIssued = false;
            admissionCompleted = false;
            semester = 0;
            currentDay = 0;
            playerName = null;
            role = null;
            department = null;
            universityId = null;
        }

        private void EnsureDependencies()
        {
            if (apiClient != null && userSession != null)
            {
                return;
            }

            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            if (host == null)
            {
                return;
            }

            if (apiClient == null)
            {
                apiClient = host.ApiClient;
            }

            if (userSession == null && host.AuthManager != null)
            {
                userSession = host.AuthManager.Session;
            }
        }
    }
}
