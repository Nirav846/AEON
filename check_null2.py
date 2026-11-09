import json

# Check specifically for explicit swap_equipment: null in proposed cricket
with open('D:/AEON/data/inbox/proposed/cricket.jsonl') as f:
    for line in f:
        r = json.loads(line.strip())
        eq = r.get('swap_equipment')
        if eq is None:
            pid = r.get('id', '?')
            print(f'id {pid}: swap_equipment key exists and is NULL')
        elif eq == []:
            pid = r.get('id', '?')
            print(f'id {pid}: swap_equipment key exists and is EMPTY LIST []')

print('Done - all records checked')