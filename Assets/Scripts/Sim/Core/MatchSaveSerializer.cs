using System;
using System.IO;
using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Fog;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using CQRa.Sim.Bridges;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CQRa.Sim.Core
{
    [Serializable]
    public sealed class SavedUnit
    {
        public string kind;
        public int team;
        public float x;
        public float y;
        public float z;
        public float health;
        public int kills;
        public string[] cargoKinds;
        public float[] cargoHealth;
    }

    [Serializable]
    public sealed class SavedBuilding
    {
        public string kind;
        public int team;
        public float x;
        public float y;
        public float z;
        public float health;
        public float charge;
    }

    [Serializable]
    public sealed class SavedOre
    {
        public float x;
        public float y;
        public float z;
        public int amount;
        public float radius;
    }

    [Serializable]
    public sealed class SavedCrate
    {
        public float x;
        public float y;
        public float z;
        public int reward;
        public int kind;
    }

    [Serializable]
    public sealed class SavedBridge
    {
        public int index;
        public int destroyed;
        public float health;
    }

    [Serializable]
    public sealed class SavedFog
    {
        public int team;
        public int width;
        public int height;
        public byte[] cells;
    }

    [Serializable]
    public sealed class SavedCommander
    {
        public int team;
        public int difficulty;
        public float thinkTimer;
        public int attacking;
    }

    [Serializable]
    public sealed class MatchSaveData
    {
        public int version = 1;
        public string mapId;
        public string playerFaction;
        public string aiFaction;
        public int teamCount;
        public string teamMode;
        public int difficulty;
        public int team0Credits;
        public int team1Credits;
        public int[] researchMasks;
        public SavedUnit[] units;
        public SavedBuilding[] buildings;
        public SavedOre[] ore;
        public SavedCrate[] crates;
        public SavedCommander[] commanders;
        public SavedFog[] fog;
        public SavedBridge[] bridges;
    }

    /// <summary>
    /// Versioned match snapshot capture. Phase B stores serialisable data; applying a
    /// snapshot back into a fresh scene is handled by the load pipeline.
    /// </summary>
    public static class MatchSaveSerializer
    {
        public const int CurrentVersion = 1;

        public static MatchSaveData Capture(EntityManager entityManager)
        {
            var map = MapRuntime.Current;
            var save = new MatchSaveData
            {
                version = CurrentVersion,
                mapId = map?.Id ?? "ash-basin",
                playerFaction = FactionRuntime.Player.ToString(),
                aiFaction = FactionRuntime.Ai.ToString(),
                teamCount = MatchRuntime.TeamCount,
                teamMode = MatchRuntime.Mode.ToString(),
            };

            if (BaseCommands.TryGetEconomy(entityManager, out _, out var economy))
            {
                save.team0Credits = Economy.GetCredits(economy, 0);
                save.team1Credits = Economy.GetCredits(economy, 1);
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<UnitTag>(),
                       ComponentType.ReadOnly<UnitInfo>(),
                       ComponentType.ReadOnly<Health>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            using (var units = query.ToEntityArray(Allocator.Temp))
            {
                save.units = new SavedUnit[units.Length];
                for (var i = 0; i < units.Length; i++)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                    var health = entityManager.GetComponentData<Health>(units[i]);
                    var position = entityManager.GetComponentData<LocalTransform>(units[i]).Position;
                    var kills = entityManager.HasComponent<Veterancy>(units[i])
                        ? entityManager.GetComponentData<Veterancy>(units[i]).Kills
                        : 0;
                    var savedUnit = new SavedUnit
                    {
                        kind = info.Kind.ToString(),
                        team = info.Team,
                        x = position.x,
                        y = position.y,
                        z = position.z,
                        health = health.Current,
                        kills = kills,
                    };

                    if (entityManager.HasBuffer<CargoUnit>(units[i]))
                    {
                        var cargo = entityManager.GetBuffer<CargoUnit>(units[i], true);
                        savedUnit.cargoKinds = new string[cargo.Length];
                        savedUnit.cargoHealth = new float[cargo.Length];
                        for (var c = 0; c < cargo.Length; c++)
                        {
                            savedUnit.cargoKinds[c] = cargo[c].Kind.ToString();
                            savedUnit.cargoHealth[c] = cargo[c].Health;
                        }
                    }

                    save.units[i] = savedUnit;                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<BuildingTag>(),
                       ComponentType.ReadOnly<BuildingInfo>(),
                       ComponentType.ReadOnly<Health>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            using (var buildings = query.ToEntityArray(Allocator.Temp))
            {
                save.buildings = new SavedBuilding[buildings.Length];
                for (var i = 0; i < buildings.Length; i++)
                {
                    var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                    var health = entityManager.GetComponentData<Health>(buildings[i]);
                    var position = entityManager.GetComponentData<LocalTransform>(buildings[i]).Position;
                    save.buildings[i] = new SavedBuilding
                    {
                        kind = info.Kind.ToString(),
                        team = info.Team,
                        x = position.x,
                        y = position.y,
                        z = position.z,
                        health = health.Current,
                        charge = entityManager.HasComponent<SuperweaponState>(buildings[i])
                            ? entityManager.GetComponentData<SuperweaponState>(buildings[i]).Charge
                            : 0f,
                    };
                }
            }

            save.researchMasks = new int[ResearchState.TeamCount];
            if (ResearchQuery.TryGet(entityManager, out _, out var research))
            {
                for (var team = 0; team < ResearchState.TeamCount; team++)
                {
                    save.researchMasks[team] = research.MaskFor(team);
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<OreNode>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            using (var ore = query.ToEntityArray(Allocator.Temp))
            {
                save.ore = new SavedOre[ore.Length];
                for (var i = 0; i < ore.Length; i++)
                {
                    var node = entityManager.GetComponentData<OreNode>(ore[i]);
                    var p = entityManager.GetComponentData<LocalTransform>(ore[i]).Position;
                    save.ore[i] = new SavedOre
                    {
                        x = p.x,
                        y = p.y,
                        z = p.z,
                        amount = node.Amount,
                        radius = node.Radius,
                    };
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<Crate>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            using (var crates = query.ToEntityArray(Allocator.Temp))
            {
                save.crates = new SavedCrate[crates.Length];
                for (var i = 0; i < crates.Length; i++)
                {
                    var crate = entityManager.GetComponentData<Crate>(crates[i]);
                    var p = entityManager.GetComponentData<LocalTransform>(crates[i]).Position;
                    save.crates[i] = new SavedCrate
                    {
                        x = p.x,
                        y = p.y,
                        z = p.z,
                        reward = crate.Reward,
                        kind = crate.Kind,
                    };
                }
            }

            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>()))
            using (var commanders = query.ToEntityArray(Allocator.Temp))
            {
                save.commanders = new SavedCommander[commanders.Length];
                for (var i = 0; i < commanders.Length; i++)
                {
                    var commander = entityManager.GetComponentData<AiCommander>(commanders[i]);
                    save.commanders[i] = new SavedCommander
                    {
                        team = commander.Team,
                        difficulty = (int)commander.Difficulty,
                        thinkTimer = commander.ThinkTimer,
                        attacking = commander.Attacking,
                    };
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<FogState>(),
                       ComponentType.ReadOnly<FogTeam>(),
                       ComponentType.ReadOnly<FogGridInfo>()))
            using (var fogs = query.ToEntityArray(Allocator.Temp))
            {
                save.fog = new SavedFog[fogs.Length];
                for (var i = 0; i < fogs.Length; i++)
                {
                    var info = entityManager.GetComponentData<FogGridInfo>(fogs[i]);
                    var cells = entityManager.GetBuffer<FogCell>(fogs[i], true);
                    var saved = new SavedFog
                    {
                        team = entityManager.GetComponentData<FogTeam>(fogs[i]).Team,
                        width = info.Size.x,
                        height = info.Size.y,
                        cells = new byte[cells.Length],
                    };

                    // Visible is transient; snapshots persist the explored mask only.
                    for (var c = 0; c < cells.Length; c++)
                    {
                        saved.cells[c] = cells[c].Value == FogMath.Visible
                            ? FogMath.Explored
                            : cells[c].Value;
                    }

                    save.fog[i] = saved;
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<BridgeTag>(),
                       ComponentType.ReadOnly<Bridge>(),
                       ComponentType.ReadOnly<Health>()))
            using (var bridges = query.ToEntityArray(Allocator.Temp))
            {
                save.bridges = new SavedBridge[bridges.Length];
                for (var i = 0; i < bridges.Length; i++)
                {
                    var bridge = entityManager.GetComponentData<Bridge>(bridges[i]);
                    save.bridges[i] = new SavedBridge
                    {
                        index = bridge.Index,
                        destroyed = bridge.Destroyed,
                        health = entityManager.GetComponentData<Health>(bridges[i]).Current,
                    };
                }
            }

            return save;
        }

        public static string ToJson(MatchSaveData save)
        {
            return JsonUtility.ToJson(save, true);
        }

        public static MatchSaveData FromJson(string json)
        {
            return JsonUtility.FromJson<MatchSaveData>(json);
        }

        public static void SaveToFile(EntityManager entityManager, string path)
        {
            File.WriteAllText(path, ToJson(Capture(entityManager)));
        }

        public static MatchSaveData LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var save = FromJson(File.ReadAllText(path));
            return save != null && save.version == CurrentVersion ? save : null;
        }

        public static void Apply(EntityManager entityManager, MatchSaveData save)
        {
            if (save == null)
            {
                return;
            }

            using (var units = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitTag>()))
            {
                if (!units.IsEmptyIgnoreFilter)
                {
                    entityManager.DestroyEntity(units.ToEntityArray(Allocator.Temp));
                }
            }

            using (var buildings = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingTag>()))
            {
                if (!buildings.IsEmptyIgnoreFilter)
                {
                    entityManager.DestroyEntity(buildings.ToEntityArray(Allocator.Temp));
                }
            }

            if (BaseCommands.TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                Economy.SetCredits(ref economy, 0, save.team0Credits);
                Economy.SetCredits(ref economy, 1, save.team1Credits);
                entityManager.SetComponentData(economyEntity, economy);
            }

            if (save.buildings != null)
            {
                for (var i = 0; i < save.buildings.Length; i++)
                {
                    var saved = save.buildings[i];
                    if (!Enum.TryParse(saved.kind, true, out BuildingKind kind))
                    {
                        continue;
                    }

                    var building = BuildingFactory.SpawnBuilding(
                        entityManager, saved.team, kind, new float3(saved.x, saved.y, saved.z));
                    var health = entityManager.GetComponentData<Health>(building);
                    health.Current = math.clamp(saved.health, 1f, health.Max);
                    entityManager.SetComponentData(building, health);

                    if (saved.charge > 0f &&
                        entityManager.HasComponent<SuperweaponState>(building))
                    {
                        var state = entityManager.GetComponentData<SuperweaponState>(building);
                        state.Charge = math.clamp(saved.charge, 0f, state.ChargeSeconds);
                        state.Ready = state.Charge >= state.ChargeSeconds - 0.0001f
                            ? (byte)1
                            : (byte)0;
                        entityManager.SetComponentData(building, state);
                    }

                    var spec = BuildCatalog.Get(kind);
                    NavGridQuery.BlockCircle(
                        entityManager,
                        new float2(saved.x, saved.z),
                        spec.NavRadius);
                }
            }

            if (save.units != null)
            {
                for (var i = 0; i < save.units.Length; i++)
                {
                    var saved = save.units[i];
                    if (!Enum.TryParse(saved.kind, true, out UnitKind kind))
                    {
                        continue;
                    }

                    var unit = UnitFactory.SpawnDefault(
                        entityManager, new float3(saved.x, saved.y, saved.z), 0f, kind, saved.team);
                    var health = entityManager.GetComponentData<Health>(unit);
                    health.Current = math.clamp(saved.health, 1f, health.Max);
                    entityManager.SetComponentData(unit, health);

                    if (saved.kills > 0 && entityManager.HasComponent<Veterancy>(unit))
                    {
                        entityManager.SetComponentData(unit, new Veterancy
                        {
                            Kills = saved.kills,
                            Rank = VeterancyMath.RankForKills(saved.kills),
                        });
                    }

                    if (saved.cargoKinds != null &&
                        saved.cargoHealth != null &&
                        entityManager.HasBuffer<CargoUnit>(unit))
                    {
                        var cargo = entityManager.GetBuffer<CargoUnit>(unit);
                        cargo.Clear();
                        for (var c = 0; c < saved.cargoKinds.Length && c < saved.cargoHealth.Length; c++)
                        {
                            if (Enum.TryParse(saved.cargoKinds[c], true, out UnitKind cargoKind))
                            {
                                cargo.Add(new CargoUnit
                                {
                                    Kind = cargoKind,
                                    Health = saved.cargoHealth[c],
                                });
                            }
                        }
                    }
                }
            }

            if (save.ore != null)
            {
                using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<OreNode>()))
                {
                    if (!existing.IsEmptyIgnoreFilter)
                    {
                        entityManager.DestroyEntity(existing.ToEntityArray(Allocator.Temp));
                    }
                }

                for (var i = 0; i < save.ore.Length; i++)
                {
                    var ore = save.ore[i];
                    BuildingFactory.SpawnOre(
                        entityManager,
                        new float3(ore.x, ore.y, ore.z),
                        ore.amount,
                        ore.radius);
                }
            }

            if (save.crates != null)
            {
                using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Crate>()))
                {
                    if (!existing.IsEmptyIgnoreFilter)
                    {
                        entityManager.DestroyEntity(existing.ToEntityArray(Allocator.Temp));
                    }
                }

                for (var i = 0; i < save.crates.Length; i++)
                {
                    var crate = save.crates[i];
                    BuildingFactory.SpawnCrate(
                        entityManager,
                        new float3(crate.x, crate.y, crate.z),
                        crate.reward,
                        (byte)crate.kind);
                }
            }

            if (save.researchMasks != null)
            {
                var researchEntity = ResearchQuery.GetOrCreate(entityManager);
                var research = entityManager.GetComponentData<ResearchState>(researchEntity);
                for (var team = 0; team < ResearchState.TeamCount && team < save.researchMasks.Length; team++)
                {
                    research.SetMask(team, save.researchMasks[team]);
                }
                entityManager.SetComponentData(researchEntity, research);
            }

            if (save.commanders != null)
            {
                SkirmishCommands.ClearCommanders(entityManager);
                for (var i = 0; i < save.commanders.Length; i++)
                {
                    var commander = save.commanders[i];
                    var entity = SkirmishCommands.CreateCommander(
                        entityManager,
                        commander.team,
                        (AiDifficulty)commander.difficulty);
                    var data = entityManager.GetComponentData<AiCommander>(entity);
                    data.ThinkTimer = commander.thinkTimer;
                    data.Attacking = (byte)commander.attacking;
                    entityManager.SetComponentData(entity, data);
                }
            }

            BridgeSystem.EnsureBridges(entityManager);
            if (save.bridges != null && save.bridges.Length > 0)
            {
                using (var query = entityManager.CreateEntityQuery(
                           ComponentType.ReadOnly<BridgeTag>(),
                           ComponentType.ReadOnly<Bridge>(),
                           ComponentType.ReadOnly<Health>()))
                using (var bridges = query.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < save.bridges.Length; i++)
                    {
                        var saved = save.bridges[i];
                        for (var b = 0; b < bridges.Length; b++)
                        {
                            var bridge = entityManager.GetComponentData<Bridge>(bridges[b]);
                            if (bridge.Index != saved.index)
                            {
                                continue;
                            }

                            if (saved.destroyed != 0)
                            {
                                BridgeSystem.DestroyBridge(entityManager, bridges[b]);
                                continue;
                            }

                            var health = entityManager.GetComponentData<Health>(bridges[b]);
                            health.Current = math.clamp(saved.health, 1f, health.Max);
                            entityManager.SetComponentData(bridges[b], health);
                            break;
                        }
                    }
                }
            }

            if (save.fog != null)
            {
                for (var i = 0; i < save.fog.Length; i++)
                {
                    var saved = save.fog[i];
                    if (saved.cells == null ||
                        !FogQuery.TryGet(entityManager, saved.team, out var fogEntity, out var info))
                    {
                        continue;
                    }

                    var cells = entityManager.GetBuffer<FogCell>(fogEntity);
                    if (cells.Length != saved.cells.Length ||
                        info.Size.x != saved.width ||
                        info.Size.y != saved.height)
                    {
                        continue;
                    }

                    for (var c = 0; c < cells.Length; c++)
                    {
                        cells[c] = new FogCell { Value = saved.cells[c] };
                    }

                    var state = entityManager.GetComponentData<FogState>(fogEntity);
                    state.Revision++;
                    entityManager.SetComponentData(fogEntity, state);
                }
            }

            NavGridQuery.InvalidateFlowFields(entityManager);
        }
    }
}
