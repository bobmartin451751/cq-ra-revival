using CQRa.Sim.Core;
using CQRa.Sim.Fog;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class MatchSaveFogPlayModeTests
    {
        [Test]
        public void Fog_CapturesExploredMask_AndRestoresIt()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var fog = entityManager.CreateEntity(
                typeof(FogState),
                typeof(FogTeam),
                typeof(FogGridInfo));
            entityManager.SetComponentData(fog, new FogState { Revision = 0 });
            entityManager.SetComponentData(fog, new FogTeam { Team = 0 });
            entityManager.SetComponentData(fog, new FogGridInfo
            {
                Origin = new float2(-8f, -8f),
                Size = new int2(4, 4),
                CellSize = 4f,
            });
            var cells = entityManager.AddBuffer<FogCell>(fog);
            cells.ResizeUninitialized(16);
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new FogCell { Value = FogMath.Never };
            }

            cells[0] = new FogCell { Value = FogMath.Visible };
            cells[5] = new FogCell { Value = FogMath.Explored };

            try
            {
                var save = MatchSaveSerializer.Capture(entityManager);
                Assert.IsNotNull(save.fog);
                Assert.AreEqual(1, save.fog.Length);
                Assert.AreEqual(0, save.fog[0].team);
                Assert.AreEqual(4, save.fog[0].width);
                Assert.AreEqual(FogMath.Explored, save.fog[0].cells[0],
                    "visible cells are stored as explored");
                Assert.AreEqual(FogMath.Explored, save.fog[0].cells[5]);

                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = new FogCell { Value = FogMath.Never };
                }

                MatchSaveSerializer.Apply(entityManager, save);
                var restored = entityManager.GetBuffer<FogCell>(fog);
                Assert.AreEqual(FogMath.Explored, restored[0].Value);
                Assert.AreEqual(FogMath.Explored, restored[5].Value);
                Assert.AreEqual(FogMath.Never, restored[1].Value);
                Assert.Greater(entityManager.GetComponentData<FogState>(fog).Revision, 0);
            }
            finally
            {
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogState>());
                if (!query.IsEmptyIgnoreFilter)
                {
                    var entities = query.ToEntityArray(Allocator.Temp);
                    entityManager.DestroyEntity(entities);
                    entities.Dispose();
                }
            }
        }
    }
}
