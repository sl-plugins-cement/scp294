# SCP-294? 搞怪饮料机

This standalone LabAPI plugin spawns a custom SCP-294 coffee cabinet on Surface at `(58, 292, -43)`, 60 seconds after round start. Hold the native interaction key on its front panel to receive a random drink; releasing early cancels without dispensing. The cabinet stays in place and round reset cancels pending spawns. Its wiki-inspired model, attribution and Unity authoring instructions are in [Model/README.md](Model/README.md).

**Each player can receive one bottle per native life.** Only a successful inventory addition consumes the quota. Cancelling the native search, a full inventory or a failed dispense leaves it available. Drinking, dropping or transferring the bottle, buff expiry, admin buff clearing and machine replacement do not reset the quota. A new native LifeId after respawn allows another bottle. Ordinary dropped-item pickup and admin buff grants bypass machine quotas. Round reset and disconnection clear the records.

程序集及项目名为 `SCP294`；LabAPI 名称使用 `SCP-294？` 的全角问号，避免 Windows 配置文件夹非法字符。源代码保留 `Qlz` 命名空间以兼容原有文件。

Eight drinks use the exact lottery in `DrinkRolls.cs`. Normal results account for 90% of draws; rarer drinks keep their high-risk effects. The per-life bottle limit applies to all results.

| Drink | Drop chance | Default effect |
| --- | ---: | --- |
| Ordinary coffee (`coffee`) | 80% | +10% movement speed for 30 seconds. |
| Ahead (`ahead`) | 10% | +20% movement speed and +5% firearm damage for 90 seconds, followed by 30 seconds of 20% slowness and 80% of the original maximum HP. Maximum HP is restored afterwards without healing damage. |
| QiaoLeZi (`qlz`) | 2% | Equal chance of +100% movement speed for 120 seconds or native cardiac arrest for 6 seconds. Native supported medicine can treat cardiac arrest. |
| 67 (`67`) | 3% | 67 HP and maximum HP, 67 non-decaying AHP, 67 Hume shield and 67-round firearm magazines for 67 seconds. Revolvers use a virtual 67-shot magazine with the native six-chamber UI. Reloading refills 67 rounds; energy weapons retain their native capacity. |
| Vodka (`vodka`) | 3% | Scale `(1.15, 0.8, 1.15)`, 200 HP, 150 non-decaying AHP, 10% slowness and native DamageReduction intensity 15 (7.5% reduction) for 120 seconds. |
| Compound V (`v`) | 1% | +255% movement speed for 10 seconds, then 30 seconds of native Burned, 50% slowness and Concussed. |
| Meteor (`meteor`) | 0.8% | Three native SCP-207 stacks and +150% movement speed. A stop lasting over one second or the 20-second deadline triggers a native HE explosion and kills the holder. Item switching, dropping and consumable use are blocked during the effect. |
| Jiahao (`jiahao`) | 0.2% | The existing 10% success / 90% death roll. Success gives 90 seconds of native SCP-1853 intensity 3, three SCP-207 stacks and at most one point of firearm/SCP damage per attack, with the existing broadcast and music. Environmental damage and SCP-207 health drain still apply. |

Jiahao possession therefore occurs on 0.02% of machine draws (about one in 5,000), and its failed branch on 0.18%. QiaoLeZi's speed branch occurs on 1% of draws. Draws are independent; there is no pity counter or guaranteed rare reward. Admin grants remain explicit and bypass the machine lottery.

The native SCP-1853/207 conflict is suppressed only while Jiahao is active, preserving pre-existing poison. Native SCP-1853 intensity represents danger rather than an old-style attribute multiplier. Drink effects, native modifiers, AHP processes, music and HUD state are cleaned up on expiry, replacement, death, role change, disconnect, round reset and plugin unload.

Jiahao music uses LabAPI's native SpeakerToy and AudioTransmitter, follows the holder and attenuates over the configured radius. NVorbis and `jh.ogg` are embedded in the DLL. A sibling `jh.ogg` or a configured absolute OGG Vorbis path can override it; custom audio must be OGG Vorbis. Decoding and caching happen in the background; speaker operations run on the Unity thread. Cancellation prevents delayed playback after cleanup, and each session destroys only its own audio source.

## 构建

