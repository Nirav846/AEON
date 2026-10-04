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