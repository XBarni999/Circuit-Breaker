"""Extract loadout compatibility from installed Blueprinter aircraft bundles."""
import json
import re
from pathlib import Path
import UnityPy

root = Path(__file__).resolve().parent.parent
native = root.parents[1] / '_donotship' / 'MonoBehaviour'
rows = {}
for bundle in sorted((root / 'Tools~' / 'AircraftBundles').iterdir()):
    if not any(name in bundle.name.lower() for name in ('eclipse', 'f16', 'f22', 'f99', 'camel', 'ternion', 'specter', 'helios', 'palafighter', 'apex', 'meridian')):
        continue
    print('Inspecting ' + bundle.name, flush=True)
    env = UnityPy.load(str(bundle))
    manifest_reader = dict(env.container.items()).get('assets/blueprinter/generated/patch_manifest.json')
    if not manifest_reader:
        continue
    manifest = json.loads(manifest_reader.read().m_Script.lstrip('\ufeff'))
    definitions = {}
    for path, reader in env.container.items():
        if not path.endswith('.asset'):
            continue
        data = reader.read_typetree()
        if not data.get('jsonKey') or not data.get('unitPrefab') or data.get('typeIdentity', {}).get('air', 0) <= 0:
            continue
        if re.search(r'mig.?29', data['jsonKey'] + data.get('unitName', ''), re.I):
            continue
        prefab_id = data['unitPrefab']['m_PathID']
        for prefab_path, prefab in env.container.items():
            if prefab.path_id == prefab_id:
                definitions[prefab_path.lower()] = (data['jsonKey'], data.get('unitName', data['jsonKey']))
    for patch in manifest['Patches']:
        key = patch['GameAsset']['asset']['locator']
        if 'WeaponMount' not in patch['GameAsset']['asset']['type'] or not key.startswith(('bomb_250_', 'CruiseMissile1_internal', 'AGM_heavy_')):
            continue
        native_file = native / (key + '_PLACEHOLDER.asset')
        if not native_file.exists():
            raise RuntimeError('Missing native mount ' + str(native_file))
        ammo_match = re.search(r'^  ammo: (\d+)', native_file.read_text(), re.M)
        if not ammo_match: continue
        ammo = int(ammo_match[1])
        for location in patch['PatchLocations']:
            match = re.fullmatch(r'hardpointSets\[(\d+)\]\.weaponOptions\[\d+\]', location['memberPath'])
            identity = definitions.get(location['asset']['locator'].lower())
            if not match or not identity:
                continue
            row = dict(aircraftJsonKey=identity[0], unitName=identity[1], index=int(match[1]), sourceWeaponKey=key, ammo=ammo, internalMount=('internal' in key or 'rotary' in key))
            rows[(identity[0], int(match[1]), key)] = row
    print('  aircraft: ' + ', '.join(sorted({value[1] for value in definitions.values()})), flush=True)
    del env
output = root / 'Editor' / 'InstalledAircraftCompatibility.json'
output.write_text(json.dumps(dict(entries=sorted(rows.values(), key=lambda r: (r['aircraftJsonKey'], r['index'], r['sourceWeaponKey']))), indent=2) + '\n', encoding='utf8')
print(f'Wrote {len(rows)} native weapon options for {len({r[0] for r in rows})} aircraft', flush=True)
