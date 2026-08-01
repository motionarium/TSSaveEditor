using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace TruckLib.Models
{
    /// <summary>
    /// Represents the metadata and geometry of a model.
    /// </summary>
    public class Model
    {
        private const int SupportedPmdVersion = 4;
        private const string PmdExtension = "pmd";

        private const byte SupportedPmgVersion = 0x15;
        private readonly char[] PmgSignature = ['g', 'm', 'P'];
        private const string PmgExtension = "pmg";

        public List<Look> Looks { get; set; } = [];

        public List<Variant> Variants { get; set; } = [];

        public AxisAlignedBox BoundingBox { get; set; } = new();

        public Vector3 BoundingBoxCenter { get; set; } = Vector3.Zero;

        public float BoundingBoxDiagonalSize { get; set; }

        public List<Bone> Skeleton { get; set; } = [];

        public List<Part> Parts { get; set; } = [];

        private List<string> strings = [];

        public Model()
        {
            Looks.Add(new Look("default"));
            Variants.Add(new Variant("default"));
        }

        /// <summary>
        /// Reads a model from disk.
        /// </summary>
        /// <param name="pmdPath">The path to the pmd file of the model.</param>
        /// <returns>A Model object.</returns>
        public static Model Open(string pmdPath)
        {
            using var pmdStream = File.OpenRead(pmdPath);

            var pmgPath = Path.ChangeExtension(pmdPath, PmgExtension);
            using var pmgStream = File.OpenRead(pmgPath);

            return Load(pmdStream, pmgStream);
        }

        /// <summary>
        /// Reads a model.
        /// </summary>
        /// <param name="pmdPath">The path to the pmd file of the model.</param>
        /// <param name="fs">The file system to load the model files from.</param>
        /// <returns>A Model object.</returns>
        public static Model Open(string pmdPath, IFileSystem fs)
        {
           using var pmdStream = fs.Open(pmdPath);

           var pmgPath = Path.ChangeExtension(pmdPath, PmgExtension);
           using var pmgStream = fs.Open(pmgPath);

           return Load(pmdStream, pmgStream);
        }

        /// <summary>
        /// Loads a model from memory.
        /// </summary>
        /// <param name="pmdBuffer">The buffer containing the pmd file.</param>
        /// <param name="pmgBuffer">The buffer containing the pmg file.</param>
        /// <returns>A Model object.</returns>
        public static Model Load(byte[] pmdBuffer, byte[] pmgBuffer)
        {
            using var pmdStream = new MemoryStream(pmdBuffer);
            using var pmgStream = new MemoryStream(pmgBuffer);
            return Load(pmdStream, pmgStream);
        }

        /// <summary>
        /// Loads a model from memory.
        /// </summary>
        /// <param name="pmdBuffer">The stream containing the pmd file.</param>
        /// <param name="pmgBuffer">The stream containing the pmg file.</param>
        /// <returns>A Model object.</returns>
        public static Model Load(Stream pmdStream, Stream pmgStream)
        {
            var model = new Model();
            if (pmdStream.Length > 0)
            {
                using var r = new BinaryReader(pmdStream);
                model.ReadPmd(r);
            }
            if (pmgStream.Length > 0)
            {
                using var r = new BinaryReader(pmgStream);
                model.ReadPmg(r);
            }
            return model;
        }

        /// <summary>
        /// Writes the model to a .pmd and .pmg file.
        /// </summary>
        /// <param name="directory">The directory to write the files to.</param>
        /// <param name="name">The name of the model file, without extension.</param>
        public void Save(string directory, string name)
        {
            var pmdPath = Path.Combine(directory, $"{name}.{PmdExtension}");
            using (var w = new BinaryWriter(new FileStream(pmdPath, FileMode.Create)))
            {
                SerializePmd(w);
            }

            var pmgPath = Path.ChangeExtension(pmdPath, PmgExtension);
            using (var w = new BinaryWriter(new FileStream(pmgPath, FileMode.Create)))
            {
                SerializePmg(w);
            }
        }

        private void ReadPmd(BinaryReader r)
        {
            // specifically check if the user passed in a pmg file by accident
            r.BaseStream.Position = 1;
            var pmgSigCheck = r.ReadChars(3);
            if (Enumerable.SequenceEqual(pmgSigCheck, PmgSignature))
            {
                throw new ArgumentException("This is a .pmg file, not a .pmd file.");
            }
            r.BaseStream.Position = 0;

            var version = r.ReadUInt32();
            if (version != SupportedPmdVersion)
            {
                throw new UnsupportedVersionException($".pmd version {version} is not supported.");
            }

            var materialCount = r.ReadUInt32();
            var lookCount = r.ReadUInt32();
            var pieceCount = r.ReadUInt32();
            var variantCount = r.ReadUInt32();
            var partCount = r.ReadUInt32();
            var attribsCount = r.ReadUInt32();

            var attribsValuesSize = r.ReadUInt32();
            var materialBlockSize = r.ReadUInt32();

            var lookOffset = r.ReadUInt32();
            var variantOffset = r.ReadUInt32();
            var partAttribsOffset = r.ReadUInt32();
            var attribsValuesOffset = r.ReadUInt32();
            var attribsHeaderOffset = r.ReadUInt32();
            var materialPathsOffsetsOffset = r.ReadUInt32();
            var materialPathsOffset = r.ReadUInt32();

            // Look names
            Looks.Clear();
            for (int i = 0; i < lookCount; i++)
            {
                Looks.Add(new Look(r.ReadToken()));
            }

            // Variant names
            Variants.Clear();
            for (int i = 0; i < variantCount; i++)
            {
                Variants.Add(new Variant(r.ReadToken()));
            }

            // "partAttribs"
            // TODO: what is this?
            for (int i = 0; i < partCount; i++)
            {
                var from = r.ReadInt32();
                var to = r.ReadInt32();
            }

            // Attribs header
            // Each variant has the same attribs
            for (int i = 0; i < attribsCount; i++)
            {
                var name = r.ReadToken();
                var type = r.ReadInt32();
                var offset = r.ReadInt32();
                foreach (var variant in Variants)
                {
                    var attrib = new PartAttribute();
                    variant.Attributes.Add(attrib);
                    attrib.Tag = name;
                    attrib.Type = type;
                }
            }

            // Attribs values
            // TODO: Find out if there are any files where a part has 
            // more than one attrib or if "visible" is actually the only attrib
            // that exists
            for (int i = 0; i < variantCount; i++)
            {
                for (int j = 0; j < attribsCount; j++)
                {
                    Variants[i].Attributes[j].Value = r.ReadUInt32();
                }
            }

            // Material path offsets
            // I think we can get away with ignoring this?
            var materialPathOffsets = new List<uint>();
            for (int i = 0; i < lookCount * materialCount; i++)
            {
                materialPathOffsets.Add(r.ReadUInt32());
            }

            // Look material paths. materialBlockSize is ignored because I've
            // encountered files where this value is wrong.
            var materialPaths = r.ReadBytes((int)(r.BaseStream.Length - materialPathsOffset));
            var materials = StringUtils.CStringBytesToList(materialPaths);
            for (int i = 0; i < Looks.Count; i++)
            {
                Looks[i].Materials.AddRange(
                    materials.GetRange(i * (int)materialCount, (int)materialCount)
                    );
            }
        }

        private void ReadPmg(BinaryReader r)
        {
            var version = r.ReadByte();
            if (version != SupportedPmgVersion)
            {
                throw new UnsupportedVersionException($".pmg version {version} is not supported.");
            }

            var signature = r.ReadChars(3);
            if (!Enumerable.SequenceEqual(signature, PmgSignature))
            {
                throw new InvalidDataException($"Probably not a pmg file");
            }

            var pieceCount = r.ReadUInt32();
            var partCount = r.ReadUInt32();
            var boneCount = r.ReadUInt32();
            var weightWidth = r.ReadInt32();
            var locatorCount = r.ReadUInt32();
            var skeletonHash = r.ReadUInt64();

            BoundingBoxCenter = r.ReadVector3();
            BoundingBoxDiagonalSize = r.ReadSingle();
            BoundingBox = new AxisAlignedBox();
            BoundingBox.Deserialize(r);

            var skeletonOffset = r.ReadUInt32();
            var partsOffset = r.ReadUInt32();
            var locatorsOffset = r.ReadUInt32();
            var piecesOffset = r.ReadUInt32();

            var stringPoolOffset = r.ReadUInt32();
            var stringPoolSize = r.ReadUInt32();
            var vertexPoolOffset = r.ReadUInt32();
            var vertexPoolSize = r.ReadUInt32();
            var indexPoolOffset = r.ReadUInt32();
            var indexPoolSize = r.ReadUInt32();

            Skeleton = r.ReadObjectList<Bone>(boneCount);

            // jump ahead and read all locators & pieces first
            r.BaseStream.Position = locatorsOffset;
            var locators = r.ReadObjectList<Locator>(locatorCount);
            var pieces = r.ReadObjectList<Piece>(pieceCount);

            // then return to parts and assign the locators and pieces right away
            r.BaseStream.Position = partsOffset;
            for (int i = 0; i < partCount; i++)
            {
                var part = new Part();
                part.Name = r.ReadToken();

                var piecesCount = r.ReadUInt32();
                var piecesIndex = r.ReadUInt32();
                part.Pieces = pieces.GetRange((int)piecesIndex, (int)piecesCount);

                var locatorsCount = r.ReadUInt32();
                var locatorsIndex = r.ReadUInt32();
                part.Locators = locators.GetRange((int)locatorsIndex, (int)locatorsCount);

                Parts.Add(part);
            }

            // TODO: what is this?
            r.BaseStream.Position = stringPoolOffset;
            if (stringPoolSize > 0)
            {
                r.BaseStream.Position = stringPoolOffset;
                var stringsBytes = r.ReadBytes((int)stringPoolSize);
                strings = StringUtils.CStringBytesToList(stringsBytes);
            }
        }

        /// <summary>
        /// Serializes the pmd part of the model to a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="w">The <see cref="BinaryWriter"/> to write to.</param>
        public void SerializePmd(BinaryWriter w)
        {
            w.Write(SupportedPmdVersion);               // 0

            w.Write(Looks[0].Materials.Count);          // 4
            w.Write(Looks.Count);                       // 8
            // why is this sometimes 0?
            w.Write(0);                                 // 12
            w.Write(Variants.Count);                    // 16
            w.Write(Parts.Count);                       // 20
            // attribs count
            w.Write(Parts.Count);                       // 24

            // sizes and offsets; to be filled in later
            w.Write(0); // attribsValuesSize               28
            w.Write(0); // materialBlockSize               32
            w.Write(0); // lookOffset                      36
            w.Write(0); // variantOffset                   40
            w.Write(0); // partAttribsOffset               44
            w.Write(0); // attribsValuesOffset             48
            w.Write(0); // attribsHeaderOffset             52
            w.Write(0); // materialPathsOffsetsOffset      56
            w.Write(0); // materialPathsOffset             60

            // Look names
            var lookOffset = w.BaseStream.Position;
            foreach (var look in Looks)
            {
                w.Write(look.Name);
            }

            // Variant names
            var variantOffset = w.BaseStream.Position;
            foreach (var variant in Variants)
            {
                w.Write(variant.Name);
            }

            // "partAttribs"
            // TODO: what is this?
            // v placeholder code
            var partAttribsOffset = w.BaseStream.Position;
            for (int i = 0; i < Parts.Count; i++)
            {
                w.Write(i);
                w.Write(i + 1);
            }

            // Attribs header
            var attribsHeaderOffset = w.BaseStream.Position;
            var offset = 0;
            foreach (var attrib in Variants[0].Attributes)
            {
                w.Write(attrib.Tag);
                w.Write(attrib.Type);
                w.Write(offset);
                offset += 4;
            }

            // Attribs values
            var attribsValuesOffset = w.BaseStream.Position;
            foreach (var variant in Variants)
            {
                foreach (var attrib in variant.Attributes)
                {
                    w.Write(attrib.Value);
                }
            }

            // Material path offsets
            var materialPathsOffsetsOffset = w.BaseStream.Position;
            var materialStrs = Looks.Select(x => x.Materials).SelectMany(x => x).ToList();
            var materials = StringUtils.ListToCStringByteList(materialStrs);
            var materialOffset = (int)w.BaseStream.Position + (materials.Count * sizeof(int));
            for (int i = 0; i < materials.Count; i++)
            {
                w.Write(materialOffset);
                materialOffset += materials[i].Length;
            }

            // Material paths
            var materialPathsOffset = w.BaseStream.Position;
            foreach (var str in materials)
            {
                w.Write(str);
            }
            var materialBlockSize = w.BaseStream.Position - materialPathsOffset;

            // Jump back to the header and fill in the offsets
            w.BaseStream.Position = 28;
            w.Write((int)(materialPathsOffsetsOffset - attribsValuesOffset) / Variants.Count); // attribsValuesSize
            w.Write((int)materialBlockSize);
            w.Write((int)lookOffset);
            w.Write((int)variantOffset);
            w.Write((int)partAttribsOffset);
            w.Write((int)attribsValuesOffset);
            w.Write((int)attribsHeaderOffset);
            w.Write((int)materialPathsOffsetsOffset);
            w.Write((int)materialPathsOffset);
        }

        /// <summary>
        /// Serializes the pmg part of the model to a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="w">The <see cref="BinaryWriter"/> to write to.</param>
        public void SerializePmg(BinaryWriter w)
        {
            // TODO: unfuck this the same way I did with pmd

            w.Write(SupportedPmgVersion);

            w.Write(Encoding.ASCII.GetBytes(PmgSignature));

            w.Write(Parts.Sum(x => x.Pieces.Count));
            w.Write(Parts.Count);
            w.Write(Skeleton.Count);
            w.Write(0); // TODO: What is "weight width"?
            w.Write(Parts.Sum(x => x.Locators.Count));
            w.Write(Skeleton.Count == 0 ? 0UL : 0UL); // TODO: How is the "skeleton hash" calculated?

            w.Write(BoundingBoxCenter);
            w.Write(BoundingBoxDiagonalSize);
            BoundingBox.Serialize(w);

            // from this point onward we need to deal with offset vals
            // which pretty much breaks my workflow but I can't be bothered
            // to do it properly

            // start from the bottom with the triangles,
            // write everything to byte[]s, get the offsets,
            // then output everything in the correct order

            var offsetSectionLength = sizeof(int) * 10;

            byte[] skeleton = ListAsByteArray(Skeleton);

            byte[] parts = WriteToByteArray((_w) =>
            {
                int pieceIndex = 0;
                int locatorIndex = 0;
                foreach (var part in Parts)
                {
                    _w.Write(part.Name);
                    _w.Write(part.Pieces.Count);
                    _w.Write(pieceIndex);
                    pieceIndex += part.Pieces.Count;
                    _w.Write(part.Locators.Count);
                    _w.Write(locatorIndex);
                    locatorIndex += part.Locators.Count;
                }
            });

            byte[] locators = ListAsByteArray(
                Parts.Select(x => x.Locators).SelectMany(x => x).ToList()
                );

            var pieces = Parts.Select(x => x.Pieces).SelectMany(x => x).ToList();

            // index pool
            var piecesTris = new List<byte[]>();
            foreach (var piece in pieces)
            {
                piecesTris.Add(
                    WriteToByteArray((_w) =>
                    {
                        piece.WriteTriangles(_w);
                    })
                );
            }

            // vert pool
            var piecesVerts = new List<byte[]>();
            foreach (var piece in pieces)
            {
                piecesVerts.Add(
                    WriteToByteArray((_w) =>
                    {
                        piece.WriteVertPart(_w);
                    })
                 );
            }

            // string pool
            var stringPool = StringUtils.ListToCStringByteList(strings);           

            // pieces header
            // first, get the byte length of the pieces segment
            // because we need it to calculate the vert*Offset values.
            var skeletonOffset = (int)w.BaseStream.Position + offsetSectionLength;
            var partsOffset = skeletonOffset + skeleton.Length;
            var locatorsOffset = partsOffset + parts.Length;
            var pieceHeaderOffset = locatorsOffset + locators.Length;
            var stringStart = (int)(pieceHeaderOffset + GetLengthOfPieceIndex(pieces));
            var vertStart = stringStart + stringPool.Sum(x => x.Length);
            var trisStart = vertStart + piecesVerts.Sum(x => x.Length);

            // then actually get the bytes
            byte[] piecesHeader = WriteToByteArray((_w) =>
            {
                var currVert = vertStart;
                var currTris = trisStart;
                for (int i = 0; i < pieces.Count; i++)
                {
                    pieces[i].WriteHeaderPart(_w, currVert, currTris);
                    currVert += piecesVerts[i].Length;
                    currTris += piecesTris[i].Length;
                }
            });

            // and now we can finally write everything
            w.Write(skeletonOffset);
            w.Write(partsOffset);
            w.Write(locatorsOffset);
            w.Write(pieceHeaderOffset);

            w.Write(stringStart);
            w.Write(strings is null ? 0 : stringPool.Sum(x => x.Length));
            w.Write(vertStart);
            w.Write(piecesVerts.Sum(x => x.Length));
            w.Write(trisStart);
            w.Write(piecesTris.Sum(x => x.Length));

            w.Write(skeleton);
            w.Write(parts);
            w.Write(locators);
            w.Write(piecesHeader);

            foreach (var str in stringPool)
            {
                w.Write(str);
            }

            foreach (var vert in piecesVerts)
            {
                w.Write(vert);
            }

            foreach (var tri in piecesTris)
            {
                w.Write(tri);
            }
        }

        private static byte[] WriteToByteArray(Action<BinaryWriter> action)
        {
            byte[] arr;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                action(w);
                arr = ms.ToArray();
            }
            return arr;
        }

        private static long GetLengthOfPieceIndex(List<Piece> pieces)
        {
            long length = 0;
            using (var ms = new MemoryStream())
            using (var w2 = new BinaryWriter(ms))
            {
                foreach (var piece in pieces)
                {
                    piece.WriteHeaderPart(w2, 0, 0);
                }

                length += ms.Length;
            }
            return length;
        }

        private static byte[] ListAsByteArray<T>(List<T> list)
        {
            byte[] array;
            using (var ms = new MemoryStream())
            using (var w2 = new BinaryWriter(ms))
            {
                w2.WriteObjectList(list);
                array = ms.ToArray();
            }
            return array;
        }
    }
}
