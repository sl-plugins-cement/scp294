# SCP-294? drink machine

This standalone LabAPI plugin spawns a custom SCP-294 cabinet on Surface, 60 seconds after round start. Hold the native interaction key on its front panel to receive a random drink; releasing early cancels without dispensing. The cabinet stays in place, and round reset cancels pending spawns. See [Model/README.md](Model/README.md) for the wiki-inspired model, attribution and Unity authoring instructions.

**Each player can receive one bottle per native life.** Only successful inventory addition consumes the quota. Cancelling the search, a full inventory or a failed dispense leaves it available. Drinking, dropping, transferring, buff expiry, admin clearing and machine replacement do not reset it. Respawning with a new native LifeId allows another bottle. Ordinary pickup and admin grants bypass machine quotas. Round reset and disconnection clear the records.

## Drinks

All eight drop percentages are configurable. Defaults allocate 78% to normal SCP-207, 10% to Ahead and 12% to the rarer high-risk drinks.

| Drink | Default chance | Default effect |
| --- | ---: | --- |
| Normal SCP-207 (`scp207`) | 78% | A normal native bottle: native drinking animation and cancellation, 30 HP healing, unlimited stamina, speed, stacking up to four and health drain. No plugin duration or buff slot. |
| Ahead (`ahead`) | 10% | +20% movement speed and +5% firearm damage for 90 seconds; then 30 seconds of 20% slowness and 80% of original maximum HP. Restores maximum HP without healing. |
| QiaoLeZi (`qlz`) | 2.4% | 50% chance of +100% movement speed for 120 seconds; otherwise native cardiac arrest for 6 seconds. Native supported medicine can treat it. |
| 67 (`67`) | 3.6% | 67 HP and maximum HP, 67 non-decaying AHP, 67 Hume shield and 67-round firearm magazines for 67 seconds. Revolvers use a virtual 67-shot magazine with native six-chamber UI; energy weapons retain native capacity. Reloading refills 67 rounds. |
| Vodka (`vodka`) | 3.6% | Scale `(1.15, 0.8, 1.15)`, 200 HP, 150 non-decaying AHP, 10% slowness and 7.5% damage reduction for 120 seconds. |
| Compound V (`v`) | 1.2% | +255% movement speed for 10 seconds; then 30 seconds of native Burned, 50% slowness and Concussed. |
| Meteor (`meteor`) | 0.96% | Three native SCP-207 stacks and +150% movement speed. Stopping for over one second or reaching 20 seconds triggers a native HE explosion and kills the holder. Blocks item switching, dropping and use. |
| Jiahao (`jiahao`) | 0.24% | 10% success, otherwise immediate death. Success gives 90 seconds of native SCP-1853 intensity 3, three SCP-207 stacks and at most one point of firearm/SCP damage per attack, with broadcast and music. Environmental damage and native health drain apply. |

With defaults, Jiahao possession occurs on 0.024% of machine draws (about one in 4,167); its failed branch occurs on 0.216%. QiaoLeZi's speed branch occurs on 1.2%. Draws are independent, without a pity counter. Admin grants bypass the lottery.

The native SCP-1853/207 conflict is suppressed only while Jiahao is active, preserving pre-existing poison. Custom drinks occupy one plugin buff slot; another custom drink replaces it. Normal SCP-207 is owned by the game and can coexist with custom effects. `buff clear` removes custom effects, preserving the native SCP-207 state captured before a custom effect modified it. Custom modifiers, AHP, music and HUD entries are cleaned up on expiry, replacement, death, role change, disconnect, round reset and unload.

## Configuration

LabAPI stores configuration at `LabAPI/configs/<port>/SCP-294？/config.yml`. The fullwidth question mark avoids an invalid Windows directory character. [config.yml](config.yml) supplies all defaults and matches `Config.cs`. Existing server settings take precedence; add or edit the desired keys and restart to load them.

Placement is configurable:

```yaml
machine_position:
  x: 58
  y: 292
  z: -43
machine_rotation:
  x: 0
  y: 0
  z: 0
machine_scale: 10
```

The position is projected onto the floor below it; the model front faces local negative Z. Scale 10 produces the authored 2.2-metre cabinet.

Drop chances use percentage values, including up to two decimal places. All eight must total **100**. Set a drink to **0** to disable it. Invalid values or totals produce a warning and use the shipped default distribution until the next load; they are not normalized.

```yaml
scp207_chance_percent: 78
ahead_chance_percent: 10
qiao_le_zi_chance_percent: 2.4
sixty_seven_chance_percent: 3.6
vodka_chance_percent: 3.6
compound_v_chance_percent: 1.2
meteor_chance_percent: 0.96
jiahao_chance_percent: 0.24
qiao_le_zi_cardiac_chance_percent: 50
jiahao_success_chance_percent: 10
```

The last two settings are conditional chances **after** selecting that drink. Both are clamped to 0–100%; non-finite values use their defaults. Explicit admin branches override these chances.

Effect percentages are also configurable:

