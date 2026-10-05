# SmokeSuppressor v0.1.0

首个版本：隐藏会长期堆积的烟雾层，保留瞬发效果（炮口焰、火花、冲击波、命中烟尘、车辆起火等）。

## 隐藏的层

- 引擎堆积：`ExhaustSmoke/System (5)`
- 炮口堆积：`MediumCannonFire/System (10)`
- 地面命中：`ShellImpact_dirt/System (1)` 与 `System (4)`
- 装甲命中：`ShellNonPenetration/System` 与 `ShellPenetration/System (5)`

四个开关默认全部开启，可在游戏内 Mod 菜单的 **Smoke Suppressor** 页逐项调整。

## 要求

- Sprocket `0.2.55.5`、BepInEx `6.0.0-be.788`（IL2CPP / net6）
- 依赖 [SprocketModAPI](https://github.com/furryaxw/SprocketModAPI) `v1.0.0` 或更高

<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->
