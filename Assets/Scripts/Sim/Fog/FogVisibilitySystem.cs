using CQRa.Sim.Base;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CQRa.Sim.Fog
{
    /// <summary>
    /// Presentation-only fog masking for the player's view. Friendly entities are always
    /// visible; enemy units and buildings are hidden unless the player's fog cell is
    /// currently visible. Ore is hidden until its area has been explored.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FogVisibilitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var hasFog = FogQuery.TryGet(entityManager, 0, out var fog, out var info);
            var playerCells = hasFog ? FogQuery.GetCells(entityManager, fog) : default;

            using var entityQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<LocalTransform>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<BuildingInfo>(),
                },
            });

            using var entities = entityQuery.ToEntityArray(Allocator.Temp);
            var toHide = new NativeList<Entity>(entities.Length, Allocator.Temp);
            var toShow = new NativeList<Entity>(entities.Length, Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var team = entityManager.HasComponent<UnitInfo>(entity)
                    ? entityManager.GetComponentData<UnitInfo>(entity).Team
                    : entityManager.GetComponentData<BuildingInfo>(entity).Team;

                var hide = false;
                if (hasFog && team != 0)
                {
                    var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    hide = !FogMath.IsVisible(
                        playerCells,
                        info,
                        new float2(position.x, position.z));
                }

                if (team != 0 && CloakQuery.IsHiddenFrom(entityManager, entity, 0))
                {
                    hide = true;
                }

                if (hide)
                {
                    toHide.Add(entity);
                }
                else
                {
                    toShow.Add(entity);
                }
            }

            using var oreQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<OreNode>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var ores = oreQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < ores.Length; i++)
            {
                var entity = ores[i];
                var hide = false;
                if (hasFog)
                {
                    var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    hide = !FogMath.IsExplored(
                        playerCells,
                        info,
                        new float2(position.x, position.z));
                }

                if (hide)
                {
                    toHide.Add(entity);
                }
                else
                {
                    toShow.Add(entity);
                }
            }

            using var crateQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Crate>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var crates = crateQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < crates.Length; i++)
            {
                var entity = crates[i];
                var hide = false;
                if (hasFog)
                {
                    var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    hide = !FogMath.IsExplored(
                        playerCells,
                        info,
                        new float2(position.x, position.z));
                }

                if (hide)
                {
                    toHide.Add(entity);
                }
                else
                {
                    toShow.Add(entity);
                }
            }

            // Apply structural changes only after every fog read is done.
            for (var i = 0; i < toHide.Length; i++)
            {
                if (!entityManager.HasComponent<DisableRendering>(toHide[i]))
                {
                    entityManager.AddComponent<DisableRendering>(toHide[i]);
                }
            }

            for (var i = 0; i < toShow.Length; i++)
            {
                if (entityManager.HasComponent<DisableRendering>(toShow[i]))
                {
                    entityManager.RemoveComponent<DisableRendering>(toShow[i]);
                }
            }

            toHide.Dispose();
            toShow.Dispose();
        }
    }
}
