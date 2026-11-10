using System;
using CQRa.Sim.Base;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CQRa.Sim.Content
{
    /// <summary>
    /// Compiled unit/building definitions. JSON is loaded at scene start and compiled into
    /// a BlobAsset; static accessors expose the values to gameplay systems. Defaults match
    /// the pre-pipeline hardcoded values and are used by tests or if no JSON is present.
    /// </summary>
    public struct UnitDefinition
    {
        public UnitKind Kind;
        public int Cost;
        public float BuildSeconds;
        public BuildingKind Producer;
        public float MaxHealth;
        public float Armor;
        public float Damage;
        public float Range;
        public float Cooldown;
        public float AcquireRange;
        public float Speed;
        public float Radius;
        public float Vision;
        public FixedString64Bytes Label;
    }

    public struct BuildingDefinition
    {
        public BuildingKind Kind;
        public int Cost;
        public int PowerProduced;
        public int PowerUsed;
        public float Radius;
        public float NavRadius;
        public float Health;
        public float Vision;
    }

    public struct ContentBlob
    {
        public BlobArray<UnitDefinition> Units;
        public BlobArray<BuildingDefinition> Buildings;
    }

    [Serializable]
    public sealed class UnitDto
    {
        public string kind;
        public int cost;
        public float buildSeconds;
        public string producer;
        public float maxHealth;
        public float armor;
        public float damage;
        public float range;
        public float cooldown;
        public float acquireRange;
        public float speed;
        public float radius;
        public float vision;
        public string label;
    }

    [Serializable]
    public sealed class BuildingDto
    {
        public string kind;
        public int cost;
        public int powerProduced;
        public int powerUsed;
        public float radius;
        public float navRadius;
        public float health;
        public float vision;
    }

    [Serializable]
    public sealed class ContentDto
    {
        public UnitDto[] units;
        public BuildingDto[] buildings;
    }

    public static class ContentDatabase
    {
        private static readonly UnitDefinition[] DefaultUnits =
        {
            new UnitDefinition
            {
                Kind = UnitKind.Infantry, Cost = 100, BuildSeconds = 4f, Producer = BuildingKind.Barracks,
                MaxHealth = 50f, Armor = 0f, Damage = 5f, Range = 13f, Cooldown = 0.45f,
                AcquireRange = 16f, Speed = 4f, Radius = 0.6f, Vision = 32f, Label = "Infantry",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Tank, Cost = 350, BuildSeconds = 8f, Producer = BuildingKind.WarFactory,
                MaxHealth = 260f, Armor = 0.35f, Damage = 45f, Range = 24f, Cooldown = 1.6f,
                AcquireRange = 28f, Speed = 9f, Radius = 3.4f, Vision = 40f, Label = "Tank",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Harvester, Cost = 500, BuildSeconds = 10f, Producer = BuildingKind.WarFactory,
                MaxHealth = 220f, Armor = 0.25f, Speed = 6f, Radius = 3.8f, Vision = 22f,
                Label = "Harvester",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Mcv, Cost = 1000, BuildSeconds = 12f, Producer = BuildingKind.WarFactory,
                MaxHealth = 420f, Armor = 0.40f, Speed = 4.5f, Radius = 4.2f, Vision = 26f,
                Label = "MCV",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Gunship, Cost = 900, BuildSeconds = 10f, Producer = BuildingKind.WarFactory,
                MaxHealth = 180f, Armor = 0.20f, Damage = 34f, Range = 22f, Cooldown = 1.1f,
                AcquireRange = 26f, Speed = 16f, Radius = 2.6f, Vision = 44f, Label = "Gunship",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Engineer, Cost = 150, BuildSeconds = 4f, Producer = BuildingKind.Barracks,
                MaxHealth = 40f, Armor = 0f, Speed = 5f, Radius = 0.6f, Vision = 30f,
                Label = "Engineer",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Thumper, Cost = 600, BuildSeconds = 9f, Producer = BuildingKind.WarFactory,
                MaxHealth = 200f, Armor = 0.30f, Damage = 70f, Range = 34f, Cooldown = 3f,
                AcquireRange = 36f, Speed = 4f, Radius = 3.2f, Vision = 38f, Label = "Thumper",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Mantis, Cost = 250, BuildSeconds = 6f, Producer = BuildingKind.Barracks,
                MaxHealth = 45f, Armor = 0f, Damage = 7f, Range = 12f, Cooldown = 0.35f,
                AcquireRange = 15f, Speed = 6.5f, Radius = 0.6f, Vision = 34f, Label = "Mantis",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Skyfall, Cost = 1000, BuildSeconds = 11f, Producer = BuildingKind.WarFactory,
                MaxHealth = 150f, Armor = 0.15f, Damage = 55f, Range = 26f, Cooldown = 1f,
                AcquireRange = 30f, Speed = 20f, Radius = 2.4f, Vision = 50f, Label = "Skyfall",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Rig, Cost = 350, BuildSeconds = 7f, Producer = BuildingKind.WarFactory,
                MaxHealth = 260f, Armor = 0.30f, Speed = 6f, Radius = 3.2f, Vision = 26f,
                Label = "Rig",
            },
            new UnitDefinition
            {
                Kind = UnitKind.Sower, Cost = 300, BuildSeconds = 6f, Producer = BuildingKind.WarFactory,
                MaxHealth = 180f, Armor = 0.25f, Speed = 7f, Radius = 3f, Vision = 28f,
                Label = "Sower",
            },
        };

        private static readonly BuildingDefinition[] DefaultBuildings =
        {
            new BuildingDefinition { Kind = BuildingKind.ConstructionYard, Cost = 0, PowerProduced = 10, Radius = 9f, NavRadius = 9f, Health = 1200f, Vision = 38f },
            new BuildingDefinition { Kind = BuildingKind.PowerPlant, Cost = 300, PowerProduced = 100, Radius = 6f, NavRadius = 6f, Health = 600f, Vision = 24f },
            new BuildingDefinition { Kind = BuildingKind.Refinery, Cost = 600, PowerUsed = 40, Radius = 9f, NavRadius = 9f, Health = 900f, Vision = 28f },
            new BuildingDefinition { Kind = BuildingKind.Barracks, Cost = 400, PowerUsed = 20, Radius = 7f, NavRadius = 7f, Health = 500f, Vision = 28f },
            new BuildingDefinition { Kind = BuildingKind.WarFactory, Cost = 900, PowerUsed = 60, Radius = 11f, NavRadius = 11f, Health = 800f, Vision = 32f },
            new BuildingDefinition { Kind = BuildingKind.GunTower, Cost = 500, PowerUsed = 20, Radius = 4f, NavRadius = 4f, Health = 450f, Vision = 24f },
            new BuildingDefinition { Kind = BuildingKind.Wall, Cost = 50, Radius = 2.2f, NavRadius = 2.2f, Health = 250f, Vision = 6f },
        };

        private static UnitDefinition[] _units = (UnitDefinition[])DefaultUnits.Clone();
        private static BuildingDefinition[] _buildings = (BuildingDefinition[])DefaultBuildings.Clone();
        private static BlobAssetReference<ContentBlob> _blob;

        public static bool HasLoaded { get; private set; }

        public static UnitDefinition Unit(UnitKind kind)
        {
            for (var i = 0; i < _units.Length; i++)
            {
                if (_units[i].Kind == kind)
                {
                    return _units[i];
                }
            }

            return DefaultUnits[0];
        }

        public static BuildingDefinition Building(BuildingKind kind)
        {
            for (var i = 0; i < _buildings.Length; i++)
            {
                if (_buildings[i].Kind == kind)
                {
                    return _buildings[i];
                }
            }

            return DefaultBuildings[0];
        }

        public static bool LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            ContentDto dto;
            try
            {
                dto = JsonUtility.FromJson<ContentDto>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Content] JSON parse failed: {e.Message}");
                return false;
            }

            if (dto == null)
            {
                return false;
            }

            var units = (UnitDefinition[])DefaultUnits.Clone();
            var buildings = (BuildingDefinition[])DefaultBuildings.Clone();

            if (dto.units != null)
            {
                foreach (var source in dto.units)
                {
                    if (!Enum.TryParse(source.kind, true, out UnitKind kind))
                    {
                        Debug.LogWarning($"[Content] unknown unit kind '{source.kind}'");
                        continue;
                    }

                    var index = FindUnitIndex(units, kind);
                    if (index < 0)
                    {
                        continue;
                    }

                    var unit = units[index];
                    unit.Cost = source.cost;
                    unit.BuildSeconds = source.buildSeconds;
                    unit.MaxHealth = source.maxHealth;
                    unit.Armor = source.armor;
                    unit.Damage = source.damage;
                    unit.Range = source.range;
                    unit.Cooldown = source.cooldown;
                    unit.AcquireRange = source.acquireRange;
                    unit.Speed = source.speed;
                    unit.Radius = source.radius;
                    unit.Vision = source.vision;
                    unit.Label = source.label ?? kind.ToString();

                    if (Enum.TryParse(source.producer, true, out BuildingKind producer))
                    {
                        unit.Producer = producer;
                    }

                    units[index] = unit;
                }
            }

            if (dto.buildings != null)
            {
                foreach (var source in dto.buildings)
                {
                    if (!Enum.TryParse(source.kind, true, out BuildingKind kind))
                    {
                        Debug.LogWarning($"[Content] unknown building kind '{source.kind}'");
                        continue;
                    }

                    var index = FindBuildingIndex(buildings, kind);
                    if (index < 0)
                    {
                        continue;
                    }

                    buildings[index] = new BuildingDefinition
                    {
                        Kind = kind,
                        Cost = source.cost,
                        PowerProduced = source.powerProduced,
                        PowerUsed = source.powerUsed,
                        Radius = source.radius,
                        NavRadius = source.navRadius,
                        Health = source.health,
                        Vision = source.vision,
                    };
                }
            }

            _units = units;
            _buildings = buildings;
            CompileBlob();
            HasLoaded = true;
            return true;
        }

        public static void ResetToDefaults()
        {
            _units = (UnitDefinition[])DefaultUnits.Clone();
            _buildings = (BuildingDefinition[])DefaultBuildings.Clone();
            HasLoaded = false;

            if (_blob.IsCreated)
            {
                _blob.Dispose();
            }
        }

        private static void CompileBlob()
        {
            if (_blob.IsCreated)
            {
                _blob.Dispose();
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ContentBlob>();

            var units = builder.Allocate(ref root.Units, _units.Length);
            for (var i = 0; i < _units.Length; i++)
            {
                units[i] = _units[i];
            }

            var buildings = builder.Allocate(ref root.Buildings, _buildings.Length);
            for (var i = 0; i < _buildings.Length; i++)
            {
                buildings[i] = _buildings[i];
            }

            _blob = builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
        }

        private static int FindUnitIndex(UnitDefinition[] units, UnitKind kind)
        {
            for (var i = 0; i < units.Length; i++)
            {
                if (units[i].Kind == kind)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindBuildingIndex(BuildingDefinition[] buildings, BuildingKind kind)
        {
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Kind == kind)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
