using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SprocketModAPI;

[assembly: AssemblyMetadata("Sprocket.Mod.Id", "furryaxw.smoke-suppressor")]
[assembly: AssemblyMetadata("Sprocket.Mod.DisplayName", "Smoke Suppressor")]
[assembly: AssemblyMetadata("Sprocket.Mod.Description", "Hides the smoke layers that keep accumulating: engine, muzzle and both shell impacts.")]
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

        private const string DiagnosticPrefix = "[SmokeSuppressor]";
        private const string CalibrationSection = "Calibration";
        private const string CalibrationEnabledKey = "Enabled";

        private bool calibrationEnabled;
        private SmokeSuppressorOptions? options;
        private VfxCalibrationProbe? calibrationProbe;

        internal static SmokeSuppressorMain? Instance
        {
            get;
            private set;
        }

        // 标定期间由探针独占输出开关，抑制必须让路。
        internal bool CalibrationActive => calibrationProbe is { Active: true };

        public override void Load()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(
                typeof(SmokeSuppressorMain).Assembly,
                PluginGuid);

            RegisterOptions();

            calibrationEnabled = Config.Bind(
                CalibrationSection,
                CalibrationEnabledKey,
                false,
                "Enable the in-game VFX output calibration probe. F6 toggles the probe, F7 cycles the candidate, F8 confirms it.").Value;
            if (calibrationEnabled)
            {
                calibrationProbe = AddComponent<VfxCalibrationProbe>();
                Log.LogInfo(
                    $"{DiagnosticPrefix} calibration probe attached; " +
                    $"set {CalibrationSection}.{CalibrationEnabledKey}=false to run normally.");
            }

            AddComponent<SmokeSuppressionRunner>().Configure(options);

            Log.LogInfo(
                $"{DiagnosticPrefix} enabled " +
                "engine=ExhaustSmoke/System-(5) " +
                "muzzle=MediumCannonFire/System-(10) " +
                "ground=ShellImpact_dirt/System-(1)+(4) " +
                "armor=ShellNonPenetration/System+ShellPenetration/System-(5) " +
                $"calibration={calibrationEnabled}");
        }

        public override bool Unload()
        {
            options?.Dispose();
            options = null;
            calibrationProbe = null;
            Instance = null;
            return true;
        }

        private void RegisterOptions()
        {
            if (!SprocketApi.TryGetService<IModConfigService>(
                    out IModConfigService? configService) ||
                configService == null)
            {
                Log.LogWarning(
                    $"{DiagnosticPrefix} the SprocketModAPI config service is " +
                    "unavailable; the built-in defaults are used.");
                return;
            }

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
    }
}
