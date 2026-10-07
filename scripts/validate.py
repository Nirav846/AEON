#!/usr/bin/env python3
"""
AEON validator.
Checks every complex in data/complexes/*.jsonl and the sport-scoped inboxes
(data/inbox/proposed/*.jsonl, data/inbox/rejected/*.jsonl) against the schema
and the biomechanical/structural checklist in AGENTS.md.

Usage:
  python3 scripts/validate.py                 # validate everything
  python3 scripts/validate.py data/inbox/proposed/badminton.jsonl   # validate one file
"""
import json, re, sys, glob
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REQUIRED_FIELDS = ["id", "name", "sport", "role", "category", "focus", "execution", "swap", "equipment", "status"]
VALID_SPORTS = {"Cricket", "Tennis", "Badminton", "Pickleball", "Hyrox", "Universal"}
VALID_ROLES = {"All", "Batsman", "Pacer", "Spinner", "WK"}
VALID_CATEGORIES = {"Multi", "Primer", "Core", "Knee Dom", "Hip Dom", "Push", "Pull"}
VALID_STATUSES = {"proposed", "approved", "rejected", "superseded"}
# Superseded records are kept for history/audit, and rejected records are kept in
# data/inbox/rejected/ with a reason. Both are deliberately retired, so neither may
# trip the data-quality checks:
#   - superseded would otherwise duplicate the record that replaced it;
#   - rejected is by definition a record of something already declined, so it will
#     duplicate a live record and will already have had its warnings adjudicated.
# Neither is a proposal any more, so warning about one is pure noise.
INACTIVE_STATUSES = {"superseded", "rejected"}

# Heavy-load keywords that must never be followed immediately by a loaded
# single-leg landing or a station requiring a rack exit.
HEAVY_PATTERN = re.compile(r"\bheavy\b.*\b(squat|deadlift|press|row)\b", re.I)
RACK_RISK_PATTERN = re.compile(r"\bbarbell\b", re.I)
SINGLE_LEG_LANDING = re.compile(r"single[- ]leg.*(landing|stick)", re.I)
IMMEDIATE_PATTERN = re.compile(r"\bimmediate(ly)?\b", re.I)

# Spellings that count as "this implement was named in the text". Keys are
# lowercased equipment tags; a tag is considered used if any alias appears.
EQUIPMENT_ALIASES = {
    "dumbbell": ["dumbbell", "db"],
    "med ball": ["med ball", "medicine ball", "medball", "med-ball"],
    "ghd": ["ghd", "glute ham raise"],
    "trap bar": ["trap bar", "trapbar", "trap"],
    "ab wheel": ["ab wheel", "abwheel", "ab-wheel"],
    "ski erg": ["skierg", "ski erg"],
    "assault bike": ["assault", "bike"],
    "stability ball": ["stability ball"],
    "heavy bag": ["heavy bag"],
    "sandbag": ["sandbag"],
    "kettlebell": ["kettlebell", "kb"],
    "jump rope": ["jump rope", "rope"],
    "pull-up bar": ["pull-up bar", "pull up bar", "pull-up", "chin"],
    "cones": ["cone"],
    "bodyweight": ["bodyweight", "body weight", "unweighted"],
    "partner": ["partner"],
    "box": ["box"],
    "bench": ["bench"],
    "machine": ["machine"],
    "trx": ["trx"],
}

# Implements an athlete can get without booking anything. Everything not listed
# here is treated as reserved/scarce by default — conservative on purpose, since
# this only ever produces a warning.
COMMON_TIER = {
    "Bodyweight", "Dumbbell", "Band", "Med Ball", "Tennis Ball",
    "Wall", "Partner", "Cones", "Jump Rope", "Mat", "Plate",
    "Pull-Up Bar",
}


def load_jsonl(path):
    records = []
    with open(path, encoding="utf-8") as f:
        for i, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                records.append((i, json.loads(line)))
            except json.JSONDecodeError as e:
                yield_error(path, i, f"invalid JSON: {e}")
    return records


ERRORS = []
WARNINGS = []


def yield_error(path, line_no, msg):
    ERRORS.append(f"{path}:{line_no}  {msg}")


