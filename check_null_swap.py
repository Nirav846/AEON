import json, glob, os

# Check ALL jsonl files for swap_equipment: null
found_null = []
for filepath in glob.glob('D:/AEON/data/**/*.jsonl', recursive=True):
    with open(filepath) as f:
        for i, line in enumerate(f, 1):
            r = json.loads(line.strip())
            eq = r.get('swap_equipment')
            if eq is None:
                found_null.append((filepath, r.get('id'), r.get('name')))

print(f'Found {len(found_null)} records with swap_equipment: null')
for f, idx, name in found_null[:20]:
    print(f'  {f.split("/")[-1]} id={idx} name={name[:50]}')