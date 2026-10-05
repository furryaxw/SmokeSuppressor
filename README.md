# SmokeSuppressor

适用于《Sprocket》的 MelonLoader 模组。它只抑制会长期堆积的引擎和炮口烟，
保留正常引擎尾烟、瞬时炮口烟、炮口焰、火花、冲击波、扰动、命中烟尘和车辆起火效果。

## 工作方式

- 引擎：在 `ExhaustEffect.PlayEffect` 后关闭已校准的 `ExhaustSmoke` 堆积表达式。
- 炮口：在 `MuzzleFlashEffect.Setup` 后、原生 VFX 更新前，将
  `MediumCannonFire/System (9)` 的独立发射 Count 设为零。

模组不修改游戏资源文件，也不持续扫描车辆或场景。原生内存布局受当前游戏二进制
指纹保护；游戏更新导致布局变化时会停止写入并记录错误，而不会猜测新偏移。

当前已验证 Sprocket `0.2.53.1` 和 `0.2.53.2`；其他游戏版本仍会安全停止原生写入。

## 安装

1. 安装适用于《Sprocket》的 MelonLoader。
2. 将 `SmokeSuppressor.dll` 放入游戏的 `Mods` 目录。

## 构建

```powershell
dotnet build .\SmokeSuppressor\SmokeSuppressor.csproj -c Release
```

使用 `-p:SkipModDeploy=true` 可只构建而不复制到 `G:\Sprocket\Mods`。

## 许可证

[GPL-3.0-only](LICENSE.txt)
