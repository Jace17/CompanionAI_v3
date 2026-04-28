# 코드 위생 개선 마스터 플랜 (Phase 0~6)

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement Phase 0+6 plan task-by-task.
> 후속 Phase(1, 2, 3 등)는 각자 별도 플랜 문서를 만들어 진행한다.

**작성일**: 2026-04-28
**근거 문서**: [CompanionAI_v3_분석_및_개선안.md](../../CompanionAI_v3_분석_및_개선안.md) (정량 검증 완료, 2026-04-28)
**검증 메모리**: `memory/MEMORY.md` 참조

**Goal**: 코드 본문 위생을 점진적으로 개선하되, **저자의 LLM 워크플로(CLAUDE.md + 메트릭 가드)를 먼저 정비하여 재오염을 차단**한 뒤 기존 부채를 정리한다.

**Architecture**: 6 Phase 순차 진행. Phase 0+6은 워크플로/측정 인프라(저비용·저위험), Phase 1~3은 코드 본문 정리(고비용·고가치), Phase 4~5는 점진 자연 소멸. 각 Phase는 별도 플랜 문서로 상세화.

**Tech Stack**: 변경 없음 — 기존 C# .NET Framework 4.8.1 + Bash(Git Bash on Windows) + 선택적 PowerShell.

---

## 1. 검증된 현재 상태 (2026-04-28 기준, v3.114.0)

| 메트릭 | 측정값 |
|---|---|
| C# 파일 수 | 121 |
| 총 LOC | 77,704 |
| `> 1,000 LOC` 파일 | 22 (최대 `BasePlan.cs` 4,395) — 분석 doc 의 23은 `wc -l` 의 `total` 행 +1 오기 |
| `> 2,000 LOC` 파일 | 4 — 분석 doc 의 5도 `total` 행 +1 오기 |
| `> 4,000 LOC` 파일 | 2 (`BasePlan.cs` 4,395, `DialogueLocalization.cs` 4,136) |
| `catch (Exception)` 총수 | 289 |
| Silent catch (`LogDebug + ex.Message`) | **205** |
| `★ vX.Y` 인라인 마커 | **3,501** |
| 깊은 중첩 if (16+ 들여쓰기) | 4,054 |
| `Main.Log*` 평면 로깅 호출 | 1,721 |
| 카테고리 로깅 인프라 | **없음** |
| CI / 메트릭 가드 | **없음** (`.github/` 부재) |

---

## 2. Phase 우선순위 매트릭스 (확정)

| Phase | 내용 | 노력 | ROI | 위험 | 의존성 | 별도 플랜 |
|---|---|---|---|---|---|---|
| **0** | **CLAUDE.md 가드레일 강화** | 0.5일 | ★★★★★ | 0 | 없음 — **즉시 시작** | 본 문서 §3 |
| **6** | **메트릭 베이스라인 + 로컬 스크립트** | 0.5일 | ★★★★ | 0 | Phase 0과 병행 가능 | 본 문서 §4 |
| 1 | Silent catch 205건 정리 | 1~2주 | ★★★★★ | 중 (실 버그 노출) | Phase 0+6 후 | TBD (Phase 1 시작 시 작성) |
| 2 | 카테고리 로깅 인프라 도입 | 1주 | ★★★★ | 낮 | Phase 1 후 | TBD |
| 3 | `BasePlan.cs` 등 godfile 분해 (상위 3개만) | 2~4주 | ★★★★ | 중-높 | Phase 2 후 권장 | TBD (Lesson 18 방법론 준용) |
| 4 | `★ vX.Y` 마커 점진 정리 | 점진 | ★★ | 낮 | Phase 0의 룰로 자연 소멸 | 별도 플랜 불필요 |
| 5 | 깊은 중첩 if 점진 평탄화 | 점진 | ★★ | 낮 | 다른 작업과 병행 | 별도 플랜 불필요 |

---

## 3. Phase 0: CLAUDE.md 가드레일 (상세 플랜)

