using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Building.Data
{
    public enum DoorSide
    {
        None,
        North,
        South,
        East,
        West
    }

    [Serializable]
    public sealed class ZoneDefinition
    {
        [SerializeField] private string zoneName = "New Zone";
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 size = new Vector3(10f, 3.5f, 10f);

        public string Name => zoneName;
        public Vector3 Position => position;
        public Vector3 Size => size;
    }

    [Serializable]
    public sealed class RoomDefinition
    {
        [SerializeField] private string roomName = "New Room";
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 size = new Vector3(10f, 3.5f, 10f);

        [Header("Door Opening")]
        [SerializeField] private DoorSide doorSide = DoorSide.South;
        [SerializeField, Min(0f)] private float doorOffset;
        [SerializeField, Min(0.8f)] private float doorWidth = 1.8f;

        public string Name => roomName;
        public Vector3 Position => position;
        public Vector3 Size => size;
        public DoorSide DoorSide => doorSide;
        public float DoorOffset => doorOffset;
        public float DoorWidth => doorWidth;
    }

    [Serializable]
    public sealed class VerticalConnectionDefinition
    {
        [SerializeField] private string connectionId = "Connection";
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 size = new Vector3(3f, 3.5f, 3f);

        public string ConnectionId => connectionId;
        public Vector3 Position => position;
        public Vector3 Size => size;
    }

    /// <summary>
    /// Authoring data for one independently generated building floor.
    /// All positions are local to the shared building coordinate system.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FloorLayout",
        menuName = "UIU Simulator/Building/Floor Layout")]
    public sealed class FloorLayoutData : ScriptableObject
    {
        [Header("Floor Information")]
        [SerializeField] private string floorName = "New Floor";
        [SerializeField, Min(0f)] private float baseElevation;
        [SerializeField, Min(1f)] private float ceilingHeight = 3.5f;
        [SerializeField, Min(0.05f)] private float wallThickness = 0.25f;
        [SerializeField, Min(0.05f)] private float floorSlabThickness = 0.2f;
        [SerializeField] private Vector2 footprintSize = new Vector2(100f, 90f);

        [Header("Entrance")]
        [SerializeField] private Vector3 entrancePosition;
        [SerializeField, Min(0.8f)] private float entranceWidth = 4f;
        [SerializeField] private Vector2 entranceApproachSize = new Vector2(20f, 12f);

        [Header("Layout")]
        [SerializeField] private List<ZoneDefinition> zones = new List<ZoneDefinition>();
        [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

        [Header("Vertical Connections")]
        [SerializeField] private List<VerticalConnectionDefinition> elevators = new List<VerticalConnectionDefinition>();
        [SerializeField] private List<VerticalConnectionDefinition> stairs = new List<VerticalConnectionDefinition>();

        public string FloorName => floorName;
        public float BaseElevation => baseElevation;
        public float CeilingHeight => ceilingHeight;
        public float WallThickness => wallThickness;
        public float FloorSlabThickness => floorSlabThickness;
        public Vector2 FootprintSize => footprintSize;
        public Vector3 EntrancePosition => entrancePosition;
        public float EntranceWidth => entranceWidth;
        public Vector2 EntranceApproachSize => entranceApproachSize;
        public IReadOnlyList<ZoneDefinition> Zones => zones;
        public IReadOnlyList<RoomDefinition> Rooms => rooms;
        public IReadOnlyList<VerticalConnectionDefinition> Elevators => elevators;
        public IReadOnlyList<VerticalConnectionDefinition> Stairs => stairs;

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(floorName))
            {
                errors.Add("Floor name is required.");
            }

            if (ceilingHeight <= 0f)
            {
                errors.Add("Ceiling height must be greater than zero.");
            }

            if (wallThickness <= 0f)
            {
                errors.Add("Wall thickness must be greater than zero.");
            }

            if (footprintSize.x <= 0f || footprintSize.y <= 0f)
            {
                errors.Add("Footprint width and depth must be greater than zero.");
            }

            if (entranceWidth <= 0f || entranceWidth >= footprintSize.x)
            {
                errors.Add("Entrance width must be positive and narrower than the footprint.");
            }

            ValidateZones(errors);
            ValidateRooms(errors);
            ValidateConnections(elevators, "Elevator", errors);
            ValidateConnections(stairs, "Stair", errors);
            return errors;
        }

        private void OnValidate()
        {
            ceilingHeight = Mathf.Max(1f, ceilingHeight);
            wallThickness = Mathf.Max(0.05f, wallThickness);
            floorSlabThickness = Mathf.Max(0.05f, floorSlabThickness);
            footprintSize.x = Mathf.Max(1f, footprintSize.x);
            footprintSize.y = Mathf.Max(1f, footprintSize.y);
            entranceWidth = Mathf.Clamp(entranceWidth, 0.8f, footprintSize.x - 0.1f);
            entranceApproachSize.x = Mathf.Max(1f, entranceApproachSize.x);
            entranceApproachSize.y = Mathf.Max(1f, entranceApproachSize.y);
        }

        private void ValidateZones(ICollection<string> errors)
        {
            for (int index = 0; index < zones.Count; index++)
            {
                ZoneDefinition zone = zones[index];
                if (zone == null)
                {
                    errors.Add($"Zone {index} is missing.");
                    continue;
                }

                ValidateArea(zone.Name, zone.Size, $"Zone {index}", errors);
            }
        }

        private void ValidateRooms(ICollection<string> errors)
        {
            for (int index = 0; index < rooms.Count; index++)
            {
                RoomDefinition room = rooms[index];
                if (room == null)
                {
                    errors.Add($"Room {index} is missing.");
                    continue;
                }

                ValidateArea(room.Name, room.Size, $"Room {index}", errors);

                float wallLength = room.DoorSide == DoorSide.East || room.DoorSide == DoorSide.West
                    ? room.Size.z
                    : room.Size.x;
                if (room.DoorSide != DoorSide.None && room.DoorWidth >= wallLength)
                {
                    errors.Add($"Room '{room.Name}' door must be narrower than its wall.");
                }
            }
        }

        private static void ValidateArea(
            string areaName,
            Vector3 size,
            string label,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(areaName))
            {
                errors.Add($"{label} name is required.");
            }

            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
            {
                errors.Add($"{label} size must be positive on every axis.");
            }
        }

        private static void ValidateConnections(
            IReadOnlyList<VerticalConnectionDefinition> connections,
            string label,
            ICollection<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < connections.Count; index++)
            {
                VerticalConnectionDefinition connection = connections[index];
                if (connection == null)
                {
                    errors.Add($"{label} {index} is missing.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(connection.ConnectionId))
                {
                    errors.Add($"{label} {index} connection ID is required.");
                }
                else if (!ids.Add(connection.ConnectionId))
                {
                    errors.Add($"Duplicate {label.ToLowerInvariant()} ID '{connection.ConnectionId}'.");
                }

                if (connection.Size.x <= 0f || connection.Size.y <= 0f || connection.Size.z <= 0f)
                {
                    errors.Add($"{label} '{connection.ConnectionId}' size must be positive on every axis.");
                }
            }
        }
    }
}
