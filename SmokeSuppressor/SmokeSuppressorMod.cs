using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SprocketModAPI;
using UnityEngine;
using UnityEngine.VFX;
using ExhaustEffect = Sprocket.Vehicles.Exhausts.ExhaustEffect;
using MuzzleFlashEffect = Sprocket.Vehicles.Fires.MuzzleFlashEffect;

[assembly: AssemblyMetadata("Sprocket.Mod.Id", "furryaxw.smoke-suppressor")]
[assembly: AssemblyMetadata("Sprocket.Mod.DisplayName", "Smoke Suppressor")]
[assembly: AssemblyMetadata("Sprocket.Mod.Description", "Limits persistent engine and muzzle-smoke accumulation while preserving normal effects.")]
[assembly: AssemblyMetadata("Sprocket.Mod.Authors", "furryAxw")]
[assembly: AssemblyMetadata("Sprocket.Mod.Repository", "furryaxw/SmokeSuppressor")]
[assembly: AssemblyMetadata("Sprocket.Mod.Category", "visual")]
[assembly: AssemblyMetadata("Sprocket.Mod.License", "GPL-3.0-only")]

namespace SmokeSuppressor
{
    [BepInPlugin(PluginGuid, "Smoke Suppressor", "2.5.2")]
    [BepInDependency("furryaxw.sprocket-mod-api")]
    public sealed class SmokeSuppressorMain : BasePlugin
    {
        internal const string PluginGuid = "furryaxw.smoke-suppressor";

        private const string DiagnosticPrefix =
            "[SmokeSuppressor]";
        private const string CalibrationSection = "Calibration";
        private const string CalibrationEnabledKey = "Enabled";

        private readonly HashSet<string> loggedFailures =
            new(StringComparer.Ordinal);
        private bool nativeWritesEnabled;
        private bool calibrationEnabled;
        private SmokeSuppressorOptions? options;
        private VfxCalibrationProbe? calibrationProbe;

        internal static SmokeSuppressorMain? Instance
        {
            get;
            private set;
        }

        public override void Load()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(
                typeof(SmokeSuppressorMain).Assembly,
                PluginGuid);

            if (SprocketApi.TryGetService<IModConfigService>(
                    out IModConfigService? configService) &&
                configService != null)
            {
                try
                {
                    options = new SmokeSuppressorOptions(configService);
                }
                catch (Exception exception)
                {
                    Log.LogError(
                        $"{DiagnosticPrefix} config page failed: " +
                        exception.Message);
                }
            }
            else
            {
                Log.LogWarning(
                    $"{DiagnosticPrefix} the SprocketModAPI config service is " +
                    "unavailable; the built-in defaults are used.");
            }

            calibrationEnabled = Config.Bind(
                CalibrationSection,
                CalibrationEnabledKey,
                false,
                "Enable the in-game VFX calibration probe. F6 toggles the probe, F7 cycles the candidate, F8 confirms it.").Value;
            if (calibrationEnabled)
            {
                calibrationProbe = AddComponent<VfxCalibrationProbe>();
                Log.LogInfo(
                    $"{DiagnosticPrefix} calibration probe attached; " +
                    $"set {CalibrationSection}.{CalibrationEnabledKey}=false to run normally.");
            }
            if (SmokeAccumulationOutputMap.TryValidateCapturedEvidence(
                    out string failure,
                    out int sampleCount))
            {
                Log.LogInfo(
                    $"{DiagnosticPrefix} output-map passed " +
                    $"samples={sampleCount}," +
                    "muzzleAccumulationMaterialConfirmed=false");
            }
            else
            {
                Log.LogError(
                    $"{DiagnosticPrefix} output-map failed " +
                    $"samples={sampleCount},failure={failure}");
            }

            bool expressionMapPassed =
                SmokeNativeExpressionMap.TryValidateCapturedEvidence(
                    out failure,
                    out sampleCount);
            if (expressionMapPassed)
            {
                Log.LogInfo(
                    $"{DiagnosticPrefix} expression-map passed " +
                    $"samples={sampleCount}," +
                    "source=compiled-VisualEffectAsset");
            }
            else
            {
                Log.LogError(
                    $"{DiagnosticPrefix} expression-map failed " +
                    $"samples={sampleCount},failure={failure}");
            }

