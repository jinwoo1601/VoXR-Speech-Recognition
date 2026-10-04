# Backlog candidates

Candidates for issues or features, one line each, dated, with where they surfaced. Not yet filed.

- 2026-10-04 (feat-resolver-slot-exemption, F11 manual check) — a `VoxrTestCase` added with the Inspector's + button gets simulated word confidence 0.00 instead of the field default -1 (`VoxrTestCase.cs:25`), so a new case fails on `confidence 0.00 < minConfidence 0.40` until it is set to -1. Candidate issue.
