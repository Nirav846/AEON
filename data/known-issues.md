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

## No equivalent fallback (5): convention recorded 2026-10-07

Resolved in this session. Five records had no swap that preserved the training
intent, and every candidate either escalated to a scarcer implement or was a
different drill wearing the same name:

| id | sport | why nothing works |
|---|---|---|
| 72 | pickleball | Pallof needs lateral resistance; only a cable produces it, and the primary already owns the band |
| 3 | cricket | The stimulus is kettlebell bottom-up instability; a dumbbell is not bottom-up at all |
| 28 | pickleball | Same — KB bottom-up hold is the stimulus, DB Farmer's Hold is a different exercise |
| 18 | tennis | Primary is already a dumbbell push press; barbell escalates, a lighter load changes the stimulus |
| 53 | hyrox | Stimulus is *loaded* eccentric yielding; the only common-tier load is the dumbbell the primary already uses |

**The convention.** Each now carries `swap: "No equivalent fallback; perform as
written."` with `swap_equipment` absent. It is the exact wording to use — the
Detail page shows it honestly, the schema and validator are untouched, and the
inversion check has nothing to judge. Recorded in `AGENTS.md` filter 3.

Making `swap` optional was considered and declined: it would touch
`schema/complex.schema.json`, `scripts/validate.py`, `src/Aeon.Data/Models.cs`,
two app pages (`ComplexDetailPage`, `SearchPage`) and this file, all to serve 5
records out of ~170. Revisit only if the count grows past a handful.

Still open on id 72: a bodyweight anti-rotation fallback needing no band is worth
judging against the intent by the coach. It is not written, because a Pallof's
lateral line of pull is the whole demand and no bodyweight version obviously
keeps it.

## Inversion check: fixed to ignore implements the primary already carries (2026-10-07)

`check_inversion` condition B used to judge *every* swap implement, including
ones the primary already needs. A bodyweight swap that reused the primary's own
slider was flagged for using a reserved implement, even though it needed strictly
fewer things — the opposite of an inversion. Condition B now judges only the
implements the swap adds (`validate.py`, the `added = [e for e in sw if e not in
eq]` line). Condition A, the implement-count rule, is deliberately untouched: a
real count inversion stays visible even when the swap shares equipment with the
primary.

**Known limitation, left as-is:** condition B uses "all", not "any". A swap
carrying one common-tier implement alongside one scarce one passes the check,
because a single common-tier tag satisfies it.

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
## Building an installable APK: Debug builds do NOT run on a device

Confirmed against a physical device (OPPO CPH2531, Android 15 / SDK 35,
arm64-v8a) on 2026-10-07.

**A default `dotnet build` produces a fast-deploy artifact that will not run
standalone.** The .NET Android SDK emits *two* APKs plus a sidecar of managed
assemblies that is normally pushed to the device by the tooling. Install the APK
alone and it aborts roughly two seconds in, with a **native `SIGABRT` and no Java
stack trace at all** - so there is nothing in `logcat` except:

```
F libc  : Fatal signal 6 (SIGABRT), code -1 (SI_QUEUE) in tid ... (com.aeon.app)
F DEBUG : Abort message: 'No assemblies found in
 '/data/user/0/com.aeon.app/files/.__override__/arm64-v8a' or '<unavailable>'.
 Assuming this is part of Fast Deployment. Exiting...'
```

This is **not** an ABI problem, not an SDK-floor problem and not an app crash.
The ABI (`arm64-v8a`) and `minSdkVersion=21` were both fine; managed code is
never reached, so no managed exception can appear.

**Use Release.** `AndroidPackageFormat=apk` is now pinned in
`src/Aeon.App/Aeon.App.csproj` for Release, so the flag cannot be forgotten:

```
dotnet build src/Aeon.App -f net10.0-android -c Release
adb install -r src/Aeon.App/bin/Release/net10.0-android/com.aeon.app-Signed.apk
adb shell monkey -p com.aeon.app -c android.intent.category.LAUNCHER 1
```

**Which APK is which.** Release emits two files and only one is installable:

| file | installable |
|---|---|
| `com.aeon.app-Signed.apk` | **yes - this is the one** |
| `com.aeon.app.apk` | no, unsigned intermediate |

