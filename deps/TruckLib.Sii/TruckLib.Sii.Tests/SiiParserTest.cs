using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib;
using TruckLib.Sii;

namespace TruckLib.Sii.Tests
{
    public class SiiParserTest
    {
        [Fact]
        public void DeserializeFromString()
        {
            var str = File.ReadAllText("Data/sample.sii");
            var file = SiiParser.DeserializeFromString(str, "Data/SiiParserTest");

            Assert.True(file.Units.Count == 1);

            Assert.NotNull(file.Units[0]);
            Assert.Equal("curve_model", file.Units[0].Class);
            Assert.Equal("curve.ibe_1200i", file.Units[0].Name);

            Assert.Equal("/model2/fences/fence_01_ibe.pmd", file.Units[0].Attributes["model_desc"]);

            Assert.True(file.Units[0].Attributes["variation"].Length == 2);
            Assert.Equal("v1 | center2 : 1.0", file.Units[0].Attributes["variation"][1]);

            Assert.Equal(0, file.Units[0].Attributes["high_tess"]);
        }

        [Fact]
        public void ParseNumbers()
        {
            var unit = @"SiiNunit {
                foo : .bar {
                    decimal_float: 1.0 
                    hex_float: &3f800000 
                    exponent: 1.312e3 
                    neg_exponent: 1.312e-3 
                    int: 42 
                    neg_int: -42 
                    long: 9999999999999 
                    neg_long: -9999999999999 
                }
            }";
            var file = SiiParser.DeserializeFromString(unit);
            Assert.Equal(1.0f, file.Units[0].Attributes["decimal_float"]);
            Assert.Equal(1.0f, file.Units[0].Attributes["hex_float"]);
            Assert.Equal(1312f, file.Units[0].Attributes["exponent"]);
            Assert.Equal(1.312e-3f, file.Units[0].Attributes["neg_exponent"]);
            Assert.Equal(42, file.Units[0].Attributes["int"]);
            Assert.Equal(-42, file.Units[0].Attributes["neg_int"]);
            Assert.Equal(9999999999999, file.Units[0].Attributes["long"]);
            Assert.Equal(-9999999999999, file.Units[0].Attributes["neg_long"]);
        }

