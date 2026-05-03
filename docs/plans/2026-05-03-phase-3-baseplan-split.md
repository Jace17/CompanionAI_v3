# Phase 3 — BasePlan.cs godfile 분할 플랜

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
> 방법론 근거: [LESSONS_LEARNED.md §18 — god-file partial class 분할 방법론](../../LESSONS_LEARNED.md) (Phase D.2 8세션 검증 완료).
> 마스터 플랜: [docs/plans/2026-04-28-code-hygiene-master-plan.md](2026-04-28-code-hygiene-master-plan.md) §2 Phase 3.

**Goal**: `Planning/Plans/BasePlan.cs` 4,396줄 / 14 region godfile 을 7개 partial class 파일 + 1 residual 로 기계적 분할 (행동 변화 0).

**Architecture**: Lesson 18 방법론 그대로 — 2-commit-per-session (pre-flight + extraction), region-local 필드/메서드 동반 이동, byte-identity 검증, deletion order high-to-low.

**Tech Stack**: 변경 없음 — C# .NET Framework 4.8.1, MSBuild 18, Git Bash on Windows.

---

## 1. 시작 시점 메트릭 (2026-05-03 기준)

| Field | Value |
|---|---|
| Date | 2026-05-03 |
| Git rev | `874a13e` (HEAD) |
| BasePlan.cs LOC | 4,396 |
| BasePlan.cs region 수 | 14 (1개 nested) |
| BasePlan.cs class 선언 | `public abstract class` (NOT partial — Session 1에서 변환) |
| Files > 4,000 LOC (전역) | 2 (BasePlan + DialogueLocalization) |
| Files > 2,000 LOC (전역) | 4 |

**Phase 3 종료 시점 목표 메트릭**:

| Field | Target |
|---|---|
| BasePlan.cs (residual) | ~250 LOC (5 small region + class header) |
| 신규 partial 파일 | 7개 (각 100~1,841 LOC) |
| Files > 4,000 LOC | 1 (DialogueLocalization 만 — Phase 3 제외) |
| Files > 2,000 LOC | 3 (ModSettings, MovementAPI, AttackPlanner — 별도 Phase 3.2/3.3 후속) |
| 행동 변화 | 0 (mechanical refactor) |
| MSBuild warning/error | 0 |
| 인게임 회귀 | 0 (5분 smoke test 통과) |

---

## 2. BasePlan.cs Region 매핑 (2026-05-03 측정, HEAD `874a13e`)

| # | Region | Line 범위 | LOC | 분할 대상 | 세션 |
|:--:|---|:--:|:--:|:--:|:--:|
| 1 | Zero-alloc temp lists | 29-52 | 24 | residual | — |
| 2 | Constants | 54-62 | 9 | residual | — |
| 3 | Confidence Helpers | 64-99 | 36 | residual | — |
| 4 | Abstract Methods | 101-113 | 13 | residual | — |
| 5 | Heal/Reload delegates | 115-133 | 19 | residual | — |
| 6 | **Movement delegates (Phase 0.2 nested + L593-899 Familiar Phase 공통)** | 135-900 | **765** | `BasePlan.Movement.cs` | **7** |
| 7 | Attack delegates | 902-1009 | 108 | `BasePlan.Attack.cs` | **2** (pilot) |
| 8 | Weapon Set Rotation | 1011-1272 | 262 | `BasePlan.WeaponRotation.cs` | **4** |
| 9 | AOE Heal/Buff | 1274-1418 | 145 | `BasePlan.AoEHealBuff.cs` | **3** |
| 10 | Buff/Debuff delegates | 1420-1731 | 312 | `BasePlan.BuffDebuff.cs` | **6** |
| 11 | Post-Plan Validation | 1733-2007 | 275 | `BasePlan.PostValidation.cs` | **5** |
| 12 | Common Methods | 2009-2552 | 544 | `BasePlan.Common.cs` | **7-pre or 8-pre** |
| 13 | **Familiar Support** | 2554-4394 | **1,841** | `BasePlan.FamiliarSupport.cs` | **8** (final, ★★★★) |

