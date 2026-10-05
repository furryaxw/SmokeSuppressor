using System;
using System.Collections.Generic;

namespace SmokeSuppressor
{
    internal static class SmokeAccumulationOutputMap
    {
        internal static bool IsAccumulationOutput(
            string assetName,
            string materialName)
        {
            if (string.Equals(
                    assetName,
                    "ExhaustSmoke",
                    StringComparison.OrdinalIgnoreCase))
            {
                return materialName.Contains(
                    "/ExhaustSmoke/System (5)/",
                    StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(
                    assetName,
                    "MediumCannonFire",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Runtime material mutations had no visual effect, and the
                // valueIndex 289 probe suppressed several base smoke systems.
                // No MediumCannonFire material is currently validated as an
                // accumulation-only output.
                return false;
            }

            return false;
        }

        internal static bool TryValidateCapturedEvidence(
            out string failure,
            out int sampleCount)
        {
            var samples = new List<OutputMapSample>
            {
                new(
                    "ExhaustSmoke",
                    "Hidden/VFX/ExhaustSmoke/System (5)/Output Particle Octagon",
                    true),
                new(
                    "ExhaustSmoke",
                    "Hidden/VFX/ExhaustSmoke/System (1)/Output Particle Octagon",
                    false),
                new(
                    "ExhaustSmoke",
                    "Hidden/VFX/ExhaustSmoke/System (2)/Output Particle HDRP Distortion Quad",
                    false),
                new(
                    "MediumCannonFire",
                    "Hidden/VFX/MediumCannonFire/System (14)/Output Particle Octagon",
                    false),
                new(
                    "MediumCannonFire",
                    "Hidden/VFX/MediumCannonFire/System (1)/Output Particle Quad",
                    false),
                new(
                    "MediumCannonFire",
                    "Hidden/VFX/MediumCannonFire/System (5)/Output Particle Quad",
                    false),
                new(
                    "MediumCannonFire",
                    "Hidden/VFX/MediumCannonFire/System (10)/Output Particle Octagon",
                    false),
                new(
                    "MediumCannonFire",
                    "Hidden/VFX/MediumCannonFire/System (4)/Output Particle HDRP Distortion Quad",
                    false),
                new(
                    "EngineFlame",
                    "Hidden/VFX/EngineFlame/System (5)/Output Particle Octagon",
                    false)
            };

            sampleCount = samples.Count;
            foreach (OutputMapSample sample in samples)
            {
                bool actual = IsAccumulationOutput(
                    sample.AssetName,
                    sample.MaterialName);
                if (actual == sample.Expected)
                    continue;

                failure =
                    $"asset={sample.AssetName},material={sample.MaterialName}," +
                    $"expected={sample.Expected},actual={actual}";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private readonly struct OutputMapSample
        {
            public OutputMapSample(
                string assetName,
                string materialName,
                bool expected)
            {
                AssetName = assetName;
                MaterialName = materialName;
                Expected = expected;
            }

            public string AssetName { get; }
            public string MaterialName { get; }
            public bool Expected { get; }
        }
    }
}
