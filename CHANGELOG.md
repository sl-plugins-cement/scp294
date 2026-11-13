# Changelog

## Unreleased

- Anchor cabinet music inside the model's front panel with scale and rotation, double its default volume to 1.2 and delay distance falloff until 2.5 metres.
- Add looping cabinet music, pre-encoded as 16 kb/s mono Opus, streamed only to listeners within its configurable hearing radius.
- Show the drink timer and 67 magazine as a row of BuffSystem's shared status panel, and send personal notices through its shared notice lane. BuffSystem is now required; `buff_hint_x`, `buff_hint_y` and `message_hint_y` are no longer read.

## 0.8.0

- Bar configured ReinforcementsSystem forces from the machine with a per-force refusal hint; Tau-9 is barred by default. Requires ReinforcementsSystem with `GetReinforcementForceId`; without it nobody is barred.

## 0.7.1

- Update the embedded Vorbis decoder to 0.10.4 and reuse compatible SecretLabNAudio decoders, preventing AutoEvent audio initialization failures when SCP294 loads first.

## 0.7.0

- Expose all eight drop probabilities, conditional branch probabilities and movement, damage and backlash percentages in configuration.
- Increase combined rare drink odds from 10% to 12%, with 78% normal SCP-207 and 10% Ahead.
- Replace ordinary coffee with a native SCP-207 bottle, preserving native use, healing, stacking and health drain; remove `coffee_duration`.
- Give native bottles through `scp294 buff give ... scp207`; retain coffee aliases and reject duration or branch overrides for this result.

## 0.6.0

- Make 90% of draws mild coffee or Ahead, with the strongest and joke drinks occupying the remaining 10%; Jiahao drops at 0.2% and retains its 10% success branch.
- Shorten rare effect durations, soften Ahead's speed and damage bonus, and make its weaker backlash expire after 30 seconds.
- Replace the oversized Surface SCP-207 pickup with a wiki-inspired coffee cabinet built from native static primitives and world text.
- Dispense through native hold-to-search on the front panel, preserving cancellation, inventory checks and one bottle per life.
- Seat the cabinet on the floor below its configured position; retain existing scale 10 as the default 2.2-metre cabinet.

## 0.5.4

- Require native ServerConfigs permission for machine and buff commands, including player senders with Remote Admin access.
- Restore Vodka's captured player scale before an accepted native role transition, including death.
- Correct SR1 installation guidance to use its loaded per-port plugin directory.
