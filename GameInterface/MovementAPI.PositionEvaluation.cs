using System;
using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.View.Covers;
using Pathfinding;
using UnityEngine;

namespace CompanionAI_v3.GameInterface
{
    public static partial class MovementAPI
    {
        #region Position Evaluation

        public static List<PositionScore> EvaluateAllPositions(
            BaseUnitEntity unit,
            Dictionary<GraphNode, WarhammerPathAiCell> reachableTiles,
            List<BaseUnitEntity> enemies,
            MovementGoal goal,
            float targetDistance = 10f,
            float minSafeDistance = 5f)
        {
            var scores = new List<PositionScore>();
            if (unit == null || reachableTiles == null || reachableTiles.Count == 0)
                return scores;

            foreach (var kvp in reachableTiles)
            {
                var node = kvp.Key as CustomGridNodeBase;
                var cell = kvp.Value;

                if (node == null || !cell.IsCanStand)
                    continue;

                var score = EvaluatePosition(unit, node, cell, enemies, goal, targetDistance, minSafeDistance);
                scores.Add(score);
            }

            // ★ v3.8.48: 정렬 제거 (호출자가 필요 시 직접 정렬)
            return scores;
        }

        public static PositionScore EvaluatePosition(
            BaseUnitEntity unit,
            CustomGridNodeBase node,
            WarhammerPathAiCell cell,
            List<BaseUnitEntity> enemies,
            MovementGoal goal,
            float targetDistance = 10f,
            float minSafeDistance = 5f)
        {
            var score = new PositionScore
            {
                Node = node,
                CanStand = cell.IsCanStand,
                APCost = cell.Length,
                ProvokedAttacks = cell.ProvokedAttacks,
                BestCover = LosCalculations.CoverType.None
            };

            if (enemies == null || enemies.Count == 0)
                return score;

            // ★ v3.111.1 Phase 6: CoverScore 공격자 관점 재설계.
            // 기존 [None=0, Half=15, Full=30, Invisible=40] 방어 aggregate → HideScore와 중복.
            // 신: 게임 fireCoverValues [None=1.0, Half=0.02, Full=0.0004, Invisible=0] 반영 — 공격 효율.
            // 적의 cover가 높을수록 우리 공격 효율 ↓. 평균 × 30으로 스케일 (0~30 범위).
            float fireEfficiencySum = 0f;
            float nearestEnemyDist = float.MaxValue;
            bool hasAnyLos = false;
            int hittableFromLos = 0;  // ★ v3.8.78: LOS 기반 hittable count (CountHittable 중복 제거)
            int validEnemyCount = 0;  // ★ v3.9.26: 유효 적 수 (dead/null 제외)

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.LifeState.IsDead) continue;

                var enemyNode = enemy.Position.GetNearestNodeXZ() as CustomGridNodeBase;
                if (enemyNode == null) continue;

                validEnemyCount++;

                // ★ v3.6.1: 타일 단위로 변환 (minSafeDistance가 타일 단위)
                float dist = CombatAPI.MetersToTiles(Vector3.Distance(node.Vector3Position, enemy.Position));
                if (dist < nearestEnemyDist) nearestEnemyDist = dist;

                try
                {
                    var los = LosCalculations.GetWarhammerLos(enemyNode, enemy.SizeRect, node, unit.SizeRect);
                    var coverType = los.CoverType;

                    if (coverType != LosCalculations.CoverType.Invisible)
                    {
                        hasAnyLos = true;
                        hittableFromLos++;  // ★ v3.8.78: LOS 있으면 hittable 카운트
                    }

                    // coverType 기반 fire efficiency 누적 (LOS 대칭 가정 — 벽 기반 cover는 양방향 동일)
                    float fireEff = 0f;
                    switch (coverType)
                    {
                        case LosCalculations.CoverType.None:      fireEff = 1.0f;    break;
                        case LosCalculations.CoverType.Half:      fireEff = 0.02f;   break;
                        case LosCalculations.CoverType.Full:      fireEff = 0.0004f; break;
                        case LosCalculations.CoverType.Invisible: fireEff = 0f;      break;
                    }
                    fireEfficiencySum += fireEff;

                    if (coverType > score.BestCover)
                        score.BestCover = coverType;
                }
                catch { }
            }