def yield_warning(path, line_no, msg):
    WARNINGS.append(f"{path}:{line_no}  {msg}")


def check_record(path, line_no, r):
    for field in REQUIRED_FIELDS:
        if field not in r or r[field] in (None, ""):
            yield_error(path, line_no, f"missing required field '{field}'")
    if r.get("sport") not in VALID_SPORTS:
        yield_error(path, line_no, f"sport '{r.get('sport')}' not in {sorted(VALID_SPORTS)}")
    if r.get("category") not in VALID_CATEGORIES:
        yield_error(path, line_no, f"category '{r.get('category')}' not in {sorted(VALID_CATEGORIES)}")
    if not isinstance(r.get("equipment"), list) or not r.get("equipment"):
        yield_error(path, line_no, "equipment must be a non-empty array")
    if r.get("status") not in VALID_STATUSES:
        yield_error(path, line_no, f"status '{r.get('status')}' not in {sorted(VALID_STATUSES)}")

    execution = r.get("execution", "")
    # Rule: a heavy barbell lift immediately followed by another movement
    # has no safe exit (can't drop a barbell like a dumbbell/sled/trap bar).
    if RACK_RISK_PATTERN.search(execution) and HEAVY_PATTERN.search(execution) and IMMEDIATE_PATTERN.search(execution):
        yield_error(path, line_no, f"[{r.get('name')}] heavy barbell lift + 'immediate' next movement — no safe exit, swap to trap bar/dumbbell/sled")

    # Rule: heavy load word near a single-leg landing phrase.
    if SINGLE_LEG_LANDING.search(execution) and re.search(r"\bheavy\b", execution, re.I):
        yield_error(path, line_no, f"[{r.get('name')}] heavy load paired with a single-leg landing — check unilateral landing safety")

    check_also_suits(path, line_no, r)
    check_equipment_tags(path, line_no, r)
    check_inversion(path, line_no, r)


def check_inversion(path, line_no, r):
    """The inversion check from AGENTS.md filter 3, mechanised.

    A swap exists for when the primary implement is taken, so it must never need
    more than the primary, nor something harder to obtain. Two conditions:

      A. swap_equipment holds more distinct implements than equipment.
      B. every swap implement is reserved/scarce while the primary has at least
         one common-tier implement — i.e. cheap was traded for scarce.

    Condition B only judges the implements the swap ADDS. An implement the
    primary already carries is not a cost of the swap — the athlete needs it
    either way, so its tier says nothing about which option is easier to get.
    Without this exclusion a swap that reuses the primary's own implement (and
    needs strictly fewer things) was flagged for that implement being reserved,
    which is the opposite of an inversion. Condition A is deliberately untouched:
    a genuine implement-count inversion stays visible even when the swap shares
    equipment with the primary.

    Condition B fires on ANY scarce implement the swap adds, not only when every
    swapped implement is scarce. The old "all" test let a swap pair one
    common-tier tag with one scarce implement and slip through entirely, which is
    the same cheap-for-scarce trade the rule exists to catch.

    Skipped on superseded records: they are frozen audit history and can never
    be promoted, so flagging them is pure noise.
    """
    if r.get("status") in INACTIVE_STATUSES:
        return
    eq = [e for e in r.get("equipment", []) if isinstance(e, str)]
    sw = [e for e in r.get("swap_equipment", []) if isinstance(e, str)]
    if not sw:
        return

    if len(sw) > len(eq):
        yield_warning(
            path, line_no,
            f"[{r.get('name')}] INVERSION: swap needs {len(sw)} implements {sw} but the "
            f"primary needs {len(eq)} {eq} — the fallback is harder to get than the default")

    # Only implements the swap adds are judged: one the primary already carries is
    # needed either way, so it is not a cost of choosing the swap. And ANY scarce
    # implement the swap adds is enough - pairing one common-tier tag alongside a
    # scarce one is still trading cheap for scarce, which the old "all" test missed.
    added = [e for e in sw if e not in eq]
    if added and any(e not in COMMON_TIER for e in added) and any(e in COMMON_TIER for e in eq):
        yield_warning(
            path, line_no,
            f"[{r.get('name')}] INVERSION: swap needs reserved implements {added} while "
            f"the primary has common-tier {eq} — check the swap is really the easier option")


