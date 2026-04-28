<!-- FROZEN BASELINE — DO NOT EDIT. Captured after Phase 1 (silent catch cleanup).
     Previous baseline: baseline-2026-04-28.md (v3.114.0, 205 silent catches).
     This baseline: post-Phase-1 (silent catch reduced to 7 intentional + 0 unintentional).
     To capture a new baseline, write to a new dated file (baseline-YYYY-MM-DD[-phase].md)
     and update docs/metrics/baseline.md to point to it. -->

# Code Hygiene Metrics

| Field | Value |
|---|---|
| Date | 2026-04-28 |
| Git rev | b06f70c |

| Metric | Count | Notes |
|---|---|---|
| C# files | 121 | |
| Total LOC | 77736 | |
| Files > 1,000 LOC | 22 | godfile 후보 |
| Files > 2,000 LOC | 4 | |
| Files > 4,000 LOC | 2 | 분해 최우선 |
| catch (Exception) total | 289 | |
| Silent catch (LogDebug+ex.Message) | 7 | Phase 1 타깃 |
| ★ vX.Y inline markers | 3501 | Phase 4 자연 소멸 |
| Indented if (16+ spaces) | 4054 | Phase 5 점진 — 향후 20+ 임계값 검토 |
| Main.Log* flat calls | 1721 | Phase 2 카테고리화 타깃 |
