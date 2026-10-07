# Changelog

## v0.1.16 - Non-radar weapon launch hotfix

- Restrict the HPM datalink launch gate to ARH/SARH weapon seekers. Optical, inertial, IR, laser-guided and unguided weapons keep native launch and guidance behavior.
- Apply the same rule to weapon assessment so non-radar weapons are not rejected indirectly.
- Remove the misleading GPS launch hint. There is no player-facing GPS designation interface; Blackout requires a selected ground radar/SAM.


## v0.1.15 - Critical plugin startup fix

- Replace the invalid 4-to-4 acceptable range with a fixed four-second acceptable value list.
- Fix the Player.log-confirmed ArgumentException in Plugin.Awake that prevented runtime patches and Locust controllers from initializing.
- Add a startup regression check executing all seven production configuration bindings against actual installed BepInEx.
- Retain the targetless deployment, collision cleanup, carrier compatibility and unchanged Unity bundle.


## v0.1.14 - Targetless Locust drop hotfix

- Bind the dispenser and mines directly at spawn, with idempotent initialization.
- Use native radar altitude and its ground-ray fallback for target-independent CCIP deployment.
- Restore collision handling after launch clearance and clean up impacted containers with the native zero-yield detonation.
- Preserve door-opening lead time, mine release spacing, all aircraft compatibility and user-edited Unity assets.


## v0.1.13 - Aircraft compatibility hotfix

- Expand Locust options to aircraft stations carrying vanilla 250 kg bombs, including internal bays.
- Expand Blackout options to all native ALM-C450 and AGM-68 heavy missile stations.
- Match rack capacity to the native station options, keeping Blackout at a maximum of three missiles.
- Add installed mod-aircraft profiles for Eclipse, CI-23 Camel, Ternion, King Viper, Shrike, Helios, Agni and Strike Raptor (Locust only). MiG-29 is excluded.
- Preserve existing manual rack placement, models, materials, icons and exhaust.


## v0.1.12 - First public release

- Restore ground electronics and aircraft radar/datalink access four seconds after the last HPM exposure.
- Automatically migrate the old 18-second recovery setting; additional active emitters still extend suppression.
- Preserve the current player-edited weapon icon, exhaust and racks by packaging the already validated Unity bundle.
- Player-tested ground air-defense suppression and Locust mine deployment work adequately. This remains an early release requiring more mission and multiplayer testing.


## Runtime 0.1.11

- Capture each missile's unit target or GPS point at launch and retain it after player deselection.
- Start the 20-second HPM run only within 10 km of that remembered designation.
- Remove proximity activation and premature retargeting toward unrelated ground radars or SAMs.
- Preserve current user-edited weapon icons, exhaust and rack assets during build-only packaging.
- Publish code and text documentation only; remove visual assets and generated content from the current repository tree.

## Runtime 0.1.10

- Add aircraft radar and receiver-specific datalink suppression, including friendly aircraft.
- Reject explicit Blackout launches against ordinary tanks while retaining GPS launches.
- Add scoped impact sweeps, native detonation and disabled-visual cleanup.

## Runtime 0.1.9

- Set terrain clearance to 35 m and add forward/downward terrain and roof preview.
- Bound and gradually ramp pitch requests; retain native aerodynamic limits.

## Earlier versions

- Add Blackout continuous HPM emission and Locust deployment with eight 7 kg mines.
- Add one/two/three-round racks, folded deployment animations and visible vanilla mounting hardware.
