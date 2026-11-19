# ATLAS Master Plan v3.3

The [canonical Markdown](ATLAS_MASTER_PLAN.md) is the exact owner-supplied 2026-11-19 edition. Its [version manifest](ATLAS_MASTER_PLAN_VERSION.json) pins the canonical bytes, [chapter index](../../indexes/ATLAS_MASTER_INDEX.json), and [requirements trace](../../indexes/ATLAS_REQUIREMENTS_TRACE.csv). The [quickstart](ATLAS_QUICKSTART.md) is the agent startup contract. Current implementation and independent verdicts are recorded in the [handoff](ATLAS_HANDOFF.md) and [owner report](ATLAS_OWNER_REPORT.md).

```powershell
python -B tools/check_master_plan.py
python -B tools/check_master_plan.py --section CH-172
python -B tools/check_master_plan.py --search "independent evaluation"
```

The source Python suite exercises the same version gate. Native planning entry points should load `lab/master_spec.py`, retrieve `CH-015` and the task-specific clauses, and record the returned master SHA-256 and consulted IDs in their receipts. This is a trusted read-only specification lookup. It confers no host permissions.

The [versioned archive register](archive/master-plan/README.md) identifies the complete preserved package, previous versions, and provenance. The [live trace](../../indexes/ATLAS_REQUIREMENTS_LIVE.csv) identifies bounded implementation evidence, limitations, and dependencies. The package-supplied requirements files remain original snapshots.