**목적**: 다음 100 커밋에서 silent catch / godfile / 버전 마커가 추가로 누적되는 것을 차단.

**범위**: `CLAUDE.md` 단일 파일 편집 1회. 약 70~100 LOC 추가.

**삽입 위치**: `## 행동 원칙` (line 252) 바로 다음, `## 참조 리소스` (line 262) 바로 앞 — 룰 계열 섹션의 자연스러운 마무리.

**위험**: 0 (코드 변경 없음, 빌드 영향 없음).

### Task 0.1: 추가할 섹션 본문 작성

**파일**:
- 수정: `CLAUDE.md` (line 258 `## 행동 원칙` 종료 직후 삽입)

**Step 1: 신규 섹션 텍스트 (아래 원문 그대로 삽입)**

```markdown
---

## 코드 위생 룰 (반드시 준수)

> 근거: [CompanionAI_v3_분석_및_개선안.md](CompanionAI_v3_분석_및_개선안.md) (2026-04-28).
> 메트릭 측정: `bash scripts/code-metrics.sh` (Phase 6 산출물).

### 예외 처리

- **절대 금지**: `catch (Exception ex) { Main.LogDebug($"... {ex.Message}"); }`
  현재 205곳에 있음. 이 패턴은 **기본 로그 레벨에서 사라지고 스택 트레이스도 손실**되어 사실상 무음 실패.
- **표준 패턴**: `catch (Exception ex) { Main.LogError(ex, $"context"); }` (Phase 1에서 시그니처 도입 예정)
- **Phase 2 이후 표준**: `catch (Exception ex) { Log.<Category>.Error(ex, $"context"); }`
- 의도적으로 무시해야 하는 예외(매 프레임 hot path, 게임의 transient null 등)는 **왜 무시하는지 주석 필수**.

### 파일 크기

- **신규 `.cs` 파일이 800 LOC 넘으면 작업 중단하고 분해 제안**.
- 기존 파일에 새 기능 append 전에 **새 파일로 분리 가능한지 먼저 검토**.
- `partial class`는 분해 부담을 줄이는 가교로만 사용 (영구 해법 아님 — Lesson 18 참조).
- 1500 LOC 초과 신규 파일 작성 금지 (예외: 자동 생성 / 다국어 테이블).

### 버전 주석

- **절대 금지**: 새로운 `★ vX.Y: ...` 형태의 인라인 마커 추가.
  현재 3,501개 누적. git이 해야 할 변경 이력 추적을 코드 주석이 떠맡고 있음.
- 변경 이력은 **git commit 메시지 + LESSONS_LEARNED.md + 별도 CHANGELOG.md**에 둘 것.
- 기존 `★` 마커는 해당 파일 편집 시 함께 제거 (Phase 4 자연 소멸).
- "왜 이 코드가 이렇게 되어 있나"는 git blame 으로 추적 가능 — 코드는 *왜*에 대한 의미만 주석에 둔다.

### 로깅

- **현재**: `Main.Log / LogDebug / LogWarning / LogError` 평면 4함수 (1,721회 사용 중).
- **Phase 2 목표**: `Log.<Category>.<Level>(...)` 카테고리 로깅. 카테고리 후보:
  - `Engine` (Core, GameInterface, Execution)
  - `Planning` (Planning/, Planning/Planners/, Planning/Plans/)
  - `Analysis` (Analysis/)
  - `Diagnostics` (Diagnostics/)
  - `UI` (UI/)
  - `Persistence` (Settings/, Data/)
  - `MachineSpirit` (MachineSpirit/, Dialogue/)
- **Phase 2 완료까지의 신규 코드는** `Main.Log*` 사용 OK (전환 비용 폭증 방지). 단, 새 모듈/큰 신규 기능은 Phase 2 진행 후 작업.

### 중첩 깊이

- **`if` 들여쓰기가 4단계(공백 16칸) 넘으면 early return / guard clause로 평탄화**.
- 현재 4,054건 중첩이 누적. 신규 코드부터 차단.

### 작업 단위

- **한 PR/커밋에서 여러 파일에 걸쳐 동일 패턴(예: try/catch 추가)을 반복하지 말 것** — 추상화 누락 신호.
- 같은 try/catch 블록을 두 곳 이상에 복사하면 **헬퍼로 추출 후 추가**.
- "이 모듈 리팩토링하면서 새 기능 추가" 형태 금지 — **작업 분리** (PR 1: 리팩토링, PR 2: 신기능).

### 코드 리뷰

- "라운드 N" 형태의 같은 모델 + 같은 프롬프트 반복 리뷰는 **같은 사각지대**를 가짐.
- 리뷰 시 **구체적 적대적 질문**을 사용:
  - "이 파일에서 silent failure를 모두 찾아라"
  - "1000 LOC 넘는 파일에서 분리 가능한 책임 영역을 찾아라"
  - "이 catch 블록이 진짜로 모든 Exception을 잡아야 하는 이유를 설명하라"
- **메트릭 기반 자동 검증(`scripts/code-metrics.sh`)을 LLM 리뷰보다 신뢰**.

### 메트릭 회귀 가드

- 작업 완료 전 `bash scripts/code-metrics.sh` 실행하여 **베이스라인 대비 악화가 없는지 확인**.
- 베이스라인은 [docs/metrics/baseline.md](docs/metrics/baseline.md) (Phase 6 산출물).
- 의도적 악화(예: 신규 모듈 추가로 LOC 증가)는 commit 메시지에 명시.
```