Debug and Release both name their output `com.aeon.app-Signed.apk`, so always
check the path contains `bin\Release\`, not `bin\Debug\`. Confirm a Release build
is self-contained by checking for `lib/arm64-v8a/libassembly-store.so` and
`libaot-Aeon.App.dll.so` inside the APK; their absence means fast-deploy.

**Do not launch with `am start -n com.aeon.app/.MainActivity`.** That component
name does not exist. .NET Android rewrites managed class names in the final
manifest, so the real activity is `com.aeon.app/crc64f3c3ad5db5b6f78a.MainActivity`
(a CRC64 of the class name, and it changes if the class is renamed). To see the
real one:

```
aapt2 dump xmltree --file AndroidManifest.xml <apk> | findstr MainActivity
```

Use the LAUNCHER intent instead - it resolves through the intent-filter and is
what tapping the icon actually does.

## Standing rule for new validator checks

A new check in `validate.py` must not early-return on an absent field: an absent field means "not judged", which is indistinguishable from "judged and passed", and that is precisely how the `swap_equipment` fold hid inversions across 40+ records for weeks.

## Four surfaced inversions (2026-10-07): mini-batch + one queued review

The `all` -> `any` tightening in condition B surfaced four swaps that add a
scarcer implement than their primary uses. Three were promoted earlier the same
day carrying their original swaps untouched, so these are pre-existing defects,
not regressions - but they are real, and none is being folded or hidden.

| id | sport | swap adds | disposition |
|---|---|---|---|
| 185 | cricket | Landmine | RESOLVED -> 194 |
| 186 | hyrox | Sled | RESOLVED -> 195 |
| 187 | hyrox | Landmine | RESOLVED -> 196 |
| 34 | universal | Bench | queued review, see below |

All three were the same shape as the six fixed earlier this week: a scarce
implement substituting for something the primary already covers. Fixed by
dropping the substitution, not by re-scoping the tier.

- **194** Landmine Twist -> the med-ball toss the primary already owns.
- **195** Reverse Sled Drag -> the band-resisted drag the primary already owns.
- **196** Landmine Push Press + Banded Squat Jumps -> bodyweight thrusters,
  keeping the toss to max height. This swap had been dropping the toss entirely,
  so `Lower-to-Upper Force Transfer` was never expressed in the fallback.

**Why 196 is not the same case as ids 3 and 28.** Both look like "a fallback
that is easier, not equivalent", and they were judged differently on purpose.
Bottom-up instability is binary: a dumbbell cannot produce it at all, so the
stimulus disappears with the swap. Load is a magnitude, and turning it down is
something a fallback is expected to do. Where the *stimulus itself* is binary,
no fallback exists and the convention applies; where only the *load* changes, a
bodyweight version of the same movement is a genuine fallback. That distinction,
not the precedent from 33 and 38, is the reason 196 was accepted.

## Queued review: id 34 Cervical-Vestibular Integration

Not urgent, but recorded here so it does not rot as an unexplained warning.

**Diagnosis - the fold is still present.** `equipment` reads
`["Band","Tennis Ball","Wall","Bench","Partner"]`, but `Bench` and `Partner` are
named only in the swap (`Isometric Neck Hold against Bench + Partner Catch`).
They are swap-only tags sitting in `equipment`, which is exactly the defect that
kept id 34 off the 2026-10-04 sweep entirely - the sweep corrected 28 records
and this one never surfaced because the fold hid it. Correctly split, it is a
B2_TIER case: primary `["Band","Tennis Ball","Wall"]`, swap
`["Bench","Partner"]`, and `Bench` is outside COMMON_TIER.

**Candidate options for a later pass**, neither applied:
- `Anti-Extension Neck Hold against a Wall + Partner Catch` - `Wall` is already
  in the primary and is common-tier, leaving `Partner` as the only added
  implement (also common-tier). Question for the coach: a wall press is less
  support than a bench, so is it a weaker neck-arming stimulus or an equivalent?
- Keep the bench and accept the warning, documenting why.

## Future backlog: cue coverage (not being fixed today)

23 approved records have a `manual.cue` that either restates `focus` or is
absent entirely. The 9 placeholder cues are fixed; the remaining 14 have no cue
at all (badminton 24; cricket 1, 2, 3, 4, 5, 9, 11, 15; hyrox 56; tennis 17,
18; universal 33, 57). `manual.cue` is optional, so an absent cue is a coverage
gap rather than a wrong value. Writing them is a coaching pass of its own and is
deliberately **not** bundled into a data-correction batch. Tracked here so the
gap is visible rather than forgotten.
