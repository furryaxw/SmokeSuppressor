using System;
using System.Collections.Generic;

namespace SmokeSuppressor
{
    internal static class SmokeNativeExpressionMap
    {
        internal static bool TryGetProfile(
            string assetName,
            out SmokeNativeExpressionProfile profile)
        {
            if (string.Equals(
                    assetName,
                    "ExhaustSmoke",
                    StringComparison.Ordinal))
            {
                profile = new SmokeNativeExpressionProfile(
                    "ExhaustSmoke",
                    "System (4) -> System (5)",
                    174,
                    new[] { 170, 172 });
                return true;
            }

            if (string.Equals(
                    assetName,
                    "MediumCannonFire",
                    StringComparison.Ordinal))
            {
                profile = default;
                return false;
            }

            profile = default;
            return false;
        }

        internal static bool TryGetFloat2Profile(
            string assetName,
            out SmokeNativeFloat2ExpressionProfile profile)
        {
            if (string.Equals(
                    assetName,
                    "MediumCannonFire",
                    StringComparison.Ordinal))
            {
                profile = new SmokeNativeFloat2ExpressionProfile(
                    "MediumCannonFire",
                    "System (9) / Count",
                    148,
                    299,
                    7.0f,
                    7.0f,
                    new[] { 289, 294 });
                return true;
            }

            profile = default;
            return false;
        }

