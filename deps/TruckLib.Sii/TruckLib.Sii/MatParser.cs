using Sprache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("TruckLib.Sii.Tests")]
namespace TruckLib.Sii
{
    internal static class MatParser
    {
        public static MatFile DeserializeFromString(string mat)
        {
            mat = Utils.TrimByteOrderMark(mat);
            mat = SiiMatUtils.RemoveComments(mat);

            var firstPass = ParserElements.Mat.Parse(mat);

            var matFile = new MatFile { Effect = firstPass.UnitName };

            var (secondPass, textures) = SecondPass(firstPass);
            matFile.Attributes = secondPass.Attributes;
            matFile.Textures = textures;
            return matFile;
        }

        private static (Unit, List<Texture>) SecondPass(FirstPassUnit firstPass)
        {
            Dictionary<string, int> arrInsertIndex = [];
            List<Texture> textures = [];

            var secondPass = new Unit(firstPass.ClassName, firstPass.UnitName);
            foreach (var (key, value) in firstPass.Attributes)
            {
                if (key.EndsWith(']'))
                {
                    SiiMatUtils.ParseListOrArrayAttribute(secondPass, key, value, 
                        arrInsertIndex, true);
                }
                else
                {
                    if (key == "texture" && value is FirstPassUnit fp)
                    {
                        var (sp, _) = SecondPass(fp);
                        textures.Add(new Texture()
                        {
                            Name = sp.Name,
                            Attributes = sp.Attributes,
                        });
                        secondPass.Attributes.Remove("texture");
                    } 
                    else
                    {
                        SiiMatUtils.AddAttribute(secondPass, key, value, true);
                    }
                }
            }

            // convert legacy mat
            if (secondPass.Attributes.TryGetValue("texture", out dynamic legacyTextures)
                && secondPass.Attributes.TryGetValue("texture_name", out dynamic legacyTextureNames))
            {
                if (legacyTextures is string)
                {
                    var texture = new Texture();
                    texture.Name = legacyTextureNames is string 
                        ? legacyTextureNames 
                        : legacyTextureNames[0];
                    texture.Attributes.Add("source", legacyTextures);
                    textures.Add(texture);
                }
                else if (legacyTextures is IList<object> list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var texture = new Texture();
                        if (legacyTextureNames is string name)
                        {
                            texture.Name = name;
                        }
                        else
                        {
                            if (i > legacyTextureNames.Length - 1)
                            {
                                // No idea what the correct behavior would be here
                                texture.Name = legacyTextureNames[legacyTextureNames.Length - 1];
                            }
                            else
                            {
                                texture.Name = legacyTextureNames[i];
                            }
                        }
                        texture.Attributes.Add("source", legacyTextures[i]);
                        textures.Add(texture);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
                secondPass.Attributes.Remove("texture");
                secondPass.Attributes.Remove("texture_name");
            }

            return (secondPass, textures);
        }

        public static MatFile DeserializeFromFile(string path, IFileSystem fs) =>
            DeserializeFromString(fs.ReadAllText(path));

        public static string Serialize(MatFile matFile, string indentation = "\t")
        {
            var sb = new StringBuilder();

            sb.AppendLine($"effect : \"{matFile.Effect}\" {{");

            ParserElements.SerializeAttributes(sb, matFile.Attributes, indentation, true);
            foreach (var texture in matFile.Textures)
            {
                sb.AppendLine($"{indentation}texture: \"{texture.Name}\" {{");
                ParserElements.SerializeAttributes(sb, texture.Attributes, 
                    indentation + indentation, true);
                sb.AppendLine($"{indentation}}}");
            }

            sb.AppendLine("}\n");

            return sb.ToString();
        }

        public static void Serialize(MatFile matFile, string path, string indentation = "\t")
        {
            var str = Serialize(matFile, indentation);
            File.WriteAllText(path, str);
        }
    } 
}
