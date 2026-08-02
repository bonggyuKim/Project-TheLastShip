using System;
using UnityEngine;

namespace DoodleUp.Core
{
    public enum Du02TaskId
    {
        T1Horizontal = 1,
        T2Rising = 2,
        T3Bridge = 3
    }

    [Serializable]
    public readonly struct Du02LaneDefinition
    {
        public readonly Du02TaskId TaskId;
        public readonly Vector3 Origin;
        public readonly Vector3 SpawnPosition;
        public readonly Vector3 StartCenter;
        public readonly Vector3 StartSize;
        public readonly Vector3 GoalCenter;
        public readonly Vector3 GoalSize;
        public readonly float EdgeGap;
        public readonly float ContactBandWidth;

        public Du02LaneDefinition(
            Du02TaskId taskId,
            Vector3 origin,
            Vector3 spawnPosition,
            Vector3 startCenter,
            Vector3 startSize,
            Vector3 goalCenter,
            Vector3 goalSize,
            float edgeGap,
            float contactBandWidth)
        {
            TaskId = taskId;
            Origin = origin;
            SpawnPosition = spawnPosition;
            StartCenter = startCenter;
            StartSize = startSize;
            GoalCenter = goalCenter;
            GoalSize = goalSize;
            EdgeGap = edgeGap;
            ContactBandWidth = contactBandWidth;
        }
    }

    public static class Du02CourseDefinition
    {
        private static readonly Vector3 LedgeSize = new Vector3(1.00f, 0.20f, 2.00f);
        private static readonly Vector3 SpawnOffset = new Vector3(-0.20f, LedgeSize.y * 0.5f, 0.00f);

        public static Du02LaneDefinition Get(Du02TaskId id)
        {
            var origin = id switch
            {
                Du02TaskId.T1Horizontal => new Vector3(0f, 0f, -4f),
                Du02TaskId.T2Rising => new Vector3(0f, 0f, 0f),
                Du02TaskId.T3Bridge => new Vector3(0f, 0f, 4f),
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
            };

            var startCenter = origin;
            var startRightEdge = startCenter.x + LedgeSize.x * 0.5f;
            Vector3 goalCenter;
            float gap;

            switch (id)
            {
                case Du02TaskId.T1Horizontal:
                    gap = Du02Profile.T1Gap;
                    goalCenter = new Vector3(startRightEdge + gap + LedgeSize.x * 0.5f, origin.y, origin.z);
                    break;
                case Du02TaskId.T2Rising:
                    gap = 0f;
                    goalCenter = origin + new Vector3(Du02Profile.T2HorizontalOffset, Du02Profile.T2VerticalOffset, 0f);
                    break;
                case Du02TaskId.T3Bridge:
                    gap = Du02Profile.T3Gap;
                    goalCenter = new Vector3(startRightEdge + gap + LedgeSize.x * 0.5f, origin.y, origin.z);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }

            return new Du02LaneDefinition(
                id,
                origin,
                origin + SpawnOffset,
                startCenter,
                LedgeSize,
                goalCenter,
                LedgeSize,
                gap,
                id == Du02TaskId.T3Bridge ? Du02Profile.T3ContactBandWidth : 0f);
        }
    }
}
