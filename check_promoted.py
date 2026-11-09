import json

# Check the 13 promoted records' status in live data
with open('D:/AEON/data/complexes/cricket.jsonl') as f:
    for line in f:
        r = json.loads(line.strip())
        rid = r.get('id', 0)
        if rid in [213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225]:
            print(f'id {rid}: {r["name"]} status={r["status"]} role={r["role"]}')

# Also check original approved count before promotions
print()
print('Checking original approveds...')
with open('D:/AEON/data/complexes/cricket.jsonl') as f:
    original_approved = []
    for line in f:
        r = json.loads(line.strip())
        if r.get('role') == 'Batsman' and r.get('status') == 'approved' and r.get('id', 0) < 213:
            original_approved.append(r['id'])
    print(f'Original approved Batsman ids (< 213): {original_approved}')
    print(f'Count: {len(original_approved)}')