**Step 2: 삽입 명령 (Edit 도구 사용)**

`CLAUDE.md` 의 line 258 직후 (`- **중복 코드 발견 시 즉시 리팩토링**, 미사용 코드 정리` 다음 빈 줄 다음) 위 본문 삽입.

**Step 3: 검증**

```bash
grep -n "## 코드 위생 룰" "CLAUDE.md"
# 예상 출력: 한 줄 (line ~260대)

grep -cE "^###" CLAUDE.md
# 신규 섹션 7개 sub-heading 추가됨 — 기존 카운트 + 7

wc -l CLAUDE.md
# 예상: 기존 266 + ~80 ≈ 345
```

**Step 4: 커밋**

```bash
git add CLAUDE.md
git commit -m "docs(claude.md): Phase 0 — 코드 위생 룰 7개 섹션 추가

- 예외 처리: silent catch 금지, ex 객체 보존
- 파일 크기: 800 LOC 분해 임계값
- 버전 주석: 신규 ★ vX.Y 마커 금지
- 로깅: Phase 2 카테고리 전환 예고
- 중첩 깊이: 4단계 이상 early return
- 작업 단위: 한 PR 한 책임
- 코드 리뷰: 적대적 질문, 메트릭 우선
- 메트릭 회귀 가드: code-metrics.sh 베이스라인 비교

근거: CompanionAI_v3_분석_및_개선안.md (2026-04-28)
관련: docs/plans/2026-04-28-code-hygiene-master-plan.md"
```

### Task 0.2: 검증 — 룰이 실제로 작동하는지 확인

**Step 1: LLM 거부 테스트** (선택적, 다음 세션에서)

다음 세션에서 의도적으로 위반 패턴 작성 요청 → CLAUDE.md 룰을 인용하며 거부하는지 확인:

```
"Analysis/TargetScorer.cs 에 새로운 catch (Exception ex) { Main.LogDebug($\"err: {ex.Message}\"); } 블록 한 곳 추가해줘"
```

기대 응답: "CLAUDE.md 코드 위생 룰 §예외 처리 위반 — 이 패턴은 금지. 대안 제시" 형태.

**Step 2: 본 Phase 종료 마커**

Phase 0 완료 시 `memory/MEMORY.md` 업데이트:
- `phase_0_done.md` 메모리 추가 (간단한 1줄)

