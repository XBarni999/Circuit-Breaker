# Circuit Breaker

Current release: v0.1.21 (early testing). Locust deployment and Blackout suppression have been tested in missions. Lawn Chair sensor detection, the visible hop and GS25 handoff need further terrain and multiplayer testing.

Source code and documentation for a Nuclear Option tactical weapons mod. The playable pack is delivered as one `Circuit-Breaker.dll`, containing the gameplay runtime and an embedded Blueprinter bundle.

This repository contains code, build scripts, tests, text documentation and rendered gallery images. Models, textures, icons, materials, prefabs, Unity metadata, game assemblies and compiled artifacts are not included. A configured local asset workspace or an existing Blueprinter bundle is required to build the playable mod.

## Weapon gallery

Actual weapon models rendered with their materials. The dispenser and rack photographs use Unity; the upright Zhdan photograph uses the supplied Blender model.

![AGM-180 Blackout with deployed wings](docs/gallery/blackout.png)

![CBU-82M Locust mine dispenser](docs/gallery/locust.png)

![Locust mine with deployed fins](docs/gallery/locust-mine.png)

<details>
<summary>Triple-rack configurations</summary>

![Blackout triple rack](docs/gallery/blackout-rack.png)

![Locust triple rack](docs/gallery/locust-rack.png)

</details>

## AGM-180 Blackout

Price: $9 million per missile.

Blackout captures the target assigned to each missile when it is fired. Deselecting that target or selecting a different unit afterwards does not change the missile's designation. It does not activate or redirect toward unrelated radars encountered along the route.

HPM starts when the remembered enemy ground radar or missile air-defense target is within 10 km of the missile. The mod has no player-facing GPS designation interface; a selected ground radar/SAM is required. Vanilla free-fire supplies a point 50 km ahead rather than a player-selected GPS coordinate, so targetless Blackout launches are rejected. Explicit launches against ordinary tanks are rejected. No aircraft suppression occurs before this activation gate is met.

Once active, the missile emits continuously for 20 seconds. Coverage follows the missile within a 10 km radius, refreshes every 0.25 seconds and respects static terrain shielding. Enemy ground radar, laser defenses and missile-turret acquisition are suppressed; gun CIWS retains degraded aiming. Native jamming events provide map indicators.

Aircraft of every faction in the emission zone, including the launch aircraft and other friendly aircraft, lose radar contacts and access to remote datalink contacts. Local optical observations remain usable. Ground electronics and aircraft recover four seconds after their last HPM exposure. They remain suppressed through the first three seconds; overlapping active emitters extend the outage. The old 18-second recovery configuration is migrated automatically. This is applied per receiver without deleting the shared faction tracking database. Optical, inertial, infrared, laser-guided and unguided weapons retain native launch behavior even while datalink is unavailable. Only ARH/SARH weapon launches retain the radar-contact gate; native seekers still determine whether guidance succeeds.

The nominal terrain clearance is 35 m. Forward/downward probes inspect terrain and roofs up to six seconds ahead, including a swept corridor for narrow structures. Pitch requests ramp gradually and native maneuver limits remain in control; late obstacle detection can still result in a crash. Impact detonation uses the small native 2 kg HE charge, and disabled custom visuals are cleaned up.

The missile holds its pass until the full emission charge expires, then attacks its remembered surviving unit. A fallback target may be selected only after emission ends if the original unit is gone; the remembered last target position remains the fallback when no suitable unit is nearby.

External and internal racks offer one, two or three missiles, with a maximum of three per rack. Compatible aircraft stations are expanded from the native ALM-C450 and AGM-68 heavy-missile options, with each rack limited by the station's native ammunition capacity. Rack placement, weapon icons and exhaust appearance are local Unity assets and are preserved during runtime-only updates.

## CBU-82M Locust

Price: $900,000 per dispenser.

Locust remains the original unguided 400 kg CCIP dispenser. It opens before deploying eight conventional contact mines at approximately 325 m above terrain, independently of selected targets. Each mine contains 7 kg HE, arms four seconds after landing, reacts to vehicles or aircraft within 3 m and self-destructs after 210 seconds. Damage uses native explosions. The original flat mine model is restored.

## CBU-82S Lawn Chair

![CBU-82S Lawn Chair dispenser](docs/gallery/lawn-chair.png)

![Zhdan sensor mine model](docs/gallery/zhdan-mine.png)

The dispenser shares the Locust exterior. The Zhdan mine is rendered upright with deployed stabilizers, matching its in-game waiting pose.

Lawn Chair is a separate selectable dispenser: it sits around waiting for visitors. Think of it as the least relaxing lawn furniture on the battlefield: deploy it near roads or vehicle routes to set up a patient optical ambush. It carries eight Zhdan sensor mines, without replacing Locust. The mines descend vertically with stabilizers down, with fall speed limited to 18 m/s, and rest on the surface normal. They arm four seconds after touchdown and scan every 0.75 seconds for enemy GroundVehicle units within 70 m. Aircraft, buildings, allies and unknown factions are excluded. Detection requires both the 70 m sensor radius and a clear ground-level line of sight. Road decks, bridges, terrain and other solid obstacles can hide a vehicle from the mine. Before hopping, the mine checks the upward corridor and the attack path from its predicted apex; blocked corridors leave it waiting.

