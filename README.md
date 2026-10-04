# AEON

A sport-specific S&C complex database, migrated from the original
"Master S&C Database" Google Sheet. Built to be cheap for an AI agent
(OpenCode) to read, review, and extend without burning tokens on a
live spreadsheet connector.

## Start here

- **AGENTS.md** — read this first. It's the agent's full operating
  instructions: schema, checklist, workflow, token-efficiency rules.
- **SOURCES.md** — plain-language training-philosophy tags.

## Layout

```
data/
  complexes/<sport>.jsonl   92 complexes migrated from SnC_Complexes_DB
  circuits.jsonl            18 circuits migrated from Circuits_DB
  conditioning.jsonl        33 protocols migrated from Conditioning_DB
  concepts.jsonl            15 coaching concepts (readiness, periodization,
                             methodology) migrated from Coach_Manual_Detailed
                             + Three_Week_Periodization
inbox/proposed/<sport>.jsonl  new ideas land here, one file per sport,
                              never written directly into data/complexes/
  inbox/rejected/<sport>.jsonl  coach rejections kept with a "reason", for
                              the agent's self-improvement loop
build/
  index.jsonl               auto-generated lightweight lookup (143 records)
                             — read this first for any browsing task
schema/                     JSON Schema for each record type
scripts/
  validate.py                run before showing the coach any new idea
  build_index.py              rebuild build/index.jsonl after any edit
  promote.py                  the ONLY script allowed to write into
                               data/complexes/
```

## What did NOT get migrated, and why

- **Coach_Manual_Quick** — a condensed duplicate of Coach_Manual_Detailed.
  Dropped as redundant.
- **Three_Day_Matrix** — explicitly not needed yet per the coach.
- **App_Wiring_Guide** — described an AppSheet build being replaced by
  this repo + a future PWA. Its intent is folded into AGENTS.md context,
  not migrated as data.
- **Readiness_Tracker** — per-athlete session log (3 real rows out of
  1000+ templated blank rows). This is live operational data, not a
  static reference database, and belongs in a separate system later.
- **Ideas_Bank** — its job (staging new ideas before approval) is now
  done by `data/inbox/proposed/<sport>.jsonl`. Not migrated; historical
  record only lives in the original sheet.
- **Floor_View** — a spreadsheet-formula UI, superseded by the future
  PWA/app reading this repo directly.

## Known open item

`data/concepts.jsonl` id 67 (Week 1 periodization scheme) is marked
`UNKNOWN` — the original sheet cell was corrupted to `8/5/2003`
(an autocorrected date) and the coach has not yet supplied the correct
sets/reps scheme. Do not guess a value here; wait for the real one.

## Day-to-day workflow

1. Agent reads `build/index.jsonl` (cheap) before anything else.
2. New ideas go into `data/inbox/proposed/<sport>.jsonl` with
   `"status": "proposed"`.
3. Agent runs `python3 scripts/validate.py data/inbox/proposed/<sport>.jsonl`
   and fixes anything flagged.
4. Coach reviews the inbox batch.
5. Coach (or agent on explicit instruction) runs
   `python3 scripts/promote.py <sport> "<name>"` or `--all-approved`.
   Add `--dry-run` first to preview without writing anything.
6. Run `python3 scripts/build_index.py` to refresh the index.
7. Run `python3 scripts/validate.py` (no args) for a full sanity check.
