import json, glob, os

# Read all jsonl files to check for id collisions
all_live_ids = set()
for sport in ['cricket', 'tennis', 'badminton', 'pickleball', 'hyrox']:
    path = f'D:/AEON/data/complexes/{sport}.jsonl'
    if os.path.exists(path):
        with open(path) as f:
            for line in f:
                r = json.loads(line.strip())
                all_live_ids.add(r.get('id'))

# Read the current proposed file
with open('D:/AEON/data/inbox/proposed/cricket.jsonl') as f:
    lines = f.readlines()

# Parse each line, keep only valid records with ids 224-235
kept = []
for line in lines:
    line = line.strip()
    if not line:
        continue
    try:
        r = json.loads(line)
        rid = r.get('id')
        # Keep only records with ids 224-235 (the 12 valid ones)
        if rid in [224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235]:
            # Verify no collision with live ids
            if rid not in all_live_ids:
                kept.append((rid, r))
                print(f'Kept id {rid}: {r.get("name")}')
            else:
                print(f'SKIP id {rid}: {r.get("name")} - collides with live')
        else:
            print(f'SKIP id {rid}: {r.get("name")} - not in target range')
    except json.JSONDecodeError as e:
        print(f'SKIP invalid JSON: {line[:50]}...')

# Write the filtered file
with open('D:/AEON/data/inbox/proposed/cricket.jsonl', 'w') as f:
    for rid, r in kept:
        f.write(json.dumps(r, ensure_ascii=False) + '\n')

print(f'\nWrote {len(kept)} records to proposed file')
print(f'Proposed ids: {[r[0] for r in kept]}')