def check_also_suits(path, line_no, r):
    """also_suits is OTHER SPORTS; also_suits_roles is other ROLES in this sport.

    Reported as warnings, not errors: the coach sees them rather than having the
    validator block the run.

    Skipped on superseded records, like every other data-quality check here.
    """
    if r.get("status") in INACTIVE_STATUSES:
        return
    own_role = r.get("role")
    for v in r.get("also_suits", []):
        if v not in VALID_SPORTS:
            yield_warning(
                path, line_no,
                f"[{r.get('name')}] also_suits '{v}' is not a bare sport name — cross-role "
                f"fits belong in also_suits_roles")
    for v in r.get("also_suits_roles", []):
        if v not in VALID_ROLES:
            yield_warning(path, line_no, f"[{r.get('name')}] also_suits_roles '{v}' not in {sorted(VALID_ROLES)}")
        elif v == own_role:
            yield_warning(path, line_no, f"[{r.get('name')}] also_suits_roles repeats its own role '{v}'")


def check_equipment_tags(path, line_no, r):
    """Flag implements tagged in `equipment` that name neither movement.

    Alias-aware so abbreviations (DB, KB) are not false positives. `Bodyweight`
    is exempt: it is a modality, not a reservable implement, so a record can
    legitimately be bodyweight without the word appearing in either field.

    Skipped on superseded records, like every other data-quality check here.
    """
    if r.get("status") in INACTIVE_STATUSES:
        return
    blob = f" {r.get('execution', '').lower()} {r.get('swap', '').lower()} "
    for e in r.get("equipment", []):
        if not isinstance(e, str) or e.lower() == "bodyweight":
            continue
        if not any(a in blob for a in EQUIPMENT_ALIASES.get(e.lower(), [e.lower()])):
            yield_warning(path, line_no, f"[{r.get('name')}] equipment '{e}' appears in neither execution nor swap")


def check_duplicates(all_records):
    # name/execution -> (id, location) of the first active record seen
    seen_names = {}
    seen_exec = {}

    for path, line_no, r in all_records:
        # Superseded records stay on disk for audit but are retired, so they are
        # skipped here — a replacement is allowed to reuse the same name/execution.
        if r.get("status") in INACTIVE_STATUSES:
            continue

        # A record may declare "supersedes": <id> to say it is the deliberate
        # replacement for an existing approved record. That is the normal
        # transitional state between "proposal written" and "coach promotes it",
        # and it is exempt from the duplicate checks below — but ONLY for the
        # exact id it names, so genuine accidental duplicates still fail.
        sid = r.get("supersedes")
        replaces = sid if isinstance(sid, int) else None

        name_key = (r.get("sport"), r.get("name", "").strip().lower())
        if name_key in seen_names:
            prev_id, prev_loc = seen_names[name_key]
            if not (replaces is not None and replaces == prev_id):
                yield_error(path, line_no, f"duplicate name '{r.get('name')}' also at {prev_loc}")
        seen_names[name_key] = (r.get("id"), f"{path}:{line_no}")

        exec_key = re.sub(r"\s+", " ", r.get("execution", "").strip().lower())
        if exec_key and exec_key in seen_exec:
            prev_id, prev_loc = seen_exec[exec_key]
            if not (replaces is not None and replaces == prev_id):
                yield_error(path, line_no, f"near-duplicate execution text, also at {prev_loc} — consider sharing a pattern_id instead of a new complex")
        seen_exec[exec_key] = (r.get("id"), f"{path}:{line_no}")


def load_schema(name):
    """Read schema/<name>.schema.json, or None if it cannot be read.

    Schemas are documentation that nothing read until check_conditioning; this is
    the single accessor for them, so a future record type validates against the
    same file rather than a re-implementation of it."""
    path = ROOT / "schema" / f"{name}.schema.json"
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:            # noqa: BLE001
        yield_warning(f"schema/{name}.schema.json", 1,
                      f"could not be read ({exc}) - skipping schema conformance")
        return None


