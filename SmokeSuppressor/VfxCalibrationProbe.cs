using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.VFX;

namespace SmokeSuppressor
{
    // 输出层标定：候选是 VisualEffect 生成的材质（`Hidden/VFX/<Asset>/System (N)/...`）。
    // F6 进出标定模式，F7 下一条候选，F8 确认。
    //
    // 进入后关闭全部候选输出，只把当前候选的 shader pass 打开，于是屏幕上只剩它对应的效果；
    // 退出时全部还原。确认结果只写日志，由开发者写回源码常量。
    internal sealed class VfxCalibrationProbe : MonoBehaviour
    {
        private const string DiagnosticPrefix = "[CSC]";

        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("SmokeSuppressor");

        // 输出身份是材质的 shader 名：同一个输出在不同实例上是不同的 Material 对象，
        // 因此按 shader 名去重，并把引用更新到最新实例。
        private readonly Dictionary<string, Candidate> candidatesByOutput =
            new(StringComparer.Ordinal);
        private readonly List<Candidate> candidates = new();

        private bool active;
        private int currentIndex = -1;
        private string status = "idle";

        public VfxCalibrationProbe(IntPtr ptr) : base(ptr)
        {
        }

        internal bool Active => active;

        // 宿主在每次效果触发时调用，把该实例的输出注册为候选。
        [HideFromIl2Cpp]
        internal void Observe(Component effect)
        {
            if (!active || effect == null)
                return;

            try
            {
                VisualEffect? visualEffect =
                    effect.GetComponentInChildren<VisualEffect>(true);
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    return;

                string assetName = visualEffect.visualEffectAsset.name ?? string.Empty;
                if (assetName.Length == 0)
                    return;

                Renderer? renderer = visualEffect.GetComponent<Renderer>();
                if (renderer == null)
                    return;

                int before = candidatesByOutput.Count;
                RegisterOutputs(assetName, renderer);
                RebuildFlatList();
                if (candidatesByOutput.Count != before)
                {
                    status = $"asset={assetName} 输出 {candidatesByOutput.Count} 个";
                    Log.LogInfo(
                        $"{DiagnosticPrefix} calibration-outputs " +
                        $"asset={assetName} total={candidatesByOutput.Count}");
                }
            }
            catch (Exception exception)
            {
                status = $"注册失败: {exception.Message}";
            }
        }

        [HideFromIl2Cpp]
        private void RegisterOutputs(string assetName, Renderer renderer)
        {
            var materials = new List<Material>();
            try
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                        materials.Add(material);
                }
            }
            catch (Exception)
            {
                return;
            }

