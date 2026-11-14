using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Base
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class ResearchSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            if (!ResearchQuery.TryGet(entityManager, out var researchEntity, out var state))
            {
                return;
            }

            var dt = SystemAPI.Time.DeltaTime;
            var changed = false;

            for (var team = 0; team < ResearchState.TeamCount; team++)
            {
                if (!state.IsActive(team))
                {
                    continue;
                }

                var timer = state.TimerFor(team) - dt;
                state.SetTimer(team, math.max(0f, timer));
                changed = true;

                if (timer > 0f)
                {
                    continue;
                }

                var kind = state.KindFor(team);
                state.SetActive(team, false);
                state.Mark(team, kind);
                ApplyUpgrade(entityManager, team, kind);
            }

            if (changed)
            {
                entityManager.SetComponentData(researchEntity, state);
            }
        }

        private static void ApplyUpgrade(EntityManager entityManager, int team, UpgradeKind kind)
        {
            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadWrite<Weapon>(),
                ComponentType.ReadWrite<MoveTarget>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < units.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                if (info.Team != team)
                {
                    continue;
                }

                if (kind == UpgradeKind.Armor)
                {
                    var health = entityManager.GetComponentData<Health>(units[i]);
                    health.Max *= 1.10f;
                    health.Current *= 1.10f;
                    entityManager.SetComponentData(units[i], health);
                }
                else if (kind == UpgradeKind.Weapons)
                {
                    var weapon = entityManager.GetComponentData<Weapon>(units[i]);
                    weapon.Damage *= 1.10f;
                    entityManager.SetComponentData(units[i], weapon);
                }
                else if (kind == UpgradeKind.Logistics)
                {
                    info.MaxSpeed *= 1.10f;
                    entityManager.SetComponentData(units[i], info);
                }
            }

            using var buildingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadWrite<Health>());
            using var buildings = buildingQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < buildings.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                if (info.Team != team)
                {
                    continue;
                }

                if (kind == UpgradeKind.Armor)
                {
                    var health = entityManager.GetComponentData<Health>(buildings[i]);
                    health.Max *= 1.10f;
                    health.Current *= 1.10f;
                    entityManager.SetComponentData(buildings[i], health);
                }
                else if (kind == UpgradeKind.Weapons &&
                         entityManager.HasComponent<DefenseWeapon>(buildings[i]))
                {
                    var weapon = entityManager.GetComponentData<DefenseWeapon>(buildings[i]);
                    weapon.Damage *= 1.10f;
                    entityManager.SetComponentData(buildings[i], weapon);
                }
            }
        }
    }
}