def check_schema_conformance(path, line_no, r, schema_name, schema):
    """Validate one record against its JSON schema.

    Uses the real jsonschema library rather than hand-rolling draft-07 rules, so
    required/type/enum/minimum are enforced by one implementation instead of a
    conditioning-specific copy that could drift from the schema file.

    Skips cleanly if jsonschema is unavailable or the schema cannot be read,
    warning once rather than failing the run - a broken check must never block
    the coach from validating the rest of the database."""
    if schema is None:
        return
    try:
        import jsonschema
    except ImportError:
        yield_warning(path, line_no,
                      "jsonschema not installed - skipping schema conformance "
                      "(pip install jsonschema to enable)")
        return
    validator = jsonschema.Draft7Validator(schema)
    for err in sorted(validator.iter_errors(r), key=lambda e: list(e.path)):
        where = "/".join(str(p) for p in err.path) or "(root)"
        yield_error(path, line_no, f"schema {schema_name}: {where}: {err.message}")


# Conditioning targets are free-form by design: the unit is context-dependent on
# modality (pace km/h, watts, seconds, grade, load, belt instruction). The schema
# types them as bare strings for that reason. So this recognises STRUCTURE - a
# comparator, a number, or an explicit no-target marker - and flags text with no
# recognisable quantity. It deliberately does not parse units or ranges, because
# no single range format would fit all four shapes present in the data.
TARGET_NO_NUMBER_OK = {"flat", "belt off", "level", "n/a", "na", "none", "-"}
TARGET_STRUCTURED = re.compile(
    r"(^-\s*$)"            # explicit no-target marker
    r"|(<\s*\d)"           # under N   ("<11.2s", "< 1:50/500m pace")
    r"|(>\s*\d)"           # over N    (">420W")
    r"|(\d+\s*[-\u2013]\s*\d+)"   # range ("18.5-21.5 kmh")
    r"|(\d)",              # bare number/grade ("10% grade", "Level 12")
    re.I)


def check_targets(path, line_no, r):
    """male_target / female_target, when present, are well-formed.

    Optional in the schema, so absence is never flagged. Warnings rather than
    errors: these are hand-entered coaching strings and the coach judges them."""
    for field in ("male_target", "female_target"):
        val = r.get(field)
        if val is None:
            continue
        if not isinstance(val, str):
            yield_error(path, line_no, f"{field} must be a string, got {type(val).__name__}")
            continue
        if not val.strip():
            yield_warning(path, line_no,
                          f"{field} is empty - use '-' if there is deliberately no target")
            continue
        if not TARGET_STRUCTURED.search(val) and val.strip().lower() not in TARGET_NO_NUMBER_OK:
            yield_warning(path, line_no,
                          f"{field} '{val}' has no recognisable quantity or placeholder "
                          f"- check it is not malformed")


def check_conditioning_record(path, line_no, r, schema, required):
    """Validate one conditioning protocol record.

    Conditioning is a genuinely different shape from Complex - no role, category,
    focus, execution, swap or equipment - so the Complex-specific checks
    (check_equipment_tags, check_inversion, the clunk-test regexes) do not apply
    and are not run here.
    """
    for field in required:
        if field not in r or r[field] in (None, ""):
            yield_error(path, line_no, f"missing required field '{field}'")

    if r.get("status") not in VALID_STATUSES:
        yield_error(path, line_no,
                    f"status '{r.get('status')}' not in {sorted(VALID_STATUSES)}")

    check_schema_conformance(path, line_no, r, "conditioning", schema)
    check_targets(path, line_no, r)


def check_ids(all_records):
    ids = {}
    # Superseded records DO still occupy their id — they must never be reused,
    # and circuits may still reference them.
    for path, line_no, r in all_records:
        cid = r.get("id")
        if cid in ids:
            yield_error(path, line_no, f"duplicate id {cid}, also at {ids[cid]}")
        else:
            ids[cid] = f"{path}:{line_no}"


def check_circuit_refs(circuit_paths, complex_ids, conditioning_ids):
    for path in circuit_paths:
        for line_no, r in load_jsonl(path):
            for st in r.get("stations", []):
                rt, rid = st.get("ref_type"), st.get("ref_id")
                if rt == "complex" and rid not in complex_ids:
                    yield_error(path, line_no, f"circuit '{r.get('name')}' references complex id {rid} which does not exist")
                if rt == "conditioning" and rid not in conditioning_ids:
                    yield_error(path, line_no, f"circuit '{r.get('name')}' references conditioning id {rid} which does not exist")