**계산**: 7 partial 추출 = 4,151 LOC 이동, residual = 245 LOC (header + 5 small region + namespace/using).

**중요 구조적 메모 (Region #6)**:
- L135-L522: 진짜 Movement delegate 메서드들
- L523-L591: nested `#region Phase 0.2: Common Early Phase Methods` (69 LOC)
- L592-L899: orphan **Familiar Phase 공통 메서드** (308 LOC) — region 으로 감싸져 있지 않으나 Movement 의 outer #endregion (L900) 안에 있음
- → Lesson 18 mechanical 원칙: **L135-L900 전체를 단일 청크로 추출** (Familiar Phase 코드도 Movement.cs 에 포함). 의미적 재배치는 Phase 3 범위 밖.

---

## 3. 세션 순서 및 사전 위험도

**원칙**: 작은 region → 큰 region 순. 각 세션 크기/난이도를 점진 증가시켜 방법론을 체화.

| 세션 | Region | LOC | 난이도 | 특이사항 |
|:--:|---|:--:|:--:|---|
| **1** | (extraction 없음) | — | ★ | **`abstract class` → `abstract partial class` 변환만**. extract 없이 빌드 0-warning 검증 |
| **2** | Attack delegates | 108 | ★ pilot | 가장 작은 추출. Lesson 18 `private static` 호출 마커 패턴 학습 |
| **3** | AOE Heal/Buff | 145 | ★ | 단일 region. AOE 헬퍼는 self-contained 가능성 높음 |
| **4** | Weapon Set Rotation | 262 | ★★ | weapon set 관련 protected helpers 군집 |
| **5** | Post-Plan Validation | 275 | ★★ | `Validate*` 메서드 군집 |
| **6** | Buff/Debuff delegates | 312 | ★★ | BuffPlanner 위임 패턴 |
| **7** | Movement (incl Phase 0.2 nested + Familiar Phase 공통) | 765 | ★★★ | nested region + orphan code 유의 |
| **7-pre or 8-pre** | Common Methods | 544 | ★★ | 7 또는 8 세션 전 별도 청크 (병합 가능) |
| **8** | Familiar Support | 1,841 | ★★★★ | 최대 region. 단일 추출 또는 sub-split 검토 |
| **9** | (선택) 최종 정리 | — | ★ | LESSONS_LEARNED Phase 3 retrospective + memory 업데이트 |

**총 commits 예상**: 16-18 (8 pre-flight + 8 extraction + 1-2 정리). Phase D.2 와 동급.

---

## 4. 세션 1 — partial 변환 (extract 없음)

**목적**: `abstract class` → `abstract partial class` 만 변경. Extraction 없이 빌드 검증으로 향후 세션의 baseline 확보.

### Files

- 수정: `Planning/Plans/BasePlan.cs:27`

### Step 1: pre-flight HEAD 확인

```bash
git status
# 예상: clean, HEAD = 874a13e
git log --oneline -1
```

### Step 2: class 선언 변경

`Planning/Plans/BasePlan.cs:27`:

```csharp
// BEFORE:
public abstract class BasePlan

// AFTER:
public abstract partial class BasePlan
```

**중요**: `partial` 키워드만 추가. 다른 변경 0.

### Step 3: MSBuild 검증

```powershell
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  CompanionAI_v3.csproj -p:Configuration=Release -t:Rebuild -v:minimal -nologo
```

**기대**: `0 Warning(s), 0 Error(s)`.

### Step 4: byte-identity 확인 (메서드/필드 변경 0)

```bash
git diff --stat Planning/Plans/BasePlan.cs
# 예상: 1 file changed, 1 insertion(+), 1 deletion(-)
```

### Step 5: Info.json 버전 bump

`Info.json`:

```json
"Version": "3.115.0",
```

### Step 6: 커밋

```bash
git add Planning/Plans/BasePlan.cs Info.json
git commit -m "refactor(v3.115.0): Phase 3 — BasePlan.cs partial class 선언 전환

abstract class → abstract partial class. 이후 Phase 3 세션 2-8 에서
14 region 중 7개를 BasePlan.<Region>.cs partial 파일로 분할 예정.

방법론: LESSONS_LEARNED §18 (Phase D.2 8세션 검증)
플랜: docs/plans/2026-05-03-phase-3-baseplan-split.md"
```

---

## 5. 세션 2 — Attack delegates 추출 (pilot)

**목적**: 가장 작은 region (108 LOC) 으로 Lesson 18 mechanical 추출 패턴 학습.

### Files

- 신규: `Planning/Plans/BasePlan.Attack.cs`
- 수정: `Planning/Plans/BasePlan.cs` (Region #7 제거)

### Step 1: Pre-flight commit

```bash
# region line 재측정
grep -n "^\s*#region\|^\s*#endregion" Planning/Plans/BasePlan.cs

# 본 플랜의 §2 표와 일치 확인. line shift 발생 시 플랜 line 번호 업데이트.
git add docs/plans/2026-05-03-phase-3-baseplan-split.md
git commit -m "docs(plan): Phase 3 Session 2 pre-flight — Attack region 실측 line 반영"
```

### Step 2: 새 partial 파일 작성

`Planning/Plans/BasePlan.Attack.cs`:

```csharp
using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using CompanionAI_v3.Analysis;
using CompanionAI_v3.Core;
using CompanionAI_v3.Planning.Planners;

namespace CompanionAI_v3.Planning.Plans
{
    public abstract partial class BasePlan
    {
        #region Attack - Delegates to AttackPlanner

        // [BasePlan.cs 의 L902-L1009 region 본문 그대로 복사]

        #endregion
    }
}
```

**using 결정 원칙**:
1. 원본 BasePlan.cs 의 using 19개 중 Attack region 본문에서 실제 참조하는 namespace 만 포함
2. Lesson 18 원칙 6 (FQN > 새 using) 우선
3. Orphan using 발견 시 같은 커밋에서 정리

### Step 3: Region 본문 복사 — byte-identity 검증

```bash
# 원본 region 추출
git show HEAD:Planning/Plans/BasePlan.cs | sed -n '902,1009p' > /tmp/orig_attack.txt

# 새 파일의 region 본문 추출 (using/namespace/class 제외)
sed -n '/#region Attack/,/#endregion/p' Planning/Plans/BasePlan.Attack.cs > /tmp/new_attack.txt

# diff (기대: 0)
diff /tmp/orig_attack.txt /tmp/new_attack.txt
```

### Step 4: BasePlan.cs 에서 region 제거

`Planning/Plans/BasePlan.cs`:
- L902 `#region Attack...` 부터 L1009 `#endregion` 까지 (108 LOC) 삭제
- 인접한 빈 줄 정리 (1 줄만 남기기)

### Step 5: MSBuild 검증

```powershell
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  CompanionAI_v3.csproj -p:Configuration=Release -t:Rebuild -v:minimal -nologo
```

**기대**: `0 Warning(s), 0 Error(s)`. CS0246 (using 누락) 발생 시 1-2 iteration 으로 추가.

### Step 6: Cross-partial `private static` 호출 점검 (Lesson 18 §5)

```bash
# Attack region 외부에서 region 내부 private static 호출하는지 확인
grep -rn "AttackPlanner\." Planning/Plans/BasePlan.cs Planning/Plans/BasePlan.*.cs
```

발견 시 호출 사이트에 `// Helper: BasePlan.Attack.cs` 마커 주석 추가.

### Step 7: Info.json 버전 bump

```json
"Version": "3.115.1",
```

### Step 8: 커밋

```bash
git add Planning/Plans/BasePlan.cs Planning/Plans/BasePlan.Attack.cs Info.json
git commit -m "refactor(v3.115.1): Phase 3 Session 2 — Attack region → BasePlan.Attack.cs (108줄)

Region: 902-1009 (Attack delegates to AttackPlanner)
방법론: Lesson 18 (byte-identical, mechanical)
검증: MSBuild 0 Warning/Error, byte-identity diff 0
다음: Session 3 — AOE Heal/Buff (145줄)"
```

---

## 6. 세션 3-7 — 동일 패턴 반복

각 세션은 §5 의 8 step 절차를 그대로 적용. 다만 region/LOC/파일명만 변경:

### Session 3: AOE Heal/Buff (145줄)
- 신규: `BasePlan.AoEHealBuff.cs`
- Region: 1274-1418
- 버전: `3.115.2`

### Session 4: Weapon Set Rotation (262줄)
- 신규: `BasePlan.WeaponRotation.cs`
- Region: 1011-1272
- 버전: `3.115.3`
- 특이: weapon-set 관련 `private static` cache 가 region 내부에 있는지 확인 → 있으면 동반 이동 (Lesson 18 §3)

### Session 5: Post-Plan Validation (275줄)
- 신규: `BasePlan.PostValidation.cs`
- Region: 1733-2007
- 버전: `3.115.4`

### Session 6: Buff/Debuff delegates (312줄)
- 신규: `BasePlan.BuffDebuff.cs`
- Region: 1420-1731
- 버전: `3.115.5`

### Session 7a: Common Methods (544줄)
- 신규: `BasePlan.Common.cs`
- Region: 2009-2552
- 버전: `3.115.6`
- 특이: "Common Methods (not delegated)" — 다른 partial 에서 호출 빈도 높음 예상. Cross-partial 마커 주석 다수 추가 필요할 가능성.

### Session 7b: Movement (765줄)
- 신규: `BasePlan.Movement.cs`
- Region: 135-900 (nested Phase 0.2 + orphan Familiar Phase 공통 포함)
- 버전: `3.115.7`
- **특이 (Lesson 18 §8 nested region)**: L523-591 의 nested `#region Phase 0.2` 자동 동반. Familiar Phase 공통 메서드 (L592-899) 도 outer endregion 내부에 있으므로 함께 이동.
- **byte-identity 검증 시**: nested region + orphan code 모두 포함하는 단일 sed 범위 사용

### Session 8: Familiar Support (1,841줄, FINAL extraction)
- 신규: `BasePlan.FamiliarSupport.cs`
- Region: 2554-4394
- 버전: `3.115.8`
- **★★★★ 난이도**: 최대 region. 단일 추출 시 partial 파일 자체가 1,841줄로 godfile 임계값에 근접 (CLAUDE.md 1500줄 초과 신규 파일 금지 룰 위반 가능성).
- **결정 시점**: Session 8 직전 별도 sub-plan 작성 검토:
  - **Option A** (단순): 단일 추출 → CLAUDE.md 룰 예외 명시 (godfile 분해 결과물이지 신규 godfile 아님)
  - **Option B** (재분할): Familiar Support 내부 sub-region 발견 시 2-3 partial 로 분할 (`BasePlan.Familiar.Buffs.cs`, `BasePlan.Familiar.Combat.cs` 등)
  - Pre-flight 단계에서 `grep -n "//\s*===\|^\s*///" L2554-L4394` 로 자연 경계 탐색 후 결정

---

## 7. 세션 9 — 최종 정리 + 회고 (선택)

### Step 1: 최종 메트릭 측정

```bash
bash scripts/code-metrics.sh > docs/metrics/baseline-2026-05-03-phase3.md
diff docs/metrics/baseline-2026-04-29-phase2.md docs/metrics/baseline-2026-05-03-phase3.md
```

**기대 변화**:
- C# files: 122 → 129 (+7 partial)
- Total LOC: 78,003 → ~78,100 (scaffold ~100줄 +)
- Files > 4,000 LOC: 2 → 1 (BasePlan 제거)
- Files > 2,000 LOC: 4 → 3 (BasePlan 제거)
- 다른 메트릭: 동등 (silent catch, marker, 중첩 if, Main.Log* 모두 동등)

### Step 2: docs/metrics/baseline.md 인덱스 업데이트

```markdown
현재 활성 베이스라인: [baseline-2026-05-03-phase3.md](baseline-2026-05-03-phase3.md)
```

### Step 3: LESSONS_LEARNED.md Lesson 18 보강

- "Phase 3 BasePlan.cs 검증 추가 사례" 섹션 신설
- Phase D.2 (CombatAPI static class) vs Phase 3 (BasePlan abstract class) 차이점 정리
- nested region + orphan code 처리 사례 (Session 7b) 추가

### Step 4: memory/MEMORY.md 업데이트

`memory/phase_3_baseplan_split.md` 신규 작성 + MEMORY.md 인덱스 추가.

### Step 5: 인게임 smoke test (사용자 의뢰)

5분 전투 세션 + UMM 로그 `GameLogFull.txt` 0 error/exception 확인.

### Step 6: 커밋 + (사용자 요청 시) release

```bash
git add docs/metrics/ docs/plans/2026-05-03-phase-3-baseplan-split.md \
        LESSONS_LEARNED.md memory/MEMORY.md memory/phase_3_baseplan_split.md
git commit -m "docs(v3.115.x): Phase 3 완료 annotation + LESSONS_LEARNED 보강

Phase 3 결과: BasePlan.cs 4,396 → ~250줄 + 7 partial (3,946줄)
검증: byte-identical 모든 세션, MSBuild 0 warning, 인게임 smoke test 통과

플랜: docs/plans/2026-05-03-phase-3-baseplan-split.md"
```

**Release 결정**: 사용자 명시 요청 시에만 (memory `feedback_release_only_on_request.md` 준수).

---

## 8. 회귀 검증 절차 (각 세션 공통)

### 8.1 빌드 검증 (필수)

```powershell
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  CompanionAI_v3.csproj -p:Configuration=Release -t:Rebuild -v:minimal -nologo
```

**Pass 조건**: `0 Warning(s), 0 Error(s)`.

### 8.2 byte-identity 검증 (필수)

```bash
# 추출된 region 본문과 원본 동일성
diff <(git show <session-pre>:Planning/Plans/BasePlan.cs | sed -n '<start>,<end>p') \
     <(sed -n '/#region <name>/,/#endregion/p' Planning/Plans/BasePlan.<Region>.cs)
```

**Pass 조건**: diff 0 또는 정확히 마커 주석 추가 줄수만큼 + (Lesson 18 §10).

### 8.3 인게임 smoke test (Session 8 + 최종 정리 시)

5분 전투 세션 + 로그 0 error/exception. **CLAUDE.md 완료 판정 6번 (런타임 로그 증거)** 충족.

### 8.4 메트릭 회귀 가드 (Session 9 시)

```bash
bash scripts/code-metrics.sh
```

baseline 대비 silent catch / marker / 중첩 if / Main.Log* 동등 또는 개선.

---

## 9. 트레이드오프 / 안 하기로 한 것

| 결정 | 이유 |
|---|---|
| DialogueLocalization.cs 분할 안 함 | CLAUDE.md "예외: 자동 생성 / 다국어 테이블" 적용. 본문 99% 가 5개 언어 Dictionary 리터럴 |
| ModSettings.cs 분할 본 플랜 미포함 | Localization Dictionary 가 1,500+ 줄 차지. 별도 Phase 3.2 에서 Localization 분리 검토 |
| MovementAPI.cs 분할 본 플랜 미포함 | 별도 Phase 3.3. 10 region 구조 양호하여 BasePlan 완료 후 동일 방법론 재사용 |
| Familiar Support sub-split 본 플랜 미확정 | Session 8 pre-flight 단계에서 sub-region 자연 경계 확인 후 결정 |
| Logic refactor 안 함 | Phase 3 는 mechanical. orphan Familiar Phase 공통 메서드의 Movement region 위치 등 의미적 재배치는 후속 Phase |
| 각 세션 인게임 검증 안 함 | byte-identity + MSBuild 0-warning 으로 충분. 최종 정리 시 1회만 |

---

## 10. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-05-03 | 초안 작성. 8 추출 세션 + 1 partial 변환 + 1 정리 = 총 10 세션 / 16-18 commits |
