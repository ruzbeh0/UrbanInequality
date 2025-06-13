using Game;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace UrbanInequality.Systems
{
    public partial class EconomyParameterUpdaterSystem : GameSystemBase
    {
        private EntityQuery _query;

        protected override void OnCreate()
        {
            base.OnCreate();

            _query = GetEntityQuery(new EntityQueryDesc()
            {
                All = new[] {
                    ComponentType.ReadWrite<EconomyParameterData>()
                }
            });

            RequireForUpdate(_query);
        }

        protected override void OnUpdate()
        {
            using var prefabs = _query.ToEntityArray(Allocator.Temp);

            foreach (var tsd in prefabs)
            {
                EconomyParameterData data = EntityManager.GetComponentData<EconomyParameterData>(tsd);

                data.m_Wage0 = Mod.m_Setting.wageLevel1;
                data.m_Wage1 = Mod.m_Setting.wageLevel2;
                data.m_Wage2 = Mod.m_Setting.wageLevel3;
                data.m_Wage3 = Mod.m_Setting.wageLevel4;
                data.m_Wage4 = Mod.m_Setting.wageLevel5;
                EntityManager.SetComponentData<EconomyParameterData>(tsd, data);
            }
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // One day (or month) in-game is '262144' ticks
            return 262144 / 8;
        }
    }
}