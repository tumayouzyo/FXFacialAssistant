"""Build VPM and legacy archives without importing a Unity project."""

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import tarfile
import zipfile


ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'dist')
    args = parser.parse_args()
    manifest = json.loads((ROOT / 'package.json').read_text(encoding='utf-8'))
    version = manifest['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?', version):
        raise ValueError('Invalid package version')
    filename = f"{manifest['name']}-{version}.zip"
    expected = f'https://github.com/tumayouzyo/FXFacialAssistant/releases/download/v{version}/{filename}'
    if manifest['url'] != expected:
        raise ValueError('Package URL must match the release version and ZIP filename')
    files = sorted(p for p in (ROOT / 'Editor').rglob('*')
                   if p.is_file() and 'Tests' not in p.relative_to(ROOT).parts and p.name != 'Tests.meta')
    guids = set()
    for path in files:
        if path.suffix != '.meta' and not path.with_name(path.name + '.meta').is_file():
            raise ValueError(f'Missing .meta: {path}')
        if path.suffix == '.meta':
            guid = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.MULTILINE)
            if not guid or guid[1] in guids:
                raise ValueError(f'Invalid or duplicate GUID: {path}')
            guids.add(guid[1])
    args.output.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output / filename, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in files + [ROOT / name for name in ('Editor.meta', 'package.json', 'LICENSE', 'README.md', 'CHANGELOG.md')]:
            archive.write(path, path.relative_to(ROOT).as_posix())
        for path in sorted((ROOT / 'Documentation~').rglob('*')):
            if path.is_file():
                archive.write(path, path.relative_to(ROOT).as_posix())
    with tarfile.open(args.output / f'FXFacialAssistant_{version}.unitypackage', 'w:gz') as archive:
        for meta in [ROOT / 'Editor.meta'] + [p for p in files if p.suffix == '.meta']:
            content = meta.read_bytes()
            guid = re.search(rb'^guid: ([0-9a-f]{32})', content, re.MULTILINE)[1].decode()
            asset = meta.with_suffix('')
            pathname = 'Assets/TY_FXFacialAssistant/' + asset.relative_to(ROOT).as_posix()
            entries = {'asset.meta': content, 'pathname': pathname.encode()}
            if asset.is_file():
                entries['asset'] = asset.read_bytes()
            for name, data in entries.items():
                info = tarfile.TarInfo(f'{guid}/{name}')
                info.size = len(data)
                archive.addfile(info, io.BytesIO(data))
    (args.output / 'package.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    digest = hashlib.sha256((args.output / filename).read_bytes()).hexdigest()
    print(f'{filename}: SHA256 {digest}; {len(guids)} asset GUIDs')


if __name__ == '__main__':
    main()
