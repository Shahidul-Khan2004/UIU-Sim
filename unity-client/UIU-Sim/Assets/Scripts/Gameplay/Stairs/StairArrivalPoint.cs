using UnityEngine;

namespace UIU.Simulator.Gameplay.Stairs
{
    public sealed class StairArrivalPoint : MonoBehaviour
    {
        [Header("Staircase Settings")]
        [SerializeField] private int staircaseId = 1;

        [SerializeField, Range(0, 10)]
        private int arrivingFromFloor = 0;

        public int StaircaseId => staircaseId;
        public int ArrivingFromFloor => arrivingFromFloor;
    }
}
