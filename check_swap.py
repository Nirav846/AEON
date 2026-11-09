import json

with open('D:/AEON/data/inbox/proposed/cricket.jsonl') as f:
    for i, line in enumerate(f, 1):
        r = json.loads(line.strip())
        eq_raw = r.get('swap_equipment', [])
        eq = eq_raw if eq_raw is not None else []
        sw = [e for e in eq if isinstance(e, str)]
        pid = r.get('id', '?')
        print(f'id {pid}: swap_equipment raw={r.get("swap_equipment")}, resolved={eq}, sw={sw}')