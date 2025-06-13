
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Economy;
using Game.Net;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Game.Zones;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities.Internal;

namespace UrbanInequality.Systems
{
    public partial class UrbanInequalityBuildingUpkeepSystem : GameSystemBase
    {
        public static readonly int kUpdatesPerDay = 16;
        public static readonly int kMaterialUpkeep = 4;
        private SimulationSystem m_SimulationSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        private ResourceSystem m_ResourceSystem;
        private ClimateSystem m_ClimateSystem;
        private CitySystem m_CitySystem;
        private IconCommandSystem m_IconCommandSystem;
        private TriggerSystem m_TriggerSystem;
        private TaxSystem m_TaxSystem;
        private ZoneBuiltRequirementSystem m_ZoneBuiltRequirementSystemSystem;
        private Game.Zones.SearchSystem m_ZoneSearchSystem;
        private ElectricityRoadConnectionGraphSystem m_ElectricityRoadConnectionGraphSystem;
        private WaterPipeRoadConnectionGraphSystem m_WaterPipeRoadConnectionGraphSystem;
        private NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment> m_UpkeepExpenseQueue;
        private NativeQueue<Entity> m_LevelupQueue;
        private NativeQueue<Entity> m_LeveldownQueue;
        private EntityQuery m_BuildingPrefabGroup;
        private EntityQuery m_BuildingSettingsQuery;
        private EntityQuery m_BuildingGroup;
        private EntityQuery m_EconomyParameterQuery;
        public bool debugFastLeveling;
        private LevelCapSystem m_LevelCapSystem;
        private UrbanInequalityBuildingUpkeepSystem.TypeHandle __TypeHandle;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            
            return 262144 / (UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay * 16);
        }

        public static float GetHeatingMultiplier(float temperature)
        {
            return math.max(0.0f, 15f - temperature);
        }