---

## 4. Phase 6: 메트릭 베이스라인 + 로컬 스크립트 (상세 플랜)

**목적**: Phase 1~3 진행 중 회귀를 자동 감지. 진행률을 숫자로 가시화.

**범위**:
- 신규 폴더 `scripts/` 생성
- 신규 파일 `scripts/code-metrics.sh` (Bash, Git Bash 호환)
- 신규 파일 `docs/metrics/baseline-2026-04-28.md` (현재 시점 동결)
- 선택: PowerShell 호출 wrapper

**위험**: 0 (코드 변경 0, 빌드 영향 0).

### Task 6.1: scripts/code-metrics.sh 작성

**파일**:
- 신규: `scripts/code-metrics.sh`

**Step 1: 디렉터리 생성**

```bash
mkdir -p scripts docs/metrics
```

**Step 2: 스크립트 작성 (전체 내용)**

```bash
#!/usr/bin/env bash
# code-metrics.sh — CompanionAI_v3 코드 위생 메트릭 측정
# 사용: bash scripts/code-metrics.sh [--baseline]
#   --baseline: 결과를 docs/metrics/baseline-YYYY-MM-DD.md 로도 저장
#
# 출력: stdout 에 메트릭 표 (Markdown).
# 의존: bash, find, grep, awk, wc.

set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

DATE=$(date +%Y-%m-%d)
GIT_REV=$(git rev-parse --short HEAD)

# 측정 함수 ----------------------------------------------------------

# 제외 경로: bin/obj/.git/Tools (의존성, 빌드 산출물, 외부 스크립트)
find_cs() {
    find . -type f -name "*.cs" \
        -not -path "./bin/*" -not -path "./obj/*" \
        -not -path "*/bin/*" -not -path "*/obj/*" \
        -not -path "./.git/*" -not -path "./Tools/*"
}

count_files()    { find_cs | wc -l | tr -d ' '; }
count_total_loc() { find_cs -exec cat {} + | wc -l | tr -d ' '; }

count_files_over() {
    local threshold=$1
    find_cs -exec wc -l {} + \
        | awk -v t="$threshold" 'NF==2 && $2!~/total/ && $1>t {n++} END{print n+0}'
}

count_silent_catches() {
    grep -rE "LogDebug.*ex\.Message" --include="*.cs" \
        --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . 2>/dev/null \
        | wc -l | tr -d ' '
}

count_total_catches() {
    grep -rE "catch\s*\(\s*Exception" --include="*.cs" \
        --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . 2>/dev/null \
        | wc -l | tr -d ' '
}

count_version_markers() {
    grep -rE "★\s*v[0-9]" --include="*.cs" \
        --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . 2>/dev/null \
        | wc -l | tr -d ' '
}

count_deep_nested_if() {
    grep -rE "^\s{16,}if\s*\(" --include="*.cs" \
        --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . 2>/dev/null \
        | wc -l | tr -d ' '
}

count_main_log_calls() {
    grep -rE "Main\.Log\w*\(" --include="*.cs" \
        --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . 2>/dev/null \
        | wc -l | tr -d ' '
}

# 측정 실행 ----------------------------------------------------------

FILES=$(count_files)
LOC=$(count_total_loc)
F1000=$(count_files_over 1000)
F2000=$(count_files_over 2000)
F4000=$(count_files_over 4000)
SILENT=$(count_silent_catches)
CATCH_TOTAL=$(count_total_catches)
MARKERS=$(count_version_markers)
DEEP_IF=$(count_deep_nested_if)
MAIN_LOG=$(count_main_log_calls)

# 출력 ---------------------------------------------------------------

cat <<EOF
# Code Hygiene Metrics

| Field | Value |
|---|---|
| Date | $DATE |
| Git rev | $GIT_REV |

| Metric | Count | Notes |
|---|---|---|
| C# files | $FILES | |
| Total LOC | $LOC | |
| Files > 1,000 LOC | $F1000 | godfile 후보 |
| Files > 2,000 LOC | $F2000 | |
| Files > 4,000 LOC | $F4000 | 분해 최우선 |
| catch (Exception) total | $CATCH_TOTAL | |
| Silent catch (LogDebug+ex.Message) | $SILENT | Phase 1 타깃 |
| ★ vX.Y inline markers | $MARKERS | Phase 4 자연 소멸 |
| Deep nested if (16+ indent) | $DEEP_IF | Phase 5 점진 |
| Main.Log* flat calls | $MAIN_LOG | Phase 2 카테고리화 타깃 |
EOF

# 베이스라인 저장 모드 ------------------------------------------------

if [[ "${1:-}" == "--baseline" ]]; then
    OUT="docs/metrics/baseline-${DATE}.md"
    bash "$0" > "$OUT"
    echo "" >&2
    echo "[saved] $OUT" >&2
fi
```

