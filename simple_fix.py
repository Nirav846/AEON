import json, os

# Read the current proposed file line by line
with open('D:/AEON/data/inbox/proposed/cricket.jsonl') as f:
    lines = f.readlines()

# Print all lines with ids for debugging
for i, line in enumerate(lines, 1):
    line_stripped = line.strip()
    if not line_stripped:
        print(f'Line {i}: (empty)')
        continue
    try:
        r = json.loads(line_stripped)
        rid = r.get('id')
        name = r.get('name', '?')[:30]
        print(f'Line {i}: id={rid} name={name}')
    except json.JSONDecodeError as e:
        print(f'Line {i}: INVALID JSON - {str(e)[:50]}')

print()

# Keep only the first occurrence of each id in range 224-235
seen_ids = set()
kept = []
for line in lines:
    line_stripped = line.strip()
    if not line_stripped:
        continue
    try:
        r = json.loads(line_stripped)
        rid = r.get('id')
        if 224 <= rid <= 235 and rid not in seen_ids:
            seen_ids.add(rid)
            kept.append(line if line.endswith('\n') else line + '\n')
        else:
            print(f'Dropping id {rid} (already seen or out of range)')
    except json.JSONDecodeError:
        print(f'Dropping invalid line')

# Write the cleaned file
with open('D:/AEON/data/inbox/proposed/cricket.jsonl', 'w') as f:
    for line in kept:
        f.write(line)

print(f'\nFinal count: {len(kept)} records')
with open('D:/AEON/data/inbox/proposed/cricket.jsonl') as f:
    ids = [json.loads(l).get('id') for l in f if l.strip()]
    print(f'Ids: {sorted(ids)}')
    print(f'Count check: {len(ids)} should be 12')