using System.Collections.Generic;
using System.Text;
using CQRa.Sim.Content;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase81Tests
    {
        [Test]
        public void EveryMapHasAUniqueLayout()
        {
            var signatures = new HashSet<string>();
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                var signature = Signature(map);
                Assert.IsTrue(
                    signatures.Add(signature),
                    $"map {map.Id} reuses another map's layout");
            }

            Assert.AreEqual(MapCatalog.Count, signatures.Count);
        }

        private static string Signature(MapDefinition map)
        {
            var builder = new StringBuilder(256);
            builder.Append(map.WorldSize).Append('|');
            var spawnCount = map.Spawns != null && map.Spawns.Length > 0
                ? map.Spawns.Length
                : 2;
            for (var i = 0; i < spawnCount; i++)
            {
                var spawn = map.SpawnFor(i);
                builder.Append(spawn.Position.x.ToString("0.###"))
                    .Append(',')
                    .Append(spawn.Position.y.ToString("0.###"))
                    .Append(';');
            }

            var bodies = map.WaterBodies ?? System.Array.Empty<WaterBody>();
            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                builder.Append((int)body.Kind).Append(':');
                if (body.Kind == WaterShapeKind.Circle)
                {
                    builder.Append(body.Centre.x.ToString("0.###"))
                        .Append(',')
                        .Append(body.Centre.y.ToString("0.###"))
                        .Append(',')
                        .Append(body.Radius.ToString("0.###"));
                }
                else if (body.Points != null)
                {
                    for (var p = 0; p < body.Points.Length; p++)
                    {
                        builder.Append(body.Points[p].x.ToString("0.###"))
                            .Append(',')
                            .Append(body.Points[p].y.ToString("0.###"))
                            .Append(';');
                    }
                }

                builder.Append('|');
            }

            for (var i = 0; i < (map.Obstacles?.Length ?? 0); i++)
            {
                builder.Append(map.Obstacles[i].Centre.x.ToString("0.###"))
                    .Append(',')
                    .Append(map.Obstacles[i].Centre.y.ToString("0.###"))
                    .Append(';');
            }

            builder.Append('|');
            for (var i = 0; i < (map.OrePositions?.Length ?? 0); i++)
            {
                builder.Append(map.OrePositions[i].x.ToString("0.###"))
                    .Append(',')
                    .Append(map.OrePositions[i].y.ToString("0.###"))
                    .Append(';');
            }

            return builder.ToString();
        }
    }
}
