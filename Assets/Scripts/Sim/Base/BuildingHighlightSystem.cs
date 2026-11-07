using CQRa.Sim.Units;
using Unity.Entities;
using Unity.Rendering;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Presentation-only highlight swap for selected buildings. Buildings use their own
    /// material array, so they cannot reuse the unit highlight indices.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BuildingHighlightSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (materialMeshInfo, info, state, entity) in SystemAPI
                         .Query<RefRW<MaterialMeshInfo>, RefRO<BuildingInfo>, RefRW<HighlightState>>()
                         .WithAll<BuildingTag>()
                         .WithEntityAccess())
            {
                var isSelected = SystemAPI.IsComponentEnabled<Selected>(entity);
                var flag = isSelected ? (byte)1 : (byte)0;

                if (state.ValueRO.IsSelected == flag)
                {
                    continue;
                }

                state.ValueRW.IsSelected = flag;

                var materialIndex = isSelected
                    ? BuildingFactory.HighlightMaterialIndex
                    : info.ValueRO.Team * BuildingFactory.BuildingKindCount + (int)info.ValueRO.Kind;

                materialMeshInfo.ValueRW = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    materialIndex,
                    (int)info.ValueRO.Kind);
            }
        }
    }
}
