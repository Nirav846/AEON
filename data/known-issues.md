# Known issues

Deliberate, documented backlog. Nothing here is an open bug or an unfinished
task — each item is a known defect that was inspected and consciously left in
place. Read this before proposing a fix, so the same records aren't
rediscovered from scratch.

## Why these exist

All 14 were found by the 2026-10-04 swap-only tag sweep, which corrected 28
records whose `equipment` field folded in swap-only tags (ids 30, 51 and 60 had
the same defect earlier). On these 14 the fold was **masking a pre-existing
inversion**: because the swap-only tag sat in `equipment`, `swap_equipment` was
absent, and `check_inversion` returns early on an empty `swap_equipment`. So
correcting the tags is what *reveals* the inversion rather than what creates it.

They are left as-is because every fix is a **coaching redesign** — choosing a
different swap movement — not a data correction. That is different work from a
tag sweep and was deferred to its own pass. Each record therefore still carries
the fold it has always had.

None of the 14 fires both inversion conditions. Every B1 is count-only and
every B2 is scarcity-only, so the two problems are independent and a redesign
can fix one without touching the other.

## B1_COUNT — condition A: swap needs more implements than the primary (4)

The swap drops the primary implement entirely and substitutes two. It is a
different drill that happens to be easier, not a like-for-like fallback. None of
these fire condition B; every tag involved is common-tier, so this is purely a
count problem.

| id | sport | name | primary | swap | why |
|---|---|---|---|---|---|
| 3 | cricket | Bat-Impact Anti-Deflection | `[Kettlebell]` | `[Band, Dumbbell]` | swap is a wholly different drill, not a fallback for the KB carry |
| 28 | pickleball | Forearm Anti-Deflection | `[Kettlebell]` | `[Band, Dumbbell]` | same shape as id 3 |
| 67 | cricket | Lead-Arm Pull & Brace | `[Cable]` | `[Band, Dumbbell]` | swap trades one cable station for two separate implements |
| 105 | cricket | Lumbar Shielding | `[Cable]` | `[Band, Dumbbell]` | already splits `Band` correctly; only `Dumbbell` is folded |

## B2_CABLE — condition B: cable swapped for a common-tier primary (3)

Same implement count (1 vs 1) — these fire purely on scarcity. Cable is the
textbook scarce implement, so in all three the swap is *harder* to obtain than
the default it stands in for, which is the exact inversion the rule forbids.
**37 and 72 are the cleanest redesign targets:** both swap a band Pallof hold or
hip turn for a cable version, so dropping cable for band or DB resolves them.

| id | sport | name | primary | swap | why |
|---|---|---|---|---|---|
| 17 | tennis | Open-Stance Decel & Scoop | `[Med Ball]` | `[Cable]` | swap adds a cable station the primary does not need |
| 37 | cricket | X-Factor Coil & Fire | `[Band, Med Ball]` | `[Cable]` | band hip turn swapped for cable rotational press |
| 72 | pickleball | Volley Block Anti-Rotation | `[Band]` | `[Cable]` | band Pallof swapped for cable Pallof |

## B2_HEAVYBAG — condition B (1)

| id | sport | name | primary | swap | why |
|---|---|---|---|---|---|
| 31 | universal | Rotational Decel Buffer | `[Med Ball]` | `[Heavy Bag]` | worst scarcity inversion in the group — a heavy bag is a fixed fixture, arguably scarcer than a cable station, so the fallback is harder to get than the default |

## B2_TIER — condition B, gated on the `COMMON_TIER` decision (6)

These fire only because `Bench`, `Box`, `Sandbag`, `Kettlebell` and `Barbell`
are outside `COMMON_TIER`. That was **reviewed and deliberately left strict** —
the tier should stay honest and the records should earn their way to passing it,
rather than the tier being widened to clear six records. A bench or barbell
genuinely is more contested on a busy floor than a band or dumbbell; that is a
fact about gyms, not a tooling artifact.

So these are real swap-design problems, not rule-change requests.

