using System;
using SprocketModAPI;

namespace SmokeSuppressor
{
    // 四个可独立关闭的堆积烟开关，默认全部开启（默认隐藏这些堆积烟）。
    internal sealed class SmokeSuppressorOptions : IDisposable
    {
        internal const string SectionId = "accumulated-smoke";

        private const string EngineKey = "hide-engine-accumulation";
        private const string MuzzleKey = "hide-muzzle-accumulation";
        private const string GroundImpactKey = "hide-ground-impact-smoke";
        private const string ArmorImpactKey = "hide-armor-impact-smoke";

        private readonly IModConfigRegistration registration;

        internal SmokeSuppressorOptions(IModConfigService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            registration = service.Register(new ModConfigDefinition
            {
                DisplayName = "Smoke Suppressor",
                Sections = new[]
                {
                    new ModConfigSectionDefinition
                    {
                        Id = SectionId,
                        Title = "Accumulated smoke",
                        Description = "Each switch hides one layer of smoke that keeps accumulating. Turn a switch off to keep that layer."
                    }
                },
                Entries = new[]
                {
                    ModConfigEntryDefinition.Toggle(
                        EngineKey,
                        "Hide engine accumulation",
                        true,
                        "Persistent smoke that builds up while the engine runs.",
                        SectionId),
                    ModConfigEntryDefinition.Toggle(
                        MuzzleKey,
                        "Hide muzzle accumulation",
                        true,
                        "Persistent smoke that builds up where the cannon fires.",
                        SectionId),
                    ModConfigEntryDefinition.Toggle(
                        GroundImpactKey,
                        "Hide ground impact smoke",
                        true,
                        "Persistent smoke a shell leaves where it strikes the ground.",
                        SectionId),
                    ModConfigEntryDefinition.Toggle(
                        ArmorImpactKey,
                        "Hide armour impact smoke",
                        true,
                        "Persistent smoke a shell leaves on armour, whether it stops or penetrates.",
                        SectionId)
                }
            });
        }

        internal bool HideEngineAccumulation => Get(EngineKey);

        internal bool HideMuzzleAccumulation => Get(MuzzleKey);

        internal bool HideGroundImpactSmoke => Get(GroundImpactKey);

        internal bool HideArmorImpactSmoke => Get(ArmorImpactKey);

        public void Dispose() => registration.Dispose();

        // 配置服务不可用时按“开启”（隐藏）处理，与默认值一致。
        private bool Get(string key)
        {
            try
            {
                return registration.GetBool(key);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
