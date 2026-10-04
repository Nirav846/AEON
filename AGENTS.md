# AEON — Agent Instructions

You are working on AEON, a sport-specific S&C complex database for a freelance
strength and conditioning coach. This file is your constitution. Read it in
full before touching any data file. It is short on purpose — re-read it every
session instead of trying to remember it.

## What you are building

A JSONL database of training "complexes" (paired movements, e.g. a brace +
an explosive expression) across five sports: Cricket, Tennis, Badminton,
Pickleball, Hyrox. More sports will be added later using the same shape.

A **pattern** is the sport-neutral biomechanical idea (e.g. "low-stance
isometric to contralateral sling drive"). A **complex** is one sport/role/
equipment-specific implementation of a pattern. Many complexes can share one
pattern_id. Before inventing a new complex, check `build/index.jsonl` for an
existing pattern that already fits — reuse it rather than duplicating.

## Where things live

- `data/complexes/<sport>.jsonl` — the live, approved database. One file per
  sport. Never write here directly — see workflow below.
- `data/concepts.jsonl` — non-complex coaching concepts (readiness testing,
  periodization methods, conditioning protocols). Same append rules apply.
- `data/inbox/proposed/<sport>.jsonl` — where ALL new ideas land first, one
  file per sport, same lowercase convention as `data/complexes/`. These are
  the only files you write to when generating new material. Pick the file by
  the record's `sport` field.
- `data/inbox/rejected/<sport>.jsonl` — rejected ideas, kept with a reason, for
  your own future reference (see Self-Improvement below). One file per sport.
  Each line is the original proposed record plus a top-level `"reason"` string
  field explaining why the coach rejected it, e.g. `"reason": "cable+barbell
  transition too far apart on this gym floor"`.
- `build/index.jsonl` — auto-generated, ~15 tokens/record. Read this first
  for any browsing, duplicate-checking, or "what already exists" question.
  Never hand-edit it. Regenerate with `scripts/build_index.py`.
  Note: the `cat` field means something different per `type` — for a
  complex it's `category` (the enum below), for a circuit it's
  `energy_system`, for a conditioning protocol it's `work_to_rest`. It's a
  human-scannable hint, not a single typed field — the schema files in
  `schema/` are the authority on each record type's real fields.
- `schema/complex.schema.json` — the structural contract.
- `scripts/validate.py` — run this before showing the coach anything.
- `SOURCES.md` — plain-language summaries of the training philosophies you
  may draw on. Tag, don't quote.

## Workflow for adding a complex

1. Read `build/index.jsonl` (cheap) to check nothing similar already exists.
2. If you need full detail on a near-match, open that one sport file —
   never open all sport files to check one idea.
3. Draft the new record in the schema below, append it to
   `data/inbox/proposed/<sport>.jsonl` with `"status": "proposed"` — pick the
   file by the record's `sport` field.
4. Run `python3 scripts/validate.py data/inbox/proposed/<sport>.jsonl`.
   Bare `python3 scripts/validate.py` validates everything and is the better
   final check before showing the coach a batch.
5. Fix anything it flags, re-run, repeat until clean.
6. Stop. Do not write into `data/complexes/`. Only the coach approves a
   promotion (`scripts/promote.py`, run by the coach or on explicit request).
   It takes an optional sport argument: `promote.py badminton "Complex Name"`,
   or `promote.py --all-approved` across every sport inbox. When a new complex
   replaces an existing approved one, add `--supersede <old id>` — that flips
   the old record to `"status": "superseded"` instead of deleting it, for
   history and audit.
7. If asked to review an idea the coach rejects, append it with a reason to
   `data/inbox/rejected/<sport>.jsonl` instead of deleting it.

## The three design filters (apply to every new complex)

**1. Sport & role specificity**
- Identify the dominant energy system the sport role needs (ATP-PC, lactic,
  aerobic) and set rest intervals accordingly.
- Identify the primary force vector (vertical, horizontal, rotational) and
  make sure the gym movement matches the sport skill's force vector.
- Never mimic the sport skill directly under load (no weighted swing
  simulations). Build the raw quality; let the athlete express it on court.

**2. Biomechanical fluidity ("the clunk test")**
- Momentum must flow one direction through the complex. A backward movement
  must not be followed immediately by a forward concentric movement without
  a reset step in between.
- A heavy lift paired with an immediate plyometric/sprint must have a SAFE
  EXIT. A barbell has no safe exit (it must be racked). Trap bar, dumbbell,
  sled, and kettlebell can be dropped or released — prefer these for any
  heavy-to-explosive pairing.
- Never pair a heavy load with a single-leg landing.

**3. Gym-floor feasibility**
- Both pieces of equipment must be reasonably reservable together on a busy
  commercial gym floor. Walking 30 feet between stations breaks a contrast
  pair.
- Any contrast/PAP transition must take under 10 seconds.
- Always supply a `swap`, in case the primary equipment is taken. The `swap`
  must use **different equipment than the primary movement**, chosen so the
  two are unlikely to be reserved together on a busy gym floor. Bodyweight or
  dumbbell is preferred when a real equivalent exists; when the primary
  stimulus has no bodyweight equivalent (e.g. sled drag, heavy carry,
  machine-based pull), a different, commonly-available implement is
  acceptable. A sled swap to a different sled-adjacent tool is fine; a sled
  swap to "jumping jacks" would defeat the point.

  The test is **different implements, not cheaper implements**. If the swap
  needs the same scarce tool as the primary, it solves nothing — an athlete
  whose cable station is taken still cannot do it.

  **The inversion check.** A swap must not require MORE distinct pieces of
  equipment, or a scarcer one, than the primary execution. If the primary is
  bodyweight or single-implement, the swap must be too.

  This is citable by name, the same way "the clunk test" is. Run it on every
  proposal before writing it. It exists because the failure is invisible to
  the "different implements" rule above: a bodyweight primary with a
  box-and-med-ball swap *does* use different equipment, so it passes that
  sentence and still fails this one. The swap is the fallback for when the
  primary tool is gone, so it must be strictly easier to obtain than the
  primary — never harder. Same root cause as the original Hyrox filter-3
  failures, in miniature: ids 14, 36 and 91 were all caught by this, not by
  the equipment-difference rule.

## Schema (see schema/complex.schema.json for the enforced version)

```json
{
  "id": 93,
  "pattern_id": "P014",
  "name": "Complex Name",
  "sport": "Pickleball",
  "role": "All",
  "category": "Multi",
  "focus": "Short biomechanical-focus phrase",
  "execution": "Movement A (load/time) + Immediate Movement B",
  "swap": "Different-implement alternative, bodyweight/DB where one exists",
  "equipment": ["Cable", "Band"],
  "swap_equipment": ["TRX"],
  "also_suits": ["Tennis", "Badminton"],
  "also_suits_roles": ["WK"],
  "sources": ["FMS", "ALTIS"],
  "status": "proposed",
  "manual": { "why": "...", "cue": "..." }
}
```

`status` is one of `proposed`, `approved`, `rejected`, `superseded`. A
`superseded` complex stays in its sport file forever as an audit trail, but it
is skipped by `validate.py`'s duplicate checks and excluded from
`build/index.jsonl` by default, so it never clutters normal browsing.

Vocabulary is fixed. Do not invent new values for `sport` or `category` —
use what's in the schema enum. New equipment strings are fine; keep them
short and Title Case, matching existing tags in the index.

### `equipment` vs `swap_equipment`

- `equipment` lists the implements named in **`execution` only**.
- `swap_equipment` (optional) lists implements named in **`swap` only**. Omit
  it when the swap reuses nothing that `execution` does not already list.
- Never tag a tool that appears in neither. An unused tag sends a coach
  hunting for equipment the drill does not need.
- This split exists so filter 3 is checkable: comparing `equipment` against
  `swap_equipment` shows at a glance whether the swap actually drops the
  scarce implement.
- Many older records still fold swap-only tags into `equipment`. That is
  legacy, not a rule; a superseding record should split them properly.

### `also_suits` vs `also_suits_roles`

These two fields answer different questions. Do not conflate them.

- `also_suits` — **other sports only**, as bare sport names from the enum:
  `["Tennis", "Badminton"]`. Never role-qualified. `"Cricket Batsman"` is
  invalid here: it is a cross-*role* fit, not a cross-sport fit.
- `also_suits_roles` (optional) — **other roles within the same sport**,
  bare role names only: `["Batsman", "Spinner", "WK"]`. Valid roles are
  `All`, `Batsman`, `Pacer`, `Spinner`, `WK`. Do not repeat the record's own
  `role`, and never write the sport name in front of the role.

A record that suits a different sport goes in `also_suits`. A record that
suits a different role in its own sport goes in `also_suits_roles`. Both may
be present.

## Sourcing rule — do not fabricate citations

You may tag a complex with the training philosophy it draws from (see
`SOURCES.md`), e.g. `"sources": ["FMS", "ALTIS"]`. Never attribute a specific
claim, statistic, or study to an organization unless you have actually
retrieved and verified that source. "This follows FMS deceleration
principles" is fine. "An NSCA study found a 23% reduction in X" is not fine
unless you can cite the actual paper — if you can't verify it, leave the
tag off entirely rather than inventing a citation.

## Self-improvement loop

After each review session:
1. Log which proposals the coach approved vs. rejected, and why, in
   `data/inbox/rejected/<sport>.jsonl` (for rejects) — rejections should carry
   a one-line `"reason"` field.
2. Before your next proposal batch, skim recent rejections for recurring
   mistakes (e.g. "kept proposing barbell+immediate combos") and avoid
   repeating them.
3. Run the validator on yourself before showing the coach a batch — the goal
   is for the coach to see mostly-clean proposals, not catch your errors.

You do not self-approve. You do not write to `data/complexes/` directly,
ever, regardless of how confident you are.

`scripts/promote.py` is the sole exception, and only the coach runs it.

## The one safety invariant

**`promote.py` never writes without `--confirm`.** Every invocation that would
write — promoting, superseding, or both — only prints a preview and exits
unless `--confirm` is explicitly passed.

```
promote.py badminton "X" --supersede 21           -> preview only, writes nothing
promote.py badminton "X" --supersede 21 --confirm -> writes for real
```

This is deliberate: the default must be the safe path, because a real write
goes straight into `data/complexes/` and cannot be undone by the script. Do not
add flags, shortcuts, or "just this once" invocations that bypass `--confirm`,
and do not change that default back. `--dry-run` still exists as a no-op alias
so older commands keep working, but it is not what makes this safe — the
absence of `--confirm` is. To test any change to `promote.py`, run it against
the live repo *without* `--confirm`; the preview path is safe to exercise
directly.

## Known issues

- `data/known-issues.md` — deliberate, documented backlog. Read it before
  proposing a fix, so known defects aren't rediscovered from scratch. Currently
  14 complexes carry an accepted swap-inversion defect, plus id 100. These are
  inspected and consciously left alone, not open bugs.

## Token-efficiency rules

- Default to reading `build/index.jsonl`, never a full sport file, unless
  you need the full `execution`/`swap`/`manual` text for one specific
  record.
- Never read all sport files in one session unless explicitly asked to
  audit the whole database.
- When asked to review "the batsman complexes" or similar, grep/filter the
  index first to get IDs, then open only the one sport file those IDs live
  in.