        internal static bool TryValidateCapturedEvidence(
            out string failure,
            out int sampleCount)
        {
            var samples = new List<ExpressionMapSample>
            {
                new(
                    "ExhaustSmoke",
                    "System (4) -> System (5)",
                    174,
                    new[] { 170, 172 },
                    true),
                new(
                    "MediumCannonFire",
                    string.Empty,
                    -1,
                    Array.Empty<int>(),
                    false),
                new(
                    "EngineFlame",
                    string.Empty,
                    -1,
                    Array.Empty<int>(),
                    false)
            };

            var float2Samples = new List<Float2ExpressionMapSample>
            {
                new(
                    "MediumCannonFire",
                    "System (9) / Count",
                    148,
                    299,
                    7.0f,
                    7.0f,
                    new[] { 289, 294 },
                    true),
                new(
                    "ExhaustSmoke",
                    string.Empty,
                    -1,
                    -1,
                    0.0f,
                    0.0f,
                    Array.Empty<int>(),
                    false),
                new(
                    "EngineFlame",
                    string.Empty,
                    -1,
                    -1,
                    0.0f,
                    0.0f,
                    Array.Empty<int>(),
                    false)
            };

            sampleCount = samples.Count + float2Samples.Count;
            foreach (ExpressionMapSample sample in samples)
            {
                bool found = TryGetProfile(
                    sample.AssetName,
                    out SmokeNativeExpressionProfile profile);
                if (found != sample.ExpectedFound)
                {
                    failure =
                        $"asset={sample.AssetName}," +
                        $"expectedFound={sample.ExpectedFound},actualFound={found}";
                    return false;
                }

                if (!found)
                    continue;

                if (!string.Equals(
                        profile.SystemPath,
                        sample.ExpectedSystemPath,
                        StringComparison.Ordinal) ||
                    profile.TargetValueIndex != sample.ExpectedValueIndex ||
                    !IndicesEqual(
                        profile.SentinelValueIndices,
                        sample.ExpectedSentinelValueIndices))
                {
                    failure =
                        $"asset={sample.AssetName}," +
                        $"systemPath={profile.SystemPath}," +
                        $"valueIndex={profile.TargetValueIndex}," +
                        $"sentinels=[{string.Join(",", profile.SentinelValueIndices)}]";
                    return false;
                }
            }

            foreach (Float2ExpressionMapSample sample in float2Samples)
            {
                bool found = TryGetFloat2Profile(
                    sample.AssetName,
                    out SmokeNativeFloat2ExpressionProfile profile);
                if (found != sample.ExpectedFound)
                {
                    failure =
                        $"asset={sample.AssetName},kind=float2," +
                        $"expectedFound={sample.ExpectedFound},actualFound={found}";
                    return false;
                }

                if (!found)
                    continue;

                if (!string.Equals(
                        profile.SystemPath,
                        sample.ExpectedSystemPath,
                        StringComparison.Ordinal) ||
                    profile.ExpressionIndex != sample.ExpectedExpressionIndex ||
                    profile.TargetValueIndex != sample.ExpectedValueIndex ||
                    profile.ExpectedX != sample.ExpectedX ||
                    profile.ExpectedY != sample.ExpectedY ||
                    !IndicesEqual(
                        profile.SentinelValueIndices,
                        sample.ExpectedSentinelValueIndices))
                {
                    failure =
                        $"asset={sample.AssetName},kind=float2," +
                        $"systemPath={profile.SystemPath}," +
                        $"expressionIndex={profile.ExpressionIndex}," +
                        $"valueIndex={profile.TargetValueIndex}," +
                        $"expected=({profile.ExpectedX:R},{profile.ExpectedY:R})," +
                        $"sentinels=[{string.Join(",", profile.SentinelValueIndices)}]";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool IndicesEqual(int[] left, int[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }

            return true;
        }

        private readonly struct ExpressionMapSample
        {
            public ExpressionMapSample(
                string assetName,
                string expectedSystemPath,
                int expectedValueIndex,
                int[] expectedSentinelValueIndices,
                bool expectedFound)
            {
                AssetName = assetName;
                ExpectedSystemPath = expectedSystemPath;
                ExpectedValueIndex = expectedValueIndex;
                ExpectedSentinelValueIndices =
                    expectedSentinelValueIndices;
                ExpectedFound = expectedFound;
            }

            public string AssetName { get; }
            public string ExpectedSystemPath { get; }
            public int ExpectedValueIndex { get; }
            public int[] ExpectedSentinelValueIndices { get; }
            public bool ExpectedFound { get; }
        }

        private readonly struct Float2ExpressionMapSample
        {
            public Float2ExpressionMapSample(
                string assetName,
                string expectedSystemPath,
                int expectedExpressionIndex,
                int expectedValueIndex,
                float expectedX,
                float expectedY,
                int[] expectedSentinelValueIndices,
                bool expectedFound)
            {
                AssetName = assetName;
                ExpectedSystemPath = expectedSystemPath;
                ExpectedExpressionIndex = expectedExpressionIndex;
                ExpectedValueIndex = expectedValueIndex;
                ExpectedX = expectedX;
                ExpectedY = expectedY;
                ExpectedSentinelValueIndices =
                    expectedSentinelValueIndices;
                ExpectedFound = expectedFound;
            }

            public string AssetName { get; }
            public string ExpectedSystemPath { get; }
            public int ExpectedExpressionIndex { get; }
            public int ExpectedValueIndex { get; }
            public float ExpectedX { get; }
            public float ExpectedY { get; }
            public int[] ExpectedSentinelValueIndices { get; }
            public bool ExpectedFound { get; }
        }
    }

    internal readonly struct SmokeNativeExpressionProfile
    {
        public SmokeNativeExpressionProfile(
            string assetName,
            string systemPath,
            int targetValueIndex,
            int[] sentinelValueIndices)
        {
            AssetName = assetName;
            SystemPath = systemPath;
            TargetValueIndex = targetValueIndex;
            SentinelValueIndices = sentinelValueIndices;
        }

        public string AssetName { get; }
        public string SystemPath { get; }
        public int TargetValueIndex { get; }
        public int[] SentinelValueIndices { get; }
    }

    internal readonly struct SmokeNativeFloat2ExpressionProfile
    {
        public SmokeNativeFloat2ExpressionProfile(
            string assetName,
            string systemPath,
            int expressionIndex,
            int targetValueIndex,
            float expectedX,
            float expectedY,
            int[] sentinelValueIndices)
        {
            AssetName = assetName;
            SystemPath = systemPath;
            ExpressionIndex = expressionIndex;
            TargetValueIndex = targetValueIndex;
            ExpectedX = expectedX;
            ExpectedY = expectedY;
            SentinelValueIndices = sentinelValueIndices;
        }

        public string AssetName { get; }
        public string SystemPath { get; }
        public int ExpressionIndex { get; }
        public int TargetValueIndex { get; }
        public float ExpectedX { get; }
        public float ExpectedY { get; }
        public int[] SentinelValueIndices { get; }
    }
}
