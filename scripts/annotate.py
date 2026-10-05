#!/usr/bin/env python3
"""Annotate the `goals` array on existing APPROVED complexes, in place.

    annotate.py cricket 4 "more power hitting sixes"            # preview
    annotate.py cricket 4 "more power hitting sixes" --confirm  # writes

This is a deliberately narrow sibling of promote.py. It exists because a
`goals` annotation is ADDITIVE METADATA, not a correction: superseding a
record to add one array field would fork an identical historical copy for
every annotation, for no audit value. So goals do not go through the
supersede workflow. Every other field change still does.

But note that in-place rewriting is genuinely more dangerous than promotion:
promote.py only appends, so a mistake is fixed by deleting a line. This
script overwrites a live approved record, so a mistake silently corrupts
data. Therefore:

  * --confirm is required for any write; the default is preview-only.
  * The target must exist AND be "approved". Superseded, proposed and
    rejected records are refused.
  * ONLY `goals` may change. Enforced structurally, and then re-verified by
    diffing the record before and after the edit - if any other key's value
    differs, the write is refused before it reaches disk.
  * validate.py runs before and after. A failed pre-check aborts without
    writing; a failed post-check restores the original file byte-for-byte.

Bundle note: unlike promote.py, this does NOT rebuild build/app-bundle.json.
After annotating, `validate.py` will emit a bundle-staleness warning because
the published bundle predates the new goals. That warning is correct and
expected - run `python scripts/build_bundle.py`, commit and push when you are
ready for the goals to reach the apps.
"""

import argparse
import glob
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# The single field this script is permitted to write. Anything not in here is
# out of scope by definition.
WRITABLE_FIELDS = {"goals"}

EXIT_OK = 0
EXIT_VALIDATE_FAILED = 1
EXIT_REFUSED = 2
EXIT_ROLLED_BACK = 3


def run_validate(label):
    """Run the repo validator over everything. Returns True if clean."""
    print("--- validate.py ({}) ---".format(label))
    # The child's stdout is encoded with the Windows ANSI codepage by default,
    # so a UTF-8 decode of it blows up on the em-dash in validate.py's own
    # output ("OK - N record(s)"). Force the child to emit UTF-8, and decode
    # leniently as well so a stray byte can never crash the safety check.
    env = dict(os.environ, PYTHONIOENCODING="utf-8")
    proc = subprocess.run(
        [sys.executable, os.path.join(ROOT, "scripts", "validate.py")],
        cwd=ROOT, capture_output=True, text=True,
        encoding="utf-8", errors="replace", env=env,
    )
    if proc.stdout:
        sys.stdout.write(proc.stdout)
    if proc.stderr and proc.stderr.strip():
        sys.stderr.write(proc.stderr)
    ok = proc.returncode == 0
    print("--- validate.py {}: {}{} ---".format(
        label, "OK" if ok else "FAILED",
        "" if ok else " (exit {})".format(proc.returncode)))
    return ok


def resolve_sport_path(sport):
    """Map a sport name to its complexes JSONL path. None if not found."""
    slug = sport.strip().lower()
    candidates = sorted(glob.glob(os.path.join(ROOT, "data", "complexes", "*.jsonl")))
    for path in candidates:
        if os.path.basename(path) == slug + ".jsonl":
            return path
    return None