            bool nativeGuardPassed =
                VfxNativeExpressionOverride.TryInitialize(
                    out string nativeGuardResult);
            // Unity 6 下的表达式偏移与索引尚未标定：这条路径暂时不写原生内存。
            nativeWritesEnabled = false;
            if (nativeWritesEnabled)
            {
                Log.LogInfo(
                    $"{DiagnosticPrefix} native-guard passed " +
                    nativeGuardResult);
                Log.LogInfo(
                    $"{DiagnosticPrefix} enabled " +
                    "engine=ExhaustSmoke/valueIndex-174/" +
                    "System-(4)->System-(5)," +
                    "muzzle=MediumCannonFire/System-(9)/Count/" +
                    "expression-148/valueIndex-299-300/(7,7)->(0,0)," +
                    "muzzleExpressions=valueIndex-289-and-294/unchanged," +
                    "normalSmokeRetained=true," +
                    "scenePolling=false");
            }
            else
            {
                Log.LogError(
                    $"{DiagnosticPrefix} activation failed " +
                    $"expressionMapPassed={expressionMapPassed}," +
                    $"nativeGuardPassed={nativeGuardPassed}," +
                    $"guard={nativeGuardResult},writesDisabled=true");
            }
        }

        public override bool Unload()
        {
            options?.Dispose();
            options = null;
            calibrationProbe = null;
            nativeWritesEnabled = false;
            loggedFailures.Clear();
            Instance = null;
            return true;
        }

        internal void SuppressEngineExpression(
            Component effect,
            string category)
        {
            try
            {
                if (calibrationProbe is { Active: true })
                {
                    calibrationProbe.Observe(effect);
                    return;
                }

                if (!nativeWritesEnabled || effect == null)
                    return;

                VisualEffect? visualEffect =
                    effect.GetComponentInChildren<VisualEffect>(true);
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    return;

                if (!VfxNativeExpressionOverride.TryDisableMappedExpression(
                        visualEffect,
                        out _,
                        out string failure))
                {
                    LogFailure(category, failure);
                }
            }
            catch (Exception exception)
            {
                LogFailure(category, exception.ToString());
            }
        }

        internal void SuppressMuzzleCount(
            Component effect,
            string category)
        {
            try
            {
                if (calibrationProbe is { Active: true })
                {
                    calibrationProbe.Observe(effect);
                    return;
                }

                if (!nativeWritesEnabled || effect == null)
                    return;

                VisualEffect? visualEffect =
                    effect.GetComponentInChildren<VisualEffect>(true);
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    return;

                if (!VfxNativeExpressionOverride.TryZeroMappedFloat2Expression(
                        visualEffect,
                        out _,
                        out string failure))
                {
                    LogFailure(category, failure);
                }
            }
            catch (Exception exception)
            {
                LogFailure(category, exception.ToString());
            }
        }

        private void LogFailure(string category, string failure)
        {
            string key = $"{category}|{failure}";
            if (!loggedFailures.Add(key))
                return;

            Log.LogError(
                $"{DiagnosticPrefix} failed " +
                $"category={category},error={failure}");
        }
    }

    [HarmonyPatch(typeof(ExhaustEffect), nameof(ExhaustEffect.PlayEffect))]
    internal static class ExhaustNativeExpressionSuppressionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ExhaustEffect __instance)
        {
            SmokeSuppressorMain.Instance?
                .SuppressEngineExpression(
                    __instance,
                    "engine-exhaust");
        }
    }

    [HarmonyPatch(typeof(MuzzleFlashEffect), nameof(MuzzleFlashEffect.Setup))]
    internal static class MuzzleNativeCountSuppressionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MuzzleFlashEffect __instance)
        {
            SmokeSuppressorMain.Instance?
                .SuppressMuzzleCount(
                    __instance,
                    "muzzle-flash");
        }
    }
}
