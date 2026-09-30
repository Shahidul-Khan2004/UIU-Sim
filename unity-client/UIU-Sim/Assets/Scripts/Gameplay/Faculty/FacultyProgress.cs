using System;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Faculty
{
    public enum FacultyUpdateSource
    {
        InitialHydration,
        GameplayMutation
    }

    /// <summary>
    /// Local source of truth for faculty Reputation. Independent of student Aura / Academic Reputation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyProgress : MonoBehaviour
    {
        public const int DefaultReputation = 50;

        public static FacultyProgress Instance { get; private set; }

        private int reputation = DefaultReputation;
        private bool facultyIdIssued;
        private bool computerUsed;
        private bool officeEntered;
        private bool classroomScanned;
        private bool lectureCompleted;
        private bool lectureLeft;
        private bool icsMaterialPrepared;
        private bool dmMaterialPrepared;
        private bool icsCompleted;
        private bool dmCompleted;
        private bool coffeeClaimedForCurrentDay;
        private string coffeeOption;

        public int Reputation => reputation;
        public bool FacultyIdIssued => facultyIdIssued;
        public bool ComputerUsed => computerUsed;
        public bool OfficeEntered => officeEntered;
        public bool ClassroomScanned => classroomScanned;
        public bool LectureCompleted => lectureCompleted;
        public bool LectureLeft => lectureLeft;
        public bool IcsMaterialPrepared => icsMaterialPrepared;
        public bool DmMaterialPrepared => dmMaterialPrepared;
        public bool IcsCompleted => icsCompleted;
        public bool DmCompleted => dmCompleted;
        public bool CourseMaterialsPrepared => icsMaterialPrepared && dmMaterialPrepared;
        public bool OfficeSetup => computerUsed || officeEntered;
        public bool FirstClassTaught => icsCompleted;
        public int CompletedAssignedCount => (icsCompleted ? 1 : 0) + (dmCompleted ? 1 : 0);
        public bool BothClassesCompleted => icsCompleted && dmCompleted;
        public bool CoffeeClaimedForCurrentDay => coffeeClaimedForCurrentDay;
        public string CoffeeOption => coffeeOption;

        public event Action<int, FacultyUpdateSource> OnReputationUpdated;
        public event Action OnObjectivesChanged;

        public static FacultyProgress EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyProgress existing = FindFirstObjectByType<FacultyProgress>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("FacultyProgress");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<FacultyProgress>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ResetToDefaults()
        {
            ApplyServerState(
                DefaultReputation,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                FacultyUpdateSource.InitialHydration,
                false,
                false,
                false,
                false,
                null);
        }

        public void SetFacultyIdIssued(bool issued)
        {
            if (facultyIdIssued == issued)
            {
                return;
            }

            facultyIdIssued = issued;
            OnObjectivesChanged?.Invoke();
        }

        public void ApplyServerState(
            int newReputation,
            bool newComputerUsed,
            bool newOfficeEntered,
            bool newClassroomScanned,
            bool newLectureCompleted,
            bool newLectureLeft,
            bool newIcsMaterialPrepared,
            bool newDmMaterialPrepared,
            FacultyUpdateSource source,
            bool newIcsCompleted = false,
            bool newDmCompleted = false,
            bool newFacultyIdIssued = false,
            bool newCoffeeClaimedForCurrentDay = false,
            string newCoffeeOption = null)
        {
            int previousReputation = reputation;
            reputation = Mathf.Clamp(newReputation, 0, 100);
            facultyIdIssued = newFacultyIdIssued;
            computerUsed = newComputerUsed;
            officeEntered = newOfficeEntered;
            classroomScanned = newClassroomScanned;
            lectureCompleted = newLectureCompleted;
            lectureLeft = newLectureLeft;
            icsMaterialPrepared = newIcsMaterialPrepared;
            dmMaterialPrepared = newDmMaterialPrepared;
            icsCompleted = newIcsCompleted;
            dmCompleted = newDmCompleted;
            coffeeClaimedForCurrentDay = newCoffeeClaimedForCurrentDay;
            coffeeOption = newCoffeeClaimedForCurrentDay ? newCoffeeOption : null;

            OnReputationUpdated?.Invoke(reputation, source);
            OnObjectivesChanged?.Invoke();

            Debug.Log(
                $"[FacultyProgress] ApplyServerState ({source}): Reputation {previousReputation} → {reputation}");
        }

        /// <summary>Test seam for local HUD / objective checks without networking.</summary>
        public void SetStateForTesting(
            int reputationValue,
            bool computerUsedValue = false,
            bool officeEnteredValue = false,
            bool classroomScannedValue = false,
            bool lectureCompletedValue = false,
            bool lectureLeftValue = false,
            bool icsPreparedValue = false,
            bool dmPreparedValue = false,
            FacultyUpdateSource source = FacultyUpdateSource.InitialHydration,
            bool icsCompletedValue = false,
            bool dmCompletedValue = false,
            bool facultyIdIssuedValue = false,
            bool coffeeClaimedForCurrentDayValue = false,
            string coffeeOptionValue = null)
        {
            ApplyServerState(
                reputationValue,
                computerUsedValue,
                officeEnteredValue,
                classroomScannedValue,
                lectureCompletedValue,
                lectureLeftValue,
                icsPreparedValue,
                dmPreparedValue,
                source,
                icsCompletedValue,
                dmCompletedValue,
                facultyIdIssuedValue,
                coffeeClaimedForCurrentDayValue,
                coffeeOptionValue);
        }

        public bool IsCourseCompleted(string courseCode)
        {
            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return false;
            }

            if (string.Equals(courseCode.Trim(), "ICS", StringComparison.OrdinalIgnoreCase))
            {
                return icsCompleted;
            }

            if (string.Equals(courseCode.Trim(), "DM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(courseCode.Trim(), "DISCRETE_MATHEMATICS", StringComparison.OrdinalIgnoreCase))
            {
                return dmCompleted;
            }

            return false;
        }
    }
}
