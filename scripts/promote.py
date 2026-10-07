#!/usr/bin/env python3
"""
Promote complexes from the sport-scoped inbox (data/inbox/proposed/<sport>.jsonl)
into data/complexes/, assigning each the next sequential id.

SAFETY INVARIANT: this script NEVER writes without --confirm.
Every invocation that would write (promote, supersede, or both) only PREVIEWS
unless --confirm is explicitly passed. Previewing is the default; writing is the
opt-in. A real write goes straight into data/complexes/ and cannot be undone by
this script, so the safe path must be the default path.

  promote.py badminton "Name"                     -> PREVIEW ONLY (no --confirm)
  promote.py badminton "Name" --confirm           -> writes for real
  promote.py badminton "Name" --supersede 21 --confirm
  promote.py --all-approved --confirm             -> writes every approved record

--dry-run is accepted as a no-op alias for backward compatibility. It is no
longer load-bearing: the absence of --confirm is what makes this safe.

Usage:
  python3 scripts/promote.py "Exact Complex Name From Inbox"
  python3 scripts/promote.py badminton "Exact Complex Name From Inbox"
  python3 scripts/promote.py --all-approved     # everything marked approved,
                                                 # across every sport inbox
  python3 scripts/promote.py badminton --all-approved   # one sport only
  python3 scripts/promote.py badminton "New Name" --supersede 21 --confirm
                                                 # promote AND mark the old
                                                 # approved record (id 21) as
                                                 # "superseded" — kept for
                                                 # history, not deleted

  python3 scripts/promote.py                     # list everything pending
  python3 scripts/promote.py --list             # same, explicit

--supersede refuses to run at all if the target id does not exist or is not
currently "approved". It never deletes the old record.

Only the coach runs this with --confirm (or explicitly tells the agent to).

A successful --confirm promotion also regenerates build/app-bundle.json and
build/bundle-meta.json, so the published bundle cannot silently fall behind the
data. It does NOT commit or push - that stays a deliberate, separate step. If the
bundle build fails, the promotion stands and the failure is reported loudly.

Omitting the sport searches every data/inbox/proposed/*.jsonl, which is fine
when the name is unique. Pass the sport when it is not, or when you want to be
explicit about which inbox is being read.
"""
import json, sys, glob, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
INBOX_DIR = ROOT / "data/inbox/proposed"
VALID_SPORTS = {"Cricket", "Tennis", "Badminton", "Pickleball", "Hyrox", "Universal"}

# Conditioning protocols are a different record type with their own schema and their
# own flat file, so they get their own inbox and their own promotion path rather than
# being forced through the sport-scoped complex one.
CONDITIONING_INBOX = INBOX_DIR / "conditioning.jsonl"
CONDITIONING_FILE = ROOT / "data/conditioning.jsonl"


def next_id():
    max_id = 0
    for path in glob.glob(str(ROOT / "data/complexes/*.jsonl")):
        for line in open(path):
            r = json.loads(line)
            max_id = max(max_id, r.get("id", 0))
    return max_id + 1


def next_conditioning_id():
    """Next free conditioning id.

    Scoped to data/conditioning.jsonl only. Conditioning ids (1-33) deliberately
    overlap complex ids, so next_id() would hand back 197+ and skip the range. Ids
    are reused after a supersede-free reject, so this is a plain max+1 over the
    conditioning file, exactly as next_id() does for complexes."""
    max_id = 0
    if CONDITIONING_FILE.exists():
        for line in CONDITIONING_FILE.read_text(encoding="utf-8").splitlines():
            if line.strip():
                max_id = max(max_id, json.loads(line).get("id", 0))
    return max_id + 1


def load_conditioning_inbox():
    if not CONDITIONING_INBOX.exists():
        return []
    return [json.loads(line) for line in
            CONDITIONING_INBOX.read_text(encoding="utf-8").splitlines() if line.strip()]


def save_conditioning_inbox(records):
    if not CONDITIONING_INBOX.exists() and not records:
        return
    body = "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in records)
    CONDITIONING_INBOX.write_bytes(body.encode("utf-8"))