```powershell
dotnet build SCP294.csproj -c Release `
  -p:SCP_SL_MANAGED="D:\\path\\to\\SCPSL_Data\\Managed" `
  -p:HINT_SERVICE_MEOW="D:\\path\\to\\HintServiceMeow.dll"
```

当前工作区验证命令：

```powershell
dotnet build SCP294.csproj -c Release `
  -p:SCP_SL_MANAGED="D:\\ZHUOMIAN\\ArcaneStrike-master\\_buildrefs" `
  -p:HINT_SERVICE_MEOW="D:\\ZHUOMIAN\\ArcaneStrike-master\\奇术打击\\LabAPI\\HintServiceMeow.dll"
```

Install `bin/Release/net48/SCP294.dll` in a plugin directory listed by `LabAPI/LabApi-<port>.yml`. On SR1, use `LabAPI/plugins/7777`; its loader excludes `plugins/global`. Remove an old `Qlz.dll` from the loaded paths to avoid duplicate instances. A compatible HintServiceMeow must be installed. NVorbis and the default music are embedded in SCP294.dll; a sibling `jh.ogg` or a configured absolute OGG path can override the music. LabAPI creates `LabAPI/configs/<port>/SCP-294？/config.yml` automatically and preserves existing configuration. The embedded NVorbis license is also available in `lib/NVorbis.LICENSE.txt`.

## 默认配置

项目根目录的 `config.yml` 是端口 7777 当前配置的完整副本，`Config.cs` 中的默认值已同步。构建输出包含 `SCP294.dll`、`README.md` 和 `config.yml`；新服务器首次加载会自动生成相同默认值，已有服务器配置继续优先使用。

Existing configurations override duration defaults. To use the balanced durations on an existing server, update the corresponding keys from `config.yml`, including the new `coffee_duration` and `ahead_backlash_duration`, while preserving its other settings.

Defaults use position `(58, 292, -43)` projected onto the floor below it, scale 10 (a 2.2-metre cabinet), a 60-second spawn delay, 6-second cardiac arrest, and a 10-metre music radius. See `config.yml` for effect durations and hint positions. Manual configuration belongs in `LabAPI/configs/<port>/SCP-294？/config.yml`; install the DLL in a loaded per-port directory, including `LabAPI/plugins/7777` on SR1.

## HSM 显示

- 右下稍向左、向上：统一单行 `巧乐兹[00:05]` 格式，反噬与自爆也显示饮料名及时间，到期后移除。Ahead backlash uses a timed `遥遥领先（反噬）[00:30]` label.
- 左轮使用 67 时，倒计时下方另起一行显示虚拟弹匣剩余弹药。
- 中部：领取、使用、结束文案，默认显示 6 秒，同一通知区域复用且不会累计。
- 全服嘉豪提示：独立中上区域，存续五秒，包含观察者。登场第一行“那个男人？”金色加粗，第二行“难道又重出江湖了吗”浅紫色；死亡语录首行浅紫斜体，第二行玩家名与“老师”金色加粗、余文灰白。附体失败使用两行粉红正文和金色加粗“这份力量”。提示使用固定行数，避免与玩家自己的喝饮料通知重叠。
- 下方：手持特殊饮料时显示名称和梗文案简介；切换物品、丢弃、使用后移除。游戏内不展示护盾、体型等属性说明。
- 死亡、变更角色、回合重置或卸载插件时清理自己的 HSM 条目。

可配置 `buff_hint_x`（默认 -260，相对右边缘向左）、`buff_hint_y`（默认 760）、`item_hint_y`（790）、`message_hint_y`（560）适应服务器其他插件的 HUD。倒计时右对齐，介绍居中，分别占用独立列。

同一玩家同时保持一种饮料效果，喝下另一种会清理上一种；美味流星期间拒绝使用物品，瓶子不消耗。Ahead backlash has its own countdown and expires after the configured duration.
67 的弹药在获得武器和换弹时补充，射击时正常消耗；不会每帧补满。临时生命上限恢复时保留已受到的伤害。

## 命令

```text
scp294 spawn    Spawn or replace SCP-294
scp294 clear    Clear SCP-294
scp294 buff list                            查看可用 Buff
scp294 buff give <玩家> <Buff> [秒数] [分支]  直接给予效果，不需要饮料
scp294 buff status <玩家>                    查看效果、阶段及剩余时间
scp294 buff clear <玩家>                     清除饮料 Buff（不移除机器）
```