        [Fact]
        public void ParseTuples()
        {
            var unit = @"SiiNunit {
                foo : .bar {
                    float3: (1.0, 2.0, 3.0)
                    int3: (1, 2, 3)
                }
            }";
            var file = SiiParser.DeserializeFromString(unit);
            Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), file.Units[0].Attributes["float3"]);
            Assert.Equal(1, file.Units[0].Attributes["int3"].Item1);
            Assert.Equal(2, file.Units[0].Attributes["int3"].Item2);
            Assert.Equal(3, file.Units[0].Attributes["int3"].Item3);
        }

        [Fact]
        public void ParseStrings()
        {
            var unit = @"SiiNunit {
                foo : .bar {
                    token: default 
                    owner_ptr: .foo.bar
                    link_ptr: foo.bar
                    string: ""hello""
                }
            }";
            var file = SiiParser.DeserializeFromString(unit);
            Assert.IsType<Token>(file.Units[0].Attributes["token"]);
            Assert.Equal((Token)"default", file.Units[0].Attributes["token"]);
            Assert.IsType<OwnerPointer>(file.Units[0].Attributes["owner_ptr"]);
            Assert.Equal(".foo.bar", file.Units[0].Attributes["owner_ptr"]);
            Assert.IsType<LinkPointer>(file.Units[0].Attributes["link_ptr"]);
            Assert.Equal("foo.bar", file.Units[0].Attributes["link_ptr"]);
            Assert.IsType<string>(file.Units[0].Attributes["string"]);
            Assert.Equal("hello", file.Units[0].Attributes["string"]);
        }

        [Fact]
        public void ParseBooleans()
        {
            var unit = @"SiiNunit {
                foo : .bar {
                    a: true
                    b: false
                }
            }";
            var file = SiiParser.DeserializeFromString(unit);
            Assert.True(file.Units[0].Attributes["a"]);
            Assert.False(file.Units[0].Attributes["b"]);
        }

        [Fact]
        public void ParsePlacement()
        {
            var unit = @"SiiNunit {
                foo : .bar {
                    a: (1, 2, 3) (4; 5, 6, 7)
                    b: (&c6b5d1a7, &41e27800, &c48e31db) (&3f29a17a; 0, &3f3fbb90, 0)
                }
            }";
            var file = SiiParser.DeserializeFromString(unit);

            Assert.Equal(new Vector3(1, 2, 3),
                file.Units[0].Attributes["a"].Position);
            Assert.Equal(new Quaternion(5, 6, 7, 4),
                file.Units[0].Attributes["a"].Rotation);

            Assert.Equal(new Vector3(-23272.826f, 28.308594f, -1137.558f),
                file.Units[0].Attributes["b"].Position);
            Assert.Equal(new Quaternion(0, 0.74895573f, 0, 0.66262019f),
                file.Units[0].Attributes["b"].Rotation);
        }


        [Fact]
        public void ParseArrays()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    without_index[]: 0
                    without_index[]: 1
                    without_index[]: 2

                    with_index[0]: 0
                    with_index[1]: 1
                    with_index[2]: 2
                    with_index[4]: 4
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal(0, sii.Units[0].Attributes["without_index"][0]);
            Assert.Equal(1, sii.Units[0].Attributes["without_index"][1]);
            Assert.Equal(2, sii.Units[0].Attributes["without_index"][2]);
            Assert.Equal(0, sii.Units[0].Attributes["with_index"][0]);
            Assert.Equal(1, sii.Units[0].Attributes["with_index"][1]);
            Assert.Equal(2, sii.Units[0].Attributes["with_index"][2]);
            Assert.Null(sii.Units[0].Attributes["with_index"][3]);
            Assert.Equal(4, sii.Units[0].Attributes["with_index"][4]);
        }

        [Fact]
        public void DeserializeNestedIncludes()
        {
            var str = File.ReadAllText("Data/SiiParserTest/NestedIncludes/parent.sii");
            var file = SiiParser.DeserializeFromString(str, "Data/SiiParserTest/NestedIncludes");

            Assert.Equal(2, file.Units.Count);
            Assert.Equal(".baz", file.Units[0].Name);
            Assert.Equal(2, file.Units[1].Attributes["b"]);
            Assert.Equal("hello", file.Units[1].Attributes["hello_from_nested"]);
            Assert.Equal(["Data/SiiParserTest/NestedIncludes/included1.sui", 
                "Data/SiiParserTest/NestedIncludes/included2.sui",
                "Data/SiiParserTest/NestedIncludes/nested.sui"], file.Includes);
        }

        [Fact]
        public void DeserializeNestedIncludesWithRelative()
        {
            var str = File.ReadAllText("Data/SiiParserTest/NestedIncludesWithRelative/parent.sii");
            var file = SiiParser.DeserializeFromString(str, "Data/SiiParserTest/NestedIncludesWithRelative");

            Assert.Equal(2, file.Units[0].Attributes["b"]);
        }

        [Fact]
        public void Serialize()
        {
            var sii = new SiiFile();
            var unit = new Unit("foo", ".bar");
            sii.Units.Add(unit);

            unit.Attributes.Add("int", 42);
            unit.Attributes.Add("float", 13.12f);
            unit.Attributes.Add("string", "Hello there");
            unit.Attributes.Add("float3", new Vector3(1f, 2f, 3f));
            unit.Attributes.Add("int3", (1,2,3));
            unit.Attributes.Add("bool", true);
            unit.Attributes.Add("quaternion", new Quaternion(1,2,3,4));
            unit.Attributes.Add("placement", new Placement(new Vector3(1,2,3), new Quaternion(1,2,3,4)));
            unit.Attributes.Add("token", new Token("default"));
            unit.Attributes.Add("link_ptr", new LinkPointer("baz"));

            var actual = sii.Serialize("    ");
            var expected = """
                SiiNunit
                {

                foo : .bar
                {
                    int: 42
                    float: 13.12
                    string: "Hello there"
                    float3: (1.0, 2.0, 3.0)
                    int3: (1, 2, 3)
                    bool: true
                    quaternion: (4.0; 1.0, 2.0, 3.0)
                    placement: (1.0, 2.0, 3.0) (4.0; 1.0, 2.0, 3.0)
                    token: default
                    link_ptr: baz
                }

                }

                """;
            Assert.Equal(expected.Replace("\r", ""), actual.Replace("\r", ""));
        }

        [Fact]
        public void MissingWhitespaceBetweenUnitNameAndCurly()
        {
            var siiStr = @"SiiNunit { 
                dont_throw:onthis{ 
                    hello:123 
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal(123, sii.Units[0].Attributes["hello"]);
        }

        [Fact]
        public void ParseHexInt()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hello : 0xBEEF 
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal(0xBEEF, sii.Units[0].Attributes["hello"]);
        }

        [Fact]
        public void ParseFloatWithFSuffix()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hello : 4.2f
                    there : 4.2
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal(4.2f, sii.Units[0].Attributes["hello"]);
            Assert.Equal(4.2f, sii.Units[0].Attributes["there"]);
        }

        [Fact]
        public void ParseStringWithMissingEndQuote()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hello : ""missing
                    there: ""not missing""
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal("missing", sii.Units[0].Attributes["hello"]);
            Assert.Equal("not missing", sii.Units[0].Attributes["there"]);
        }

        [Fact]
        public void ParseOwnerPtrStartingWithNumber()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hello : .42ptr
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.IsType<OwnerPointer>(sii.Units[0].Attributes["hello"]);
            Assert.Equal(".42ptr", sii.Units[0].Attributes["hello"]);
        }

        [Fact]
        public void ClassNameWithDoubleColon()
        {
            var siiStr = @"SiiNunit { 
                ui::window:test {
                    hello : 123
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal("ui::window", sii.Units[0].Class);
        }

        [Fact]
        public void FloatWithDotButNoFractionalDigits()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hello : 727.
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.IsType<float>(sii.Units[0].Attributes["hello"]);
            Assert.Equal(727f, sii.Units[0].Attributes["hello"]);
        }

        [Fact]
        public void ArrayIndexWithSpaceAfterKeyName()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hawk [2]: ""ah""
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal("ah", sii.Units[0].Attributes["hawk"][2]);
        }

        [Fact]
        public void SurpriseItWasAnArrayThisWholeTime()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    hello: ""there""
                    hello[2]: ""world""
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal("there", sii.Units[0].Attributes["hello"][0]);
            Assert.Equal("world", sii.Units[0].Attributes["hello"][2]);
        }

        [Fact]
        public void VeryLargeFloat()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    lv_limit: 340282346638528859811704183484516925440.000000
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.IsType<float>(sii.Units[0].Attributes["lv_limit"]);
            Assert.Equal(340282346638528859811704183484516925440f, sii.Units[0].Attributes["lv_limit"]);
        }

        [Fact]
        public void FixedLengthArrayWhichLiesAboutNumberOfEntries()
        {
            var siiStr = @"SiiNunit { 
                foo : bar {
                    words: 2
                    words[]: ""foo""
                    words[]: ""bar""
                    words[]: ""baz""
                } 
            }";
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal(3, sii.Units[0].Attributes["words"].Length);
            Assert.Equal("foo", sii.Units[0].Attributes["words"][0]);
            Assert.Equal("bar", sii.Units[0].Attributes["words"][1]);
            Assert.Equal("baz", sii.Units[0].Attributes["words"][2]);
        }

        [Fact]
        public void GluedCurlies()
        {
            var siiStr = """
                SiiNunit {            
                building_model : bld_model.hello1{
                    railing_model: true
                    follow_curve_dir: true
                    model_desc: "/model/foo/bar.pmd"
                }building_model : bld_model.hello2{
                    railing_model: true
                    follow_curve_dir: true
                    model_desc: "/model/727/wysi.pmd"
                }building_model : bld_model.hello3{
                    single_part: true
                    model_desc: "/model/aei/ou.pmd"
                    width: 45
                    model_size_override: 0.00
                }
                }
                """;
            var sii = SiiParser.DeserializeFromString(siiStr);
            Assert.Equal(3, sii.Units.Count);
            Assert.Equal("bld_model.hello1", sii.Units[0].Name);
            Assert.Equal("/model/foo/bar.pmd", sii.Units[0].Attributes["model_desc"]);
            Assert.Equal("bld_model.hello2", sii.Units[1].Name);
            Assert.Equal("/model/727/wysi.pmd", sii.Units[1].Attributes["model_desc"]);
            Assert.Equal("bld_model.hello3", sii.Units[2].Name);
            Assert.Equal("/model/aei/ou.pmd", sii.Units[2].Attributes["model_desc"]);
        }

        [Fact]
        public void RecursiveIncludesDontThrow()
        {
            var ex = Record.Exception(() => SiiFile.Open("Data/SiiParserTest/RecursiveIncludes/one.sii"));
            Assert.Null(ex);
        }
    }
}
