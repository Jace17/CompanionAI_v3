using System;
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.UnitLogic.Parts;
using Pathfinding;
using CompanionAI_v3.Logging;

namespace CompanionAI_v3.GameInterface
{
    public static partial class MovementAPI
    {
        #region Tile Discovery

        public static Dictionary<GraphNode, WarhammerPathPlayerCell> FindAllReachableTilesSync(
            BaseUnitEntity unit,
            float? maxAP = null)
        {
            if (unit == null) return new Dictionary<GraphNode, WarhammerPathPlayerCell>();

            try
            {
                float ap = maxAP ?? unit.CombatState?.ActionPointsBlue ?? 0f;
                if (ap <= 0) return new Dictionary<GraphNode, WarhammerPathPlayerCell>();

                var agent = unit.View?.MovementAgent;
                if (agent == null) return new Dictionary<GraphNode, WarhammerPathPlayerCell>();

                var tiles = PathfindingService.Instance.FindAllReachableTiles_Blocking(
                    agent,
                    unit.Position,
                    ap,
                    ignoreThreateningAreaCost: false
                );

                if (Main.IsDebugEnabled) Log.Engine.Debug($"[MovementAPI] {unit.CharacterName}: Found {tiles?.Count ?? 0} reachable tiles");
                return tiles ?? new Dictionary<GraphNode, WarhammerPathPlayerCell>();
            }
            catch (Exception ex)
            {
                if (Main.IsDebugEnabled) Log.Engine.Error(ex, $"[MovementAPI] FindAllReachableTilesSync error");
                return new Dictionary<GraphNode, WarhammerPathPlayerCell>();
            }
        }

        /// <summary>
        /// ★ v3.8.13: AI용 패스파인딩 - 경로 위협 데이터 포함 (ProvokedAttacks, EnteredAoE, StepsInsideDamagingAoE)
        /// ★ v3.8.15: 캐싱 추가 - 한 턴에 한 번만 계산하여 스터터링 방지
        ///
        /// 게임의 AI와 동일한 메서드 사용 (AiAreaScanner.FindAllReachableNodesAsync 참조)
        ///
        /// 핵심 차이점:
        /// - FindAllReachableTilesSync: WarhammerPathPlayerCell 반환 (위협 데이터 없음)
        /// - FindAllReachableTilesWithThreatsSync: WarhammerPathAiCell 반환 (위협 데이터 포함)
        /// </summary>
        public static Dictionary<GraphNode, WarhammerPathAiCell> FindAllReachableTilesWithThreatsSync(
            BaseUnitEntity unit,
            float? maxAP = null)
        {
            if (unit == null) return new Dictionary<GraphNode, WarhammerPathAiCell>();

            try
            {
                float ap = maxAP ?? unit.CombatState?.ActionPointsBlue ?? 0f;
                if (ap <= 0) return new Dictionary<GraphNode, WarhammerPathAiCell>();

                string unitId = unit.UniqueId;
                int currentTurn = Game.Instance?.TurnController?.CombatRound ?? -1;

                // ★ v3.8.78: 2-슬롯 LRU 캐시 체크
                if (_cachedAiTiles1 != null && _cachedUnitId1 == unitId &&
                    _cachedTurnNumber1 == currentTurn && Math.Abs(_cachedAP1 - ap) < 0.1f)
                {
                    _lastUsedSlot = 1;
                    if (Main.IsDebugEnabled) Log.Engine.Debug($"[MovementAPI] {unit.CharacterName}: Cache HIT slot1 (AP={ap:F1}, {_cachedAiTiles1.Count} tiles)");
                    return _cachedAiTiles1;
                }
                if (_cachedAiTiles2 != null && _cachedUnitId2 == unitId &&
                    _cachedTurnNumber2 == currentTurn && Math.Abs(_cachedAP2 - ap) < 0.1f)
                {
                    _lastUsedSlot = 2;
                    if (Main.IsDebugEnabled) Log.Engine.Debug($"[MovementAPI] {unit.CharacterName}: Cache HIT slot2 (AP={ap:F1}, {_cachedAiTiles2.Count} tiles)");
                    return _cachedAiTiles2;
                }

                var agent = unit.View?.MovementAgent;
                if (agent == null) return new Dictionary<GraphNode, WarhammerPathAiCell>();

                // ★ 게임 AI와 동일: 먼저 위협 데이터 수집
                var threatsDict = AiBrainHelper.GatherThreatsData(unit);

                // ★ AI용 패스파인딩 호출 (비동기 → 동기 변환)
                // 참고: 이 호출은 AI 턴 처리 중이므로 블로킹해도 안전
                var task = PathfindingService.Instance.FindAllReachableTiles_Delayed_Task(
                    agent,
                    unit.Position,
                    (int)ap,
                    threatsDict
                );

                // ★ v3.8.80: 타임아웃 100ms로 축소 (기존 200ms)
                // 대규모 맵에서 AI 패스파인더가 거의 항상 타임아웃 → Player 폴백 사용
                // 100ms로도 소-중규모 맵 성공 여지 유지, 대규모 맵 100ms 절약
                if (!task.Wait(100))
                {
                    if (Main.IsDebugEnabled) Log.Engine.Debug($"[MovementAPI] {unit.CharacterName}: AI pathfinding timeout, falling back to player version");
                    // ★ v3.9.42: 폴백 시에도 위협 데이터 보존 (threatsDict 활용)
                    var fallback = ConvertToAiCells(FindAllReachableTilesSync(unit, maxAP), threatsDict);
                    CacheAiTiles(unitId, ap, currentTurn, fallback);
                    return fallback;
                }

                var tiles = task.Result;
                if (tiles == null || tiles.Count == 0)
                {
                    if (Main.IsDebugEnabled) Log.Engine.Debug($"[MovementAPI] {unit.CharacterName}: AI pathfinding returned null/empty, falling back");
                    var fallback = ConvertToAiCells(FindAllReachableTilesSync(unit, maxAP), threatsDict);
                    CacheAiTiles(unitId, ap, currentTurn, fallback);
                    return fallback;
                }

                // ★ v3.8.78: 2-슬롯 LRU 캐시 저장
                CacheAiTiles(unitId, ap, currentTurn, tiles);

                // ★ 디버그: 경로 위협 데이터 샘플 출력 (첫 호출에서만)
                int threatsFound = 0;
                foreach (var kvp in tiles)
                {
                    var cell = kvp.Value;
                    if (cell.ProvokedAttacks > 0 || cell.EnteredAoE > 0 || cell.StepsInsideDamagingAoE > 0)
                    {
                        threatsFound++;
                        if (threatsFound <= 3)  // 최대 3개만 로깅
                        {
                            if (Main.IsDebugEnabled) Log.Engine.Debug($"[MovementAPI] ThreatData: Node({cell.Position.x:F1},{cell.Position.z:F1}) AoO={cell.ProvokedAttacks}, AoE={cell.EnteredAoE}, DmgAoE={cell.StepsInsideDamagingAoE}");
                        }
                    }
                }

                Log.Engine.Info($"[MovementAPI] {unit.CharacterName}: AI pathfinding found {tiles.Count} tiles, {threatsFound} with threats");
                return tiles;
            }
            catch (Exception ex)
            {
                if (Main.IsDebugEnabled) Log.Engine.Error(ex, $"[MovementAPI] FindAllReachableTilesWithThreatsSync error");
                return ConvertToAiCells(FindAllReachableTilesSync(unit, maxAP));
            }
        }

