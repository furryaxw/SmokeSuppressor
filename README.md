# SmokeSuppressor

适用于《Sprocket》`0.2.55.5`（BepInEx 6 IL2CPP）的模组：隐藏会长期堆积的烟雾层，保留瞬发效果
（炮口焰、火花、冲击波、扰动、命中烟尘、车辆起火等）。

## 隐藏的层

| 开关 | 隐藏的输出层 |
| --- | --- |
| Hide engine accumulation | `ExhaustSmoke/System (5)` |
| Hide muzzle accumulation | `MediumCannonFire/System (10)` |
| Hide ground impact smoke | `ShellImpact_dirt/System (1)` 与 `System (4)` |
| Hide armour impact smoke | `ShellNonPenetration/System` 与 `ShellPenetration/System (5)` |

四个开关默认全部开启（默认隐藏）。它们由 SprocketModAPI 的配置服务提供，在游戏内 Mod 菜单的
**Smoke Suppressor** 页里逐项开关。

## 工作方式

- 输出层的身份来自对游戏资源里 VFX asset 的静态盘点：堆积层都是 `task=4`、`taskType=1073741830`、
  使用 `WispySmoke03_8x8` 的输出；同一个 asset 里的其余输出是瞬发层，必须保留。
- 抑制方式是在 `VisualEffect.Play()` 之后关闭该输出生成材质的 shader pass
  （`Material.SetShaderPassEnabled`）。输出材质还没生成时，只对该实例重试有限帧。
- 不修改任何游戏资源文件。

## 要求

- 《Sprocket》`0.2.55.5`、BepInEx `6.0.0-be.788`（IL2CPP / net6）
- 依赖 [SprocketModAPI](https://github.com/furryaxw/SprocketModAPI)：配置页与游戏内菜单
- Windows x64

游戏更新后 VFX asset 里的 `System (N)` 编号可能变化，需要重新盘点资源并更新规则。

## 安装

1. 为《Sprocket》安装 BepInEx 6（IL2CPP）。
2. 把 `SprocketModAPI.dll` 与 `SmokeSuppressor.dll` 放入 `BepInEx\plugins`。
3. 启动游戏。

## 排查

`BepInEx\config\furryaxw.smoke-suppressor.cfg` 里的 `Calibration.Enabled`（默认 `false`）可以打开
输出层标定探针：**F6** 进出、**F7** 下一条候选、**F8** 确认。进入后关闭其余输出、只显示当前候选，
用来确认某个 `System (N)` 到底是哪一层。

## 构建

```powershell
dotnet build .\SmokeSuppressor\SmokeSuppressor.csproj -c Release
```

- 默认部署到 `$(SprocketGameRoot)\BepInEx\plugins`
- `-p:SkipModDeploy=true` 只构建不部署
- `-p:SprocketGameRoot="D:\Games\Sprocket"` 指定其它游戏目录

## 许可证

[GPL-3.0-only](LICENSE.txt)
