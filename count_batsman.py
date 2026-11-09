import json

total = 0
approved = 0
with open('D:/AEON/data/complexes/cricket.jsonl') as f:
    for line in f:
        r = json.loads(line.strip())
        if r.get('role') == 'Batsman':
            total += 1
            if r.get('status') == 'approved':
                approved += 1

print(f'Total Batsman: {total}')
print(f'Approved Batsman: {approved}')