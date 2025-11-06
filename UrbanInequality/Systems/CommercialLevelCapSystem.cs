// File: Systems/CommercialLevelCapSystem.cs
using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace UrbanInequality.Systems
{
    public partial class CommercialLevelCapSystem : GameSystemBase
    {
        public static float[] MaxLevelPercent => Mod.m_Setting.levelCaps; // same caps as residential

        private int[] _buildingCounts = new int[6];     // Index = 0..5 (0 unused)
        private int[] _maxBuildingsPerLevel = new int[6];
        private int _totalBuildings;

        private EntityQuery _buildingQuery;

        protected override void OnCreate()
        {
            _buildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<BuildingCondition>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<UpdateFrame>()
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<CommercialProperty>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Abandoned>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                }
            });

            RequireForUpdate(_buildingQuery);
        }

        protected override void OnUpdate()
        {
            var buildings = _buildingQuery.ToEntityArray(Allocator.Temp);

            int[] tempCounts = new int[6];
            int[] tempMax = new int[6];
            int totalTemp = 0;

            foreach (var e in buildings)
            {
                if (!EntityManager.TryGetComponent(e, out PrefabRef prefab))
                    continue;

                if (EntityManager.TryGetComponent<SpawnableBuildingData>(prefab.m_Prefab, out var spawn)
                    && EntityManager.TryGetComponent<ZoneData>(spawn.m_ZonePrefab, out var zone)
                    && zone.m_AreaType == AreaType.Commercial)
                {
                    int level = math.clamp(spawn.m_Level, 1, 5);
                    tempCounts[level]++;
                    totalTemp++;
                }
            }

            _totalBuildings = totalTemp;

            for (int i = 1; i <= 5; i++)
            {
                tempMax[i] = math.max(1, Mathf.FloorToInt(totalTemp * MaxLevelPercent[i]));
                _maxBuildingsPerLevel[i] = tempMax[i];
                _buildingCounts[i] = tempCounts[i];
            }

            buildings.Dispose();
        }

        public void GetLevelData(out NativeArray<int> levelCounts, out NativeArray<int> maxCounts, Allocator allocator)
        {
            levelCounts = new NativeArray<int>(6, allocator);
            maxCounts = new NativeArray<int>(6, allocator);
            for (int i = 0; i <= 5; i++)
            {
                levelCounts[i] = _buildingCounts[i];
                maxCounts[i] = _maxBuildingsPerLevel[i];
            }
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // match residential sampler cadence
            return 262144 / (UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay * 16);
        }
    }
}
