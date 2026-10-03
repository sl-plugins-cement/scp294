# Changelog

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