            for (int materialIndex = 0; materialIndex < materials.Count; materialIndex++)
            {
                Material material = materials[materialIndex];
                string shaderName = GetShaderName(material);
                if (!IsVfxGeneratedOutput(shaderName))
                    continue;

                candidatesByOutput[shaderName] = new Candidate(
                    assetName,
                    renderer,
                    materialIndex,
                    shaderName,
                    GetFirstPassName(material));
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

        // VFX 生成的输出材质都挂在 `Hidden/VFX/` 下。
        private static bool IsVfxGeneratedOutput(string shaderName)
            => shaderName.StartsWith("Hidden/VFX/", StringComparison.Ordinal);

        private static string GetFirstPassName(Material material)
        {
            try
            {
                return material.GetPassName(0) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void RebuildFlatList()
        {
            candidates.Clear();
            candidates.AddRange(candidatesByOutput.Values);
            candidates.Sort(static (left, right) =>
            {
                int byAsset = string.Compare(
                    left.AssetName,
                    right.AssetName,
                    StringComparison.Ordinal);
                return byAsset != 0
                    ? byAsset
                    : string.Compare(left.ShaderName, right.ShaderName, StringComparison.Ordinal);
            });
        }

        private void Update()
        {
            UnityEngine.InputSystem.Keyboard? keyboard =
                UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                ToggleActive();
                return;
            }

            if (!active)
                return;

            if (keyboard.f7Key.wasPressedThisFrame)
                NextCandidate();
            else if (keyboard.f8Key.wasPressedThisFrame)
                Confirm();
        }

        private void OnGUI()
        {
            if (!active)
                return;

            try
            {
                GUI.Label(new Rect(24f, 24f, 1400f, 260f), BuildOverlayText());
            }
            catch (Exception)
            {
                // IMGUI 失败不应影响游戏。
            }
        }

        private string BuildOverlayText()
        {
            string header =
                "[SmokeSuppressor 标定] F6 退出  F7 下一条  F8 确认\n" +
                $"候选 {Math.Max(currentIndex + 1, 0)}/{candidates.Count}  {status}\n";

            if (currentIndex < 0 || currentIndex >= candidates.Count)
                return header + "等待效果触发以采集输出（开引擎 / 开炮）";

            Candidate candidate = candidates[currentIndex];
            return header +
                $"asset={candidate.AssetName}\n" +
                $"output={candidate.ShaderName}\n" +
                $"materialIndex={candidate.MaterialIndex} pass={candidate.PassName}";
        }

        private void ToggleActive()
        {
            active = !active;
            if (!active)
            {
                RestoreAll();
                status = "已退出标定，输出已还原";
                Log.LogInfo($"{DiagnosticPrefix} calibration-exit candidates={candidates.Count}");
                return;
            }

            MuteAll();
            currentIndex = candidates.Count > 0 ? 0 : -1;
            if (currentIndex >= 0)
                ApplyCurrent();

            status = candidates.Count == 0
                ? "已进入标定，等待效果触发"
                : "已进入标定：其余输出已关闭";
            Log.LogInfo($"{DiagnosticPrefix} calibration-enter candidates={candidates.Count}");
        }

        private void NextCandidate()
        {
            if (candidates.Count == 0)
            {
                status = "还没有候选：先让效果触发一次";
                return;
            }

            currentIndex = (currentIndex + 1) % candidates.Count;
            ApplyCurrent();
        }

        private void Confirm()
        {
            if (currentIndex < 0 || currentIndex >= candidates.Count)
            {
                status = "没有可确认的候选";
                return;
            }

            Candidate candidate = candidates[currentIndex];
            Log.LogInfo(
                $"{DiagnosticPrefix} CALIBRATED " +
                $"asset={candidate.AssetName} " +
                $"output={candidate.ShaderName} " +
                $"materialIndex={candidate.MaterialIndex} " +
                $"pass={candidate.PassName} " +
                $"candidate={currentIndex + 1}/{candidates.Count}");
            status = $"已确认 {candidate.ShaderName}（见日志 CALIBRATED）";
        }

        // 关掉其余输出，只打开当前候选：屏幕上只剩它对应的效果。
        private void ApplyCurrent()
        {
            MuteAll();
            Candidate candidate = candidates[currentIndex];
            SetEnabled(candidate, enabled: true);
            status = $"只显示 {candidate.ShaderName}";
            Log.LogInfo(
                $"{DiagnosticPrefix} calibration-select " +
                $"asset={candidate.AssetName} " +
                $"output={candidate.ShaderName} " +
                $"materialIndex={candidate.MaterialIndex} " +
                $"candidate={currentIndex + 1}/{candidates.Count}");
        }

        private void MuteAll()
        {
            foreach (Candidate candidate in candidates)
                SetEnabled(candidate, enabled: false);
        }

        private void RestoreAll()
        {
            foreach (Candidate candidate in candidates)
                SetEnabled(candidate, enabled: true);
        }

        private static void SetEnabled(Candidate candidate, bool enabled)
        {
            try
            {
                Renderer? renderer = candidate.Renderer;
                if (renderer == null)
                    return;

                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material>?
                    materials = renderer.sharedMaterials;
                if (materials == null ||
                    candidate.MaterialIndex < 0 ||
                    candidate.MaterialIndex >= materials.Length)
                {
                    return;
                }

                Material material = materials[candidate.MaterialIndex];
                if (material == null)
                    return;

                string passName = candidate.PassName;
                if (passName.Length == 0)
                    passName = material.GetPassName(0) ?? string.Empty;
                if (passName.Length == 0)
                    return;

                material.SetShaderPassEnabled(passName, enabled);
            }
            catch (Exception)
            {
                // 实例销毁后写入会失败，忽略。
            }
        }

        private readonly struct Candidate
        {
            internal Candidate(
                string assetName,
                Renderer renderer,
                int materialIndex,
                string shaderName,
                string passName)
            {
                AssetName = assetName;
                Renderer = renderer;
                MaterialIndex = materialIndex;
                ShaderName = shaderName;
                PassName = passName;
            }

            internal string AssetName { get; }
            internal Renderer Renderer { get; }
            internal int MaterialIndex { get; }
            internal string ShaderName { get; }
            internal string PassName { get; }
        }
    }
}
