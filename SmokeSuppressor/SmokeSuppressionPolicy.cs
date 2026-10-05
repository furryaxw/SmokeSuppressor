using System;

namespace SmokeSuppressor
{
    // 会长期堆积的烟雾层。每一层对应一个 VFX 输出，即生成材质的 shader 名
    // （`Hidden/VFX/<Asset>/System (N)/<Output>`）。
    internal enum SmokeLayer
    {
        EngineAccumulation,
        MuzzleAccumulation,
        GroundImpactSmoke,
        ArmorImpactSmoke
    }

    internal static class SmokeSuppressionPolicy
    {
        // 规则来自对 0.2.55.5 资源文件的静态盘点：堆积层都是 task=4、taskType=1073741830
        // 且使用 WispySmoke03_8x8 的输出；同一 asset 里其余输出是瞬发层，必须保留。
        internal static bool Matches(SmokeLayer layer, string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return false;

            return layer switch
            {
                SmokeLayer.EngineAccumulation =>
                    shaderName.Contains(
                        "/ExhaustSmoke/System (5)/",
                        StringComparison.OrdinalIgnoreCase),
                SmokeLayer.MuzzleAccumulation =>
                    shaderName.Contains(
                        "/MediumCannonFire/System (10)/",
                        StringComparison.OrdinalIgnoreCase),
                SmokeLayer.GroundImpactSmoke =>
                    shaderName.Contains(
                        "/ShellImpact_dirt/System (1)/",
                        StringComparison.OrdinalIgnoreCase) ||
                    shaderName.Contains(
                        "/ShellImpact_dirt/System (4)/",
                        StringComparison.OrdinalIgnoreCase),
                SmokeLayer.ArmorImpactSmoke =>
                    shaderName.Contains(
                        "/ShellNonPenetration/System/",
                        StringComparison.OrdinalIgnoreCase) ||
                    shaderName.Contains(
                        "/ShellPenetration/System (5)/",
                        StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        // 配置服务不可用时按“隐藏”处理，与默认值一致。
        internal static bool ShouldHide(SmokeLayer layer, SmokeSuppressorOptions? options)
        {
            if (options == null)
                return true;

            return layer switch
            {
                SmokeLayer.EngineAccumulation => options.HideEngineAccumulation,
                SmokeLayer.MuzzleAccumulation => options.HideMuzzleAccumulation,
                SmokeLayer.GroundImpactSmoke => options.HideGroundImpactSmoke,
                SmokeLayer.ArmorImpactSmoke => options.HideArmorImpactSmoke,
                _ => false
            };
        }
    }
}
