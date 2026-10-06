# Changelog

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