All `scp294` machine and buff commands require native `ServerConfigs` permission for both player and console senders. Remote Admin access alone does not authorize these commands. 目标支持玩家 ID、`me`（自身）、`all`/`*`（全体）、完整 UserId、完整昵称或唯一昵称片段；昵称歧义会列出玩家 ID，建议用 ID。给予仅适用于存活人类，`all` 自动跳过 SCP 和观察者。直接给予复用喝饮料的效果、HSM、音乐及清理流程，一名玩家同时保持一个 Buff，给予新效果会替换原效果。管理员可清除或替换美味流星，清除后解除锁物品并取消该次自爆。

```text
scp294 buff give me 67
scp294 buff give 2 coffee                Ordinary coffee with its configured duration
scp294 buff give 2 qlz cardiac 45         指定心脏骤停 45 秒
scp294 buff give me jiahao                直接嘉豪附体成功
scp294 buff give 2 jiahao random          按饮料原有 10% 成功概率抽取
scp294 buff give all vodka 60             全体存活人类获得 60 秒伏特加
scp294 buff give 2 meteor 30              保留静止超过一秒即自爆的规则
scp294 buff status 2
scp294 buff clear all
```

Aliases: `普通咖啡/咖啡/coffee`, `巧乐兹/qlz/qiaolezi`, `67/sixtyseven`, `遥遥领先/ahead`, `伏特加/vodka`, `5号化合物/v/compoundv`, `美味流星/meteor`, `嘉豪/嘉豪の圣遗物？/jiahao`. QiaoLeZi defaults to `speed`, with `cardiac` and `random` available; Jiahao defaults to `success`, with `fail` and `random` available. Their branch probabilities stay 50% and 10% respectively. A duration may be supplied once, in either option order, between 0.1 and 3600 seconds; omitted durations use the active configuration. Compound V and Ahead duration overrides affect the positive phase; their backlash uses its separate configured duration. Clearing a buff restores modifiers without healing damage.

旧 `qlz` 命令仍作为别名可用。坐标、旋转、机器缩放、领取冷却、刷新延迟、提示显示时间、各饮料持续时间及 BGM 路径/半径/音量都在插件配置中调整。替换 DLL 后需重启服务器进程来加载新程序集。

## 验证

`dotnet run --project tests/LifeBottleQuota/LifeBottleQuota.csproj -c Release` 检查同一生命成功领取后拒绝重复、取消交互不消耗、玩家独立额度、新生命恢复、离开与回合清理，并逐字段对比 `Config.cs` 与随附 `config.yml` 的所有默认值。原生拾取和复活需游戏内验收。

`dotnet run --project tests/HintLifecycle.csproj -c Release` 在隔离的 HSM 测试替身中检查提示过期、对象复用、倒计时更新、介绍清理、玩家/回合清理和行间距。此检查不模拟 Unity 客户端；原生拾取进度、多人拾取和与其他插件 HUD 的实际位置仍需在游戏内验收。

`dotnet run --project tests/WeaponAmmo67/WeaponAmmo67.csproj -c Release` 检查 67 弹药适配器对空枪、开闭膛枪械、正在换弹和换弹完成、霰弹枪的处理，模拟左轮完整 67 次消耗及重新装填，检查反噬阶段与时间边界。使用原生接口契约的测试替身；实际开枪、流星移动和爆炸网络同步仍需在游戏内验收。

`dotnet run --project tests/AudioFollow/AudioFollow.csproj -c Release` 在没有 AudioRobot 和旁置 NVorbis DLL 的条件下，从内嵌资源加载真实解码器并解码真实 `jh.ogg`，检查单声道样本、文件覆盖、取消和路径错误；通过原生扬声器契约替身验证五米空间播放、循环、创建顺序、ID 冲突处理、位置同步、停止/换角色/回合/卸载清理及失败提示。真实客户端听感与衰减仍需游戏内验收。

`dotnet run --project tests/BuffCommands/BuffCommands.csproj -c Release` 检查管理员/控制台权限、父命令注册及参数偏移、默认与指定分支、目标 ID/昵称/全体筛选、歧义拒绝、非法参数在修改前拒绝、状态查询和清除。实际效果仍需在服务器内验证。