| Keys | Units and limits |
| --- | --- |
| `qiao_le_zi_speed_boost_percent`, `ahead_speed_boost_percent`, `compound_v_speed_boost_percent`, `meteor_speed_boost_percent` | Bonus movement speed; native MovementBoost intensity, integer 0–255. |
| `ahead_firearm_damage_bonus_percent` | Bonus firearm damage; clamped to 0–1000. |
| `ahead_backlash_slowness_percent`, `vodka_slowness_percent` | Slowness; integer, clamped to 0–100. |
| `ahead_backlash_max_health_percent` | Original maximum HP retained during backlash; clamped to 1–100. |
| `vodka_damage_reduction_percent` | Actual damage reduction; clamped to 0–100 and rounded to native 0.5% steps. |
| `compound_v_backlash_slowness` | Native backlash slowness intensity, default 50. |

Non-finite floating effect percentages use their defaults. Durations, personal cooldown, automatic spawn/delay, broadcasts, hint coordinates and music settings are in `config.yml`. Native SCP-207 has no plugin duration.

## Commands

All commands require native `ServerConfigs` permission for player and console senders. Remote Admin access alone is insufficient.

```text
scp294 spawn
scp294 clear
scp294 buff list
scp294 buff give <player> <drink> [seconds] [branch]
scp294 buff status <player>
scp294 buff clear <player>
```

Targets accept player ID, `me`, `all`/`*`, full UserId, exact nickname or a unique nickname fragment. Ambiguous names list player IDs. Grants require alive humans; `all` skips SCPs and spectators. Clearing supports all roles.

```text
scp294 buff give 2 scp207               Give a native bottle, subject to inventory space
scp294 buff give me 67
scp294 buff give 2 qlz cardiac 45
scp294 buff give me jiahao              Explicit success branch
scp294 buff give 2 jiahao random        Configured success chance
scp294 buff give all vodka 60
scp294 buff give 2 meteor 30
scp294 buff status 2
scp294 buff clear all
```

Normal SCP-207 accepts `scp207`, `207`, `正常207`, `普通207`, `普通scp-207`, `普通咖啡`, `咖啡` and `coffee`; it gives a bottle and rejects duration/branch overrides. Custom drinks accept their table identifiers and Chinese in-game names. QiaoLeZi defaults to `speed`, also accepting `cardiac` and `random`; Jiahao defaults to `success`, also accepting `fail` and `random`.

Custom duration overrides accept 0.1–3600 seconds once, in either option order. Omitted durations use config. Compound V and Ahead overrides affect their positive phases; backlash uses its separate duration. Admin clearing or replacement of Meteor cancels its explosion and item lock. `qlz` remains a parent-command alias.

## Installation and build

```powershell
dotnet build SCP294.csproj -c Release `
  -p:DeployToLocalServer=false `
  -p:SCP_SL_MANAGED="D:\path\to\SCPSL_Data\Managed" `
  -p:HINT_SERVICE_MEOW="D:\path\to\HintServiceMeow.dll"
```

Install `bin/Release/net48/SCP294.dll` in a plugin directory listed by `LabAPI/LabApi-<port>.yml`. On SR1 use `LabAPI/plugins/7777`; its loader excludes `plugins/global`. Remove another loaded `Qlz.dll` to avoid duplicate instances. A compatible HintServiceMeow is required. Restart the server process after replacing the DLL.

NVorbis and `jh.ogg` are embedded. A sibling `jh.ogg` or a configured absolute OGG Vorbis path overrides the default music. Jiahao uses native SpeakerToy/AudioTransmitter, follows its holder and attenuates over the configured radius. Decoding/caching runs in the background; native speaker operations run on the Unity thread. Each session destroys only its own source, and cancellation prevents delayed playback. See `lib/NVorbis.LICENSE.txt` for the decoder license.

## HUD and verification

Custom drink countdowns use a single right-aligned `巧乐兹[00:05]` line, including backlash and explosion deadlines. A virtual revolver magazine adds its own line. Custom held bottles show the Chinese name and joke; switching, dropping and use clear it. Temporary notices share a timed message area. Jiahao uses a separate global two-line notice, including spectators.

Adjust `buff_hint_x` (default -260 from the right edge), `buff_hint_y` (760), `item_hint_y` (790) and `message_hint_y` (560) for other server HUDs.

Existing local checks:

```powershell
dotnet run --project tests/LifeBottleQuota/LifeBottleQuota.csproj -c Release
dotnet run --project tests/BuffCommands/BuffCommands.csproj -c Release
dotnet run --project tests/HintLifecycle.csproj -c Release
dotnet run --project tests/WeaponAmmo67/WeaponAmmo67.csproj -c Release
dotnet run --project tests/AudioFollow/AudioFollow.csproj -c Release
```

The quota check compares every config default to YAML and enumerates the lottery, including disabled and invalid odds. Command checks cover permissions, targeting, native bottle aliases and rejection before mutation. Other checks use native interface contracts for HUD, ammunition/timelines and audio. Real input, networking, rendering and sound require the isolated offline client runner: `tests/balance-and-presentation.ps1` covers configurable placement/odds/strengths and native SCP-207 use; `tests/custom-model.ps1` covers the model, quota and cleanup.
