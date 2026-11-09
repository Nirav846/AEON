import json

# Check tennis.jsonl id collisions
with open('D:/AEON/data/complexes/tennis.jsonl') as f:
    tennis_ids = set()
    for line in f:
        r = json.loads(line.strip())
        tennis_ids.add(r.get('id'))

# Check proposed cricket.jsonl id collisions
with open('D:/AEON/data/inbox/proposed/cricket.jsonl') as f:
    cricket_proposed_ids = set()
    for line in f:
        r = json.loads(line.strip())
        cricket_proposed_ids.add(r.get('id'))

# Check live cricket.jsonl id collisions
with open('D:/AEON/data/complexes/cricket.jsonl') as f:
    cricket_live_ids = set()
    for line in f:
        r = json.loads(line.strip())
        cricket_live_ids.add(r.get('id'))

print('Tennis ids:', sorted(tennis_ids))
print('Cricket proposed ids:', sorted(cricket_proposed_ids))
print('Cricket live ids:', sorted(cricket_live_ids))

collisions = tennis_ids & cricket_proposed_ids
if collisions:
    print('COLLISIONS between tennis and cricket proposed:', sorted(collisions))
else:
    print('No collisions between tennis and cricket proposed')