using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TruckLib.Models;

namespace TruckLib.Models.Tests
{
    public class ModelTest
    {
        private byte[] pmdBuffer;
        private byte[] pmgBuffer;

        public ModelTest()
        {
            pmdBuffer = File.ReadAllBytes("Data/ModelTest/admin_04_blkw.pmd");
            pmgBuffer = File.ReadAllBytes("Data/ModelTest/admin_04_blkw.pmg");
        }

        [Fact]
        public void SerializePmd()
        {
            var model = Model.Load(pmdBuffer, pmgBuffer);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            model.SerializePmd(w);

            Assert.Equal(pmdBuffer, ms.ToArray());
        }

        [Fact]
        public void SerializePmg()
        {
            var model = Model.Load(pmdBuffer, pmgBuffer);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            model.SerializePmg(w);

            Assert.Equal(pmgBuffer, ms.ToArray());
        }
    }
}