**Step 3: 실행 권한 부여 + 동작 확인**

```bash
chmod +x scripts/code-metrics.sh
bash scripts/code-metrics.sh
```

**기대 출력**: 위 형식의 Markdown 표. 카운트는 §1 표와 일치 (FILES=121, LOC=77704, F1000=23, F2000=5, F4000=2, SILENT=205, CATCH_TOTAL=289, MARKERS=3501, DEEP_IF=4054, MAIN_LOG=1721).

**Step 4: 베이스라인 파일 저장**

```bash
bash scripts/code-metrics.sh > docs/metrics/baseline-2026-04-28.md
cat docs/metrics/baseline-2026-04-28.md
```

**Step 5: docs/metrics/baseline.md 심볼릭 포인터 (선택)**

Windows 에서는 심볼릭 링크 대신 별도 인덱스 파일 사용:

```bash
cat > docs/metrics/baseline.md <<'EOF'
# Baseline Index

현재 활성 베이스라인: [baseline-2026-04-28.md](baseline-2026-04-28.md)

새 베이스라인 갱신 시:
1. `bash scripts/code-metrics.sh > docs/metrics/baseline-YYYY-MM-DD.md`
2. 본 인덱스의 "현재 활성" 링크 업데이트
3. 이전 베이스라인은 archive 로 보존 (회귀 추적용)
EOF
```

**Step 6: 커밋**

```bash
git add scripts/code-metrics.sh docs/metrics/baseline-2026-04-28.md docs/metrics/baseline.md
git commit -m "build(metrics): Phase 6 — code-metrics.sh + 베이스라인 동결

scripts/code-metrics.sh:
- C# 파일 수, 총 LOC, 파일 크기 분포
- catch / silent catch / ★ 마커 / 깊은 중첩 if / Main.Log* 카운트
- --baseline 옵션으로 docs/metrics/baseline-YYYY-MM-DD.md 자동 저장

docs/metrics/baseline-2026-04-28.md:
- v3.114.0 (HEAD $GIT_REV) 시점 동결
- 121 files, 77,704 LOC, 205 silent catches, 3,501 markers

근거: CompanionAI_v3_분석_및_개선안.md (Phase 6)
관련: docs/plans/2026-04-28-code-hygiene-master-plan.md"
```

### Task 6.2: PowerShell wrapper (선택)

**파일**:
- 신규 (선택): `scripts/code-metrics.ps1`

```powershell
# code-metrics.ps1 — Bash 스크립트 호출 wrapper
# 사용: ./scripts/code-metrics.ps1
# 의존: Git Bash (Git for Windows 설치 시 기본 동봉)

$bashExe = "C:\Program Files\Git\bin\bash.exe"
if (-not (Test-Path $bashExe)) {
    Write-Error "Git Bash not found at $bashExe"
    exit 1
}

$script = Join-Path $PSScriptRoot "code-metrics.sh"
& $bashExe $script $args
```

**Step 1: 동작 확인**

```powershell
./scripts/code-metrics.ps1
```

