#!/usr/bin/env python3
"""Rebuild build/index.jsonl from data/complexes/*.jsonl, data/circuits.jsonl,
and data/conditioning.jsonl. Run this after promote.py, or after any manual
edit to a sport file. Never edit build/index.jsonl by hand.

Superseded complexes are excluded from the index by default so they don't clutter
normal browsing. Pass --include-superseded to keep them, marked "SUPERSEDED" in
the cat field."""
import json, glob, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
INCLUDE_SUPERSEDED = "--include-superseded" in sys.argv
skipped = []

index = []
for path in sorted(glob.glob(str(ROOT / "data/complexes/*.jsonl"))):
    for line in open(path):
        r = json.loads(line)
        if r.get("status") == "superseded" and not INCLUDE_SUPERSEDED:
            skipped.append((r["id"], r["name"]))
            continue
        index.append({"type": "complex", "id": r["id"], "name": r["name"],
                      "sport": r["sport"], "role": r["role"],
                      "cat": ("SUPERSEDED: " if r.get("status") == "superseded" else "") + r["category"],
                      "equip": r.get("equipment", [])})

circuits_path = ROOT / "data/circuits.jsonl"
if circuits_path.exists():
    for line in open(circuits_path):
        r = json.loads(line)
        index.append({"type": "circuit", "id": r["id"], "name": r["name"],
                      "sport": r["sport"], "role": r["role"],
                      "cat": r.get("energy_system", ""), "equip": []})

conditioning_path = ROOT / "data/conditioning.jsonl"
if conditioning_path.exists():
    for line in open(conditioning_path):
        r = json.loads(line)
        index.append({"type": "conditioning", "id": r["id"], "name": r["name"],
                      "sport": r["sport"], "role": r.get("modality", ""),
                      "cat": r.get("work_to_rest", ""), "equip": []})

index.sort(key=lambda x: (x["type"], x["id"]))
out_path = ROOT / "build/index.jsonl"
with open(out_path, "w") as f:
    for r in index:
        f.write(json.dumps(r, ensure_ascii=False) + "\n")

print(f"wrote {len(index)} records to {out_path}")
if skipped:
    print(f"excluded {len(skipped)} superseded complex(es): "
          + ", ".join(f"{i} '{n}'" for i, n in skipped))
    print("  (re-run with --include-superseded to list them)")