def main():
    parser = argparse.ArgumentParser(
        description="Edit ONLY the goals array on an existing approved complex.",
        epilog="Without --confirm this prints a preview and writes nothing.")
    parser.add_argument("sport", help="sport, e.g. cricket")
    parser.add_argument("id", type=int, help="complex id (not a name)")
    parser.add_argument("goals", nargs="+", help="one or more short outcome phrases")
    parser.add_argument("--confirm", action="store_true",
                        help="actually write (otherwise preview only)")
    parser.add_argument("--dry-run", action="store_true",
                        help="no-op alias, kept for symmetry with promote.py")
    args = parser.parse_args()

    if args.dry_run and args.confirm:
        print("error: --dry-run and --confirm are mutually exclusive")
        return EXIT_REFUSED

    # --- resolve target -----------------------------------------------------
    path = resolve_sport_path(args.sport)
    if path is None:
        print("error: no complexes file for sport {!r}".format(args.sport))
        print("       available: {}".format(
            ", ".join(os.path.basename(p)[:-6]
                      for p in sorted(glob.glob(os.path.join(ROOT, "data", "complexes", "*.jsonl"))))))
        return EXIT_REFUSED

    sport_label = os.path.basename(path)[:-6]
    with open(path, "r", encoding="utf-8", newline="") as fh:
        original = fh.read()
    lines = original.split("\n")

    matches = []
    for i, line in enumerate(lines):
        if not line.strip():
            continue
        try:
            rec = json.loads(line)
        except json.JSONDecodeError:
            continue
        if rec.get("id") == args.id:
            matches.append((i, rec))

    if not matches:
        print("error: no record with id {} in {}".format(args.id, os.path.relpath(path, ROOT)))
        return EXIT_REFUSED
    if len(matches) > 1:
        print("error: id {} appears {} times in {} - refusing to guess".format(
            args.id, len(matches), sport_label))
        return EXIT_REFUSED

    line_no, record = matches[0]

    # --- refuse anything not approved --------------------------------------
    status = record.get("status")
    if status != "approved":
        print("error: id {} is {!r}, not 'approved'".format(args.id, status))
        print("       only approved complexes may be annotated in place")
        return EXIT_REFUSED

    declared_sport = str(record.get("sport", "")).strip().lower()
    if declared_sport != sport_label:
        print("warning: record sport is {!r} but file is {!r}".format(
            record.get("sport"), sport_label))

    # --- build the new goals array (append + dedup) ------------------------
    incoming = [g.strip() for g in args.goals]
    incoming = [g for g in incoming if g]
    if not incoming:
        print("error: no usable goal text supplied")
        return EXIT_REFUSED
    for g in incoming:
        if len(g) < 3:
            print("error: goal {!r} is shorter than the schema's 3-char minimum".format(g))
            return EXIT_REFUSED

    before_goals = list(record.get("goals", []))
    merged = list(before_goals)
    added = []
    for g in incoming:
        if g in merged:
            continue
        merged.append(g)
        added.append(g)

    updated = dict(record)
    updated["goals"] = merged

    # --- prove we are only changing goals ----------------------------------
    touched = {k for k in set(record) | set(updated)
               if record.get(k) != updated.get(k)}
    if not touched <= WRITABLE_FIELDS:
        print("error: internal guard tripped - would change {}, only {} permitted".format(
            sorted(touched), sorted(WRITABLE_FIELDS)))
        return EXIT_REFUSED

    # --- preview ------------------------------------------------------------
    print("annotate preview ({} {})".format(sport_label, args.id))
    print("  file   : {}".format(os.path.relpath(path, ROOT)))
    print("  line   : {}".format(line_no + 1))
    print("  name   : {}".format(record.get("name")))
    print("  role   : {}".format(record.get("role")))
    print("  status : {}".format(status))
    print("  goals before : {}".format(before_goals if before_goals else "(none)"))
    print("  goals after  : {}".format(merged))
    if not added:
        print("  note   : every supplied goal was already present - no change needed")
    print("  writes : goals only (no id, status or any other field)")

    if not args.confirm:
        print("\npreview only - nothing written. Pass --confirm to apply.")
        return EXIT_OK

    if not added:
        print("\nno new goals to add - nothing written.")
        return EXIT_OK

    # --- pre-flight validation ---------------------------------------------
    if not run_validate("before"):
        print("error: repo does not validate - aborting without writing")
        return EXIT_VALIDATE_FAILED

    # --- write ---------------------------------------------------------------
    new_line = json.dumps(updated, ensure_ascii=False)
    # round-trip guard: the reserialised record must differ only in goals
    if json.loads(new_line) != updated:
        print("error: reserialisation did not round-trip - aborting")
        return EXIT_VALIDATE_FAILED

    lines[line_no] = new_line
    new_text = "\n".join(lines)

    tmp_path = path + ".annotate.tmp"
    try:
        with open(tmp_path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(new_text)
        os.replace(tmp_path, path)
    except OSError as exc:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)
        print("error: could not write {}: {}".format(path, exc))
        return EXIT_VALIDATE_FAILED

    # --- post validation, with rollback -------------------------------------
    if run_validate("after"):
        print("\nannotated id {} ({}): goals {}".format(
            args.id, record.get("name"), merged))
        print("reminder: bundle not rebuilt - run scripts/build_bundle.py to publish")
        return EXIT_OK

    print("error: post-write validation failed - restoring original file")
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(original)
    print("restored {} to its pre-annotation contents".format(
        os.path.relpath(path, ROOT)))
    return EXIT_ROLLED_BACK


if __name__ == "__main__":
    sys.exit(main())