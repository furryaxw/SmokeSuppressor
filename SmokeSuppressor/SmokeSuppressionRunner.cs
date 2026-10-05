using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.VFX;

namespace SmokeSuppressor
{
    // 定期扫描场景里的 VisualEffect，按策略关闭堆积层的 shader pass。
    // 不挂钩具体效果类型，所以引擎、炮口与两种炮弹命中都覆盖得到。
    internal sealed class SmokeSuppressionRunner : MonoBehaviour
    {
        private const float ScanIntervalSeconds = 0.25f;
        private const string DiagnosticPrefix = "[SmokeSuppressor]";

        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("SmokeSuppressor");

        private readonly HashSet<string> loggedFailures = new(StringComparer.Ordinal);

        private SmokeSuppressorOptions? options;
        private float nextScanTime;

        public SmokeSuppressionRunner(IntPtr ptr) : base(ptr)
        {
        }

        [HideFromIl2Cpp]
        public void Configure(SmokeSuppressorOptions? configured) => options = configured;

        private void Update()
        {
            // 标定期间由探针独占输出开关。
            if (SmokeSuppressorMain.Instance is { CalibrationActive: true })
                return;

            float now = Time.unscaledTime;
            if (now < nextScanTime)
                return;

            nextScanTime = now + ScanIntervalSeconds;
            Apply();
        }

        private void Apply()
        {
            try
            {
                foreach (VisualEffect visualEffect in
                         UnityEngine.Object.FindObjectsOfType<VisualEffect>())
                {
                    if (visualEffect == null)
                        continue;

                    Renderer? renderer = visualEffect.GetComponent<Renderer>();
                    if (renderer == null)
                        continue;

                    ApplyToRenderer(renderer);
                }
            }
            catch (Exception exception)
            {
                LogFailure("scan", exception.ToString());
            }
        }

        private void ApplyToRenderer(Renderer renderer)
        {
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material>?
                materials;
            try
            {
                materials = renderer.sharedMaterials;
            }
            catch (Exception)
            {
                return;
            }

            if (materials == null)
                return;

            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null)
                    continue;

                string shaderName = GetShaderName(material);
                if (shaderName.Length == 0)
                    continue;

                foreach (SmokeLayer layer in AllLayers)
                {
                    if (!SmokeSuppressionPolicy.Matches(layer, shaderName))
                        continue;
                    if (!SmokeSuppressionPolicy.ShouldHide(layer, options))
                        continue;

                    DisableOutput(material, layer, shaderName);
                }
            }
        }

        private void DisableOutput(Material material, SmokeLayer layer, string shaderName)
        {
            try
            {
                string passName = material.GetPassName(0) ?? string.Empty;
                if (passName.Length == 0)
                    return;

                material.SetShaderPassEnabled(passName, false);
            }
            catch (Exception exception)
            {
                LogFailure($"{layer}:{shaderName}", exception.Message);
            }
        }

        private static string GetShaderName(Material material)
        {
            try
            {
                Shader? shader = material.shader;
                return shader == null ? string.Empty : (shader.name ?? string.Empty);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void LogFailure(string category, string failure)
        {
            if (!loggedFailures.Add($"{category}|{failure}"))
                return;

            Log.LogError(
                $"{DiagnosticPrefix} suppression failed " +
                $"category={category},error={failure}");
        }

        private static readonly SmokeLayer[] AllLayers =
        {
            SmokeLayer.EngineAccumulation,
            SmokeLayer.MuzzleAccumulation,
            SmokeLayer.GroundImpactSmoke,
            SmokeLayer.ArmorImpactSmoke
        };
    }
}