        /// <summary>
        /// ★ v3.8.13: WarhammerPathPlayerCell → WarhammerPathAiCell 변환 (폴백용)
        /// ★ v3.9.42: threatsDict가 있으면 각 노드의 AoE/AoO 위협 데이터 복원
        /// </summary>
        private static Dictionary<GraphNode, WarhammerPathAiCell> ConvertToAiCells(
            Dictionary<GraphNode, WarhammerPathPlayerCell> playerCells,
            Dictionary<GraphNode, AiBrainHelper.ThreatsInfo> threatsDict = null)
        {
            var result = new Dictionary<GraphNode, WarhammerPathAiCell>();
            if (playerCells == null) return result;

            int threatsRestored = 0;
            foreach (var kvp in playerCells)
            {
                var pc = kvp.Value;
                var node = pc.Node as CustomGridNodeBase;
                if (node == null) continue;

                int enteredAoE = 0;
                int stepsInsideDamagingAoE = 0;
                int provokedAttacks = 0;

                // ★ v3.9.42: threatsDict에서 해당 노드의 위협 데이터 조회
                if (threatsDict != null && threatsDict.TryGetValue(kvp.Key, out var threats))
                {
                    if (threats.aes != null) enteredAoE = threats.aes.Count;
                    if (threats.dmgOnMoveAes != null) stepsInsideDamagingAoE = threats.dmgOnMoveAes.Count;
                    if (threats.aooUnits != null) provokedAttacks = threats.aooUnits.Count;
                    if (enteredAoE > 0 || stepsInsideDamagingAoE > 0 || provokedAttacks > 0)
                        threatsRestored++;
                }

                result[kvp.Key] = new WarhammerPathAiCell(
                    pc.Position,
                    pc.DiagonalsCount,
                    pc.Length,
                    pc.Node,
                    pc.ParentNode,
                    pc.IsCanStand,
                    enteredAoE,
                    stepsInsideDamagingAoE,
                    provokedAttacks
                );
            }

            if (threatsRestored > 0 && Main.IsDebugEnabled)
                Log.Engine.Debug($"[MovementAPI] ConvertToAiCells: Restored threat data for {threatsRestored}/{result.Count} tiles");

            return result;
        }

        #endregion
    }
}
