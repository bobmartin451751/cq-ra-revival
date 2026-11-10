using System.Collections;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class VeterancyPlayModeTests
    {
        [UnityTest]
        public IEnumerator Veterancy_RankUp_IncreasesDamageAndHealth()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            try
            {
                entityManager.AddComponentData(entity, new Veterancy { Kills = 2, Rank = 0 });
                entityManager.AddComponentData(entity, new Weapon
                {
                    Damage = 100f,
                    Range = 20f,
                    Cooldown = 1f,
                });
                entityManager.AddComponentData(entity, new Health
                {
                    Max = 200f,
                    Current = 200f,
                });

                yield return new WaitForSeconds(0.2f);

                var veterancy = entityManager.GetComponentData<Veterancy>(entity);
                Assert.AreEqual(1, veterancy.Rank);
                Assert.AreEqual(115f, entityManager.GetComponentData<Weapon>(entity).Damage, 0.01f);

                var health = entityManager.GetComponentData<Health>(entity);
                Assert.AreEqual(220f, health.Max, 0.01f);
                Assert.AreEqual(220f, health.Current, 0.01f);
            }
            finally
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }
            }
        }
    }
}
