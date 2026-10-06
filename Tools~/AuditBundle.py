"""Check the actual UnityFS delivery bundle, independently of the source YAML."""
from pathlib import Path
import json
import UnityPy

root = Path(__file__).resolve().parent.parent
bundle = root / 'Delivery~' / 'Circuit Breaker_0.1.2.nobp'
env = UnityPy.load(str(bundle))
base = 'assets/blueprinter/mods/iron gate/'
report = []
for name in ['blackout.prefab', 'locust.prefab', 'locustmine.prefab',
             'blackoutdeploy.anim', 'locustopen.anim', 'locustminedeploy.anim',
             'models/champ.fbx', 'models/clusterbomb.fbx', 'models/submunition_mine.fbx']:
    assert base + name in env.container, name
    report.append('PRESENT ' + name)

objects = {o.path_id: o for o in env.objects}
for name, expected in [('blackout', 2), ('locust', 0), ('locustmine', 10)]:
    go = env.container[base + name + '.prefab'].read_typetree()
    components = [objects[c['component']['m_PathID']] for c in go['m_Component']]
    data = [o.read_typetree() for o in components if o.type.name == 'MonoBehaviour']
    missiles = [d for d in data if 'blastYield' in d]
    assert len(missiles) == 1
    missile = missiles[0]
    assert missile['blastYield'] == expected, (name, missile['blastYield'])
    assert missile['impactFuse'] == (name == 'blackout')
    if name == 'blackout':
        assert len(missile['foldingFins']) == 4
    else:
        assert len(missile['motors']) == 0
    report.append(f'CHECKED {name}: native HE yield {expected} kg; impact fuse {missile["impactFuse"]}')

locust_info = env.container[base + 'wi_locust.asset'].read_typetree()
assert locust_info['bomb'] and locust_info['gravMult'] == 1
assert not locust_info['glideBomb'] and not locust_info['missile']
report.append('CHECKED native unguided CCIP WeaponInfo flags')

manifest = json.loads(env.container['assets/blueprinter/generated/patch_manifest.json'].read().m_Script.lstrip('\ufeff'))
assert manifest['modName'] == 'Circuit Breaker' and manifest['modVersion'] == '0.1.2'
ops = [json.loads(o['payloadJson']) for o in manifest['Ops'] if o['opId'] == 'OpAddWeaponToHardpoint']
assert len(ops) == 11
assert all(op['aircraft'] for op in ops)
assert len(manifest['Patches']) > 0
report.append(f'CHECKED {len(ops)} carrier operations and {len(manifest["Patches"])} vanilla-reference patches')
mounts = [(path, reader.read_typetree()) for path, reader in env.container.items()
          if path.startswith(base + 'wm_blackout')]
assert len(mounts) == 6
assert sorted(m['ammo'] for _, m in mounts) == [1, 1, 2, 2, 3, 3]
locust_mounts = [reader.read_typetree() for path, reader in env.container.items() if path.startswith(base + 'wm_locust')]
assert sorted(m['ammo'] for m in locust_mounts) == [1, 1, 2, 2, 3]
for path, mount in mounts:
    assert 1 <= mount['ammo'] <= 3, path
    report.append(f'CHECKED {mount["jsonKey"]}: {mount["ammo"]} missiles per rack')
for path, reader in env.container.items():
    if not path.startswith(base + 'wm_'):
        continue
    mount = reader.read_typetree()
    prefab = objects[mount['prefab']['m_PathID']].read_typetree()
    def count_stations(go):
        total = 0
        for entry in go['m_Component']:
            obj = objects[entry['component']['m_PathID']]
            data = obj.read_typetree()
            if obj.type.name == 'MonoBehaviour' and 'railDirection' in data:
                total += 1
            if obj.type.name == 'Transform':
                for child in data['m_Children']:
                    transform = objects[child['m_PathID']].read_typetree()
                    total += count_stations(objects[transform['m_GameObject']['m_PathID']].read_typetree())
        return total
    assert count_stations(prefab) == mount['ammo'], path
report.append('CHECKED all missile/bomb racks contain one visible launch station per round')
def station_transforms(go):
    components = [objects[e['component']['m_PathID']] for e in go['m_Component']]
    transforms = [o.read_typetree() for o in components if o.type.name == 'Transform']
    result = transforms[:] if any(o.type.name == 'MonoBehaviour' and 'railDirection' in o.read_typetree() for o in components) else []
    for transform in transforms:
        for child in transform['m_Children']:
            child_transform = objects[child['m_PathID']].read_typetree()
            result.extend(station_transforms(objects[child_transform['m_GameObject']['m_PathID']].read_typetree()))
    return result
for key in ['bomb_500_double', 'bomb_250_triple']:
    go = env.container[base + 'cb_locust_' + key + '.prefab'].read_typetree()
    positions = [t['m_LocalPosition'] for t in station_transforms(go)]
    if key.endswith('double'):
        assert all(abs(p['x']) < .001 for p in positions)
    else:
        assert len({round(p['y'], 3) for p in positions}) == 2
        assert sum(abs(p['x']) < .001 for p in positions) == 1
report.append('CHECKED actual bundle: double bombs align with their rails; triple bombs retain triangular mounting')
for texture in ['champ_albedo.png', 'cbu_albedo.png', 'submunition_mine_albedo.png']:
    assert base + 'textures/' + texture in env.container
report.append('CHECKED all three supplied albedo textures are packaged')
for path, reader in env.container.items():
    if path.startswith(base + 'materials/') and reader.type.name == 'Material' and 'hpm' not in path:
        material = reader.read_typetree()
        maps = dict(material['m_SavedProperties']['m_TexEnvs'])
        texture = maps['_BaseMap']['m_Texture']['m_PathID']
        assert texture and objects[texture].type.name == 'Texture2D', path
report.append('CHECKED supplied textures are referenced by model materials')
report.append('Game assembly target: ' + manifest['gameVersion'])
report.append('BUNDLE_AUDIT_PASSED. Live mission/multiplayer behavior remains unverified.')
text = '\n'.join(report) + '\n'
(root / 'Validation~' / 'bundle.txt').write_text(text, encoding='utf-8')
print(text)
