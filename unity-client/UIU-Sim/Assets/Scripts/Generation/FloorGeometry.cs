using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Building.Generation
{
    [Serializable]
    public readonly struct WallSegment
    {
        public WallSegment(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = size;
        }

        public Vector3 Center { get; }
        public Vector3 Size { get; }
    }

    /// <summary>
    /// Deterministic geometry calculations shared by the editor generator and tests.
    /// </summary>
    public static class FloorGeometry
    {
        public static IReadOnlyList<WallSegment> CreateHorizontalWallWithOpening(
            float wallCenterX,
            float wallLength,
            float wallZ,
            float baseY,
            float wallHeight,
            float wallThickness,
            float openingCenterX,
            float openingWidth)
        {
            float wallMin = wallCenterX - wallLength * 0.5f;
            float wallMax = wallCenterX + wallLength * 0.5f;
            float openingMin = Mathf.Clamp(openingCenterX - openingWidth * 0.5f, wallMin, wallMax);
            float openingMax = Mathf.Clamp(openingCenterX + openingWidth * 0.5f, wallMin, wallMax);

            var segments = new List<WallSegment>(2);
            AddHorizontalSegment(segments, wallMin, openingMin, wallZ, baseY, wallHeight, wallThickness);
            AddHorizontalSegment(segments, openingMax, wallMax, wallZ, baseY, wallHeight, wallThickness);
            return segments;
        }

        public static IReadOnlyList<WallSegment> CreateVerticalWallWithOpening(
            float wallX,
            float wallCenterZ,
            float wallLength,
            float baseY,
            float wallHeight,
            float wallThickness,
            float openingCenterZ,
            float openingWidth)
        {
            float wallMin = wallCenterZ - wallLength * 0.5f;
            float wallMax = wallCenterZ + wallLength * 0.5f;
            float openingMin = Mathf.Clamp(openingCenterZ - openingWidth * 0.5f, wallMin, wallMax);
            float openingMax = Mathf.Clamp(openingCenterZ + openingWidth * 0.5f, wallMin, wallMax);

            var segments = new List<WallSegment>(2);
            AddVerticalSegment(segments, wallX, wallMin, openingMin, baseY, wallHeight, wallThickness);
            AddVerticalSegment(segments, wallX, openingMax, wallMax, baseY, wallHeight, wallThickness);
            return segments;
        }

        private static void AddHorizontalSegment(
            ICollection<WallSegment> segments,
            float minX,
            float maxX,
            float z,
            float baseY,
            float height,
            float thickness)
        {
            float length = maxX - minX;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            segments.Add(new WallSegment(
                new Vector3((minX + maxX) * 0.5f, baseY + height * 0.5f, z),
                new Vector3(length, height, thickness)));
        }

        private static void AddVerticalSegment(
            ICollection<WallSegment> segments,
            float x,
            float minZ,
            float maxZ,
            float baseY,
            float height,
            float thickness)
        {
            float length = maxZ - minZ;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            segments.Add(new WallSegment(
                new Vector3(x, baseY + height * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(thickness, height, length)));
        }
    }
}
