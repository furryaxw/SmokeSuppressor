using System;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.VFX;

namespace SmokeSuppressor
{
    // 在 VisualEffect 开始播放时立刻按策略关闭堆积层的 shader pass。
    //
    // VFX 的输出材质要等首次渲染才生成，所以播放瞬间可能还拿不到材质；那种情况下只对
    // 这一个实例重试有限帧，而不是全局轮询场景。
    internal static class SmokeSuppressionRuntime
    {
        private const int MaximumRetryFrames = 8;
        private const string DiagnosticPrefix = "[SmokeSuppressor]";

        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("SmokeSuppressor");

        private static readonly Dictionary<int, PendingEffect> Pending = new();
        private static readonly List<int> StaleKeys = new();
        private static readonly HashSet<string> LoggedFailures = new(StringComparer.Ordinal);

        private static readonly SmokeLayer[] AllLayers =
        {
            SmokeLayer.EngineAccumulation,
            SmokeLayer.MuzzleAccumulation,
            SmokeLayer.GroundImpactSmoke,
            SmokeLayer.ArmorImpactSmoke
        };

        internal static SmokeSuppressorOptions? Options { get; set; }

        // 由 VisualEffect.Play 的 postfix 调用。
        internal static void ObserveEffect(VisualEffect? visualEffect)
        {
            if (visualEffect == null)
                return;

            if (TryApply(visualEffect))
                return;

            Pending[visualEffect.GetInstanceID()] =
                new PendingEffect(visualEffect, MaximumRetryFrames);
        }

        internal static void Tick()
        {
            if (Pending.Count == 0)
                return;

            StaleKeys.Clear();
            foreach (KeyValuePair<int, PendingEffect> pair in Pending)
            {
                PendingEffect entry = pair.Value;
                if (entry.Effect == null ||
                    entry.AttemptsLeft <= 1 ||
                    TryApply(entry.Effect))
                {
                    StaleKeys.Add(pair.Key);
                    continue;
                }

                Pending[pair.Key] =
                    new PendingEffect(entry.Effect, entry.AttemptsLeft - 1);
            }

            foreach (int key in StaleKeys)
                Pending.Remove(key);
        }

        internal static void Reset()
        {
            Pending.Clear();
            LoggedFailures.Clear();
        }

        private static bool TryApply(VisualEffect visualEffect)
        {
            Renderer? renderer;
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material>?
                materials;
            try
            {
                renderer = visualEffect.GetComponent<Renderer>();
                if (renderer == null)
                    return false;

                materials = renderer.sharedMaterials;
            }
            catch (Exception exception)
            {
                LogFailure("renderer", exception.Message);
                return false;
            }

            if (materials == null || materials.Length == 0)
                return false;

            bool applied = false;
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
                    if (!SmokeSuppressionPolicy.ShouldHide(layer, Options))
                        continue;

                    applied |= DisableOutput(material, layer, shaderName);
                }
            }

            return applied;
        }

        private static bool DisableOutput(
            Material material,
            SmokeLayer layer,
            string shaderName)
        {
            try
            {
                string passName = material.GetPassName(0) ?? string.Empty;
                if (passName.Length == 0)
                    return false;

                material.SetShaderPassEnabled(passName, false);
                return true;
            }
            catch (Exception exception)
            {
                LogFailure($"{layer}:{shaderName}", exception.Message);
                return false;
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

        private static void LogFailure(string category, string failure)
        {
            if (!LoggedFailures.Add($"{category}|{failure}"))
                return;

            Log.LogError(
                $"{DiagnosticPrefix} suppression failed " +
                $"category={category},error={failure}");
        }

        private readonly struct PendingEffect
        {
            internal PendingEffect(VisualEffect effect, int attemptsLeft)
            {
                Effect = effect;
                AttemptsLeft = attemptsLeft;
            }

            internal VisualEffect Effect { get; }

            internal int AttemptsLeft { get; }
        }
    }

    // 每一帧只推进刚播放的那些实例；没有待处理实例时不做事。
    internal sealed class SmokeSuppressionRunner : MonoBehaviour
    {
        public SmokeSuppressionRunner(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            // 标定期间由探针独占输出开关。
            if (SmokeSuppressorMain.Instance is { CalibrationActive: true })
                return;

            SmokeSuppressionRuntime.Tick();
        }

        private void OnDestroy() => SmokeSuppressionRuntime.Reset();
    }

    [HarmonyPatch(typeof(VisualEffect), "Play", new Type[0])]
    internal static class VisualEffectPlayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(VisualEffect __instance)
            => SmokeSuppressionRuntime.ObserveEffect(__instance);
    }
}
