#!/usr/bin/env python3
"""Build build/app-bundle.json and build/bundle-meta.json for the coaching app.

Reads every active record from the four data sources and writes one JSON object
the app can fetch in a single request. Only `approved` records are included, plus
records that carry no `status` field at all (see the note on concepts below).

Outputs:
  build/app-bundle.json  - the full dataset
  build/bundle-meta.json - tiny manifest: version, generated_at, record_counts

The app fetches bundle-meta.json first, compares `version` against what it has
cached, and only re-downloads the full bundle when the hash has changed. Both
files are served straight from the repo by GitHub, so the clients read a static
file and never talk to a live database.

Served at:
  https://raw.githubusercontent.com/Nirav846/AEON/main/build/app-bundle.json
  https://raw.githubusercontent.com/Nirav846/AEON/main/build/bundle-meta.json

Note on concepts: data/concepts.jsonl records carry no `status` field - they are
a different record type with no promote/reject lifecycle. A strict
`status == "approved"` filter would silently drop all 15 of them, so a missing
status is treated as active. Anything explicitly proposed, rejected or superseded
is still excluded wherever the field is present.
"""
import json, glob, hashlib
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BUILD = ROOT / "build"

ACTIVE_STATUSES = {"approved"}   # explicit opt-in to inclusion
SOURCES = [
    ("complexes", sorted(glob.glob(str(ROOT / "data/complexes/*.jsonl")))),
    ("circuits", [ROOT / "data/circuits.jsonl"]),
    ("conditioning", [ROOT / "data/conditioning.jsonl"]),
    ("concepts", [ROOT / "data/concepts.jsonl"]),
]


def is_active(rec):
    status = rec.get("status")
    return status is None or status in ACTIVE_STATUSES


bundle, counts, excluded = {}, {}, []

for kind, paths in SOURCES:
    rows = []
    for path in paths:
        if not Path(path).exists():
            print(f"  ! missing source, skipped: {path}")
            continue
        for lineno, line in enumerate(open(path, encoding="utf-8"), 1):
            if not line.strip():
                continue
            rec = json.loads(line)
            if not is_active(rec):
                excluded.append((kind, rec.get("id"), rec.get("status")))
                continue
            rows.append(rec)
    rows.sort(key=lambda r: r.get("id", 0))
    bundle[kind] = rows
    counts[kind] = len(rows)

# Content hash over the payload itself, so the version tracks the DATA and not
# the clock. generated_at is excluded deliberately - otherwise every run would
# produce a new version and the app would re-download for no reason.
payload = json.dumps(
    {"complexes": bundle["complexes"], "circuits": bundle["circuits"],
     "conditioning": bundle["conditioning"], "concepts": bundle["concepts"]},
    sort_keys=True, ensure_ascii=False, separators=(",", ":"),
)
version = hashlib.sha256(payload.encode("utf-8")).hexdigest()[:16]
generated_at = datetime.now(timezone.utc).isoformat(timespec="seconds")

bundle["version"] = version
bundle["generated_at"] = generated_at
# Key order for the written file: the four arrays first, then the metadata.
ordered = {k: bundle[k] for k in ("complexes", "circuits", "conditioning", "concepts")}
ordered["version"] = version
ordered["generated_at"] = generated_at

BUILD.mkdir(exist_ok=True)
bundle_path = BUILD / "app-bundle.json"
with open(bundle_path, "w", encoding="utf-8") as f:
    json.dump(ordered, f, ensure_ascii=False, separators=(",", ":"))
    f.write("\n")

meta_path = BUILD / "bundle-meta.json"
with open(meta_path, "w", encoding="utf-8") as f:
    json.dump({"version": version, "generated_at": generated_at,
               "record_counts": counts}, f, ensure_ascii=False, indent=2)
    f.write("\n")

size = bundle_path.stat().st_size
print(f"wrote {bundle_path.relative_to(ROOT)}  ({size:,} bytes, {size/1024:.1f} KB)")
print(f"wrote {meta_path.relative_to(ROOT)}  ({meta_path.stat().st_size:,} bytes)")
print(f"  version      : {version}")
print(f"  generated_at : {generated_at}")
print(f"  records      : {counts}  total={sum(counts.values())}")
if excluded:
    from collections import Counter
    by = Counter(f"{k}/{s}" for k, _, s in excluded)
    print(f"  excluded     : {len(excluded)} ({', '.join(f'{k}={v}' for k, v in sorted(by.items()))})")
else:
    print("  excluded     : none")