using Unity.Entities;
using Unity.Rendering;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Presentation-only system: swaps selected units onto the highlight material.
    /// Runs in the presentation group so a click is visible on the same frame.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SelectionHighlightSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (materialMeshInfo, info, state, entity) in SystemAPI
                         .Query<RefRW<MaterialMeshInfo>, RefRO<UnitInfo>, RefRW<HighlightState>>()
                         .WithAll<UnitTag>()
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
                    ? UnitMaterials.HighlightIndex
                    : UnitMaterials.BaseIndex(info.ValueRO.Team, info.ValueRO.Kind);

                materialMeshInfo.ValueRW = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    materialIndex,
                    (int)info.ValueRO.Kind);
            }
        }
    }
}
