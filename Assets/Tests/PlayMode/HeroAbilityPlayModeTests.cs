using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class HeroAbilityPlayModeTests
    {
        [Test]
        public void AshfallOverdrive_BuffsAllies_AndStartsCooldown()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Verdant });

            var hero = CreateHero(entityManager, 10, new float3(0f, 0f, 0f));
            var ally = UnitFactory.SpawnDefault(
                entityManager, new float3(8f, 0f, 0f), 0f, UnitKind.Tank, 0);

            try
            {
                Assert.IsTrue(HeroAbilityCommands.TryActivate(entityManager, hero, out var failure), failure);
                Assert.IsTrue(entityManager.HasComponent<WeaponBuff>(ally),
                    "allies inside the radius gain the fire-rate buff");

                var ability = entityManager.GetComponentData<HeroAbility>(hero);
                Assert.Greater(ability.CooldownRemaining, 0f);

                Assert.IsFalse(HeroAbilityCommands.TryActivate(entityManager, hero, out _),
                    "the ability must be on cooldown");
            }
            finally
            {
                DestroyAll<UnitTag>(entityManager);
                FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Ashfall });
            }
        }

        [Test]
        public void VerdantRegrowth_HealsAndMeridianAegis_Shields()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            FactionRuntime.SetAll(new[] { FactionId.Verdant, FactionId.Ashfall });
            var verdantHero = CreateHero(entityManager, 6, float3.zero);
            var wounded = UnitFactory.SpawnDefault(
                entityManager, new float3(5f, 0f, 0f), 0f, UnitKind.Infantry, 0);
            var health = entityManager.GetComponentData<Health>(wounded);
            health.Current = 10f;
            entityManager.SetComponentData(wounded, health);

            try
            {
                Assert.IsTrue(HeroAbilityCommands.TryActivate(
                    entityManager, verdantHero, out var failure), failure);
                Assert.Greater(
                    entityManager.GetComponentData<Health>(wounded).Current, 10f);
                Assert.IsTrue(entityManager.HasComponent<RegenBuff>(wounded));

                DestroyAll<UnitTag>(entityManager);

                FactionRuntime.SetAll(new[] { FactionId.Meridian, FactionId.Ashfall });
                var meridianHero = CreateHero(entityManager, 8, float3.zero);
                var friend = UnitFactory.SpawnDefault(
                    entityManager, new float3(4f, 0f, 0f), 0f, UnitKind.Infantry, 0);

                Assert.IsTrue(HeroAbilityCommands.TryActivate(
                    entityManager, meridianHero, out failure), failure);
                Assert.IsTrue(entityManager.HasComponent<Shield>(friend));
                Assert.GreaterOrEqual(
                    entityManager.GetComponentData<Shield>(friend).Current, 80f);
            }
            finally
            {
                DestroyAll<UnitTag>(entityManager);
                FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Ashfall });
            }
        }

        private static Entity CreateHero(EntityManager entityManager, int kills, float3 position)
        {
            var hero = UnitFactory.SpawnDefault(
                entityManager, position, 0f, UnitKind.Infantry, 0);
            entityManager.AddComponentData(hero, new Hero { Flag = 1 });
            entityManager.AddComponentData(hero, new HeroAbility
            {
                Cooldown = HeroAbilityCatalog.Cooldown,
                CooldownRemaining = 0f,
            });
            if (entityManager.HasComponent<Veterancy>(hero))
            {
                entityManager.SetComponentData(hero, new Veterancy
                {
                    Kills = kills,
                    Rank = VeterancyMath.RankForKills(kills),
                });
            }

            return hero;
        }

        private static void DestroyAll<T>(EntityManager entityManager) where T : struct, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
            entities.Dispose();
        }
    }
}
