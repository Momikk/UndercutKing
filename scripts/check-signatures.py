"""Read-only verification of published hooks against the local CN executable .text section."""
import hashlib
import json
import re
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[1]
config = json.loads((Path.home() / 'AppData/Roaming/XIVLauncherCN/launcherConfigV3.json').read_text(encoding='utf-8-sig'))
exe = Path(config['GamePath']) / 'game/ffxiv_dx11.exe'
data = exe.read_bytes()
pe = struct.unpack_from('<I', data, 0x3c)[0]
sections = struct.unpack_from('<H', data, pe + 6)[0]
optional = struct.unpack_from('<H', data, pe + 20)[0]
text = None
for i in range(sections):
    pos = pe + 24 + optional + 40 * i
    if data[pos:pos + 8].rstrip(b'\0') == b'.text':
        _, rva, size, raw = struct.unpack_from('<IIII', data, pos + 8)
        text = data[raw:raw + size]
        break
assert text is not None, 'Executable has no .text section'
source = (root / 'UndercutKing/NativeReader.cs').read_text(encoding='utf-8-sig')
results = {}
for name, signature in re.findall(r'const string (\w+Signature) = "([0-9A-F? ]+)"', source):
    pattern = b''.join(b'.' if byte == '??' else re.escape(bytes.fromhex(byte)) for byte in signature.split())
    matches = list(re.finditer(pattern, text, re.DOTALL))
    assert len(matches) == 1, f'{name} must match exactly once; found {len(matches)}'
    results[name] = {'matches': 1, 'rva': hex(rva + matches[0].start())}
print(json.dumps({'gameVersion': (exe.parent / 'ffxivgame.ver').read_text().strip(),
                  'sha256': hashlib.sha256(data).hexdigest(), 'signatures': results}, indent=2))