def append_conditioning(record):
    with open(CONDITIONING_FILE, "a", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")


def load_inbox(sport=None):
    """Return [(source_path, record), ...] from the sport-scoped inbox.

    sport=None reads every file in the inbox directory."""
    paths = [INBOX_DIR / f"{sport.lower()}.jsonl"] if sport else sorted(INBOX_DIR.glob("*.jsonl"))
    out = []
    for path in paths:
        if not path.exists():
            continue
        for line in open(path, encoding="utf-8"):
            if line.strip():
                out.append((path, json.loads(line)))
    return out


def save_inbox(records_by_path):
    """Rewrite only the inbox files we actually touched.

    Writes bytes with explicit LF so Windows text-mode translation cannot
    silently rewrite line endings across the whole file."""
    for path, records in records_by_path.items():
        if not path.exists() and not records:
            continue
        body = "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in records)
        path.write_bytes(body.encode("utf-8"))


def promote_one(record, new_id):
    sport_file = ROOT / f"data/complexes/{record['sport'].lower()}.jsonl"
    record = dict(record)
    record["id"] = new_id
    record["status"] = "approved"
    with open(sport_file, "a", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")
    print(f"promoted '{record['name']}' -> {sport_file.name} as id {new_id}")


def parse_args(argv):
    """Return (sport, all_approved, name, supersede_id, confirm).

    --dry-run is accepted and deliberately ignored: previewing is the default,
    so an explicit "don't write" flag is redundant."""
    sport = name = supersede = None
    all_approved = confirm = False
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--all-approved":
            all_approved = True
        elif a == "--confirm":
            confirm = True
        elif a in ("--dry-run", "--preview"):
            pass  # no-op alias: preview is already the default
        elif a == "--supersede":
            i += 1
            if i >= len(argv):
                raise SystemExit("ERROR: --supersede requires an id, e.g. --supersede 21")
            try:
                supersede = int(argv[i])
            except ValueError:
                raise SystemExit(f"ERROR: --supersede expects a numeric id, got '{argv[i]}'")
        elif a.startswith("--"):
            raise SystemExit(f"ERROR: unknown flag '{a}'")
        elif a.capitalize() in VALID_SPORTS and sport is None:
            sport = a.capitalize()
        elif name is None:
            name = a
        i += 1
    return sport, all_approved, name, supersede, confirm


def find_supersede_target(target_id, records):
    """Locate the approved record to supersede. Returns ((path, raw_line, rec), error).

    records is a list of plain inbox record dicts."""
    sports = {r["sport"].lower() for r in records}
    if len(sports) > 1:
        return None, (f"--supersede {target_id} is ambiguous across {sorted(sports)}; "
                      "pass the sport argument to scope it")
    sport = sports.pop()
    path = ROOT / f"data/complexes/{sport}.jsonl"
    if not path.exists():
        return None, f"{path.name} does not exist"
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        rec = json.loads(line)
        if rec.get("id") == target_id:
            st = rec.get("status")
            if st != "approved":
                return None, (f"id {target_id} ('{rec.get('name')}') has status "
                              f"'{st}', not 'approved' — nothing to supersede")
            return (path, line, rec), None
    return None, f"no record with id {target_id} in {path.name}"


def apply_supersede(target):
    """Rewrite the target line in place with status 'superseded'. Returns new id."""
    path, old_line, rec = target
    new_line = old_line.replace('"status": "approved"', '"status": "superseded"', 1)
    if new_line == old_line:  # formatting differed, fall back to a clean dump
        new_line = json.dumps({**rec, "status": "superseded"}, ensure_ascii=False)
    json.loads(new_line)  # must still be valid JSON before we overwrite anything
    lines = path.read_text(encoding="utf-8").splitlines()
    out = []
    for line in lines:
        if line.strip() and json.loads(line).get("id") == rec["id"]:
            out.append(new_line)
        else:
            out.append(line)
    path.write_bytes(("\n".join(out) + "\n").encode("utf-8"))


def select(inbox, sport, flag, name):
    """Choose which inbox records to promote. Returns (matches, error_or_None)."""
    if flag:
        return [(p, r) for p, r in inbox if r.get("status") == "approved"], None
    if name:
        matches = [(p, r) for p, r in inbox if r["name"] == name]
        if not matches:
            where = f" in {sport.lower()}.jsonl" if sport else " in any inbox file"
            return [], f"no inbox entry named '{name}' found{where}"
        return matches, None
    return [], None


def rebuild_bundle():
    """Regenerate build/app-bundle.json + build/bundle-meta.json after a promotion.

    Called only on the --confirm write path. The published bundle is served
    straight from the repo by GitHub, so a complex promoted without a rebuild is
    invisible to the apps until the bundle is rebuilt AND pushed.

    Deliberately does NOT commit or push. A promotion is a data-correctness
    decision the coach makes; publishing it is a separate, deliberate act.

    If the build fails, it fails LOUDLY and does NOT roll anything back. The
    promotion has already been written and is correct; the bundle is merely
    stale. Rolling back a coaching decision because a publishing step hiccuped
    would be worse than the staleness it was trying to avoid.
    """
    script = ROOT / "scripts/build_bundle.py"
    if not script.exists():
        print("\nBUNDLE REBUILD FAILED: scripts/build_bundle.py not found.")
        print("  The promotion above SUCCEEDED and is safely written.")
        print("  The published bundle is now STALE - run scripts/build_bundle.py by hand.")
        return False
    try:
        proc = subprocess.run([sys.executable, str(script)],
                              capture_output=True, text=True)
    except Exception as exc:                      # noqa: BLE001 - never mask the promotion
        print(f"\nBUNDLE REBUILD FAILED to launch: {exc}")
        print("  The promotion above SUCCEEDED and is safely written.")
        print("  The published bundle is now STALE - run scripts/build_bundle.py by hand.")
        return False
    if proc.returncode != 0:
        print("\n" + "!" * 68)
        print("BUNDLE REBUILD FAILED - the published bundle is now STALE.")
        print("!" * 68)
        print(f"  exit code: {proc.returncode}")
        if proc.stdout.strip():
            print("  stdout:\n" + "\n".join("    " + l for l in proc.stdout.strip().splitlines()))
        if proc.stderr.strip():
            print("  stderr:\n" + "\n".join("    " + l for l in proc.stderr.strip().splitlines()))
        print("\n  The promotion above SUCCEEDED and is safely written.")
        print("  Nothing was rolled back - a failed publish must never undo a")
        print("  coaching decision. Fix the cause, then re-run:")
        print("      python scripts/build_bundle.py")
        print("  until the apps have been told about this promotion.")
        return False
    for line in proc.stdout.strip().splitlines():
        print("  " + line)
    print("\nBundle rebuilt. Still TODO (deliberately NOT automatic):")
    print("  git add build/ && git commit && git push")
    print("  Apps read the bundle from raw.githubusercontent.com, so they will keep")
    print("  serving the old version until this is pushed (1-2 min CDN delay after).")
    return True


def promote_conditioning_main(argv):
    """Promotion path for conditioning protocols.

    SAFETY INVARIANT, same as the complex path: PREVIEW ONLY unless --confirm is
    explicitly passed. A real write goes straight into data/conditioning.jsonl and
    cannot be undone by this script, so the safe path is the default.

    Deliberately minimal. Conditioning has no supersede lineage (all 33 records are
    flat 'approved'), so --supersede is refused rather than half-implemented. Adding
    a promotion that silently overwrote a protocol would be worse than not having one.

      promote.py conditioning --list
      promote.py conditioning "Name"              -> PREVIEW ONLY
      promote.py conditioning "Name" --confirm    -> writes for real
      promote.py conditioning --all-approved --confirm
    """
    args = [a for a in argv if a != "conditioning"]
    listing = "--list" in args
    all_approved = "--all-approved" in args
    confirm = "--confirm" in args

    if any(a == "--supersede" for a in args):
        print("REFUSING TO RUN: --supersede is not supported for conditioning.\n"
              "  Conditioning records have no supersede lineage, so there is no "
              "predecessor to flip.\n"
              "  Edit the record deliberately instead, or add lineage support first.")
        return

    unknown = [a for a in args
               if a not in ("--list", "--all-approved", "--confirm", "--dry-run", "--preview")]
    name = unknown[0] if unknown else None
    if len(unknown) > 1:
        print(f"ERROR: expected at most one name, got {unknown}")
        return

    inbox = load_conditioning_inbox()
    if listing or not inbox:
        if not inbox:
            print("conditioning inbox is empty — nothing pending")
            return
        for r in sorted(inbox, key=lambda x: x.get("id", 0)):
            print(f"  id {r.get('id'):>4}  [{r.get('status'):9s}] {r.get('name')}")
        return

    if all_approved:
        selected = [r for r in inbox if r.get("status") == "approved"]
    elif name:
        selected = [r for r in inbox if r.get("name") == name]
        if not selected:
            names = sorted(r.get("name", "") for r in inbox)
            print(f"no conditioning entry named '{name}'. Pending: {names}")
            return
    else:
        print("pending in data/inbox/proposed/conditioning.jsonl:")
        for r in sorted(inbox, key=lambda x: x.get("id", 0)):
            print(f"  id {r.get('id'):>4}  [{r.get('status'):9s}] {r.get('name')}")
        print("\nNothing written. Select with a name, or --all-approved.")
        return

    if not selected:
        print("nothing matches — 0 record(s) would be promoted. Nothing written.")
        return

    # --- preview first, always ---
    nid = next_conditioning_id()
    plan = []
    for r in selected:
        plan.append((r, nid))
        print(f"would promote inbox id {r.get('id')} '{r.get('name')}'\n"
              f"            -> data/conditioning.jsonl as id {nid} (status 'approved')")
        nid += 1

    if not confirm:
        print(f"\nPREVIEW ONLY — nothing written. {len(selected)} record(s) would be promoted, "
              f"{len(inbox) - len(selected)} would remain in the inbox.")
        print("Re-run with --confirm to apply: add --confirm to this exact command.")
        return

    # --- write path, only reachable with --confirm ---
    for r, new_id in plan:
        out = dict(r)
        out["id"] = new_id
        out["status"] = "approved"
        append_conditioning(out)
        print(f"promoted '{out['name']}' -> data/conditioning.jsonl as id {new_id}")

    promoted = {id(r) for r in selected}
    save_conditioning_inbox([r for r in inbox if id(r) not in promoted])
    print(f"{len(selected)} record(s) promoted. Run scripts/validate.py to confirm.")
    rebuild_bundle()


def main():
    argv = sys.argv[1:]
    if not argv:
        print(__doc__)
        return

    # Conditioning is a separate record type with its own inbox and file; it never
    # goes through the sport-scoped complex path.
    if "conditioning" in argv and not any(a.capitalize() in VALID_SPORTS for a in argv):
        promote_conditioning_main(argv)
        return

    if "--list" in argv:
        rows = load_inbox()
        if not rows:
            print("inbox is empty — nothing pending")
            return
        for path, r in rows:
            print(f"  {path.name:20s} id {r['id']:>4}  [{r.get('status'):9s}] {r['name']}")
        return

    sport, all_approved, name, supersede, confirm = parse_args(argv)

    inbox = load_inbox(sport)
    if not inbox:
        scope = f"data/inbox/proposed/{sport.lower()}.jsonl" if sport else "the inbox"
        print(f"{scope} is empty — nothing to promote")
        return

    to_promote, error = select(inbox, sport, all_approved, name)
    if error:
        print(error)
        return

    if not all_approved and not name:
        # no selector given — listing mode, never writes
        print(f"pending in {'data/inbox/proposed/' if not sport else sport.lower() + '.jsonl'}:")
        for path, r in inbox:
            mark = "would promote (--all-approved)" if r.get("status") == "approved" else ""
            print(f"  {path.name:20s} id {r['id']:>4}  [{r.get('status'):9s}] {r['name']}  {mark}")
        n_approved = sum(1 for _, r in inbox if r.get("status") == "approved")
        print(f"\nno name or --all-approved given, so nothing selected. "
              f"{n_approved} of {len(inbox)} pending record(s) have status 'approved'.")
        print("Nothing written.")
        return

    if not to_promote:
        print("nothing matches — 0 record(s) would be promoted. Nothing written.")
        return

    # --- pre-flight: resolve the supersede target BEFORE any write happens ---
    sup_target = sup_err = None
    if supersede is not None:
        sup_target, sup_err = find_supersede_target(supersede, [r for _, r in to_promote])
        if sup_err:
            print(f"REFUSING TO RUN: {sup_err}")
            return

    # --- preview (always printed, so a --confirm run is auditable) ---
    nid = next_id()
    first_free = nid
    selected = {id(r) for _, r in to_promote}
    plan = []
    for _, r in to_promote:
        sport_file = f"data/complexes/{r['sport'].lower()}.jsonl"
        plan.append((r, sport_file, nid))
        print(f"would promote inbox id {r['id']} '{r['name']}'\n"
              f"            -> {sport_file} as id {nid} (status 'approved')")
        nid += 1

    # A single promotion from a file that has several records pending always
    # previews as the first free id, because nothing has been written yet. That
    # is true but useless across a sitting's worth of separate --dry-run runs:
    # they all print the same number. Show the queue so the sequence is legible.
    # Assigned ids above are untouched -- they stay the genuinely free next id,
    # so promoting out of order leaves no gap in the sequence.
    shown = set()
    for path, r in to_promote:
        if path in shown:
            continue
        shown.add(path)
        rows = sorted(((rr["id"], rr) for p, rr in inbox if p == path),
                      key=lambda t: t[0])
        if len(rows) < 2:
            continue
        print(f"\n  pending in {path.name}, in the order they would be promoted "
              f"(first free id {first_free}):")
        for pos, (iid, rr) in enumerate(rows):
            mark = "   <-- this run" if id(rr) in selected else ""
            print(f"    {first_free + pos:>4}  inbox id {iid:>4}  {rr['name']}{mark}")
        print(f"    (each --confirm run claims the first free id, so promoting these"
              f"\n     one at a time lands them on {first_free}..{first_free + len(rows) - 1}.)")

    if sup_target:
        path, line, rec = sup_target
        print(f"would mark id {rec['id']} '{rec['name']}' in {path.name}\n"
              f"            status 'approved' -> 'superseded' (record kept, not deleted)")

    if not confirm:
        # SAFETY INVARIANT: no --confirm means no write, regardless of other flags.
        print(f"\nPREVIEW ONLY — nothing written. {len(to_promote)} record(s) would be promoted"
              + (", 1 record superseded" if sup_target else "")
              + f", {len(inbox) - len(to_promote)} would remain in the inbox.")
        print(f"Re-run with --confirm to apply: add --confirm to this exact command.")
        return

    # --- write path (only reachable with --confirm) ---
    for r, _, new_id in plan:
        promote_one(r, new_id)
    if sup_target:
        apply_supersede(sup_target)
        print(f"marked id {sup_target[2]['id']} '{sup_target[2]['name']}' "
              f"as superseded in {sup_target[0].name}")

    promoted_ids = {id(r) for _, r in to_promote}
    touched = {}
    for path, r in inbox:
        bucket = touched.setdefault(path, [])
        if id(r) not in promoted_ids:
            bucket.append(r)
    save_inbox(touched)

    print(f"{len(to_promote)} record(s) promoted"
          + (", 1 record superseded." if sup_target else ".")
          + " Run scripts/build_index.py next.")

    # Publish step, last, only on the write path. Never reached without --confirm.
    rebuild_bundle()


if __name__ == "__main__":
    main()