            // ★ v3.111.1 Phase 6: 공격자 관점 fire efficiency 평균 × 30.
            // 0~30 범위 (모두 완전 노출 = 30, 모두 Full cover = ~0.01).
            float avgFireEff = validEnemyCount > 0 ? fireEfficiencySum / validEnemyCount : 0f;
            score.CoverScore = avgFireEff * 30f;
            score.HasLosToEnemy = hasAnyLos;
            score.HittableEnemyCount = hittableFromLos;  // ★ v3.8.78: LOS 기반 hittable count

            // ★ v3.111.0 Phase 5: predictedMoves 주어지면 ensured cover (적 예상 이동 후에도 유지되는 엄폐),
            //   없으면 Phase 1a fallback (적 현재 위치 기반).
            // _currentPredictedMoves는 SituationAnalyzer가 턴 시작 시 SetPredictedMoves로 설정.
            var pm = _currentPredictedMoves;
            var hideComponents = pm != null
                ? TileScorerPort.GetEnsuredCoverComponents(node, unit.SizeRect, enemies, pm)
                : TileScorerPort.GetHideScoreComponents(node, unit.SizeRect, enemies);
            score.ApplyHideComponents(hideComponents);

            // ★ v3.110.20 Phase 2: 적별 Turn Threat Score 합산.
            // 게임 EnemyThreatScore 패턴 — 각 적이 이 턴에 이 위치를 공격 가능한가.
            // threatRange (게임 학습 + 무기 사거리) + AP_Blue 기반.
            float turnThreatSum = 0f;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.LifeState.IsDead) continue;
                turnThreatSum += CombatAPI.GetEnemyTurnThreatScore(enemy, node.Vector3Position);
            }
            score.EnemyTurnThreatSum = turnThreatSum;

            // ★ v3.110.15: ExposureScore — "이 위치를 공격 가능한 적 수"를 페널티화.
            // hittableFromLos는 대칭 LOS(enemyNode → node) 계산이라 "적→자신 LOS 수"와 동일.
            //
            // 공식: sqrt(exposed) × 10
            //   1명: 10, 3명: 17, 5명: 22, 8명: 28, 10명: 32, 15명: 39, 20명: 45
            // 이전 v3.110.15a 공식 `min(hittable, 5) × 5`는 대부분 전장에서 5+ 노출이라 모두 cap=25로
            // saturate → 변별력 0. 실증 로그 27건 전부 -25.0 동일.
            //
            // sqrt 감쇠로 cap 제거. 많은 적에 노출될수록 더 큰 페널티지만 증가율 감소.
            // Attack score(53 평균, 83 최대)와 균형 — 충분히 유의미하되 공격 기회를 완전히 포기시키진 않음.
            // InfluenceMap threat축의 원래 의도를 게임 API로 정확히 구현 (벽/고저차/엄폐 자동 반영).
            score.ExposureScore = hittableFromLos > 0
                ? Mathf.Sqrt(hittableFromLos) * 10f
                : 0f;

            // ★ v3.110.22 Phase 4: StayingAwayScore — 적 이동능력 반영 안전거리.
            //   goal별 가중치: Retreat/FindCover 적극적 거리 유지, 근접 approach는 낮음.
            score.StayingAwayScore = TileScorerPort.GetStayingAwayScore(node, unit, enemies);
            float stayingWeight;
            switch (goal)
            {
                case MovementGoal.Retreat:              stayingWeight = 40f; break;
                case MovementGoal.FindCover:            stayingWeight = 30f; break;
                case MovementGoal.RangedAttackPosition: stayingWeight = 25f; break;
                case MovementGoal.MaintainDistance:     stayingWeight = 20f; break;
                default:                                 stayingWeight = 10f; break;  // Approach/AttackPosition 등 근접 계열
            }
            score.StayingAwayBonus = score.StayingAwayScore * stayingWeight;

            switch (goal)
            {
                case MovementGoal.FindCover:
                case MovementGoal.Retreat:
                    score.DistanceScore = Math.Min(30f, nearestEnemyDist * 2f);
                    break;

                case MovementGoal.MaintainDistance:
                    float distDiff = Math.Abs(nearestEnemyDist - targetDistance);
                    score.DistanceScore = Math.Max(0f, 20f - distDiff * 2f);
                    break;

                case MovementGoal.ApproachEnemy:
                    score.DistanceScore = Math.Max(0f, 30f - nearestEnemyDist * 2f);
                    break;

                case MovementGoal.AttackPosition:
                    if (nearestEnemyDist <= targetDistance && nearestEnemyDist >= 3f)
                        score.DistanceScore = 25f;
                    else if (nearestEnemyDist <= targetDistance)
                        score.DistanceScore = 15f;
                    else
                        score.DistanceScore = 0f;
                    break;

                case MovementGoal.RangedAttackPosition:
                    float weaponRange = targetDistance;

                    if (nearestEnemyDist < minSafeDistance)
                    {
                        // 안전 거리 미만 = 위험 (변경 없음)
                        score.DistanceScore = -50f + nearestEnemyDist * 5f;
                    }
                    else if (nearestEnemyDist <= weaponRange)
                    {
                        // ★ v3.110.8: 게임 공식 일치 — RuleCalculateAbilityDistanceFactor에 따르면
                        //   d ≤ MaxD/2 → DistanceFactor 1.0 (풀 명중률, hitChance ≈ (BS+30)×1.0)
                        //   d ≤ MaxD   → DistanceFactor 0.5 (반토막, hitChance ≈ (BS+30)×0.5)
                        // optimalRatio = 0.5 (game MaxD/2) + weaponRange 기준 정규화 (minSafe 배제).
                        // 이전 (v3.9.48 ~ v3.110.7): optimalRatio=0.6 + minSafe 정규화 → optimal d = minSafe + 0.6×(MaxD-minSafe).
                        // 예: MaxD=15, minSafe=5 → optimal d=11. 그러나 게임 공식 optimal = MaxD/2 = 7.5타일.
                        // 즉 이전 공식은 게임 공식보다 3.5타일 멀리 최적점 설정 → DistanceFactor 0.5 영역(반토막 명중률) 선호 버그.
                        // 이차 감쇠 형태는 유지 (tie-break 연속성).
                        float distRatio = weaponRange > 0.1f ? (nearestEnemyDist / weaponRange) : 0.5f;
                        float optimalRatio = 0.5f;
                        float deviation = Math.Abs(distRatio - optimalRatio);
                        score.DistanceScore = 25f - (deviation * deviation) * 60f;
                    }
                    else
                    {
                        // 무기 사거리 초과 = 접근 필요
                        score.DistanceScore = Math.Max(0f, 10f - (nearestEnemyDist - weaponRange) * 2f);
                    }
                    break;
            }

            score.ThreatScore = cell.ProvokedAttacks * WEIGHT_AOO + cell.EnteredAoE * WEIGHT_AOE_ENTRY;

            if (hasAnyLos && nearestEnemyDist <= targetDistance)
                score.AttackScore = 20f;

            // ★ v3.9.50: Hittable 적 수 보너스 — 공격 가능 위치에 적극적 보너스
            // 방어 패널티만 있고 공격 기회 보너스가 없으면 항상 후퇴가 유리해짐
            //
            // ★ v3.110.9: 포화 곡선으로 변경. 이전 hittable × 8 선형 → hittable 16명이면 +128점
            //   단일 축이 총점의 35~45% 지배 → "멀리서 많이 보이는 위치" 절대 선호 (근거리 소수 커버 밀림).
            // 현재: 1~3명 강보상(+10/명), 4명+부터 sqrt 감쇠.
            //   1명→10, 3명→30, 6명→44, 16명→59.
            //   16명 대 1명 비중 128:8 = 16배 → 60:10 = 6배로 축소.
            //   AoE multi-hit 기회는 여전히 선호하되 과도한 독주는 완화.
            if (hittableFromLos > 0)
            {
                int hc = hittableFromLos;
                float baseBonus = Math.Min(hc, 3) * 10f;
                float extraBonus = hc > 3 ? (float)Math.Sqrt(hc - 3) * 8f : 0f;
                score.AttackScore += baseBonus + extraBonus;
            }

            return score;
        }

        #endregion
    }
}