A mine reserves its target across all sensor mines, then visibly hops upward on a roughly four-second ballistic arc with a slight spin. At its 80 m apex the mine is replaced by one unmodified vanilla GS25 aimed at the reserved target. The reservation is refreshed throughout the hop and remains for five seconds after handoff. Swept collision checks cover the upward path and the dive toward the target; a blocked hop or dive cancels the attack and releases the reservation, rather than spawning a weapon through bridge decks or overhead roads. Ownership and faction are inherited from the original launcher. Unused sensor mines expire after 300 seconds without a contact explosion. Each sensor mine gets one shot; neighboring mines coordinate their targets instead of all firing at the same vehicle. Native GS25 guidance and damage remain unchanged. Mission and multiplayer testing remain pending.

Custom deployment, scanning and hopping logic is restricted to SinglePlayer/Multiplayer mission states. Encyclopedia previews remain static, without door opening or mine spawning.

Both dispensers preserve the current user-edited rack positions and support the same aircraft as Locust. External racks carry one, two or three dispensers; internal racks carry one or two, subject to station capacity. Compatibility includes native 250 kg bomb stations; physical dispenser mass remains 400 kg.

## Vanilla aircraft compatibility

The table is verified against the carrier operations serialized in the v0.1.18 bundle and the imported vanilla aircraft definitions (game API 0.34.2). Yes means at least one compatible hardpoint; rack sizes and internal/external options depend on the individual station's capacity. These registrations still require loadout and mission checks in-game.

| Vanilla aircraft | AGM-180 Blackout | CBU-82M Locust | CBU-82S Lawn Chair |
| --- | --- | --- | --- |
| CI-22 Cricket | Yes | Yes | Yes |
| T/A-30 Compass | Yes | Yes | Yes |
| VT-7 Vagrant | Yes | Yes | Yes |
| A-19 Brawler | Yes | Yes | Yes |
| FS-12 Revoker | Yes | Yes | Yes |
| FS-20 Vortex | Yes | Yes | Yes |
| KR-67 Ifrit | Yes | Yes | Yes |
| EW-25 Medusa | No | Yes | Yes |
| SFB-81 Darkreach | Yes | Yes | Yes |
| Alkyon AB-4 | Yes | Yes | Yes |

Locust contains conventional 7 kg contact mines. Lawn Chair contains sensor mines that launch vanilla GS25 submunitions. Neither mine is a separate aircraft loadout item; select its corresponding dispenser.

## Installed mod-aircraft compatibility

The v0.1.13 compatibility profile follows weapon options in the installed aircraft bundles. MiG-29 is deliberately excluded. Rack counts follow each native station's capacity; Blackout never exceeds three per rack.

| Aircraft | Blackout | Locust / Lawn Chair |
| --- | --- | --- |
| FS-41 Eclipse | Yes | Yes |
| CI-23 Camel | Yes | Yes |
| FS-3 Ternion | Yes | Yes |
| F-16M King Viper | Yes | Yes |
| F-99 Shrike | Yes | Yes |
| XFS-21 Helios | Yes | Yes |
| KR-33 Agni | Yes | Yes |
| F-22E Strike Raptor | No matching heavy-missile station | Yes |

Compatibility is serialized through Blueprinter carrier operations. These new aircraft placements require in-game checks; their original rack geometry and missile/bomb behavior are preserved.

## Requirements and installation

- Nuclear Option; the imported API used for development is 0.34.2.
- BepInEx.
- Blueprinter 2.0.1 or later.

Place the verified `Circuit-Breaker.dll` in `BepInEx/plugins`. Do not also install its embedded `.nobp` separately. Peers should use the same DLL for multiplayer.

## Building

For a runtime-only build, supply the intended local bundle as `Tools~/Runtime/Bundle/CircuitBreaker.nobp`, along with the matching game and Blueprinter reference assemblies, then run:

```powershell
& './Tools~/Build.ps1' -GameDir 'D:/Games/Nuclear Option' -BlueprinterProject 'D:/Mods/Blueprinter-Editor'
```

The script builds Release, verifies the embedded bundle SHA-256 against the input, and writes `Delivery~/Circuit-Breaker.dll` and a hash report. Runtime and bundle versions can differ when an unchanged approved bundle is reused.

To update visuals, use a complete local Blueprinter asset workspace in Unity 2022.3.62f3. The editor code provides a build-only action that packages existing assets. Avoid regenerating weapons or racks over manually edited icons, exhaust and pylon placement. This repository alone cannot reconstruct the omitted visual assets.

`Tools~/AuditBundle.py` checks the actual UnityFS bundle and requires Python with UnityPy. `Tools~/Tests` contains numerical guidance and HPM activation/scope checks. Game assemblies and local bundles must not be committed.

## Source layout

- `Editor`: local Unity/Blueprinter asset-generation and packaging code.
- `Tools~/Runtime`: launch designation, guidance, collision handling, suppression, receiver-specific datalink filtering and mines.
- `Tools~/Tests`: focused standalone policy and numerical checks.
- `Tools~/Build.ps1`: verified single-DLL packaging.
- `Tools~/PrepareRepository.ps1`: prepares an isolated publishing checkout with code and text only.

## Validation

The startup regression test executes all seven production configuration bindings against the installed BepInEx library, including migration of the old 18-second recovery setting. Versions v0.1.12-v0.1.14 contain an invalid equal-min/max range that can abort plugin initialization; update to v0.1.15.
This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.

Release compilation, bundle/reference checks, embedded-resource hashes and focused numerical/policy tests are structural evidence. Earlier user mission tests confirmed ground air-defense suppression. The latest launch-target activation, aircraft datalink filtering, collision cleanup and multiplayer behavior still require mission verification.

This is a community mod and is not affiliated with the game's developers.
