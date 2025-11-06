
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Economy;
using Game.Net;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Game.Zones;
using Game.Simulation;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UrbanInequality.Systems;
using Game;

#nullable disable
namespace UrbanInequality.Systems;

//[CompilerGenerated]
public partial class UrbanInequalityBuildingUpkeepSystem : GameSystemBase
{
    public static readonly int kUpdatesPerDay = 16 /*0x10*/;
    public static readonly int kMaterialUpkeep = 4;
    private SimulationSystem m_SimulationSystem;
    private EndFrameBarrier m_EndFrameBarrier;
    private ResourceSystem m_ResourceSystem;
    private ClimateSystem m_ClimateSystem;
    private CitySystem m_CitySystem;
    private IconCommandSystem m_IconCommandSystem;
    private TriggerSystem m_TriggerSystem;
    private ZoneBuiltRequirementSystem m_ZoneBuiltRequirementSystemSystem;
    private Game.Zones.SearchSystem m_ZoneSearchSystem;
    private ElectricityRoadConnectionGraphSystem m_ElectricityRoadConnectionGraphSystem;
    private WaterPipeRoadConnectionGraphSystem m_WaterPipeRoadConnectionGraphSystem;
    private CityProductionStatisticSystem m_CityProductionStatisticSystem;
    private NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment> m_UpkeepExpenseQueue;
    private NativeQueue<UrbanInequalityBuildingUpkeepSystem.LevelUpMaterial> m_LevelUpMaterialQueue;
    private NativeQueue<Entity> m_LevelupQueue;
    private NativeQueue<Entity> m_LeveldownQueue;
    private EntityQuery m_BuildingPrefabGroup;
    private EntityQuery m_BuildingSettingsQuery;
    private EntityQuery m_BuildingGroup;
    private EntityQuery m_ResourceNeedingBuildingGroup;
    public bool debugFastLeveling;
    private ResidentialLevelCapSystem m_ResidentialLevelCapSystem;
    private CommercialLevelCapSystem m_CommercialLevelCapSystem;
    private UrbanInequalityBuildingUpkeepSystem.TypeHandle __TypeHandle;

    public override int GetUpdateInterval(SystemUpdatePhase phase)
    {
        // ISSUE: reference to a compiler-generated field
        return 262144 /*0x040000*/ / (UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay * 16 /*0x10*/);
    }