| id | sport | name | primary | swap | scarce in swap | why |
|---|---|---|---|---|---|---|
| 18 | tennis | Serve Launch | `[Dumbbell]` | `[Barbell]` | Barbell | swap escalates to a barbell, scarcer than the DB default |
| 33 | universal | Overspeed Eccentric Snap-Down | `[Band]` | `[Bench]` | Bench | altitude drop needs a bench the banded default does not |
| 38 | cricket | Rapid CoM Drop & Block | `[Band]` | `[Box]` | Box | box drop needs a box the banded default does not |
| 53 | hyrox | Fatigue Lunge Yielding | `[Dumbbell]` | `[Sandbag]` | Sandbag | clearest case — a sandbag is genuinely less available than a dumbbell |
| 54 | hyrox | Compromised Grip Carry | `[Trap Bar, Dumbbell]` | `[Kettlebell]` | Kettlebell | swap needs a KB while the primary's trap bar is freely dropped |
| 98 | badminton | Deep-Lunge Hamstring Eccentric | `[Slider, Dumbbell]` | `[Bench]` | Bench | swap is bodyweight but introduces a bench to heel-elevate onto |

## Related: id 100 Blind-Reaction Lateral Pounce (cricket, still approved)

Not part of the 14. A fix was drafted and **rejected** — see
`data/inbox/rejected/cricket.jsonl` for the full record and reason.

id 100 still carries `equipment: ["Partner"]` where `Partner` is used only in
the swap. The fold cannot be corrected without emptying `equipment`, because the
primary is genuinely bodyweight. The proposed fix (`equipment: ["Bodyweight"]` +
naming the bodyweight iso-hold in `execution`) was mechanically correct but
tripped the inversion check on tag-count, and that warning is **honest**: a
partner is the blind-reaction drill's actual stimulus, not an accessory, so no
swap can remove the partner and leave the drill intact. Designing one would mean
inventing a different drill to satisfy a check.

Do not "fix" this record without a genuine partner-free version of the drill.

## Placeholder cues — `manual.cue` restates `focus` (9)

Found 2026-10-06 while fixing a wicketkeeping cue. Two members of the same class
(tennis 161, universal 167) are being corrected in that 3-record cue-fix batch;
these 9 were inspected and **deferred to their own pass**, the same treatment as
the 14-record swap redesign above: each fix is a coaching rewrite of the cue,
not a data correction, so it does not belong folded into a batch doing something
else.

A cue that repeats the focus field is not a cue. It tells the athlete which
quality is being trained instead of how to perform the rep, and it hides in
plain sight only because the two strings match character for character.

| id | sport | name | focus (= current cue) |
|---|---|---|---|
| 127 | tennis | Reactive Split-Step | `Reactive Agility` |
| 132 | tennis | Closed-Stance Transfer | `Rotational Transfer` |
| 138 | universal | Hinge Potentiation | `Horizontal Power Transfer` |
| 143 | cricket | Forearm Torque | `Forearm Strength` |
| 144 | cricket | Zero-Rise Lateral Pouch | `Isometric Strength` |
| 146 | cricket | Transverse Sling Power | `Loaded Rotation to Velocity` |
| 153 | hyrox | Sled Pull Backward Lean | `Upper Body Pull & Posterior Chain Drag` |
| 154 | hyrox | Wall Ball Elastic Rebound | `Lower-to-Upper Force Transfer` |
| 166 | universal | Asymmetrical Chaos Lunge & Push | `Core Anti-Lateral Flexion` |

Also noted, **not** counted above and not a defect on its own: 14 approved
records carry no cue at all (badminton 24; cricket 1, 2, 3, 4, 5, 9, 11, 15;
hyrox 56; tennis 17, 18; universal 33, 57). `manual.cue` is optional, so an
absent cue is a gap in coverage rather than a wrong one — worth filling when
the placeholder pass runs, but it is not this backlog item.

## Rejected-inbox id re-keyed: 168 → 999 (2026-10-06)

`data/inbox/rejected/cricket.jsonl` held id **168**, which sat immediately
above the active complex range (max 167) and therefore collided with the id
`promote.py`'s `next_id()` hands out — it scans `data/complexes/` only, so the
first promotion after this backlog was created would have claimed 168 and
`validate.py` (whose id check covers both inbox directories) would have failed
the very next run with `duplicate id 168`. Proven with a probe record before
any write, not inferred.

**Why 999:** rejected records are inactive, unreferenced by circuits, and
nothing ever promotes from them, so they need an id that is unmistakably out of
the allocation path rather than merely the next free one. 999 is inside the
schema's range (integer, minimum 1, no maximum), far above any near-future
`next_id()` result, and leaves 168–998 as genuine headroom for future complexes.
The edit changed the id digits and nothing else — byte length unchanged, one
line, `status`, `manual` and `reason` all preserved.

If rejected proposals accumulate, re-key them into a reserved band (900+) rather
than letting them occupy the live sequence again.