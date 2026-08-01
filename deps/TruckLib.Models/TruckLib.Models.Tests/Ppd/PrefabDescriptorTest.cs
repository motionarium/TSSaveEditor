using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib.Models.Ppd;

namespace TruckLib.Models.Tests.Ppd
{
    public class PrefabDescriptorTest
    {
        [Fact]
        public void ParseCrossing18()
        {
            var ppd = PrefabDescriptor.Open("Data/PrefabDescriptorTest/fr_r1_x_r1_t_narrow_tmpl.ppd.18");

            Assert.Equal(0x18u, ppd.Version);

            // Test nodes
            Assert.Equal(3, ppd.Nodes.Count);
            Assert.Equal(new Vector3(-18f, 0, 0), ppd.Nodes[0].Position);
            Assert.Equal(1f, ppd.Nodes[0].Direction.X, 0.0001f);
            Assert.Equal(0f, ppd.Nodes[0].Direction.Y, 0.0001f);
            Assert.Equal(0f, ppd.Nodes[0].Direction.Z, 0.0001f);

            // Test terrain points
            Assert.Equal(5, ppd.Nodes[0].TerrainPoints.Count);
            Assert.Equal(3, ppd.Nodes[0].TerrainPoints[0].Count);
            Assert.Equal(3, ppd.Nodes[0].TerrainPoints[1].Count);
            Assert.Equal(5, ppd.Nodes[0].TerrainPoints[2].Count);
            Assert.Equal(5, ppd.Nodes[0].TerrainPoints[3].Count);
            Assert.Equal(3, ppd.Nodes[0].TerrainPoints[4].Count);

            Assert.Equal(5, ppd.Nodes[1].TerrainPoints.Count);
            Assert.Equal(3, ppd.Nodes[1].TerrainPoints[0].Count);
            Assert.Equal(3, ppd.Nodes[1].TerrainPoints[1].Count);
            Assert.Equal(5, ppd.Nodes[1].TerrainPoints[2].Count);
            Assert.Equal(5, ppd.Nodes[1].TerrainPoints[3].Count);
            Assert.Equal(3, ppd.Nodes[1].TerrainPoints[4].Count);

            Assert.Equal(5, ppd.Nodes[2].TerrainPoints.Count);
            Assert.Equal(3, ppd.Nodes[2].TerrainPoints[0].Count);
            Assert.Equal(3, ppd.Nodes[2].TerrainPoints[1].Count);
            Assert.Equal(2, ppd.Nodes[2].TerrainPoints[2].Count);
            Assert.Equal(2, ppd.Nodes[2].TerrainPoints[3].Count);
            Assert.Equal(3, ppd.Nodes[2].TerrainPoints[4].Count);

            // Test spawn points
            Assert.Empty(ppd.SpawnPoints);

            // Test semaphores
            Assert.Equal(3, ppd.Semaphores.Count);
            Assert.Equal("cr_1x1", ppd.Semaphores[0].Profile);
            Assert.Equal("cr_1x1", ppd.Semaphores[1].Profile);
            Assert.Equal("cr_1x1", ppd.Semaphores[2].Profile);

            // TODO test all the other stuff.
            // I don't know a whole lot about how their model files work,
            // so I'm focusing on the parts I need for mapping
        }

        [Fact]
        public void ParseCrossing19()
        {
            var ppd = PrefabDescriptor.Open("Data/PrefabDescriptorTest/fr_r1_x_r1_t_narrow_tmpl.ppd.19");

            Assert.Equal(0x19u, ppd.Version);

            // Test nodes
            Assert.Equal(3, ppd.Nodes.Count);
            Assert.Equal(new Vector3(-18f, 0, 0), ppd.Nodes[0].Position);
            Assert.Equal(1f, ppd.Nodes[0].Direction.X, 0.0001f);
            Assert.Equal(0f, ppd.Nodes[0].Direction.Y, 0.0001f);
            Assert.Equal(0f, ppd.Nodes[0].Direction.Z, 0.0001f);

            // Test semaphores
            Assert.Equal(3, ppd.Semaphores.Count);
            Assert.Equal("cr_1x1", ppd.Semaphores[0].Profile);
            Assert.Equal("cr_1x1", ppd.Semaphores[1].Profile);
            Assert.Equal("cr_1x1", ppd.Semaphores[2].Profile);
        }

        [Fact]
        public void ParseService18()
        {
            var ppd = PrefabDescriptor.Open("Data/PrefabDescriptorTest/gas_plaza_01_ger.ppd.18");

            Assert.Equal(0x18u, ppd.Version);

            Assert.Equal(3, ppd.Nodes.Count);

            // Test terrain points
            Assert.Single(ppd.Nodes[0].TerrainPoints);
            Assert.Equal(22, ppd.Nodes[0].TerrainPoints[0].Count);

            Assert.Single(ppd.Nodes[1].TerrainPoints);
            Assert.Equal(8, ppd.Nodes[1].TerrainPoints[0].Count);

            Assert.Single(ppd.Nodes[2].TerrainPoints);
            Assert.Empty(ppd.Nodes[2].TerrainPoints[0]);

            // Test spawn points
            Assert.Equal(2, ppd.SpawnPoints.Count);
            Assert.Equal(SpawnPointType.GasStation, ppd.SpawnPoints[0].Type);
            Assert.Equal(SpawnPointType.GasStation, ppd.SpawnPoints[1].Type);
        }

        [Fact]
        public void ParseService19()
        {
            var ppd = PrefabDescriptor.Open("Data/PrefabDescriptorTest/gas_plaza_01_ger.ppd.19");

            Assert.Equal(0x19u, ppd.Version);

            Assert.Equal(3, ppd.Nodes.Count);

            // Test terrain points
            Assert.Single(ppd.Nodes[0].TerrainPoints);
            Assert.Equal(22, ppd.Nodes[0].TerrainPoints[0].Count);

            Assert.Single(ppd.Nodes[1].TerrainPoints);
            Assert.Equal(8, ppd.Nodes[1].TerrainPoints[0].Count);

            Assert.Single(ppd.Nodes[2].TerrainPoints);
            Assert.Empty(ppd.Nodes[2].TerrainPoints[0]);

            // Test spawn points
            Assert.Equal(2, ppd.SpawnPoints.Count);
            Assert.Equal(SpawnPointType.GasStation, ppd.SpawnPoints[0].Type);
            Assert.Equal(SpawnPointType.GasStation, ppd.SpawnPoints[1].Type);
        }
    }
}
