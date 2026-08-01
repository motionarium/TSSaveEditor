using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib.Sii;

namespace TruckLib.Sii.Tests
{
    public class MatParserTest
    {
        [Fact]
        public void DeserializeMat()
        {
            var str = File.ReadAllText("Data/mat_current.mat");
            var mat = MatParser.DeserializeFromString(str);

            Assert.Equal("eut2.dif.spec.mult.dif.iamod.dif.add.env.tsnmap.rfx", mat.Effect);
            Assert.Equal(new Vector2(0.2f, 0.9f), mat.Attributes["fresnel"]);
            Assert.Equal(25f, mat.Attributes["shininess"]);
            Assert.Equal("texture_reflection", mat.Textures[4].Name);
            Assert.Equal("/material/environment/building_reflection/building_ref.tobj", 
                mat.Textures[4].Attributes["source"]);
            Assert.Equal("clamp", mat.Textures[4].Attributes["u_address"]);
        }

        [Fact]
        public void DeserializeLegacyMat()
        {
            var str = File.ReadAllText("Data/mat_legacy.mat");
            var mat = MatParser.DeserializeFromString(str);

            Assert.Equal("eut2.lamp.add.env", mat.Effect);

            Assert.Equal("texture_reflection", mat.Textures[2].Name);
            Assert.Equal("/material/environment/vehicle_reflection.tobj", 
                mat.Textures[2].Attributes["source"]);
        }

        [Fact]
        public void LegacyTextureSourceWithoutArrayIndex()
        {
            var matStr = @"material : ""eut2.sign"" {
                    texture : ""road_ru_118.tobj""
                    texture_name[0] : ""texture_base""
                    diffuse : { 1 , 1 , 1 }
                    specular : { 0 , 0 , 0 }
                    shininess : 4
                    add_ambient : 0
                }";
            var mat = MatParser.DeserializeFromString(matStr);

            Assert.Single(mat.Textures);
            Assert.Single(mat.Textures[0].Attributes);
            Assert.Equal("texture_base", mat.Textures[0].Name);
            Assert.Equal("road_ru_118.tobj", mat.Textures[0].Attributes["source"]);
        }

        [Fact]
        public void LegacyTextureNameWithoutArrayIndex()
        {
            var matStr = @"material : ""eut2.dif.spec"" {
                texture[0] : ""/model/road/road_gravel1.tobj""
                texture_name : ""texture_base""
                substance : ""road_dirt""
                aux[0] : { 3, 3 }
                
                specular : { 0.0, 0.0, 0.0 }
                shininess : 5
            }";
            var mat = MatParser.DeserializeFromString(matStr);

            Assert.Single(mat.Textures);
            Assert.Single(mat.Textures[0].Attributes);
            Assert.Equal("texture_base", mat.Textures[0].Name);
            Assert.Equal("/model/road/road_gravel1.tobj", mat.Textures[0].Attributes["source"]);
        }

        [Fact]
        public void LegacyTextureSourceWithoutName()
        {
            var matStr = @"material : ""eut2.dif.spec"" {
                texture[0] : ""wa-tm_ks_lep.tobj""
                texture_name[0] : ""texture_base""
                texture[1] : ""/material/environment/vehicle_reflection.tobj""
                ambient : { 0.400000 , 0.400000 , 0.400000 }
                diffuse : { 0.400000 , 0.400000 , 0.400000 }
                specular : { 0.0 , 0.0 , 0.0 }
                tint : { 1.000000 , 1.000000 , 1.000000 }
                env_factor : { 0.6 , 0.6 , 0.6 }
                shininess : 100
                add_ambient : 0
                reflection : 0
            }";
            var mat = MatParser.DeserializeFromString(matStr);

            Assert.Equal(2, mat.Textures.Count);
            Assert.Equal("texture_base", mat.Textures[0].Name);
            Assert.Equal("wa-tm_ks_lep.tobj", mat.Textures[0].Attributes["source"]);
            Assert.Equal("texture_base", mat.Textures[1].Name);
            Assert.Equal("/material/environment/vehicle_reflection.tobj", mat.Textures[1].Attributes["source"]);
        }

        [Fact]
        public void NoClosingCurly()
        {
            var matStr = @"effect : ""eut2.dif.spec.mult.dif.iamod.dif.add.env.tsnmap.rfx"" {
                diffuse : { 1.000000 , 1.000000 , 1.000000 }
                specular : { 0.380056 , 0.380056 , 0.380056 }
                texture : ""texture_base"" {
                    source : ""/asset/prefab/depots/offshore_shipyard/tx_container02_red.tobj""
                    sampler : default
                }";
            var mat = MatParser.DeserializeFromString(matStr);

            Assert.Single(mat.Textures);
            Assert.Equal(new Vector3(0.380056f, 0.380056f, 0.380056f), mat.Attributes["specular"]);
            Assert.Equal("texture_base", mat.Textures[0].Name);
            Assert.Equal("/asset/prefab/depots/offshore_shipyard/tx_container02_red.tobj", 
                mat.Textures[0].Attributes["source"]);
        }
    }
}
