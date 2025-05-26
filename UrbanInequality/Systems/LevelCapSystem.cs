using Game;
using Game.Buildings;
using Game.Prefabs;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

namespace UrbanInequality.Systems
{
    public partial class LevelCapSystem : GameSystemBase
    {
        public static float[] MaxLevelPercent = Mod.m_Setting.levelCaps; // Percentages for each level cap, indexed from 1 to 5

        private int[] _buildingCounts = new int[6];     // Index = level
        private int[] _maxBuildingsPerLevel = new int[6];
        private int _totalBuildings;

        private EntityQuery _buildingQuery;

        protected override void OnCreate()
        {
            _buildingQuery = GetEntityQuery(ComponentType.ReadOnly<SpawnableBuildingData>());
            RequireForUpdate(_buildingQuery);
        }

        protected override void OnUpdate()
        {
            var buildingDataArray = _buildingQuery.ToComponentDataArray<SpawnableBuildingData>(Allocator.Temp);

            int[] _buildingCounts_Temp = new int[6];     // Index = level
            int[] _maxBuildingsPerLevel_Temp = new int[6];
            int _totalBuildings_Temp = 0;

            foreach (var building in buildingDataArray)
            {
                int level = math.clamp(building.m_Level, 1, 5);
                _buildingCounts_Temp[level]++;
                _totalBuildings_Temp++;
            }

            _totalBuildings = _totalBuildings_Temp;

            for (int i = 1; i <= 5; i++)
            {
                _maxBuildingsPerLevel_Temp[i] = math.max(1, Mathf.FloorToInt(_totalBuildings_Temp * MaxLevelPercent[i]));
                _maxBuildingsPerLevel[i] = _maxBuildingsPerLevel_Temp[i];
                _buildingCounts[i] = _buildingCounts_Temp[i];
                //Mod.log.Info($"_buildingCounts[i]: {_buildingCounts[i]} _maxBuildingsPerLevel[{i}]={_maxBuildingsPerLevel[i]}");
            }
            
            buildingDataArray.Dispose(); 
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

            return 262144 / (UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay * 16);
        }
    }
}