기대: bash 스크립트와 동일한 Markdown 표.

**Step 2: 커밋 (선택 시)**

```bash
git add scripts/code-metrics.ps1
git commit -m "build(metrics): PowerShell wrapper for code-metrics.sh"
```

### Task 6.3: WORK_TRACKER.md 에 메트릭 가드 항목 추가

**파일**:
- 수정: `WORK_TRACKER.md` (완료 판정 기준에 메트릭 회귀 체크 추가)

**Step 1: WORK_TRACKER.md 의 "완료 판정 기준" 섹션 위치 확인**

```bash
grep -n "완료 판정 기준" WORK_TRACKER.md
```

**Step 2: 기존 6항목 다음에 7번 추가**

```markdown
7. **메트릭 회귀 없음** (Phase 6 이후): `bash scripts/code-metrics.sh` 결과가 `docs/metrics/baseline.md` 대비 모든 항목 동등 또는 개선. 의도적 악화는 commit 메시지에 명시.
```

**Step 3: 커밋**

```bash
git add WORK_TRACKER.md
git commit -m "docs(work-tracker): 완료 판정 기준 7번 — 메트릭 회귀 없음

bash scripts/code-metrics.sh 가 baseline.md 대비 동등 또는 개선될 때만 완료.
의도적 악화는 commit 메시지에 명시.

관련: docs/plans/2026-04-28-code-hygiene-master-plan.md"
```

---

## 5. Phase 0+6 완료 후 → Phase 1 진입 조건

다음 모두 충족 시 Phase 1 별도 플랜 작성 시작:

- [ ] Phase 0 commit 1개 (CLAUDE.md)
- [ ] Phase 6 commit 2~3개 (script + baseline + WORK_TRACKER)
- [ ] `bash scripts/code-metrics.sh` 가 §1 표와 일치하는 출력 산출
- [ ] CLAUDE.md `## 코드 위생 룰` 섹션 grep 으로 7개 sub-heading 확인
- [ ] 다음 세션에서 위반 패턴 거부 테스트 1회 (선택)

---

## 6. 후속 Phase 의 진입 시 작성할 별도 플랜 문서

각 Phase 시작 시점에 본 마스터 플랜과 같은 형식으로 별도 작성:

- `docs/plans/YYYY-MM-DD-phase-1-silent-catch-cleanup.md`
- `docs/plans/YYYY-MM-DD-phase-2-category-logging.md`
- `docs/plans/YYYY-MM-DD-phase-3-baseplan-split.md` (Lesson 18 방법론 준용)

각 플랜은:
1. 시작 시점의 메트릭 스냅샷 (`bash scripts/code-metrics.sh` 결과 첨부)
2. 종료 시점의 목표 메트릭
3. Bite-sized 작업 단위
4. 회귀 검증 절차 (Lesson 17: 런타임 로그 증거 필수)

---

## 7. 트레이드오프 / 안 하기로 한 것

| 결정 | 이유 |
|---|---|
| GitHub Actions CI 도입 안 함 (지금) | `.github/` 부재 + 단일 사용자 모드 → 로컬 메트릭 스크립트로 충분. 외부 기여자 받기 시작 시 재검토. |
| Phase 1 일괄 변환 스크립트 본 플랜에 포함 안 함 | 별도 집중 세션 필요 — 200건 변환 후 1~2주 디버깅 여력 확보 시 시작. |
| `★` 마커 일괄 제거 안 함 | 저자의 grep 워크플로 보호 + 단일 거대 커밋 리뷰 불가능. Phase 4 자연 소멸로 처리. |
| 78K LOC 전면 리팩토링 안 함 | ROI 음수. godfile 상위 3개만. |
| Logging/ 폴더 즉시 도입 안 함 | Phase 1 silent catch 정리 먼저 → Phase 2 에서 카테고리화 (변경 폭 분리). |

---

## 8. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-04-28 | 초안 작성. Phase 0+6 상세, Phase 1~5 outline. |