        [UnityEngine.Scripting.Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            this.m_SimulationSystem = this.World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_EndFrameBarrier = this.World.GetOrCreateSystemManaged<EndFrameBarrier>();
            this.m_ResourceSystem = this.World.GetOrCreateSystemManaged<ResourceSystem>();
            this.m_ClimateSystem = this.World.GetOrCreateSystemManaged<ClimateSystem>();
            this.m_IconCommandSystem = this.World.GetOrCreateSystemManaged<IconCommandSystem>();
            this.m_TriggerSystem = this.World.GetOrCreateSystemManaged<TriggerSystem>();
            this.m_CitySystem = this.World.GetOrCreateSystemManaged<CitySystem>();
            this.m_ZoneBuiltRequirementSystemSystem = this.World.GetOrCreateSystemManaged<ZoneBuiltRequirementSystem>();
            this.m_ZoneSearchSystem = this.World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            this.m_ElectricityRoadConnectionGraphSystem = this.World.GetOrCreateSystemManaged<ElectricityRoadConnectionGraphSystem>();
            this.m_WaterPipeRoadConnectionGraphSystem = this.World.GetOrCreateSystemManaged<WaterPipeRoadConnectionGraphSystem>();
            this.m_TaxSystem = this.World.GetOrCreateSystemManaged<TaxSystem>();
            this.m_UpkeepExpenseQueue = new NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment>((AllocatorManager.AllocatorHandle)Allocator.Persistent);
            this.m_BuildingSettingsQuery = this.GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            this.m_EconomyParameterQuery = this.GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
            this.m_LevelupQueue = new NativeQueue<Entity>((AllocatorManager.AllocatorHandle)Allocator.Persistent);
            this.m_LeveldownQueue = new NativeQueue<Entity>((AllocatorManager.AllocatorHandle)Allocator.Persistent);

            m_LevelCapSystem = World.GetOrCreateSystemManaged<LevelCapSystem>();

            this.m_BuildingGroup = this.GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[3]
              {
          ComponentType.ReadOnly<BuildingCondition>(),
          ComponentType.ReadOnly<PrefabRef>(),
          ComponentType.ReadOnly<UpdateFrame>()
              },
                Any = new ComponentType[0],
                None = new ComponentType[4]
              {
          ComponentType.ReadOnly<Abandoned>(),
          ComponentType.ReadOnly<Destroyed>(),
          ComponentType.ReadOnly<Deleted>(),
          ComponentType.ReadOnly<Temp>()
              }
            });
            
            this.m_BuildingPrefabGroup = this.GetEntityQuery(ComponentType.ReadOnly<Game.Prefabs.BuildingData>(), ComponentType.ReadOnly<BuildingSpawnGroupData>(), ComponentType.ReadOnly<PrefabData>());
            
            this.RequireForUpdate(this.m_BuildingGroup);
            
            this.RequireForUpdate(this.m_BuildingSettingsQuery);
        }

        [UnityEngine.Scripting.Preserve]
        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            this.m_UpkeepExpenseQueue.Dispose();
            
            this.m_LevelupQueue.Dispose();
            
            this.m_LeveldownQueue.Dispose();
        }

        [UnityEngine.Scripting.Preserve]
        protected override void OnUpdate()
        {
            uint updateFrame = SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex, UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay, 16);
            
            BuildingConfigurationData singleton = this.m_BuildingSettingsQuery.GetSingleton<BuildingConfigurationData>();

            // From managed system
            m_LevelCapSystem.GetLevelData(out NativeArray<int> levelCounts, out NativeArray<int> maxCounts, Allocator.TempJob);


            UrbanInequalityBuildingUpkeepSystem.BuildingUpkeepJob jobData1 = new UrbanInequalityBuildingUpkeepSystem.BuildingUpkeepJob()
            {
                m_ConditionType = InternalCompilerInterface.GetComponentTypeHandle<BuildingCondition>(ref this.__TypeHandle.__Game_Buildings_BuildingCondition_RW_ComponentTypeHandle, ref this.CheckedStateRef),
                m_PrefabType = InternalCompilerInterface.GetComponentTypeHandle<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle, ref this.CheckedStateRef),
                m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref this.CheckedStateRef),
                m_BuildingType = InternalCompilerInterface.GetComponentTypeHandle<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentTypeHandle, ref this.CheckedStateRef),
                m_RenterType = InternalCompilerInterface.GetBufferTypeHandle<Renter>(ref this.__TypeHandle.__Game_Buildings_Renter_RO_BufferTypeHandle, ref this.CheckedStateRef),
                m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref this.CheckedStateRef),
                m_ConsumptionDatas = InternalCompilerInterface.GetComponentLookup<ConsumptionData>(ref this.__TypeHandle.__Game_Prefabs_ConsumptionData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Availabilities = InternalCompilerInterface.GetBufferLookup<ResourceAvailability>(ref this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup, ref this.CheckedStateRef),
                m_BuildingDatas = InternalCompilerInterface.GetComponentLookup<Game.Prefabs.BuildingData>(ref this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_BuildingPropertyDatas = InternalCompilerInterface.GetComponentLookup<BuildingPropertyData>(ref this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_CityModifierBufs = InternalCompilerInterface.GetBufferLookup<CityModifier>(ref this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup, ref this.CheckedStateRef),
                m_SignatureDatas = InternalCompilerInterface.GetComponentLookup<SignatureBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SignatureBuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Abandoned = InternalCompilerInterface.GetComponentLookup<Abandoned>(ref this.__TypeHandle.__Game_Buildings_Abandoned_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Destroyed = InternalCompilerInterface.GetComponentLookup<Destroyed>(ref this.__TypeHandle.__Game_Common_Destroyed_RO_ComponentLookup, ref this.CheckedStateRef),
                m_SpawnableBuildingDatas = InternalCompilerInterface.GetComponentLookup<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_ZoneDatas = InternalCompilerInterface.GetComponentLookup<ZoneData>(ref this.__TypeHandle.__Game_Prefabs_ZoneData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref this.CheckedStateRef),
                m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref this.CheckedStateRef),
                m_LayoutElements = InternalCompilerInterface.GetBufferLookup<LayoutElement>(ref this.__TypeHandle.__Game_Vehicles_LayoutElement_RO_BufferLookup, ref this.CheckedStateRef),
                m_DeliveryTrucks = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.DeliveryTruck>(ref this.__TypeHandle.__Game_Vehicles_DeliveryTruck_RO_ComponentLookup, ref this.CheckedStateRef),
                m_City = this.m_CitySystem.City,
                m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs(),
                m_ResourceDatas = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Resources = InternalCompilerInterface.GetBufferLookup<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup, ref this.CheckedStateRef),
                m_Citizens = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Residents = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_HouseholdCitizen_RO_BufferLookup, ref this.CheckedStateRef),
                m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref this.CheckedStateRef),
                m_HealthProblems = InternalCompilerInterface.GetComponentLookup<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentLookup, ref this.CheckedStateRef),
                m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
                m_TaxRates = this.m_TaxSystem.GetTaxRates(),
                m_BuildingConfigurationData = singleton,
                m_UpdateFrameIndex = updateFrame,
                m_SimulationFrame = this.m_SimulationSystem.frameIndex,
                m_UpkeepExpenseQueue = this.m_UpkeepExpenseQueue.AsParallelWriter(),
                m_LevelupQueue = this.m_LevelupQueue.AsParallelWriter(),
                m_LevelDownQueue = this.m_LeveldownQueue.AsParallelWriter(),
                m_DebugFastLeveling = this.debugFastLeveling,
                m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                m_TemperatureUpkeep = UrbanInequalityBuildingUpkeepSystem.GetHeatingMultiplier((float)this.m_ClimateSystem.temperature),
                LevelCounts = levelCounts,
                MaxLevelCounts = maxCounts,
                maxEduPenalty = Mod.m_Setting.maxEducationPenalty,
                minEduPenalty = Mod.m_Setting.minEducationPenalty,
                maxIncPenalty = Mod.m_Setting.maxIncomePenalty,
                minIncPenalty = Mod.m_Setting.minIncomePenalty
            };
            
            this.Dependency = jobData1.ScheduleParallel<UrbanInequalityBuildingUpkeepSystem.BuildingUpkeepJob>(this.m_BuildingGroup, this.Dependency);
            
            this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);

            this.m_ResourceSystem.AddPrefabsReader(this.Dependency);

            levelCounts.Dispose();
            maxCounts.Dispose();

            JobHandle outJobHandle;
            JobHandle dependencies;
            JobHandle deps1;

            UrbanInequalityBuildingUpkeepSystem.LevelupJob jobData2 = new UrbanInequalityBuildingUpkeepSystem.LevelupJob()
            {
                m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref this.CheckedStateRef),
                m_SpawnableBuildingType = InternalCompilerInterface.GetComponentTypeHandle<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
                m_BuildingType = InternalCompilerInterface.GetComponentTypeHandle<Game.Prefabs.BuildingData>(ref this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
                m_BuildingPropertyType = InternalCompilerInterface.GetComponentTypeHandle<BuildingPropertyData>(ref this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
                m_ObjectGeometryType = InternalCompilerInterface.GetComponentTypeHandle<ObjectGeometryData>(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
                m_BuildingSpawnGroupType = InternalCompilerInterface.GetSharedComponentTypeHandle<BuildingSpawnGroupData>(ref this.__TypeHandle.__Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle, ref this.CheckedStateRef),
                m_TransformData = InternalCompilerInterface.GetComponentLookup<Game.Objects.Transform>(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref this.CheckedStateRef),
                m_BlockData = InternalCompilerInterface.GetComponentLookup<Game.Zones.Block>(ref this.__TypeHandle.__Game_Zones_Block_RO_ComponentLookup, ref this.CheckedStateRef),
                m_ValidAreaData = InternalCompilerInterface.GetComponentLookup<ValidArea>(ref this.__TypeHandle.__Game_Zones_ValidArea_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref this.CheckedStateRef),
                m_PrefabDatas = InternalCompilerInterface.GetComponentLookup<PrefabData>(ref this.__TypeHandle.__Game_Prefabs_PrefabData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_SpawnableBuildings = InternalCompilerInterface.GetComponentLookup<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Buildings = InternalCompilerInterface.GetComponentLookup<Game.Prefabs.BuildingData>(ref this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_BuildingPropertyDatas = InternalCompilerInterface.GetComponentLookup<BuildingPropertyData>(ref this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_OfficeBuilding = InternalCompilerInterface.GetComponentLookup<OfficeBuilding>(ref this.__TypeHandle.__Game_Prefabs_OfficeBuilding_RO_ComponentLookup, ref this.CheckedStateRef),
                m_ZoneData = InternalCompilerInterface.GetComponentLookup<ZoneData>(ref this.__TypeHandle.__Game_Prefabs_ZoneData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Cells = InternalCompilerInterface.GetBufferLookup<Cell>(ref this.__TypeHandle.__Game_Zones_Cell_RO_BufferLookup, ref this.CheckedStateRef),
                m_BuildingConfigurationData = singleton,
                m_SpawnableBuildingChunks = this.m_BuildingPrefabGroup.ToArchetypeChunkListAsync((AllocatorManager.AllocatorHandle)this.World.UpdateAllocator.ToAllocator, out outJobHandle),
                m_ZoneSearchTree = this.m_ZoneSearchSystem.GetSearchTree(true, out dependencies),
                m_RandomSeed = RandomSeed.Next(),
                m_IconCommandBuffer = this.m_IconCommandSystem.CreateCommandBuffer(),
                m_LevelupQueue = this.m_LevelupQueue,
                m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer(),
                m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer(),
                m_ZoneBuiltLevelQueue = this.m_ZoneBuiltRequirementSystemSystem.GetZoneBuiltLevelQueue(out deps1)
            };
            this.Dependency = jobData2.Schedule<UrbanInequalityBuildingUpkeepSystem.LevelupJob>(JobUtils.CombineDependencies(this.Dependency, outJobHandle, dependencies, deps1));
            
            this.m_ZoneSearchSystem.AddSearchTreeReader(this.Dependency);
            
            this.m_ZoneBuiltRequirementSystemSystem.AddWriter(this.Dependency);
            
            this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
            
            this.m_TriggerSystem.AddActionBufferWriter(this.Dependency);
            
            JobHandle deps2;
            JobHandle deps3;
            
            UrbanInequalityBuildingUpkeepSystem.LeveldownJob jobData3 = new UrbanInequalityBuildingUpkeepSystem.LeveldownJob()
            {
                m_BuildingDatas = InternalCompilerInterface.GetComponentLookup<Game.Prefabs.BuildingData>(ref this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref this.CheckedStateRef),
                m_SpawnableBuildings = InternalCompilerInterface.GetComponentLookup<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_Buildings = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RW_ComponentLookup, ref this.CheckedStateRef),
                m_ElectricityConsumers = InternalCompilerInterface.GetComponentLookup<ElectricityConsumer>(ref this.__TypeHandle.__Game_Buildings_ElectricityConsumer_RO_ComponentLookup, ref this.CheckedStateRef),
                m_GarbageProducers = InternalCompilerInterface.GetComponentLookup<GarbageProducer>(ref this.__TypeHandle.__Game_Buildings_GarbageProducer_RO_ComponentLookup, ref this.CheckedStateRef),
                m_MailProducers = InternalCompilerInterface.GetComponentLookup<MailProducer>(ref this.__TypeHandle.__Game_Buildings_MailProducer_RO_ComponentLookup, ref this.CheckedStateRef),
                m_WaterConsumers = InternalCompilerInterface.GetComponentLookup<WaterConsumer>(ref this.__TypeHandle.__Game_Buildings_WaterConsumer_RO_ComponentLookup, ref this.CheckedStateRef),
                m_BuildingPropertyDatas = InternalCompilerInterface.GetComponentLookup<BuildingPropertyData>(ref this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup, ref this.CheckedStateRef),
                m_OfficeBuilding = InternalCompilerInterface.GetComponentLookup<OfficeBuilding>(ref this.__TypeHandle.__Game_Prefabs_OfficeBuilding_RO_ComponentLookup, ref this.CheckedStateRef),
                m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer(),
                m_CrimeProducers = InternalCompilerInterface.GetComponentLookup<CrimeProducer>(ref this.__TypeHandle.__Game_Buildings_CrimeProducer_RW_ComponentLookup, ref this.CheckedStateRef),
                m_Renters = InternalCompilerInterface.GetBufferLookup<Renter>(ref this.__TypeHandle.__Game_Buildings_Renter_RW_BufferLookup, ref this.CheckedStateRef),
                m_BuildingConfigurationData = singleton,
                m_LeveldownQueue = this.m_LeveldownQueue,
                m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer(),
                m_UpdatedElectricityRoadEdges = this.m_ElectricityRoadConnectionGraphSystem.GetEdgeUpdateQueue(out deps2),
                m_UpdatedWaterPipeRoadEdges = this.m_WaterPipeRoadConnectionGraphSystem.GetEdgeUpdateQueue(out deps3),
                m_IconCommandBuffer = this.m_IconCommandSystem.CreateCommandBuffer(),
                m_SimulationFrame = this.m_SimulationSystem.frameIndex
            };
            this.Dependency = jobData3.Schedule<UrbanInequalityBuildingUpkeepSystem.LeveldownJob>(JobHandle.CombineDependencies(this.Dependency, deps2, deps3));
            
            this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
            
            this.m_ElectricityRoadConnectionGraphSystem.AddQueueWriter(this.Dependency);
            
            this.m_IconCommandSystem.AddCommandBufferWriter(this.Dependency);
            
            this.m_TriggerSystem.AddActionBufferWriter(this.Dependency);

            
            UrbanInequalityBuildingUpkeepSystem.UpkeepPaymentJob jobData4 = new UrbanInequalityBuildingUpkeepSystem.UpkeepPaymentJob()
            {
                m_Resources = InternalCompilerInterface.GetBufferLookup<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferLookup, ref this.CheckedStateRef),
                m_UpkeepExpenseQueue = this.m_UpkeepExpenseQueue
            };
            this.Dependency = jobData4.Schedule<UrbanInequalityBuildingUpkeepSystem.UpkeepPaymentJob>(this.Dependency);
        }

        public void DebugLevelUp(
          Entity building,
          ComponentLookup<BuildingCondition> conditions,
          ComponentLookup<SpawnableBuildingData> spawnables,
          ComponentLookup<PrefabRef> prefabRefs,
          ComponentLookup<ZoneData> zoneDatas,
          ComponentLookup<BuildingPropertyData> propertyDatas)
        {
            if (!conditions.HasComponent(building) || !prefabRefs.HasComponent(building))
                return;
            BuildingCondition condition = conditions[building];
            Entity prefab = prefabRefs[building].m_Prefab;
            if (!spawnables.HasComponent(prefab) || !propertyDatas.HasComponent(prefab))
                return;
            SpawnableBuildingData spawnable = spawnables[prefab];
            if (!zoneDatas.HasComponent(spawnable.m_ZonePrefab))
                return;
            ZoneData zoneData = zoneDatas[spawnable.m_ZonePrefab];
            
            this.m_LevelupQueue.Enqueue(building);
        }

        public void DebugLevelDown(
          Entity building,
          ComponentLookup<BuildingCondition> conditions,
          ComponentLookup<SpawnableBuildingData> spawnables,
          ComponentLookup<PrefabRef> prefabRefs,
          ComponentLookup<ZoneData> zoneDatas,
          ComponentLookup<BuildingPropertyData> propertyDatas)
        {
            if (!conditions.HasComponent(building) || !prefabRefs.HasComponent(building))
                return;
            BuildingCondition condition = conditions[building];
            Entity prefab = prefabRefs[building].m_Prefab;
            if (!spawnables.HasComponent(prefab) || !propertyDatas.HasComponent(prefab))
                return;
            SpawnableBuildingData spawnable = spawnables[prefab];
            if (!zoneDatas.HasComponent(spawnable.m_ZonePrefab))
                return;
            
            int levelingCost = BuildingUtils.GetLevelingCost(zoneDatas[spawnable.m_ZonePrefab].m_AreaType, propertyDatas[prefab], (int)spawnable.m_Level, this.EntityManager.GetBuffer<CityModifier>(this.m_CitySystem.City, true));
            condition.m_Condition = -3 * levelingCost / 2;
            conditions[building] = condition;
            
            this.m_LeveldownQueue.Enqueue(building);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
            new EntityQueryBuilder((AllocatorManager.AllocatorHandle)Allocator.Temp).Dispose();
        }

        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            this.__AssignQueries(ref this.CheckedStateRef);
            
            this.__TypeHandle.__AssignHandles(ref this.CheckedStateRef);
        }

        [UnityEngine.Scripting.Preserve]
        public UrbanInequalityBuildingUpkeepSystem()
        {
        }

        private struct UpkeepPayment
        {
            public Entity m_RenterEntity;
            public int m_Price;
        }

        [BurstCompile]
        private struct BuildingUpkeepJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;
            public ComponentTypeHandle<BuildingCondition> m_ConditionType;
            [ReadOnly]
            public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;
            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabType;
            [ReadOnly]
            public ComponentTypeHandle<Building> m_BuildingType;
            [ReadOnly]
            public BufferTypeHandle<Renter> m_RenterType;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingDatas;
            [ReadOnly]
            public ComponentLookup<ZoneData> m_ZoneDatas;
            [ReadOnly]
            public BufferLookup<Game.Economy.Resources> m_Resources;
            [ReadOnly]
            public ResourcePrefabs m_ResourcePrefabs;
            [ReadOnly]
            public ComponentLookup<ResourceData> m_ResourceDatas;
            [ReadOnly]
            public ComponentLookup<Game.Prefabs.BuildingData> m_BuildingDatas;
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;
            [ReadOnly]
            public BufferLookup<CityModifier> m_CityModifierBufs;
            [ReadOnly]
            public ComponentLookup<Abandoned> m_Abandoned;
            [ReadOnly]
            public ComponentLookup<Destroyed> m_Destroyed;
            [ReadOnly]
            public ComponentLookup<SignatureBuildingData> m_SignatureDatas;
            [ReadOnly]
            public ComponentLookup<Household> m_Households;
            [ReadOnly]
            public BufferLookup<OwnedVehicle> m_OwnedVehicles;
            [ReadOnly]
            public BufferLookup<LayoutElement> m_LayoutElements;
            [ReadOnly]
            public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;
            [ReadOnly]
            public BuildingConfigurationData m_BuildingConfigurationData;
            [ReadOnly]
            public ComponentLookup<ConsumptionData> m_ConsumptionDatas;
            [ReadOnly]
            public BufferLookup<ResourceAvailability> m_Availabilities;
            [ReadOnly]
            public BufferLookup<HouseholdCitizen> m_Residents;
            [ReadOnly]
            public ComponentLookup<Citizen> m_Citizens;
            [ReadOnly]
            public ComponentLookup<Worker> m_Workers;
            [ReadOnly]
            public ComponentLookup<HealthProblem> m_HealthProblems;
            [ReadOnly]
            public EconomyParameterData m_EconomyParameters;
            [ReadOnly]
            public NativeArray<int> m_TaxRates;            
            [ReadOnly]
            public Entity m_City;
            public uint m_UpdateFrameIndex;
            public uint m_SimulationFrame;
            public float m_TemperatureUpkeep;
            public bool m_DebugFastLeveling;
            public NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment>.ParallelWriter m_UpkeepExpenseQueue;
            public NativeQueue<Entity>.ParallelWriter m_LevelupQueue;
            public NativeQueue<Entity>.ParallelWriter m_LevelDownQueue;
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
            [ReadOnly] public NativeArray<int> LevelCounts;
            [ReadOnly] public NativeArray<int> MaxLevelCounts;
            [ReadOnly]
            public float maxEduPenalty;
            [ReadOnly]
            public float minEduPenalty;
            [ReadOnly] 
            public float maxIncPenalty;
            [ReadOnly]
            public float minIncPenalty;

            public void Execute(
              in ArchetypeChunk chunk,
              int unfilteredChunkIndex,
              bool useEnabledMask,
              in v128 chunkEnabledMask)
            {
                if ((int)chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != (int)this.m_UpdateFrameIndex)
                    return;

                NativeArray<Entity> nativeArray1 = chunk.GetNativeArray(this.m_EntityType);
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray<PrefabRef>(ref this.m_PrefabType);
                NativeArray<BuildingCondition> nativeArray3 = chunk.GetNativeArray<BuildingCondition>(ref this.m_ConditionType);
                chunk.GetNativeArray<Building>(ref this.m_BuildingType);
                BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor<Renter>(ref this.m_RenterType);

                for (int index1 = 0; index1 < chunk.Count; ++index1)
                {
                    int num1 = 0;
                    Entity entity = nativeArray1[index1];
                    Entity prefab = nativeArray2[index1].m_Prefab;

                    ConsumptionData consumptionData = this.m_ConsumptionDatas[prefab];
                    Game.Prefabs.BuildingData buildingData = this.m_BuildingDatas[prefab];
                    BuildingPropertyData buildingPropertyData1 = this.m_BuildingPropertyDatas[prefab];
                    DynamicBuffer<CityModifier> cityModifierBuf = this.m_CityModifierBufs[this.m_City];
                    SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingDatas[prefab];
                    AreaType areaType = this.m_ZoneDatas[spawnableBuildingData.m_ZonePrefab].m_AreaType;
                    DynamicBuffer<Renter> dynamicBuffer = bufferAccessor[index1];
                    int num3 = consumptionData.m_Upkeep / BuildingUpkeepSystem.kUpdatesPerDay;

                    BuildingPropertyData buildingPropertyData2 = this.m_BuildingPropertyDatas[prefab];
                    int levelingCost = BuildingUtils.GetLevelingCost(areaType, buildingPropertyData2, (int)spawnableBuildingData.m_Level, cityModifierBuf);

                    Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)(1UL + (ulong)entity.Index * (ulong)this.m_SimulationFrame));

                    if (areaType == AreaType.Residential)
                    {
                        int totalEdu = 0, eduCount = 0;
                        int totalIncome = 0, incomeCount = 0;

                        for (int index2 = 0; index2 < dynamicBuffer.Length; ++index2)
                        {
                            DynamicBuffer<HouseholdCitizen> residents;
                            if (m_Residents.TryGetBuffer(dynamicBuffer[index2].m_Renter, out residents))
                            {
                                foreach (var citizenRef in residents)
                                {
                                    var citizen = m_Citizens[citizenRef.m_Citizen];
                                    var age = citizen.GetAge();
                                    if (age == CitizenAge.Adult || age == CitizenAge.Elderly)
                                    {
                                        totalEdu += citizen.GetEducationLevel(); // 0–4
                                        eduCount++;
                                    }
                                }

                                int income = EconomyUtils.GetHouseholdIncome(residents, ref m_Workers, ref m_Citizens, ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);
                                totalIncome += income;
                                incomeCount++;
                            }
                        }

                        float avgEdu = eduCount > 0 ? (float)totalEdu / eduCount : 0f;
                        float avgIncome = incomeCount > 0 ? (float)totalIncome / incomeCount : 0f;

                        //Calculate score to be used to decide if the building can level up
                        float eduScore = avgEdu / 4.0f;
                        float incomeScore = avgIncome / 4000f;
                        float score = Math.Max(0.05f,0.6f * eduScore + 0.4f * incomeScore);
                        float weightedScore = math.pow(score, 3);

                        int incomeBracket = 4;
                        if (avgIncome < 500) incomeBracket = 0;
                        else if (avgIncome < 1000) incomeBracket = 1;
                        else if (avgIncome < 1500) incomeBracket = 2;
                        else if (avgIncome < 2500) incomeBracket = 3;

                        float eduPenalty = maxEduPenalty - (maxEduPenalty - minEduPenalty) * avgEdu /4;
                        float incomePenalty = maxIncPenalty - (maxIncPenalty - minIncPenalty) * incomeBracket /4;
                        float finalPenalty = eduPenalty * incomePenalty;

                        int baseCost = levelingCost;
                        levelingCost = Mathf.RoundToInt(baseCost * finalPenalty);

                        //Mod.log.Info($"Residential Leveling Cost: Base={baseCost}, Adjusted={levelingCost}, EduPenalty={eduPenalty}, IncomePenalty={incomePenalty}");

                        int currentLevel = spawnableBuildingData.m_Level;
                        int targetLevel = math.clamp(currentLevel + 1, 1, 5);
                        //Mod.log.Info($"Target Level: {targetLevel} LevelCounts={LevelCounts[targetLevel]}, MaxLevelCounts={MaxLevelCounts[targetLevel]}");

                        if(LevelCounts[targetLevel] > 0 && MaxLevelCounts[targetLevel] > 0)
                        {
                            // Check if the target level is already at max capacity
                            if (LevelCounts[targetLevel] >= MaxLevelCounts[targetLevel])
                            {
                                //Mod.log.Info($"Target Level {targetLevel} already at max capacity. Cannot level up.");
                                continue;
                            }
                        }
                        bool levelUp = (LevelCounts[targetLevel] >= MaxLevelCounts[targetLevel]);
                        
                        if (levelUp)
                        {
                            continue;
                        } else
                        {
                            if (random.NextFloat() < (1 - weightedScore))
                            {
                                //Mod.log.Info($"Residential Building Failed to level up. avgEdu: {avgEdu}, avgIncome: {avgIncome}, incomeBracket:{incomeBracket}, Score: {score}, Weighted Score: {weightedScore}, EduPenalty: {eduPenalty}, IncomePenalty: {incomePenalty}");
                                continue;
                            } else
                            {
                                //Mod.log.Info($"Residential Building Level up. avgEdu: {avgEdu}, avgIncome: {avgIncome}, incomeBracket:{incomeBracket}, Score: {score}, Weighted Score: {weightedScore}, EduPenalty: {eduPenalty}, IncomePenalty: {incomePenalty}");
                            }
                        }
                    }
                    int num2 = spawnableBuildingData.m_Level == (byte)5 ? BuildingUtils.GetLevelingCost(areaType, buildingPropertyData2, 4, cityModifierBuf) : levelingCost;
                    if (areaType == AreaType.Residential && buildingPropertyData2.m_ResidentialProperties > 1)
                        num2 = Mathf.RoundToInt((float)(num2 * (6 - (int)spawnableBuildingData.m_Level)) / math.sqrt((float)buildingPropertyData2.m_ResidentialProperties));

                    int num4 = num3 / BuildingUpkeepSystem.kMaterialUpkeep;
                    int num5 = num1 + (num3 - num4);
                    int num6 = 0;

                    for (int index2 = 0; index2 < dynamicBuffer.Length; ++index2)
                    {
                        DynamicBuffer<Game.Economy.Resources> bufferData;
                        
                        if (this.m_Resources.TryGetBuffer(dynamicBuffer[index2].m_Renter, out bufferData))
                        {
                            
                            if (this.m_Households.HasComponent(dynamicBuffer[index2].m_Renter))
                            {
                                num6 += EconomyUtils.GetResources(Resource.Money, bufferData);
                            }
                            else
                            {

                                if (this.m_OwnedVehicles.HasBuffer(dynamicBuffer[index2].m_Renter))
                                {
                                    num6 += EconomyUtils.GetCompanyTotalWorth(bufferData, this.m_OwnedVehicles[dynamicBuffer[index2].m_Renter], ref this.m_LayoutElements, ref this.m_DeliveryTrucks, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
                                }
                                else
                                {
                                    num6 += EconomyUtils.GetCompanyTotalWorth(bufferData, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
                                }
                            }
                        }
                    }
                    BuildingCondition buildingCondition = nativeArray3[index1];
                    int num7 = 0;
                    if (num5 > num6)
                    {
                        
                        num7 = -this.m_BuildingConfigurationData.m_BuildingConditionDecrement * (int)math.pow(2f, (float)spawnableBuildingData.m_Level) * math.max(1, dynamicBuffer.Length);
                    }
                    else if (dynamicBuffer.Length > 0)
                    {
                        
                        num7 = this.m_BuildingConfigurationData.m_BuildingConditionIncrement * (int)math.pow(2f, (float)spawnableBuildingData.m_Level) * math.max(1, dynamicBuffer.Length);
                        int num8 = num5 / dynamicBuffer.Length;
                        for (int index3 = 0; index3 < dynamicBuffer.Length; ++index3)
                        {
                            this.m_UpkeepExpenseQueue.Enqueue(new UrbanInequalityBuildingUpkeepSystem.UpkeepPayment()
                            {
                                m_RenterEntity = dynamicBuffer[index3].m_Renter,
                                m_Price = -num8
                            });
                        }
                    }
                    
                    if (this.m_DebugFastLeveling)
                        buildingCondition.m_Condition = levelingCost;
                    else
                        buildingCondition.m_Condition += num7;
                    if (buildingCondition.m_Condition >= levelingCost)
                    {
                        
                        this.m_LevelupQueue.Enqueue(nativeArray1[index1]);
                        buildingCondition.m_Condition -= levelingCost;
                    }
                    
                    if ((this.m_Abandoned.HasComponent(nativeArray1[index1]) ? 0 : (!this.m_Destroyed.HasComponent(nativeArray1[index1]) ? 1 : 0)) != 0 && nativeArray3[index1].m_Condition <= -num2 && !this.m_SignatureDatas.HasComponent(prefab))
                    {
                        
                        this.m_LevelDownQueue.Enqueue(nativeArray1[index1]);
                        buildingCondition.m_Condition += levelingCost;
                    }
                    nativeArray3[index1] = buildingCondition;
                }
            }

            void IJobChunk.Execute(
              in ArchetypeChunk chunk,
              int unfilteredChunkIndex,
              bool useEnabledMask,
              in v128 chunkEnabledMask)
            {
                this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        [BurstCompile]
        private struct UpkeepPaymentJob : IJob
        {
            public BufferLookup<Game.Economy.Resources> m_Resources;
            public NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment> m_UpkeepExpenseQueue;

            public void Execute()
            {
                UrbanInequalityBuildingUpkeepSystem.UpkeepPayment upkeepPayment;
                
                while (this.m_UpkeepExpenseQueue.TryDequeue(out upkeepPayment))
                {
                    if (this.m_Resources.HasBuffer(upkeepPayment.m_RenterEntity))
                    {
                        EconomyUtils.AddResources(Resource.Money, upkeepPayment.m_Price, this.m_Resources[upkeepPayment.m_RenterEntity]);
                    }
                }
            }
        }

        [BurstCompile]
        private struct LeveldownJob : IJob
        {
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_Prefabs;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;
            [ReadOnly]
            public ComponentLookup<Game.Prefabs.BuildingData> m_BuildingDatas;
            public ComponentLookup<Building> m_Buildings;
            [ReadOnly]
            public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;
            [ReadOnly]
            public ComponentLookup<WaterConsumer> m_WaterConsumers;
            [ReadOnly]
            public ComponentLookup<GarbageProducer> m_GarbageProducers;
            [ReadOnly]
            public ComponentLookup<MailProducer> m_MailProducers;
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;
            [ReadOnly]
            public ComponentLookup<OfficeBuilding> m_OfficeBuilding;
            public NativeQueue<TriggerAction> m_TriggerBuffer;
            public ComponentLookup<CrimeProducer> m_CrimeProducers;
            public BufferLookup<Renter> m_Renters;
            [ReadOnly]
            public BuildingConfigurationData m_BuildingConfigurationData;
            public NativeQueue<Entity> m_LeveldownQueue;
            public EntityCommandBuffer m_CommandBuffer;
            public NativeQueue<Entity> m_UpdatedElectricityRoadEdges;
            public NativeQueue<Entity> m_UpdatedWaterPipeRoadEdges;
            public IconCommandBuffer m_IconCommandBuffer;
            public uint m_SimulationFrame;

            public void Execute()
            {
                Entity entity;
                
                while (this.m_LeveldownQueue.TryDequeue(out entity))
                {
                    
                    if (this.m_Prefabs.HasComponent(entity))
                    {
                        
                        Entity prefab = this.m_Prefabs[entity].m_Prefab;
                        
                        if (this.m_SpawnableBuildings.HasComponent(prefab))
                        {
                            
                            SpawnableBuildingData spawnableBuilding = this.m_SpawnableBuildings[prefab];
                            
                            Game.Prefabs.BuildingData buildingData = this.m_BuildingDatas[prefab];
                            
                            BuildingPropertyData buildingPropertyData = this.m_BuildingPropertyDatas[prefab];
                            
                            
                            this.m_CommandBuffer.AddComponent<Abandoned>(entity, new Abandoned()
                            {
                                m_AbandonmentTime = this.m_SimulationFrame
                            });
                            
                            this.m_CommandBuffer.AddComponent<Updated>(entity, new Updated());
                            
                            if (this.m_ElectricityConsumers.HasComponent(entity))
                            {
                                
                                this.m_CommandBuffer.RemoveComponent<ElectricityConsumer>(entity);
                                
                                Entity roadEdge = this.m_Buildings[entity].m_RoadEdge;
                                if (roadEdge != Entity.Null)
                                {
                                    
                                    this.m_UpdatedElectricityRoadEdges.Enqueue(roadEdge);
                                }
                            }
                            
                            if (this.m_WaterConsumers.HasComponent(entity))
                            {
                                
                                this.m_CommandBuffer.RemoveComponent<WaterConsumer>(entity);
                                
                                Entity roadEdge = this.m_Buildings[entity].m_RoadEdge;
                                if (roadEdge != Entity.Null)
                                {
                                    
                                    this.m_UpdatedWaterPipeRoadEdges.Enqueue(roadEdge);
                                }
                            }
                            
                            if (this.m_GarbageProducers.HasComponent(entity))
                            {
                                
                                this.m_CommandBuffer.RemoveComponent<GarbageProducer>(entity);
                            }
                            
                            if (this.m_MailProducers.HasComponent(entity))
                            {
                                
                                this.m_CommandBuffer.RemoveComponent<MailProducer>(entity);
                            }
                            
                            if (this.m_CrimeProducers.HasComponent(entity))
                            {
                                
                                CrimeProducer crimeProducer = this.m_CrimeProducers[entity];
                                
                                this.m_CommandBuffer.SetComponent<CrimeProducer>(entity, new CrimeProducer()
                                {
                                    m_Crime = crimeProducer.m_Crime * 2f,
                                    m_PatrolRequest = crimeProducer.m_PatrolRequest
                                });
                            }
                            
                            if (this.m_Renters.HasBuffer(entity))
                            {
                                
                                DynamicBuffer<Renter> renter = this.m_Renters[entity];
                                for (int index = renter.Length - 1; index >= 0; --index)
                                {
                                    
                                    this.m_CommandBuffer.RemoveComponent<PropertyRenter>(renter[index].m_Renter);
                                    renter.RemoveAt(index);
                                }
                            }
                            
                            if ((this.m_Buildings[entity].m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) != Game.Buildings.BuildingFlags.None)
                            {
                                
                                Building building = this.m_Buildings[entity];
                                
                                
                                this.m_IconCommandBuffer.Remove(entity, this.m_BuildingConfigurationData.m_HighRentNotification);
                                building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                                
                                this.m_Buildings[entity] = building;
                            }
                            
                            this.m_IconCommandBuffer.Remove(entity, IconPriority.Problem);
                            
                            this.m_IconCommandBuffer.Remove(entity, IconPriority.FatalProblem);
                            
                            
                            this.m_IconCommandBuffer.Add(entity, this.m_BuildingConfigurationData.m_AbandonedNotification, IconPriority.FatalProblem);
                            if (buildingPropertyData.CountProperties(AreaType.Commercial) > 0)
                            {
                                
                                this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownCommercialBuilding, Entity.Null, entity, entity));
                            }
                            if (buildingPropertyData.CountProperties(AreaType.Industrial) > 0)
                            {
                                
                                if (this.m_OfficeBuilding.HasComponent(prefab))
                                {
                                    
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownOfficeBuilding, Entity.Null, entity, entity));
                                }
                                else
                                {
                                    
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownIndustrialBuilding, Entity.Null, entity, entity));
                                }
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct LevelupJob : IJob
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;
            [ReadOnly]
            public ComponentTypeHandle<SpawnableBuildingData> m_SpawnableBuildingType;
            [ReadOnly]
            public ComponentTypeHandle<Game.Prefabs.BuildingData> m_BuildingType;
            [ReadOnly]
            public ComponentTypeHandle<BuildingPropertyData> m_BuildingPropertyType;
            [ReadOnly]
            public ComponentTypeHandle<ObjectGeometryData> m_ObjectGeometryType;
            [ReadOnly]
            public SharedComponentTypeHandle<BuildingSpawnGroupData> m_BuildingSpawnGroupType;
            [ReadOnly]
            public ComponentLookup<Game.Objects.Transform> m_TransformData;
            [ReadOnly]
            public ComponentLookup<Game.Zones.Block> m_BlockData;
            [ReadOnly]
            public ComponentLookup<ValidArea> m_ValidAreaData;
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_Prefabs;
            [ReadOnly]
            public ComponentLookup<PrefabData> m_PrefabDatas;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;
            [ReadOnly]
            public ComponentLookup<Game.Prefabs.BuildingData> m_Buildings;
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;
            [ReadOnly]
            public ComponentLookup<OfficeBuilding> m_OfficeBuilding;
            [ReadOnly]
            public ComponentLookup<ZoneData> m_ZoneData;
            [ReadOnly]
            public BufferLookup<Cell> m_Cells;
            [ReadOnly]
            public BuildingConfigurationData m_BuildingConfigurationData;
            [ReadOnly]
            public NativeList<ArchetypeChunk> m_SpawnableBuildingChunks;
            [ReadOnly]
            public NativeQuadTree<Entity, Bounds2> m_ZoneSearchTree;
            [ReadOnly]
            public RandomSeed m_RandomSeed;
            public IconCommandBuffer m_IconCommandBuffer;
            public NativeQueue<Entity> m_LevelupQueue;
            public EntityCommandBuffer m_CommandBuffer;
            public NativeQueue<TriggerAction> m_TriggerBuffer;
            public NativeQueue<ZoneBuiltLevelUpdate> m_ZoneBuiltLevelQueue;

            public void Execute()
            {
                
                Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(0);
                Entity entity1;
                
                while (this.m_LevelupQueue.TryDequeue(out entity1))
                {
                    
                    Entity prefab = this.m_Prefabs[entity1].m_Prefab;
                    
                    if (this.m_SpawnableBuildings.HasComponent(prefab))
                    {
                        
                        SpawnableBuildingData spawnableBuilding = this.m_SpawnableBuildings[prefab];
                        
                        if (this.m_PrefabDatas.IsComponentEnabled(spawnableBuilding.m_ZonePrefab))
                        {
                            
                            Game.Prefabs.BuildingData building = this.m_Buildings[prefab];
                            
                            BuildingPropertyData buildingPropertyData = this.m_BuildingPropertyDatas[prefab];
                            
                            ZoneData zoneData = this.m_ZoneData[spawnableBuilding.m_ZonePrefab];
                            float maxHeight = this.GetMaxHeight(entity1, building);
                            Entity entity2 = this.SelectSpawnableBuilding(zoneData.m_ZoneType, (int)spawnableBuilding.m_Level + 1, building.m_LotSize, maxHeight, building.m_Flags & (Game.Prefabs.BuildingFlags.LeftAccess | Game.Prefabs.BuildingFlags.RightAccess), buildingPropertyData, ref random);
                            if (entity2 != Entity.Null)
                            {
                                
                                this.m_CommandBuffer.AddComponent<UnderConstruction>(entity1, new UnderConstruction()
                                {
                                    m_NewPrefab = entity2,
                                    m_Progress = byte.MaxValue
                                });
                                if (buildingPropertyData.CountProperties(AreaType.Residential) > 0)
                                {
                                    
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpResidentialBuilding, Entity.Null, entity1, entity1));
                                }
                                if (buildingPropertyData.CountProperties(AreaType.Commercial) > 0)
                                {
                                    
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpCommercialBuilding, Entity.Null, entity1, entity1));
                                }
                                if (buildingPropertyData.CountProperties(AreaType.Industrial) > 0)
                                {
                                    
                                    if (this.m_OfficeBuilding.HasComponent(prefab))
                                    {
                                        
                                        this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpOfficeBuilding, Entity.Null, entity1, entity1));
                                    }
                                    else
                                    {
                                        
                                        this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpIndustrialBuilding, Entity.Null, entity1, entity1));
                                    }
                                }
                                
                                this.m_ZoneBuiltLevelQueue.Enqueue(new ZoneBuiltLevelUpdate()
                                {
                                    m_Zone = spawnableBuilding.m_ZonePrefab,
                                    m_FromLevel = (int)spawnableBuilding.m_Level,
                                    m_ToLevel = (int)spawnableBuilding.m_Level + 1,
                                    m_Squares = building.m_LotSize.x * building.m_LotSize.y
                                });
                                
                                
                                this.m_IconCommandBuffer.Add(entity1, this.m_BuildingConfigurationData.m_LevelUpNotification, clusterLayer: IconClusterLayer.Transaction);
                            }
                        }
                    }
                }
            }

            private Entity SelectSpawnableBuilding(
              ZoneType zoneType,
              int level,
              int2 lotSize,
              float maxHeight,
              Game.Prefabs.BuildingFlags accessFlags,
              BuildingPropertyData buildingPropertyData,
              ref Unity.Mathematics.Random random)
            {
                int max = 0;
                Entity entity = Entity.Null;
                
                for (int index1 = 0; index1 < this.m_SpawnableBuildingChunks.Length; ++index1)
                {
                    
                    ArchetypeChunk spawnableBuildingChunk = this.m_SpawnableBuildingChunks[index1];
                    
                    if (spawnableBuildingChunk.GetSharedComponent<BuildingSpawnGroupData>(this.m_BuildingSpawnGroupType).m_ZoneType.Equals(zoneType))
                    {
                        
                        NativeArray<Entity> nativeArray1 = spawnableBuildingChunk.GetNativeArray(this.m_EntityType);
                        
                        NativeArray<SpawnableBuildingData> nativeArray2 = spawnableBuildingChunk.GetNativeArray<SpawnableBuildingData>(ref this.m_SpawnableBuildingType);
                        
                        NativeArray<Game.Prefabs.BuildingData> nativeArray3 = spawnableBuildingChunk.GetNativeArray<Game.Prefabs.BuildingData>(ref this.m_BuildingType);
                        
                        NativeArray<BuildingPropertyData> nativeArray4 = spawnableBuildingChunk.GetNativeArray<BuildingPropertyData>(ref this.m_BuildingPropertyType);
                        
                        NativeArray<ObjectGeometryData> nativeArray5 = spawnableBuildingChunk.GetNativeArray<ObjectGeometryData>(ref this.m_ObjectGeometryType);
                        for (int index2 = 0; index2 < spawnableBuildingChunk.Count; ++index2)
                        {
                            SpawnableBuildingData spawnableBuildingData = nativeArray2[index2];
                            Game.Prefabs.BuildingData buildingData = nativeArray3[index2];
                            BuildingPropertyData buildingPropertyData1 = nativeArray4[index2];
                            ObjectGeometryData objectGeometryData = nativeArray5[index2];
                            if (level == (int)spawnableBuildingData.m_Level && lotSize.Equals(buildingData.m_LotSize) && (double)objectGeometryData.m_Size.y <= (double)maxHeight && (buildingData.m_Flags & (Game.Prefabs.BuildingFlags.LeftAccess | Game.Prefabs.BuildingFlags.RightAccess)) == accessFlags && buildingPropertyData.m_ResidentialProperties <= buildingPropertyData1.m_ResidentialProperties && buildingPropertyData.m_AllowedManufactured == buildingPropertyData1.m_AllowedManufactured && buildingPropertyData.m_AllowedInput == buildingPropertyData1.m_AllowedInput && buildingPropertyData.m_AllowedSold == buildingPropertyData1.m_AllowedSold && buildingPropertyData.m_AllowedStored == buildingPropertyData1.m_AllowedStored)
                            {
                                int num = 100;
                                max += num;
                                if (random.NextInt(max) < num)
                                    entity = nativeArray1[index2];
                            }
                        }
                    }
                }
                return entity;
            }

            private float GetMaxHeight(Entity building, Game.Prefabs.BuildingData prefabBuildingData)
            {
                
                Game.Objects.Transform transform = this.m_TransformData[building];
                float2 xz1 = math.rotate(transform.m_Rotation, new float3(8f, 0.0f, 0.0f)).xz;
                float2 xz2 = math.rotate(transform.m_Rotation, new float3(0.0f, 0.0f, 8f)).xz;
                float2 x1 = xz1 * (float)((double)prefabBuildingData.m_LotSize.x * 0.5 - 0.5);
                float2 x2 = xz2 * (float)((double)prefabBuildingData.m_LotSize.y * 0.5 - 0.5);
                float2 float2 = math.abs(x2) + math.abs(x1);
                
                
                
                // ISSUE: object of a compiler-generated type is created
                // ISSUE: variable of a compiler-generated type
                UrbanInequalityBuildingUpkeepSystem.LevelupJob.Iterator iterator = new UrbanInequalityBuildingUpkeepSystem.LevelupJob.Iterator()
                {
                    m_Bounds = new Bounds2(transform.m_Position.xz - float2, transform.m_Position.xz + float2),
                    m_LotSize = prefabBuildingData.m_LotSize,
                    m_StartPosition = transform.m_Position.xz + x2 + x1,
                    m_Right = xz1,
                    m_Forward = xz2,
                    m_MaxHeight = int.MaxValue,
                    m_BlockData = this.m_BlockData,
                    m_ValidAreaData = this.m_ValidAreaData,
                    m_Cells = this.m_Cells
                };
                
                this.m_ZoneSearchTree.Iterate<UrbanInequalityBuildingUpkeepSystem.LevelupJob.Iterator>(ref iterator);
                
                return (float)iterator.m_MaxHeight - transform.m_Position.y;
            }

            private struct Iterator :
              INativeQuadTreeIterator<Entity, Bounds2>,
              IUnsafeQuadTreeIterator<Entity, Bounds2>
            {
                public Bounds2 m_Bounds;
                public int2 m_LotSize;
                public float2 m_StartPosition;
                public float2 m_Right;
                public float2 m_Forward;
                public int m_MaxHeight;
                public ComponentLookup<Game.Zones.Block> m_BlockData;
                public ComponentLookup<ValidArea> m_ValidAreaData;
                public BufferLookup<Cell> m_Cells;

                public bool Intersect(Bounds2 bounds) => MathUtils.Intersect(bounds, this.m_Bounds);

                public void Iterate(Bounds2 bounds, Entity blockEntity)
                {
                    
                    if (!MathUtils.Intersect(bounds, this.m_Bounds))
                        return;
                    
                    ValidArea validArea = this.m_ValidAreaData[blockEntity];
                    if (validArea.m_Area.y <= validArea.m_Area.x)
                        return;
                    
                    Game.Zones.Block block = this.m_BlockData[blockEntity];
                    
                    DynamicBuffer<Cell> cell1 = this.m_Cells[blockEntity];
                    
                    float2 startPosition = this.m_StartPosition;
                    int2 int2;
                    
                    for (int2.y = 0; int2.y < this.m_LotSize.y; ++int2.y)
                    {
                        float2 position = startPosition;
                        
                        for (int2.x = 0; int2.x < this.m_LotSize.x; ++int2.x)
                        {
                            int2 cellIndex = ZoneUtils.GetCellIndex(block, position);
                            if (math.all(cellIndex >= validArea.m_Area.xz & cellIndex < validArea.m_Area.yw))
                            {
                                int index = cellIndex.y * block.m_Size.x + cellIndex.x;
                                Cell cell2 = cell1[index];
                                if ((cell2.m_State & CellFlags.Visible) != CellFlags.None)
                                {
                                    
                                    
                                    this.m_MaxHeight = math.min(this.m_MaxHeight, (int)cell2.m_Height);
                                }
                            }
                            
                            position -= this.m_Right;
                        }
                        
                        startPosition -= this.m_Forward;
                    }
                }
            }
        }

        private struct TypeHandle
        {
            public ComponentTypeHandle<BuildingCondition> __Game_Buildings_BuildingCondition_RW_ComponentTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<Building> __Game_Buildings_Building_RO_ComponentTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<Renter> __Game_Buildings_Renter_RO_BufferTypeHandle;
            [ReadOnly]
            public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentLookup;
            [ReadOnly]
            public BufferLookup<HouseholdCitizen> __Game_HouseholdCitizen_RO_BufferLookup;
            public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;
            [ReadOnly]
            public ComponentLookup<ConsumptionData> __Game_Prefabs_ConsumptionData_RO_ComponentLookup;
            [ReadOnly]
            public BufferLookup<ResourceAvailability> __Game_Net_ResourceAvailability_RO_BufferLookup;
            [ReadOnly]
            public ComponentLookup<Game.Prefabs.BuildingData> __Game_Prefabs_BuildingData_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> __Game_Prefabs_BuildingPropertyData_RO_ComponentLookup;
            [ReadOnly]
            public BufferLookup<CityModifier> __Game_City_CityModifier_RO_BufferLookup;
            [ReadOnly]
            public ComponentLookup<SignatureBuildingData> __Game_Prefabs_SignatureBuildingData_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<Abandoned> __Game_Buildings_Abandoned_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<Destroyed> __Game_Common_Destroyed_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<ZoneData> __Game_Prefabs_ZoneData_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;
            [ReadOnly]
            public BufferLookup<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferLookup;
            [ReadOnly]
            public BufferLookup<LayoutElement> __Game_Vehicles_LayoutElement_RO_BufferLookup;
            [ReadOnly]
            public ComponentLookup<Game.Vehicles.DeliveryTruck> __Game_Vehicles_DeliveryTruck_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;
            [ReadOnly]
            public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RO_BufferLookup;
            [ReadOnly]
            public ComponentTypeHandle<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<Game.Prefabs.BuildingData> __Game_Prefabs_BuildingData_RO_ComponentTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<BuildingPropertyData> __Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<ObjectGeometryData> __Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle;
            public SharedComponentTypeHandle<BuildingSpawnGroupData> __Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle;
            [ReadOnly]
            public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<Game.Zones.Block> __Game_Zones_Block_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<ValidArea> __Game_Zones_ValidArea_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<PrefabData> __Game_Prefabs_PrefabData_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<OfficeBuilding> __Game_Prefabs_OfficeBuilding_RO_ComponentLookup;
            [ReadOnly]
            public BufferLookup<Cell> __Game_Zones_Cell_RO_BufferLookup;
            public ComponentLookup<Building> __Game_Buildings_Building_RW_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<ElectricityConsumer> __Game_Buildings_ElectricityConsumer_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<GarbageProducer> __Game_Buildings_GarbageProducer_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<MailProducer> __Game_Buildings_MailProducer_RO_ComponentLookup;
            [ReadOnly]
            public ComponentLookup<WaterConsumer> __Game_Buildings_WaterConsumer_RO_ComponentLookup;
            public ComponentLookup<CrimeProducer> __Game_Buildings_CrimeProducer_RW_ComponentLookup;
            public BufferLookup<Renter> __Game_Buildings_Renter_RW_BufferLookup;
            public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferLookup;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                
                this.__Game_Buildings_BuildingCondition_RW_ComponentTypeHandle = state.GetComponentTypeHandle<BuildingCondition>();
                
                this.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(true);
                
                this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                
                this.__Game_Buildings_Building_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Building>(true);
                
                this.__Game_Buildings_Renter_RO_BufferTypeHandle = state.GetBufferTypeHandle<Renter>(true);

                this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
                this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
                this.__Game_Citizens_HealthProblem_RO_ComponentLookup = state.GetComponentLookup<HealthProblem>(true);

                this.__Game_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);

                this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
                
                this.__Game_Prefabs_ConsumptionData_RO_ComponentLookup = state.GetComponentLookup<ConsumptionData>(true);
                
                this.__Game_Net_ResourceAvailability_RO_BufferLookup = state.GetBufferLookup<ResourceAvailability>(true);
                
                this.__Game_Prefabs_BuildingData_RO_ComponentLookup = state.GetComponentLookup<Game.Prefabs.BuildingData>(true);
                
                this.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup = state.GetComponentLookup<BuildingPropertyData>(true);
                
                this.__Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(true);
                
                this.__Game_Prefabs_SignatureBuildingData_RO_ComponentLookup = state.GetComponentLookup<SignatureBuildingData>(true);
                
                this.__Game_Buildings_Abandoned_RO_ComponentLookup = state.GetComponentLookup<Abandoned>(true);
                
                this.__Game_Common_Destroyed_RO_ComponentLookup = state.GetComponentLookup<Destroyed>(true);
                
                this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup = state.GetComponentLookup<SpawnableBuildingData>(true);
                
                this.__Game_Prefabs_ZoneData_RO_ComponentLookup = state.GetComponentLookup<ZoneData>(true);
                
                this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
                
                this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
                
                this.__Game_Vehicles_LayoutElement_RO_BufferLookup = state.GetBufferLookup<LayoutElement>(true);
                
                this.__Game_Vehicles_DeliveryTruck_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true);
                
                this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
                
                this.__Game_Economy_Resources_RO_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>(true);
                
                this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<SpawnableBuildingData>(true);
                
                this.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Prefabs.BuildingData>(true);
                
                this.__Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<BuildingPropertyData>(true);
                
                this.__Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ObjectGeometryData>(true);
                
                this.__Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<BuildingSpawnGroupData>();
                
                this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(true);
                
                this.__Game_Zones_Block_RO_ComponentLookup = state.GetComponentLookup<Game.Zones.Block>(true);
                
                this.__Game_Zones_ValidArea_RO_ComponentLookup = state.GetComponentLookup<ValidArea>(true);
                
                this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
                
                this.__Game_Prefabs_PrefabData_RO_ComponentLookup = state.GetComponentLookup<PrefabData>(true);
                
                this.__Game_Prefabs_OfficeBuilding_RO_ComponentLookup = state.GetComponentLookup<OfficeBuilding>(true);
                
                this.__Game_Zones_Cell_RO_BufferLookup = state.GetBufferLookup<Cell>(true);
                
                this.__Game_Buildings_Building_RW_ComponentLookup = state.GetComponentLookup<Building>();
                
                this.__Game_Buildings_ElectricityConsumer_RO_ComponentLookup = state.GetComponentLookup<ElectricityConsumer>(true);
                
                this.__Game_Buildings_GarbageProducer_RO_ComponentLookup = state.GetComponentLookup<GarbageProducer>(true);
                
                this.__Game_Buildings_MailProducer_RO_ComponentLookup = state.GetComponentLookup<MailProducer>(true);
                
                this.__Game_Buildings_WaterConsumer_RO_ComponentLookup = state.GetComponentLookup<WaterConsumer>(true);
                
                this.__Game_Buildings_CrimeProducer_RW_ComponentLookup = state.GetComponentLookup<CrimeProducer>();
                
                this.__Game_Buildings_Renter_RW_BufferLookup = state.GetBufferLookup<Renter>();
                
                this.__Game_Economy_Resources_RW_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>();
            }
        }
    }
}