    public static float GetHeatingMultiplier(float temperature) => math.max(0.0f, 15f - temperature);

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
        this.m_CityProductionStatisticSystem = this.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
        this.m_UpkeepExpenseQueue = new NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment>((AllocatorManager.AllocatorHandle)Allocator.Persistent);
        this.m_LevelUpMaterialQueue = new NativeQueue<UrbanInequalityBuildingUpkeepSystem.LevelUpMaterial>((AllocatorManager.AllocatorHandle)Allocator.Persistent);
        this.m_BuildingSettingsQuery = this.GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>(), ComponentType.ReadOnly<ZoneLevelUpResourceData>());
        this.m_LevelupQueue = new NativeQueue<Entity>((AllocatorManager.AllocatorHandle)Allocator.Persistent);
        this.m_LeveldownQueue = new NativeQueue<Entity>((AllocatorManager.AllocatorHandle)Allocator.Persistent);
        m_ResidentialLevelCapSystem = World.GetOrCreateSystemManaged<ResidentialLevelCapSystem>();
        m_CommercialLevelCapSystem = World.GetOrCreateSystemManaged<CommercialLevelCapSystem>();
        this.m_BuildingGroup = this.GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[3]
          {
        ComponentType.ReadOnly<BuildingCondition>(),
        ComponentType.ReadOnly<PrefabRef>(),
        ComponentType.ReadOnly<UpdateFrame>()
          },
            Any = new ComponentType[0],
            None = new ComponentType[5]
          {
        ComponentType.ReadOnly<Abandoned>(),
        ComponentType.ReadOnly<Destroyed>(),
        ComponentType.ReadOnly<Deleted>(),
        ComponentType.ReadOnly<Temp>(),
        ComponentType.ReadWrite<ResourceNeeding>()
          }
        });
        // ISSUE: reference to a compiler-generated field
        this.m_ResourceNeedingBuildingGroup = this.GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[4]
          {
        ComponentType.ReadWrite<ResourceNeeding>(),
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
        this.m_LevelUpMaterialQueue.Dispose();
        this.m_LevelupQueue.Dispose();
        this.m_LeveldownQueue.Dispose();
    }


    [UnityEngine.Scripting.Preserve]
    protected override void OnUpdate()
    {
        this.m_BuildingGroup.SetSharedComponentFilter<UpdateFrame>(new UpdateFrame(SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex, UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay, 16 /*0x10*/)));
        BuildingConfigurationData singleton = this.m_BuildingSettingsQuery.GetSingleton<BuildingConfigurationData>();
        m_ResidentialLevelCapSystem.GetLevelData(out NativeArray<int> levelCounts, out NativeArray<int> maxCounts, Allocator.TempJob);
        m_CommercialLevelCapSystem.GetLevelData(out NativeArray<int> comLevelCounts, out NativeArray<int> comMaxCounts, Allocator.TempJob);


        UrbanInequalityBuildingUpkeepSystem.BuildingUpkeepJob jobData1 = new UrbanInequalityBuildingUpkeepSystem.BuildingUpkeepJob()
        {
            m_ConditionType = InternalCompilerInterface.GetComponentTypeHandle<BuildingCondition>(ref this.__TypeHandle.__Game_Buildings_BuildingCondition_RW_ComponentTypeHandle, ref this.CheckedStateRef),
            m_PrefabType = InternalCompilerInterface.GetComponentTypeHandle<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle, ref this.CheckedStateRef),
            m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref this.CheckedStateRef),
            m_RenterType = InternalCompilerInterface.GetBufferTypeHandle<Renter>(ref this.__TypeHandle.__Game_Buildings_Renter_RO_BufferTypeHandle, ref this.CheckedStateRef),
            m_ConsumptionDatas = InternalCompilerInterface.GetComponentLookup<ConsumptionData>(ref this.__TypeHandle.__Game_Prefabs_ConsumptionData_RO_ComponentLookup, ref this.CheckedStateRef),
            m_Availabilities = InternalCompilerInterface.GetBufferLookup<ResourceAvailability>(ref this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup, ref this.CheckedStateRef),
            m_LevelUpResourceDataBufs = InternalCompilerInterface.GetBufferLookup<LevelUpResourceData>(ref this.__TypeHandle.__Game_Prefabs_LevelUpResourceData_RO_BufferLookup, ref this.CheckedStateRef),
            m_ZoneLevelUpResourceDataBufs = InternalCompilerInterface.GetBufferLookup<ZoneLevelUpResourceData>(ref this.__TypeHandle.__Game_Prefabs_ZoneLevelUpResourceData_RO_BufferLookup, ref this.CheckedStateRef),
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
            m_Resources = InternalCompilerInterface.GetBufferLookup<Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup, ref this.CheckedStateRef),
            m_Citizens = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref this.CheckedStateRef),
            m_Residents = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_HouseholdCitizen_RO_BufferLookup, ref this.CheckedStateRef),
            m_FrameIndex = (uint)m_SimulationSystem.frameIndex,
            m_BuildingConfigurationData = singleton,
            m_BuildingConfigLevelResourceBuf = this.m_BuildingSettingsQuery.GetSingletonBuffer<ZoneLevelUpResourceData>(true),
            m_TemperatureUpkeep = UrbanInequalityBuildingUpkeepSystem.GetHeatingMultiplier((float)this.m_ClimateSystem.temperature),
            m_DebugFastLeveling = this.debugFastLeveling,
            m_UpkeepExpenseQueue = this.m_UpkeepExpenseQueue.AsParallelWriter(),
            m_LevelDownQueue = this.m_LeveldownQueue.AsParallelWriter(),
            m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
            m_LevelCounts = levelCounts,
            m_MaxLevelCounts = maxCounts,
            m_ComLevelCounts = comLevelCounts,
            m_ComMaxLevelCounts = comMaxCounts,
            m_MaxEduPenalty = Mod.m_Setting.maxEducationPenalty,
            m_MinEduPenalty = Mod.m_Setting.minEducationPenalty,
            m_MaxIncPenalty = Mod.m_Setting.maxIncomePenalty,
            m_MinIncPenalty = Mod.m_Setting.minIncomePenalty,
            m_LevelupMaterialFactor = Mod.m_Setting.levelUpMaterialFactor
        };
        this.Dependency = jobData1.ScheduleParallel<UrbanInequalityBuildingUpkeepSystem.BuildingUpkeepJob>(this.m_BuildingGroup, this.Dependency);
        this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
        this.m_ResourceSystem.AddPrefabsReader(this.Dependency);
        levelCounts.Dispose();
        maxCounts.Dispose();
        comLevelCounts.Dispose();
        comMaxCounts.Dispose();
        UrbanInequalityBuildingUpkeepSystem.ResourceNeedingUpkeepJob jobData2 = new UrbanInequalityBuildingUpkeepSystem.ResourceNeedingUpkeepJob()
        {
            m_ConditionType = InternalCompilerInterface.GetComponentTypeHandle<BuildingCondition>(ref this.__TypeHandle.__Game_Buildings_BuildingCondition_RW_ComponentTypeHandle, ref this.CheckedStateRef),
            m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref this.CheckedStateRef),
            m_ResourceNeedingType = InternalCompilerInterface.GetBufferTypeHandle<ResourceNeeding>(ref this.__TypeHandle.__Game_Buildings_ResourceNeeding_RW_BufferTypeHandle, ref this.CheckedStateRef),
            m_GuestVehicleBufs = InternalCompilerInterface.GetBufferLookup<GuestVehicle>(ref this.__TypeHandle.__Game_Vehicles_GuestVehicle_RO_BufferLookup, ref this.CheckedStateRef),
            m_DeliveryTrucks = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.DeliveryTruck>(ref this.__TypeHandle.__Game_Vehicles_DeliveryTruck_RO_ComponentLookup, ref this.CheckedStateRef),
            m_LeveUpMaterialQueue = this.m_LevelUpMaterialQueue.AsParallelWriter(),
            m_LevelupQueue = this.m_LevelupQueue.AsParallelWriter(),
            m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
        };
        // ISSUE: reference to a compiler-generated field
        this.Dependency = jobData2.ScheduleParallel<UrbanInequalityBuildingUpkeepSystem.ResourceNeedingUpkeepJob>(this.m_ResourceNeedingBuildingGroup, this.Dependency);
        // ISSUE: reference to a compiler-generated field
        this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
        JobHandle outJobHandle;
        JobHandle dependencies;
        JobHandle deps1;
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: object of a compiler-generated type is created
        // ISSUE: variable of a compiler-generated type
        UrbanInequalityBuildingUpkeepSystem.LevelupJob jobData3 = new UrbanInequalityBuildingUpkeepSystem.LevelupJob()
        {
            m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref this.CheckedStateRef),
            m_SpawnableBuildingType = InternalCompilerInterface.GetComponentTypeHandle<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
            m_BuildingType = InternalCompilerInterface.GetComponentTypeHandle<Game.Prefabs.BuildingData>(ref this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
            m_BuildingPropertyType = InternalCompilerInterface.GetComponentTypeHandle<BuildingPropertyData>(ref this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
            m_ObjectGeometryType = InternalCompilerInterface.GetComponentTypeHandle<ObjectGeometryData>(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle, ref this.CheckedStateRef),
            m_BuildingSpawnGroupType = InternalCompilerInterface.GetSharedComponentTypeHandle<BuildingSpawnGroupData>(ref this.__TypeHandle.__Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle, ref this.CheckedStateRef),
            m_TransformData = InternalCompilerInterface.GetComponentLookup<Transform>(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref this.CheckedStateRef),
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
        this.Dependency = jobData3.Schedule<UrbanInequalityBuildingUpkeepSystem.LevelupJob>(JobUtils.CombineDependencies(this.Dependency, outJobHandle, dependencies, deps1));
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        this.m_ZoneSearchSystem.AddSearchTreeReader(this.Dependency);
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        this.m_ZoneBuiltRequirementSystemSystem.AddWriter(this.Dependency);
        // ISSUE: reference to a compiler-generated field
        this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        this.m_TriggerSystem.AddActionBufferWriter(this.Dependency);
        JobHandle deps2;
        JobHandle deps3;
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        // ISSUE: reference to a compiler-generated field
        // ISSUE: object of a compiler-generated type is created
        // ISSUE: variable of a compiler-generated type
        UrbanInequalityBuildingUpkeepSystem.LeveldownJob jobData4 = new UrbanInequalityBuildingUpkeepSystem.LeveldownJob()
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
        this.Dependency = jobData4.Schedule<UrbanInequalityBuildingUpkeepSystem.LeveldownJob>(JobHandle.CombineDependencies(this.Dependency, deps2, deps3));
        // ISSUE: reference to a compiler-generated field
        this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        this.m_ElectricityRoadConnectionGraphSystem.AddQueueWriter(this.Dependency);
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        this.m_IconCommandSystem.AddCommandBufferWriter(this.Dependency);
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
        this.m_TriggerSystem.AddActionBufferWriter(this.Dependency);
        JobHandle deps4;
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: object of a compiler-generated type is created
        // ISSUE: variable of a compiler-generated type
        UrbanInequalityBuildingUpkeepSystem.UpkeepPaymentJob jobData5 = new UrbanInequalityBuildingUpkeepSystem.UpkeepPaymentJob()
        {
            m_Resources = InternalCompilerInterface.GetBufferLookup<Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferLookup, ref this.CheckedStateRef),
            m_UpkeepExpenseQueue = this.m_UpkeepExpenseQueue,
            m_LevelUpMaterialQueue = this.m_LevelUpMaterialQueue,
            m_UpkeepMaterialAccumulator = this.m_CityProductionStatisticSystem.GetCityResourceUsageAccumulator(CityProductionStatisticSystem.CityResourceUsage.Consumer.LevelUp, out deps4)
        };
        this.Dependency = jobData5.Schedule<UrbanInequalityBuildingUpkeepSystem.UpkeepPaymentJob>(JobHandle.CombineDependencies(this.Dependency, deps4));
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
        Entity prefab = prefabRefs[building].m_Prefab;
        if (!spawnables.HasComponent(prefab) || !propertyDatas.HasComponent(prefab))
            return;
        SpawnableBuildingData spawnable = spawnables[prefab];
        if (!zoneDatas.HasComponent(spawnable.m_ZonePrefab))
            return;
        // ISSUE: reference to a compiler-generated field
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
        int areaType = (int)zoneDatas[spawnable.m_ZonePrefab].m_AreaType;
        // ISSUE: reference to a compiler-generated field
        int levelingCost = BuildingUtils.GetLevelingCost((AreaType)areaType, propertyDatas[prefab], (int)spawnable.m_Level, this.EntityManager.GetBuffer<CityModifier>(this.m_CitySystem.City, true));
        // ISSUE: reference to a compiler-generated field
        int abandonCost = BuildingUtils.GetAbandonCost((AreaType)areaType, propertyDatas[prefab], (int)spawnable.m_Level, levelingCost, this.EntityManager.GetBuffer<CityModifier>(this.m_CitySystem.City, true));
        condition.m_Condition = -3 * abandonCost / 2;
        conditions[building] = condition;
        // ISSUE: reference to a compiler-generated field
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
        // ISSUE: reference to a compiler-generated method
        this.__AssignQueries(ref this.CheckedStateRef);
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated method
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

    private struct LevelUpMaterial
    {
        public Resource m_Resource;
        public int m_Amount;
    }

    [BurstCompile]
    private struct BuildingUpkeepJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;
        public ComponentTypeHandle<BuildingCondition> m_ConditionType;
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly]
        public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingDatas;
        [ReadOnly]
        public BufferLookup<LevelUpResourceData> m_LevelUpResourceDataBufs;
        [ReadOnly]
        public BufferLookup<ZoneLevelUpResourceData> m_ZoneLevelUpResourceDataBufs;
        [ReadOnly]
        public ComponentLookup<ZoneData> m_ZoneDatas;
        [ReadOnly]
        public BufferLookup<Resources> m_Resources;
        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;
        [ReadOnly]
        public ComponentLookup<ResourceData> m_ResourceDatas;
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
        public DynamicBuffer<ZoneLevelUpResourceData> m_BuildingConfigLevelResourceBuf;
        [ReadOnly]
        public ComponentLookup<ConsumptionData> m_ConsumptionDatas;
        [ReadOnly]
        public BufferLookup<ResourceAvailability> m_Availabilities;
        [ReadOnly]
        public Entity m_City;
        [ReadOnly] public float m_LevelupMaterialFactor;
        public float m_TemperatureUpkeep;
        public bool m_DebugFastLeveling;
        public NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment>.ParallelWriter m_UpkeepExpenseQueue;
        public NativeQueue<Entity>.ParallelWriter m_LevelDownQueue;
        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        // NEW: penalty inputs
        [ReadOnly] public BufferLookup<HouseholdCitizen> m_Residents;
        [ReadOnly] public ComponentLookup<Citizen> m_Citizens;

        // NEW: level cap arrays (populate from your LevelCapSystem or equivalent)
        [ReadOnly] public NativeArray<int> m_LevelCounts;
        [ReadOnly] public NativeArray<int> m_MaxLevelCounts;
        [ReadOnly] public NativeArray<int> m_ComLevelCounts;   
        [ReadOnly] public NativeArray<int> m_ComMaxLevelCounts;

        // NEW: penalty tunables (copy from config or hardcode to start)
        [ReadOnly] public float m_MaxEduPenalty;  // e.g., 1.50f
        [ReadOnly] public float m_MinEduPenalty;  // e.g., 1.00f
        [ReadOnly] public float m_MaxIncPenalty;  // e.g., 1.50f
        [ReadOnly] public float m_MinIncPenalty;  // e.g., 1.00f

        // NEW: for probabilistic gate
        [ReadOnly] public uint m_FrameIndex;



        public void Execute(
          in ArchetypeChunk chunk,
          int unfilteredChunkIndex,
          bool useEnabledMask,
          in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray1 = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<BuildingCondition> nativeArray2 = chunk.GetNativeArray<BuildingCondition>(ref this.m_ConditionType);
            NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray<PrefabRef>(ref this.m_PrefabType);
            BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor<Renter>(ref this.m_RenterType);
            for (int index1 = 0; index1 < chunk.Count; ++index1)
            {
                Entity e = nativeArray1[index1];
                BuildingCondition buildingCondition = nativeArray2[index1];
                DynamicBuffer<Renter> dynamicBuffer1 = bufferAccessor[index1];
                Entity prefab = nativeArray3[index1].m_Prefab;
                // ISSUE: reference to a compiler-generated field
                ConsumptionData consumptionData = this.m_ConsumptionDatas[prefab];
                // ISSUE: reference to a compiler-generated field
                // ISSUE: reference to a compiler-generated field
                DynamicBuffer<CityModifier> cityModifierBuf = this.m_CityModifierBufs[this.m_City];
                // ISSUE: reference to a compiler-generated field
                SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingDatas[prefab];
                // ISSUE: reference to a compiler-generated field
                AreaType areaType = this.m_ZoneDatas[spawnableBuildingData.m_ZonePrefab].m_AreaType;
                // ISSUE: reference to a compiler-generated field
                BuildingPropertyData buildingPropertyData = this.m_BuildingPropertyDatas[prefab];
                int levelingCost = BuildingUtils.GetLevelingCost(areaType, buildingPropertyData, (int)spawnableBuildingData.m_Level, cityModifierBuf);
                int abandonCost = BuildingUtils.GetAbandonCost(areaType, buildingPropertyData, (int)spawnableBuildingData.m_Level, levelingCost, cityModifierBuf);
                // ISSUE: reference to a compiler-generated field
                int num1 = consumptionData.m_Upkeep / UrbanInequalityBuildingUpkeepSystem.kUpdatesPerDay;
                // ISSUE: reference to a compiler-generated field
                int num2 = num1 - num1 / UrbanInequalityBuildingUpkeepSystem.kMaterialUpkeep;
                int num3 = 0;
                for (int index2 = 0; index2 < dynamicBuffer1.Length; ++index2)
                {
                    DynamicBuffer<Resources> bufferData;
                    // ISSUE: reference to a compiler-generated field
                    if (this.m_Resources.TryGetBuffer(dynamicBuffer1[index2].m_Renter, out bufferData))
                    {
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_Households.HasComponent(dynamicBuffer1[index2].m_Renter))
                        {
                            num3 += EconomyUtils.GetResources(Resource.Money, bufferData);
                        }
                        else
                        {
                            // ISSUE: reference to a compiler-generated field
                            if (this.m_OwnedVehicles.HasBuffer(dynamicBuffer1[index2].m_Renter))
                            {
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                num3 += EconomyUtils.GetCompanyTotalWorth(bufferData, this.m_OwnedVehicles[dynamicBuffer1[index2].m_Renter], ref this.m_LayoutElements, ref this.m_DeliveryTrucks, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
                            }
                            else
                            {
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                num3 += EconomyUtils.GetCompanyTotalWorth(bufferData, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
                            }
                        }
                    }
                }
                int num4 = 0;
                if (num2 > num3)
                {
                    // ISSUE: reference to a compiler-generated field
                    num4 = -this.m_BuildingConfigurationData.m_BuildingConditionDecrement * (int)math.pow(2f, (float)spawnableBuildingData.m_Level) * math.max(1, dynamicBuffer1.Length);
                }
                else if (dynamicBuffer1.Length > 0)
                {
                    // ISSUE: reference to a compiler-generated field
                    num4 = BuildingUtils.GetBuildingConditionChange(areaType, this.m_BuildingConfigurationData) * (int)math.pow(2f, (float)spawnableBuildingData.m_Level) * math.max(1, dynamicBuffer1.Length);
                    int num5 = num2 / dynamicBuffer1.Length;
                    for (int index3 = 0; index3 < dynamicBuffer1.Length; ++index3)
                    {
                        // ISSUE: reference to a compiler-generated field
                        // ISSUE: object of a compiler-generated type is created
                        this.m_UpkeepExpenseQueue.Enqueue(new UrbanInequalityBuildingUpkeepSystem.UpkeepPayment()
                        {
                            m_RenterEntity = dynamicBuffer1[index3].m_Renter,
                            m_Price = -num5
                        });
                    }
                }
                // ISSUE: reference to a compiler-generated field
                if (this.m_DebugFastLeveling)
                    buildingCondition.m_Condition = levelingCost;
                else
                    buildingCondition.m_Condition += num4;

                // compute avgEdu/avgIncome over renters (quick inline; reuse your existing renters buffer)
                float avgEdu = 0f, avgIncome = 0f;
                int eduCount = 0, incomeCount = 0, rentersCount = 0;

                var renters = bufferAccessor[index1]; // you already have this per-entity buffer in vanilla
                rentersCount = renters.Length;
                for (int r = 0; r < renters.Length; r++)
                {
                    var hh = renters[r].m_Renter;
                    // income
                    avgIncome += GetHouseholdIncomeProxy(hh, m_Resources); incomeCount++;

                    // education: walk residents → citizens
                    if (m_Residents.TryGetBuffer(hh, out var res))
                    {
                        for (int i = 0; i < res.Length; i++)
                        {
                            var c = m_Citizens[res[i].m_Citizen];
                            var age = c.GetAge();
                            if (age == CitizenAge.Adult || age == CitizenAge.Elderly)
                            {
                                avgEdu += c.GetEducationLevel(); eduCount++;
                            }
                        }
                    }
                }
                avgEdu = eduCount > 0 ? (avgEdu / eduCount) : 0f;
                avgIncome = incomeCount > 0 ? (avgIncome / incomeCount) : 0f;

                // call helper
                bool allowLevel;
                int adjustedLevelingCost = ApplyPenaltiesAndGates(
                    e, /* building entity */
                    spawnableBuildingData,
                    this.m_ZoneDatas[spawnableBuildingData.m_ZonePrefab],
                    levelingCost,
                    rentersCount,
                    out allowLevel, avgEdu, avgIncome);


                if (allowLevel && buildingCondition.m_Condition >= adjustedLevelingCost)
                {
                    // ISSUE: reference to a compiler-generated field
                    DynamicBuffer<ResourceNeeding> dynamicBuffer2 = this.m_CommandBuffer.AddBuffer<ResourceNeeding>(unfilteredChunkIndex, e);
                    // ISSUE: reference to a compiler-generated field
                    this.m_CommandBuffer.AddBuffer<GuestVehicle>(unfilteredChunkIndex, e);
                    DynamicBuffer<LevelUpResourceData> bufferData1;
                    ResourceNeeding resourceNeeding;
                    // ISSUE: reference to a compiler-generated field
                    if (this.m_LevelUpResourceDataBufs.TryGetBuffer(prefab, out bufferData1) && bufferData1.Length > 0)
                    {
                        for (int index4 = 0; index4 < bufferData1.Length; ++index4)
                        {
                            ref DynamicBuffer<ResourceNeeding> local = ref dynamicBuffer2;
                            resourceNeeding = new ResourceNeeding();
                            resourceNeeding.m_Resource = bufferData1[index4].m_LevelUpResource.m_Resource;
                            resourceNeeding.m_Amount = (int)(bufferData1[index4].m_LevelUpResource.m_Amount * m_LevelupMaterialFactor);
                            resourceNeeding.m_Flags = ResourceNeedingFlags.None;
                            ResourceNeeding elem = resourceNeeding;
                            local.Add(elem);
                        }
                    }
                    else
                    {
                        DynamicBuffer<ZoneLevelUpResourceData> bufferData2;
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_ZoneLevelUpResourceDataBufs.TryGetBuffer(spawnableBuildingData.m_ZonePrefab, out bufferData2) && bufferData2.Length > 0)
                        {
                            for (int index5 = 0; index5 < bufferData2.Length; ++index5)
                            {
                                if (bufferData2[index5].m_Level == (int)spawnableBuildingData.m_Level)
                                {
                                    ref DynamicBuffer<ResourceNeeding> local = ref dynamicBuffer2;
                                    resourceNeeding = new ResourceNeeding();
                                    resourceNeeding.m_Resource = bufferData2[index5].m_LevelUpResource.m_Resource;
                                    resourceNeeding.m_Amount = (int)(bufferData2[index5].m_LevelUpResource.m_Amount * m_LevelupMaterialFactor);
                                    resourceNeeding.m_Flags = ResourceNeedingFlags.None;
                                    ResourceNeeding elem = resourceNeeding;
                                    local.Add(elem);
                                }
                            }
                        }
                        else
                        {
                            // ISSUE: reference to a compiler-generated field
                            for (int index6 = 0; index6 < this.m_BuildingConfigLevelResourceBuf.Length; ++index6)
                            {
                                // ISSUE: reference to a compiler-generated field
                                if (this.m_BuildingConfigLevelResourceBuf[index6].m_Level == (int)spawnableBuildingData.m_Level)
                                {
                                    ref DynamicBuffer<ResourceNeeding> local = ref dynamicBuffer2;
                                    resourceNeeding = new ResourceNeeding();
                                    // ISSUE: reference to a compiler-generated field
                                    resourceNeeding.m_Resource = this.m_BuildingConfigLevelResourceBuf[index6].m_LevelUpResource.m_Resource;
                                    // ISSUE: reference to a compiler-generated field
                                    resourceNeeding.m_Amount = (int)(this.m_BuildingConfigLevelResourceBuf[index6].m_LevelUpResource.m_Amount * m_LevelupMaterialFactor);
                                    resourceNeeding.m_Flags = ResourceNeedingFlags.None;
                                    ResourceNeeding elem = resourceNeeding;
                                    local.Add(elem);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // ISSUE: reference to a compiler-generated field
                    // ISSUE: reference to a compiler-generated field
                    // ISSUE: reference to a compiler-generated field
                    if ((this.m_Abandoned.HasComponent(nativeArray1[index1]) ? 0 : (!this.m_Destroyed.HasComponent(nativeArray1[index1]) ? 1 : 0)) != 0 && nativeArray2[index1].m_Condition <= -abandonCost && !this.m_SignatureDatas.HasComponent(prefab))
                    {
                        // ISSUE: reference to a compiler-generated field
                        this.m_LevelDownQueue.Enqueue(nativeArray1[index1]);
                        buildingCondition.m_Condition += levelingCost;
                    }
                }
                nativeArray2[index1] = buildingCondition;
            }
        }

        // NEW
        private int ApplyPenaltiesAndGates(
            Entity building,
            in SpawnableBuildingData spawnable,
            in ZoneData zone,
            int vanillaCost,
            int rentersCount,
            out bool allowLevel,
            float avgEdu,
            float avgIncome)
        {
            allowLevel = true;

            int currentLevel = (int)spawnable.m_Level;
            int targetLevel = math.clamp(currentLevel + 1, 1, 5);

            if (zone.m_AreaType == AreaType.Commercial)
            {
                // Commercial: cap gate only (same caps as residential, but tracked separately)
                bool comAtCap = (m_ComMaxLevelCounts.Length > targetLevel && m_ComLevelCounts.Length > targetLevel &&
                              m_ComMaxLevelCounts[targetLevel] > 0 &&
                              m_ComLevelCounts[targetLevel] >= m_ComMaxLevelCounts[targetLevel]);
                if (comAtCap)
                    allowLevel = false;

                // Do not apply education/income penalties to the cost for commercial
                return vanillaCost;
            }

            if (zone.m_AreaType != AreaType.Residential)
            {
                // Industrial/offices/etc: no gates/penalties
                return vanillaCost;
            }

            // Non-residential: no penalty/gates
            if (zone.m_AreaType != AreaType.Residential)
                return vanillaCost;

            // ------- Scoring & penalty (matches Urban) -------
            float eduScore = avgEdu / 4f;
            float incomeScore = avgIncome / 4000f;
            float score = math.max(0.05f, 0.6f * eduScore + 0.4f * incomeScore);
            float weightedScore = math.pow(score, 3f);

            int incomeBracket = 4;
            if (avgIncome < 500) incomeBracket = 0;
            else if (avgIncome < 1000) incomeBracket = 1;
            else if (avgIncome < 1500) incomeBracket = 2;
            else if (avgIncome < 2500) incomeBracket = 3;

            float eduPenalty = m_MaxEduPenalty - (m_MaxEduPenalty - m_MinEduPenalty) * (avgEdu / 4f);
            float incomePenalty = m_MaxIncPenalty - (m_MaxIncPenalty - m_MinIncPenalty) * (incomeBracket / 4f);
            float finalPenalty = eduPenalty * incomePenalty;

            int adjusted = (int)(vanillaCost * finalPenalty);

            // ------- Level-cap gate -------
            bool atCap = (m_MaxLevelCounts.Length > targetLevel && m_LevelCounts.Length > targetLevel &&
                          m_MaxLevelCounts[targetLevel] > 0 && m_LevelCounts[targetLevel] >= m_MaxLevelCounts[targetLevel]);
            if (atCap) { allowLevel = false; return adjusted; }

            // ------- Probabilistic gate (lower score → more rejection) -------
            uint seed = (uint)building.Index * 1103515245u ^ (m_FrameIndex * 747796405u) ^ 0xA511E9B3u;
            if (seed == 0) seed = 1u;
            var rng = new Unity.Mathematics.Random(seed);
            if (rng.NextFloat() < (1f - weightedScore))
                allowLevel = false;

            return adjusted;
        }

        // NEW (same as Urban): money proxy from household resources
        private static int GetHouseholdIncomeProxy(Entity household, BufferLookup<Game.Economy.Resources> resources)
        {
            if (!resources.HasBuffer(household)) return 0;
            var buf = resources[household];
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].m_Resource == Resource.Money)
                    return buf[i].m_Amount;
            return 0;
        }

        void IJobChunk.Execute(
          in ArchetypeChunk chunk,
          int unfilteredChunkIndex,
          bool useEnabledMask,
          in v128 chunkEnabledMask)
        {
            // ISSUE: reference to a compiler-generated method
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

    [BurstCompile]
    private struct ResourceNeedingUpkeepJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;
        public ComponentTypeHandle<BuildingCondition> m_ConditionType;
        public BufferTypeHandle<ResourceNeeding> m_ResourceNeedingType;
        [ReadOnly]
        public BufferLookup<GuestVehicle> m_GuestVehicleBufs;
        [ReadOnly]
        public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;
        public NativeQueue<Entity>.ParallelWriter m_LevelupQueue;
        public NativeQueue<UrbanInequalityBuildingUpkeepSystem.LevelUpMaterial>.ParallelWriter m_LeveUpMaterialQueue;
        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        public void Execute(
          in ArchetypeChunk chunk,
          int unfilteredChunkIndex,
          bool useEnabledMask,
          in v128 chunkEnabledMask)
        {
            // ISSUE: reference to a compiler-generated field
            NativeArray<Entity> nativeArray1 = chunk.GetNativeArray(this.m_EntityType);
            // ISSUE: reference to a compiler-generated field
            BufferAccessor<ResourceNeeding> bufferAccessor = chunk.GetBufferAccessor<ResourceNeeding>(ref this.m_ResourceNeedingType);
            // ISSUE: reference to a compiler-generated field
            NativeArray<BuildingCondition> nativeArray2 = chunk.GetNativeArray<BuildingCondition>(ref this.m_ConditionType);
            for (int index1 = 0; index1 < chunk.Count; ++index1)
            {
                Entity entity = nativeArray1[index1];
                // ISSUE: reference to a compiler-generated field
                if (this.m_GuestVehicleBufs.HasBuffer(entity))
                {
                    DynamicBuffer<ResourceNeeding> dynamicBuffer = bufferAccessor[index1];
                    bool flag1 = true;
                    for (int index2 = 0; index2 < dynamicBuffer.Length; ++index2)
                    {
                        ResourceNeeding resourceNeeding = dynamicBuffer[index2];
                        if (resourceNeeding.m_Flags == ResourceNeedingFlags.None)
                        {
                            // ISSUE: reference to a compiler-generated method
                            this.RequestDelivery(unfilteredChunkIndex, ref resourceNeeding, entity);
                        }
                        else if (resourceNeeding.m_Flags == ResourceNeedingFlags.Delivering)
                        {
                            bool flag2 = false;
                            // ISSUE: reference to a compiler-generated field
                            DynamicBuffer<GuestVehicle> guestVehicleBuf = this.m_GuestVehicleBufs[entity];
                            for (int index3 = 0; index3 < guestVehicleBuf.Length; ++index3)
                            {
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                if (this.m_DeliveryTrucks.HasComponent(guestVehicleBuf[index3].m_Vehicle) && (this.m_DeliveryTrucks[guestVehicleBuf[index3].m_Vehicle].m_State & DeliveryTruckFlags.UpkeepDelivery) != (DeliveryTruckFlags)0 && resourceNeeding.m_Resource == this.m_DeliveryTrucks[guestVehicleBuf[index3].m_Vehicle].m_Resource && resourceNeeding.m_Amount == this.m_DeliveryTrucks[guestVehicleBuf[index3].m_Vehicle].m_Amount)
                                {
                                    flag2 = true;
                                    break;
                                }
                            }
                            if (!flag2)
                            {
                                // ISSUE: reference to a compiler-generated method
                                this.RequestDelivery(unfilteredChunkIndex, ref resourceNeeding, entity);
                                dynamicBuffer[index2] = resourceNeeding;
                            }
                        }
                        if (resourceNeeding.m_Flags != ResourceNeedingFlags.Delivered)
                            flag1 = false;
                    }
                    if (flag1)
                    {
                        for (int index4 = 0; index4 < dynamicBuffer.Length; ++index4)
                        {
                            // ISSUE: reference to a compiler-generated field
                            // ISSUE: object of a compiler-generated type is created
                            this.m_LeveUpMaterialQueue.Enqueue(new UrbanInequalityBuildingUpkeepSystem.LevelUpMaterial()
                            {
                                m_Resource = dynamicBuffer[index4].m_Resource,
                                m_Amount = dynamicBuffer[index4].m_Amount
                            });
                        }
                        // ISSUE: reference to a compiler-generated field
                        this.m_CommandBuffer.RemoveComponent<ResourceNeeding>(unfilteredChunkIndex, entity);
                        // ISSUE: reference to a compiler-generated field
                        this.m_LevelupQueue.Enqueue(entity);
                        BuildingCondition buildingCondition = nativeArray2[index1] with
                        {
                            m_Condition = 0
                        };
                        nativeArray2[index1] = buildingCondition;
                    }
                }
            }
        }

        private void RequestDelivery(
          int unfilteredChunkIndex,
          ref ResourceNeeding resourceNeeding,
          Entity entity)
        {
            // ISSUE: reference to a compiler-generated field
            Entity entity1 = this.m_CommandBuffer.CreateEntity(unfilteredChunkIndex);
            // ISSUE: reference to a compiler-generated field
            this.m_CommandBuffer.AddComponent<GoodsDeliveryRequest>(unfilteredChunkIndex, entity1, new GoodsDeliveryRequest()
            {
                m_Amount = resourceNeeding.m_Amount,
                m_Flags = GoodsDeliveryFlags.BuildingUpkeep | GoodsDeliveryFlags.IndustrialAllowed | GoodsDeliveryFlags.ImportAllowed,
                m_Resource = resourceNeeding.m_Resource,
                m_Target = entity
            });
        }

        void IJobChunk.Execute(
          in ArchetypeChunk chunk,
          int unfilteredChunkIndex,
          bool useEnabledMask,
          in v128 chunkEnabledMask)
        {
            // ISSUE: reference to a compiler-generated method
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

    [BurstCompile]
    private struct UpkeepPaymentJob : IJob
    {
        public BufferLookup<Resources> m_Resources;
        public NativeQueue<UrbanInequalityBuildingUpkeepSystem.UpkeepPayment> m_UpkeepExpenseQueue;
        public NativeQueue<UrbanInequalityBuildingUpkeepSystem.LevelUpMaterial> m_LevelUpMaterialQueue;
        public NativeArray<int> m_UpkeepMaterialAccumulator;

        public void Execute()
        {
            // ISSUE: variable of a compiler-generated type
            UrbanInequalityBuildingUpkeepSystem.UpkeepPayment upkeepPayment;
            // ISSUE: reference to a compiler-generated field
            while (this.m_UpkeepExpenseQueue.TryDequeue(out upkeepPayment))
            {
                // ISSUE: reference to a compiler-generated field
                // ISSUE: reference to a compiler-generated field
                if (this.m_Resources.HasBuffer(upkeepPayment.m_RenterEntity))
                {
                    // ISSUE: reference to a compiler-generated field
                    // ISSUE: reference to a compiler-generated field
                    // ISSUE: reference to a compiler-generated field
                    EconomyUtils.AddResources(Resource.Money, upkeepPayment.m_Price, this.m_Resources[upkeepPayment.m_RenterEntity]);
                }
            }
            // ISSUE: variable of a compiler-generated type
            UrbanInequalityBuildingUpkeepSystem.LevelUpMaterial levelUpMaterial;
            // ISSUE: reference to a compiler-generated field
            while (this.m_LevelUpMaterialQueue.TryDequeue(out levelUpMaterial))
            {
                // ISSUE: reference to a compiler-generated field
                // ISSUE: reference to a compiler-generated field
                // ISSUE: reference to a compiler-generated field
                this.m_UpkeepMaterialAccumulator[EconomyUtils.GetResourceIndex(levelUpMaterial.m_Resource)] += levelUpMaterial.m_Amount;
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
            // ISSUE: reference to a compiler-generated field
            while (this.m_LeveldownQueue.TryDequeue(out entity))
            {
                // ISSUE: reference to a compiler-generated field
                if (this.m_Prefabs.HasComponent(entity))
                {
                    // ISSUE: reference to a compiler-generated field
                    Entity prefab = this.m_Prefabs[entity].m_Prefab;
                    // ISSUE: reference to a compiler-generated field
                    if (this.m_SpawnableBuildings.HasComponent(prefab))
                    {
                        // ISSUE: reference to a compiler-generated field
                        SpawnableBuildingData spawnableBuilding = this.m_SpawnableBuildings[prefab];
                        // ISSUE: reference to a compiler-generated field
                        Game.Prefabs.BuildingData buildingData = this.m_BuildingDatas[prefab];
                        // ISSUE: reference to a compiler-generated field
                        BuildingPropertyData buildingPropertyData = this.m_BuildingPropertyDatas[prefab];
                        // ISSUE: reference to a compiler-generated field
                        // ISSUE: reference to a compiler-generated field
                        this.m_CommandBuffer.AddComponent<Abandoned>(entity, new Abandoned()
                        {
                            m_AbandonmentTime = this.m_SimulationFrame
                        });
                        // ISSUE: reference to a compiler-generated field
                        this.m_CommandBuffer.AddComponent<Updated>(entity, new Updated());
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_ElectricityConsumers.HasComponent(entity))
                        {
                            // ISSUE: reference to a compiler-generated field
                            this.m_CommandBuffer.RemoveComponent<ElectricityConsumer>(entity);
                            // ISSUE: reference to a compiler-generated field
                            Entity roadEdge = this.m_Buildings[entity].m_RoadEdge;
                            if (roadEdge != Entity.Null)
                            {
                                // ISSUE: reference to a compiler-generated field
                                this.m_UpdatedElectricityRoadEdges.Enqueue(roadEdge);
                            }
                        }
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_WaterConsumers.HasComponent(entity))
                        {
                            // ISSUE: reference to a compiler-generated field
                            this.m_CommandBuffer.RemoveComponent<WaterConsumer>(entity);
                            // ISSUE: reference to a compiler-generated field
                            Entity roadEdge = this.m_Buildings[entity].m_RoadEdge;
                            if (roadEdge != Entity.Null)
                            {
                                // ISSUE: reference to a compiler-generated field
                                this.m_UpdatedWaterPipeRoadEdges.Enqueue(roadEdge);
                            }
                        }
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_GarbageProducers.HasComponent(entity))
                        {
                            // ISSUE: reference to a compiler-generated field
                            this.m_CommandBuffer.RemoveComponent<GarbageProducer>(entity);
                        }
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_MailProducers.HasComponent(entity))
                        {
                            // ISSUE: reference to a compiler-generated field
                            this.m_CommandBuffer.RemoveComponent<MailProducer>(entity);
                        }
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_CrimeProducers.HasComponent(entity))
                        {
                            // ISSUE: reference to a compiler-generated field
                            CrimeProducer crimeProducer = this.m_CrimeProducers[entity];
                            // ISSUE: reference to a compiler-generated field
                            this.m_CommandBuffer.SetComponent<CrimeProducer>(entity, new CrimeProducer()
                            {
                                m_Crime = crimeProducer.m_Crime * 2f,
                                m_PatrolRequest = crimeProducer.m_PatrolRequest
                            });
                        }
                        // ISSUE: reference to a compiler-generated field
                        if (this.m_Renters.HasBuffer(entity))
                        {
                            // ISSUE: reference to a compiler-generated field
                            DynamicBuffer<Renter> renter = this.m_Renters[entity];
                            for (int index = renter.Length - 1; index >= 0; --index)
                            {
                                // ISSUE: reference to a compiler-generated field
                                this.m_CommandBuffer.RemoveComponent<PropertyRenter>(renter[index].m_Renter);
                                renter.RemoveAt(index);
                            }
                        }
                        // ISSUE: reference to a compiler-generated field
                        if ((this.m_Buildings[entity].m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) != Game.Buildings.BuildingFlags.None)
                        {
                            // ISSUE: reference to a compiler-generated field
                            Building building = this.m_Buildings[entity];
                            // ISSUE: reference to a compiler-generated field
                            // ISSUE: reference to a compiler-generated field
                            this.m_IconCommandBuffer.Remove(entity, this.m_BuildingConfigurationData.m_HighRentNotification);
                            building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                            // ISSUE: reference to a compiler-generated field
                            this.m_Buildings[entity] = building;
                        }
                        // ISSUE: reference to a compiler-generated field
                        this.m_IconCommandBuffer.Remove(entity, IconPriority.Problem);
                        // ISSUE: reference to a compiler-generated field
                        this.m_IconCommandBuffer.Remove(entity, IconPriority.FatalProblem);
                        // ISSUE: reference to a compiler-generated field
                        // ISSUE: reference to a compiler-generated field
                        this.m_IconCommandBuffer.Add(entity, this.m_BuildingConfigurationData.m_AbandonedNotification, IconPriority.FatalProblem);
                        if (buildingPropertyData.CountProperties(AreaType.Commercial) > 0)
                        {
                            // ISSUE: reference to a compiler-generated field
                            this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownCommercialBuilding, Entity.Null, entity, entity));
                        }
                        if (buildingPropertyData.CountProperties(AreaType.Industrial) > 0)
                        {
                            // ISSUE: reference to a compiler-generated field
                            if (this.m_OfficeBuilding.HasComponent(prefab))
                            {
                                // ISSUE: reference to a compiler-generated field
                                this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownOfficeBuilding, Entity.Null, entity, entity));
                            }
                            else
                            {
                                // ISSUE: reference to a compiler-generated field
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
        public ComponentLookup<Transform> m_TransformData;
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
            // ISSUE: reference to a compiler-generated field
            Random random = this.m_RandomSeed.GetRandom(0);
            Entity entity1;
            // ISSUE: reference to a compiler-generated field
            while (this.m_LevelupQueue.TryDequeue(out entity1))
            {
                // ISSUE: reference to a compiler-generated field
                Entity prefab = this.m_Prefabs[entity1].m_Prefab;
                // ISSUE: reference to a compiler-generated field
                if (this.m_SpawnableBuildings.HasComponent(prefab))
                {
                    // ISSUE: reference to a compiler-generated field
                    SpawnableBuildingData spawnableBuilding = this.m_SpawnableBuildings[prefab];
                    // ISSUE: reference to a compiler-generated field
                    if (this.m_PrefabDatas.IsComponentEnabled(spawnableBuilding.m_ZonePrefab))
                    {
                        // ISSUE: reference to a compiler-generated field
                        Game.Prefabs.BuildingData building = this.m_Buildings[prefab];
                        // ISSUE: reference to a compiler-generated field
                        BuildingPropertyData buildingPropertyData = this.m_BuildingPropertyDatas[prefab];
                        // ISSUE: reference to a compiler-generated field
                        ZoneData zoneData = this.m_ZoneData[spawnableBuilding.m_ZonePrefab];
                        // ISSUE: reference to a compiler-generated method
                        float maxHeight = this.GetMaxHeight(entity1, building);
                        // ISSUE: reference to a compiler-generated method
                        Entity entity2 = this.SelectSpawnableBuilding(zoneData.m_ZoneType, (int)spawnableBuilding.m_Level + 1, building.m_LotSize, maxHeight, building.m_Flags & (Game.Prefabs.BuildingFlags.LeftAccess | Game.Prefabs.BuildingFlags.RightAccess), buildingPropertyData, ref random);
                        if (entity2 != Entity.Null)
                        {
                            // ISSUE: reference to a compiler-generated field
                            this.m_CommandBuffer.AddComponent<UnderConstruction>(entity1, new UnderConstruction()
                            {
                                m_NewPrefab = entity2,
                                m_Progress = byte.MaxValue
                            });
                            if (buildingPropertyData.CountProperties(AreaType.Residential) > 0)
                            {
                                // ISSUE: reference to a compiler-generated field
                                this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpResidentialBuilding, Entity.Null, entity1, entity1));
                            }
                            if (buildingPropertyData.CountProperties(AreaType.Commercial) > 0)
                            {
                                // ISSUE: reference to a compiler-generated field
                                this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpCommercialBuilding, Entity.Null, entity1, entity1));
                            }
                            if (buildingPropertyData.CountProperties(AreaType.Industrial) > 0)
                            {
                                // ISSUE: reference to a compiler-generated field
                                if (this.m_OfficeBuilding.HasComponent(prefab))
                                {
                                    // ISSUE: reference to a compiler-generated field
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpOfficeBuilding, Entity.Null, entity1, entity1));
                                }
                                else
                                {
                                    // ISSUE: reference to a compiler-generated field
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpIndustrialBuilding, Entity.Null, entity1, entity1));
                                }
                            }
                            // ISSUE: reference to a compiler-generated field
                            this.m_ZoneBuiltLevelQueue.Enqueue(new ZoneBuiltLevelUpdate()
                            {
                                m_Zone = spawnableBuilding.m_ZonePrefab,
                                m_FromLevel = (int)spawnableBuilding.m_Level,
                                m_ToLevel = (int)spawnableBuilding.m_Level + 1,
                                m_Squares = building.m_LotSize.x * building.m_LotSize.y
                            });
                            // ISSUE: reference to a compiler-generated field
                            // ISSUE: reference to a compiler-generated field
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
          ref Random random)
        {
            int max = 0;
            Entity entity = Entity.Null;
            // ISSUE: reference to a compiler-generated field
            for (int index1 = 0; index1 < this.m_SpawnableBuildingChunks.Length; ++index1)
            {
                // ISSUE: reference to a compiler-generated field
                ArchetypeChunk spawnableBuildingChunk = this.m_SpawnableBuildingChunks[index1];
                // ISSUE: reference to a compiler-generated field
                if (spawnableBuildingChunk.GetSharedComponent<BuildingSpawnGroupData>(this.m_BuildingSpawnGroupType).m_ZoneType.Equals(zoneType))
                {
                    // ISSUE: reference to a compiler-generated field
                    NativeArray<Entity> nativeArray1 = spawnableBuildingChunk.GetNativeArray(this.m_EntityType);
                    // ISSUE: reference to a compiler-generated field
                    NativeArray<SpawnableBuildingData> nativeArray2 = spawnableBuildingChunk.GetNativeArray<SpawnableBuildingData>(ref this.m_SpawnableBuildingType);
                    // ISSUE: reference to a compiler-generated field
                    NativeArray<Game.Prefabs.BuildingData> nativeArray3 = spawnableBuildingChunk.GetNativeArray<Game.Prefabs.BuildingData>(ref this.m_BuildingType);
                    // ISSUE: reference to a compiler-generated field
                    NativeArray<BuildingPropertyData> nativeArray4 = spawnableBuildingChunk.GetNativeArray<BuildingPropertyData>(ref this.m_BuildingPropertyType);
                    // ISSUE: reference to a compiler-generated field
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
            // ISSUE: reference to a compiler-generated field
            Transform transform = this.m_TransformData[building];
            float2 xz1 = math.rotate(transform.m_Rotation, new float3(8f, 0.0f, 0.0f)).xz;
            float2 xz2 = math.rotate(transform.m_Rotation, new float3(0.0f, 0.0f, 8f)).xz;
            float2 x1 = xz1 * (float)((double)prefabBuildingData.m_LotSize.x * 0.5 - 0.5);
            float2 x2 = xz2 * (float)((double)prefabBuildingData.m_LotSize.y * 0.5 - 0.5);
            float2 float2 = math.abs(x2) + math.abs(x1);
            // ISSUE: reference to a compiler-generated field
            // ISSUE: reference to a compiler-generated field
            // ISSUE: reference to a compiler-generated field
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
            // ISSUE: reference to a compiler-generated field
            this.m_ZoneSearchTree.Iterate<UrbanInequalityBuildingUpkeepSystem.LevelupJob.Iterator>(ref iterator);
            // ISSUE: reference to a compiler-generated field
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
                // ISSUE: reference to a compiler-generated field
                if (!MathUtils.Intersect(bounds, this.m_Bounds))
                    return;
                // ISSUE: reference to a compiler-generated field
                ValidArea validArea = this.m_ValidAreaData[blockEntity];
                if (validArea.m_Area.y <= validArea.m_Area.x)
                    return;
                // ISSUE: reference to a compiler-generated field
                Game.Zones.Block block = this.m_BlockData[blockEntity];
                // ISSUE: reference to a compiler-generated field
                DynamicBuffer<Cell> cell1 = this.m_Cells[blockEntity];
                // ISSUE: reference to a compiler-generated field
                float2 startPosition = this.m_StartPosition;
                int2 int2;
                // ISSUE: reference to a compiler-generated field
                for (int2.y = 0; int2.y < this.m_LotSize.y; ++int2.y)
                {
                    float2 position = startPosition;
                    // ISSUE: reference to a compiler-generated field
                    for (int2.x = 0; int2.x < this.m_LotSize.x; ++int2.x)
                    {
                        int2 cellIndex = ZoneUtils.GetCellIndex(block, position);
                        if (math.all(cellIndex >= validArea.m_Area.xz & cellIndex < validArea.m_Area.yw))
                        {
                            int index = cellIndex.y * block.m_Size.x + cellIndex.x;
                            Cell cell2 = cell1[index];
                            if ((cell2.m_State & CellFlags.Visible) != CellFlags.None)
                            {
                                // ISSUE: reference to a compiler-generated field
                                // ISSUE: reference to a compiler-generated field
                                this.m_MaxHeight = math.min(this.m_MaxHeight, (int)cell2.m_Height);
                            }
                        }
                        // ISSUE: reference to a compiler-generated field
                        position -= this.m_Right;
                    }
                    // ISSUE: reference to a compiler-generated field
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
        public BufferTypeHandle<Renter> __Game_Buildings_Renter_RO_BufferTypeHandle;
        [ReadOnly]
        public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;
        [ReadOnly]
        public BufferLookup<HouseholdCitizen> __Game_HouseholdCitizen_RO_BufferLookup;
        [ReadOnly]
        public ComponentLookup<ConsumptionData> __Game_Prefabs_ConsumptionData_RO_ComponentLookup;
        [ReadOnly]
        public BufferLookup<ResourceAvailability> __Game_Net_ResourceAvailability_RO_BufferLookup;
        [ReadOnly]
        public BufferLookup<LevelUpResourceData> __Game_Prefabs_LevelUpResourceData_RO_BufferLookup;
        [ReadOnly]
        public BufferLookup<ZoneLevelUpResourceData> __Game_Prefabs_ZoneLevelUpResourceData_RO_BufferLookup;
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
        public BufferLookup<Resources> __Game_Economy_Resources_RO_BufferLookup;
        public BufferTypeHandle<ResourceNeeding> __Game_Buildings_ResourceNeeding_RW_BufferTypeHandle;
        [ReadOnly]
        public BufferLookup<GuestVehicle> __Game_Vehicles_GuestVehicle_RO_BufferLookup;
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
        public ComponentLookup<Transform> __Game_Objects_Transform_RO_ComponentLookup;
        [ReadOnly]
        public ComponentLookup<Game.Zones.Block> __Game_Zones_Block_RO_ComponentLookup;
        [ReadOnly]
        public ComponentLookup<ValidArea> __Game_Zones_ValidArea_RO_ComponentLookup;
        [ReadOnly]
        public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;
        [ReadOnly]
        public ComponentLookup<PrefabData> __Game_Prefabs_PrefabData_RO_ComponentLookup;
        [ReadOnly]
        public ComponentLookup<Game.Prefabs.BuildingData> __Game_Prefabs_BuildingData_RO_ComponentLookup;
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
        public BufferLookup<Resources> __Game_Economy_Resources_RW_BufferLookup;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref SystemState state)
        {
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_BuildingCondition_RW_ComponentTypeHandle = state.GetComponentTypeHandle<BuildingCondition>();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_Renter_RO_BufferTypeHandle = state.GetBufferTypeHandle<Renter>(true);
            this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
            this.__Game_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
            this.__Game_Prefabs_ConsumptionData_RO_ComponentLookup = state.GetComponentLookup<ConsumptionData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Net_ResourceAvailability_RO_BufferLookup = state.GetBufferLookup<ResourceAvailability>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_LevelUpResourceData_RO_BufferLookup = state.GetBufferLookup<LevelUpResourceData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_ZoneLevelUpResourceData_RO_BufferLookup = state.GetBufferLookup<ZoneLevelUpResourceData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup = state.GetComponentLookup<BuildingPropertyData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_SignatureBuildingData_RO_ComponentLookup = state.GetComponentLookup<SignatureBuildingData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_Abandoned_RO_ComponentLookup = state.GetComponentLookup<Abandoned>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Common_Destroyed_RO_ComponentLookup = state.GetComponentLookup<Destroyed>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup = state.GetComponentLookup<SpawnableBuildingData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_ZoneData_RO_ComponentLookup = state.GetComponentLookup<ZoneData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Vehicles_LayoutElement_RO_BufferLookup = state.GetBufferLookup<LayoutElement>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Vehicles_DeliveryTruck_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Economy_Resources_RO_BufferLookup = state.GetBufferLookup<Resources>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_ResourceNeeding_RW_BufferTypeHandle = state.GetBufferTypeHandle<ResourceNeeding>();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Vehicles_GuestVehicle_RO_BufferLookup = state.GetBufferLookup<GuestVehicle>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<SpawnableBuildingData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Prefabs.BuildingData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<BuildingPropertyData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ObjectGeometryData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<BuildingSpawnGroupData>();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Transform>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Zones_Block_RO_ComponentLookup = state.GetComponentLookup<Game.Zones.Block>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Zones_ValidArea_RO_ComponentLookup = state.GetComponentLookup<ValidArea>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_PrefabData_RO_ComponentLookup = state.GetComponentLookup<PrefabData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_BuildingData_RO_ComponentLookup = state.GetComponentLookup<Game.Prefabs.BuildingData>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Prefabs_OfficeBuilding_RO_ComponentLookup = state.GetComponentLookup<OfficeBuilding>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Zones_Cell_RO_BufferLookup = state.GetBufferLookup<Cell>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_Building_RW_ComponentLookup = state.GetComponentLookup<Building>();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_ElectricityConsumer_RO_ComponentLookup = state.GetComponentLookup<ElectricityConsumer>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_GarbageProducer_RO_ComponentLookup = state.GetComponentLookup<GarbageProducer>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_MailProducer_RO_ComponentLookup = state.GetComponentLookup<MailProducer>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_WaterConsumer_RO_ComponentLookup = state.GetComponentLookup<WaterConsumer>(true);
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_CrimeProducer_RW_ComponentLookup = state.GetComponentLookup<CrimeProducer>();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Buildings_Renter_RW_BufferLookup = state.GetBufferLookup<Renter>();
            // ISSUE: reference to a compiler-generated field
            this.__Game_Economy_Resources_RW_BufferLookup = state.GetBufferLookup<Resources>();
        }
    }
}