def check_bundle_freshness():
    """Warn when build/bundle-meta.json no longer matches what data/ would produce.

    The apps read build/app-bundle.json from raw.githubusercontent.com, so a
    stale bundle means the apps silently serve outdated data no matter how clean
    data/ is. That failure is invisible by construction - nothing in the records
    is wrong - so it needs its own check.

    Covers drift that promote.py's auto-rebuild cannot: a hand edit to a data
    file, a failed build, a rebuild that was never committed, or a checkout
    that landed data/ without the bundle.

    Deliberately a WARNING, not an error. Stale data published is bad, but the
    records themselves are valid and the fix is a rebuild, not a data change -
    blocking a validation run over it would train people to ignore this output.
    """
    meta_path = ROOT / "build/bundle-meta.json"
    rel = "build/bundle-meta.json"
    if not meta_path.exists():
        yield_warning(rel, 1, "does not exist - run scripts/build_bundle.py")
        return
    try:
        meta = json.loads(meta_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        yield_warning(rel, 1, f"is unreadable ({exc}) - run scripts/build_bundle.py")
        return

    committed = meta.get("version")
    try:
        if str(ROOT / "scripts") not in sys.path:
            sys.path.insert(0, str(ROOT / "scripts"))
        from build_bundle import current_version
        live = current_version()
    except Exception as exc:                      # noqa: BLE001 - a broken check must not fail the run
        yield_warning(rel, 1, f"could not recompute the bundle version ({exc}) - skipping staleness check")
        return

    if committed != live:
        yield_warning(rel, 1,
            f"published bundle is STALE: meta says version {committed!r} but data/ now "
            f"hashes to {live!r}. The apps are serving outdated data. Fix with: "
            f"python scripts/build_bundle.py && git add build/ && git commit && git push")


def main():
    complex_paths = glob.glob(str(ROOT / "data/complexes/*.jsonl"))
    inbox_paths = (glob.glob(str(ROOT / "data/inbox/proposed/*.jsonl")) +
                   glob.glob(str(ROOT / "data/inbox/rejected/*.jsonl")))
    circuit_paths = glob.glob(str(ROOT / "data/circuits.jsonl"))
    conditioning_paths = glob.glob(str(ROOT / "data/conditioning.jsonl"))

    targets = sys.argv[1:] or (complex_paths + inbox_paths)

    all_records = []
    for path in targets:
        for line_no, r in load_jsonl(path):
            check_record(path, line_no, r)
            all_records.append((path, line_no, r))

    check_duplicates(all_records)
    check_ids(all_records)

    if not sys.argv[1:]:
        complex_ids = {r["id"] for _, _, r in all_records if "name" in r and "execution" in r}

        # Conditioning is validated as records in their own right, not only as
        # reference targets for circuits. It is a separate pass because its ids
        # (1-33) deliberately overlap the complex ids (1-33), so feeding it
        # through check_ids would report 33 false duplicate-id errors.
        cond_schema = load_schema("conditioning")
        cond_required = cond_schema.get("required", []) if cond_schema else []
        conditioning_records = []
        for path in conditioning_paths:
            for line_no, r in load_jsonl(path):
                check_conditioning_record(path, line_no, r, cond_schema, cond_required)
                conditioning_records.append((path, line_no, r))

        check_ids(conditioning_records)
        conditioning_ids = {r["id"] for _, _, r in conditioning_records}
        check_circuit_refs(circuit_paths, complex_ids, conditioning_ids)
        check_bundle_freshness()

    if ERRORS:
        print(f"FAILED — {len(ERRORS)} issue(s):\n")
        for e in ERRORS:
            print(" -", e)
        if WARNINGS:
            print(f"\nplus {len(WARNINGS)} warning(s):\n")
            for w in WARNINGS:
                print(" ~", w)
        sys.exit(1)
    else:
        print(f"OK — {len(all_records)} record(s) validated, no issues.")
        if WARNINGS:
            print(f"\n{len(WARNINGS)} warning(s):\n")
            for w in WARNINGS:
                print(" ~", w)


if __name__ == "__main__":
    main()
