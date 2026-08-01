using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Models.Ppd
{
    /// <summary>
    /// Represents a prefab descriptor (.ppd) file.
    /// </summary>
    public class PrefabDescriptor : IBinarySerializable
    {
        public uint Version { get; private set; }

        public List<ControlNode> Nodes { get; set; } = [];

        public List<NavCurve> NavCurves { get; set; } = [];

        public List<Sign> Signs { get; set; } = [];

        public List<Semaphore> Semaphores { get; set; } = [];

        public List<SpawnPoint> SpawnPoints { get; set; } = [];

        public List<MapPoint> MapPoints { get; set; } = [];

        public List<TriggerPoint> TriggerPoints { get; set; } = [];

        public List<Intersection> Intersections { get; set; } = [];

        public List<NavNode> NavNodes { get; set; } = [];

        /// <summary>
        /// Reads a ppd file from disk.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The prefab descriptor.</returns>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public static PrefabDescriptor Open(string path)
        {
            using var ppdStream = File.OpenRead(path);
            return Load(ppdStream);
        }

        /// <summary>
        /// Reads a ppd file from disk.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="fs">The file system to load the file from.</param>
        /// <returns>The prefab descriptor.</returns>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public static PrefabDescriptor Open(string path, IFileSystem fs)
        {
            using var ppdStream = fs.Open(path);
            return Load(ppdStream);
        }

        /// <summary>
        /// Reads a ppd file from memory.
        /// </summary>
        /// <param name="ppdBuffer">The buffer containing the file.</param>
        /// <returns>The prefab descriptor.</returns>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public static PrefabDescriptor Load(byte[] ppdBuffer)
        {
            using var ms = new MemoryStream(ppdBuffer);
            return Load(ms);
        }

        /// <summary>
        /// Reads a ppd file from memory.
        /// </summary>
        /// <param name="ppdStream">The stream containing the file.</param>
        /// <returns>The prefab descriptor.</returns>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public static PrefabDescriptor Load(Stream ppdStream)
        {
            var ppd = new PrefabDescriptor();
            using var r = new BinaryReader(ppdStream);
            ppd.Deserialize(r);
            return ppd;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public void Deserialize(BinaryReader r, uint? version = null)
        {
            Version = r.ReadUInt32();
            switch (Version)
            {
                case 0x15:
                    Deserialize15(r);
                    break;
                case 0x16:
                    Deserialize16(r);
                    break;
                case 0x17:
                    Deserialize17(r);
                    break;
                case 0x18:
                    Deserialize18(r);
                    break;
                case 0x19:
                    Deserialize19(r);
                    break;
                default:
                    throw new UnsupportedVersionException($"Version {Version} is not supported.");
            }
        }

        private void Deserialize15(BinaryReader r)
        {
            const int version = 0x15;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 11; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount, version);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount, version);
            var terrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);

            SetTerrainPoints(terrainPointPositions, terrainPointNormals, terrainPointVariants);
        }

        private void Deserialize16(BinaryReader r)
        {
            const int version = 0x16;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();
            var navNodeCount = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 12; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount, version);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount, version);
            var terrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);
            NavNodes = r.ReadObjectList<NavNode>(navNodeCount);

            SetTerrainPoints(terrainPointPositions, terrainPointNormals, terrainPointVariants);
        }

        private void Deserialize17(BinaryReader r)
        {
            const int version = 0x17;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();
            var navNodeCount = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 12; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount, version);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount, version);
            var terrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);
            NavNodes = r.ReadObjectList<NavNode>(navNodeCount);

            SetTerrainPoints(terrainPointPositions, terrainPointNormals, terrainPointVariants);
        }

        private void Deserialize18(BinaryReader r)
        {
            const int version = 0x18;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();
            var navNodeCount = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 12; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount, version);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount, version);
            var terrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);
            NavNodes = r.ReadObjectList<NavNode>(navNodeCount);

            SetTerrainPoints(terrainPointPositions, terrainPointNormals, terrainPointVariants);
        }

        private void Deserialize19(BinaryReader r)
        {
            const int version = 0x19;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();
            var navNodeCount = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 12; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount, version);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount, version);
            var terrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            var terrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);
            NavNodes = r.ReadObjectList<NavNode>(navNodeCount);

            SetTerrainPoints(terrainPointPositions, terrainPointNormals, terrainPointVariants);
        }

        /// <summary>
        /// Parses the terrain point data into a more user-friendly format.
        /// </summary>
        /// <param name="positions">The raw, flat list of terrain point positions.</param>
        /// <param name="normals">The raw, flat list of terrain point normals.</param>
        /// <param name="variants">The raw, flat list of terrain point variant mappings.</param>
        private void SetTerrainPoints(List<Vector3> positions, List<Vector3> normals, 
            List<TerrainPointVariant> variants)
        {
            for (int nodeIdx = 0; nodeIdx < Nodes.Count; nodeIdx++)
            {
                var node = Nodes[nodeIdx];

                var index = (int)node.TerrainPointIndex;
                var count = (int)node.TerrainPointCount;

                var mappingIndex = (int)node.TerrainPointVariantIndex;
                var mappingsCount = (int)node.TerrainPointVariantCount;

                if (mappingsCount > 0)
                {
                    var mappings = variants[mappingIndex..(mappingIndex + mappingsCount)];

                    for (int mapIdx = 0; mapIdx < mappings.Count; mapIdx++)
                    {
                        var list = new List<TerrainPoint>((int)mappings[mapIdx].Length);
                        node.TerrainPoints[mapIdx] = list;

                        var first = (int)mappings[mapIdx].Start;
                        var last = mappings[mapIdx].Start + mappings[mapIdx].Length;

                        for (int pointIdx = first; pointIdx < last; pointIdx++)
                        {
                            list.Add(new TerrainPoint(positions[index+pointIdx], normals[index+pointIdx]));
                        }
                    }
                }
                else
                {
                    var nodePositions = positions[index..(index + count)];
                    var nodeNormals = normals[index..(index + count)];
                    var list = new List<TerrainPoint>(count);
                    node.TerrainPoints[0] = list;
                    for (int pointIdx = 0; pointIdx < count; pointIdx++)
                    {
                        list.Add(new TerrainPoint(nodePositions[pointIdx], nodeNormals[pointIdx]));
                    }
                }
            }
        }

        private (List<Vector3> Positions, List<Vector3> Normals, List<TerrainPointVariant> Variants) SerializeTerrainPoints()
        {
            List<Vector3> positions = [];
            List<Vector3> normals = [];
            List<TerrainPointVariant> variants = [];

            for (int nodeIdx = 0; nodeIdx < Nodes.Count; nodeIdx++)
            {
                var node = Nodes[nodeIdx];

                int index = positions.Count;
                int count = 0;
                int mappingsIndex = variants.Count;
                int mappingsCount = node.TerrainPoints.Count > 1 ? node.TerrainPoints.Count : 0;
                int variantStart = 0;

                foreach (var (_, variant) in node.TerrainPoints)
                {
                    count += variant.Count;
                    positions.AddRange(variant.Select(x => x.Position));
                    normals.AddRange(variant.Select(x => x.Normal));
                    if (node.TerrainPoints.Count > 1)
                    {
                        variants.Add(new((uint)variantStart, (uint)variant.Count));
                        variantStart += variant.Count;
                    }
                }

                node.TerrainPointIndex = (uint)index;
                node.TerrainPointCount = (uint)count;
                node.TerrainPointVariantIndex = (uint)mappingsIndex;
                node.TerrainPointVariantCount = (uint)mappingsCount;
            }

            return (positions, normals, variants);
        }

        public void Serialize(BinaryWriter w)
        {
            var (terrainPointPositions, terrainPointNormals, terrainPointVariants) = SerializeTerrainPoints();

            w.Write(0x19);                         // 0

            w.Write(Nodes.Count);                  // 4
            w.Write(NavCurves.Count);              // 8
            w.Write(Signs.Count);                  // 12
            w.Write(Semaphores.Count);             // 16
            w.Write(SpawnPoints.Count);            // 20
            w.Write(terrainPointPositions.Count);  // 24
            w.Write(terrainPointVariants.Count);   // 28
            w.Write(MapPoints.Count);              // 32
            w.Write(TriggerPoints.Count);          // 36
            w.Write(Intersections.Count);          // 40
            w.Write(NavNodes.Count);               // 44

            // Offsets; to be filled in later         48
            for (int i = 0; i < 12; i++)
                w.Write(0);

            var nodesOffset = w.BaseStream.Position;
            w.WriteObjectList(Nodes);

            var navCurvesOffset = w.BaseStream.Position;
            w.WriteObjectList(NavCurves);

            var signsOffset = w.BaseStream.Position;
            w.WriteObjectList(Signs);

            var semaphoresOffset = w.BaseStream.Position;
            w.WriteObjectList(Semaphores);

            var spawnPointsOffset = w.BaseStream.Position;
            w.WriteObjectList(SpawnPoints);

            var terrainPointPositionsOffset = w.BaseStream.Position;
            w.WriteObjectList(terrainPointPositions);

            var terrainPointNormalsOffset = w.BaseStream.Position;
            w.WriteObjectList(terrainPointNormals);

            var terrainPointVariantsOffset = w.BaseStream.Position;
            w.WriteObjectList(terrainPointVariants);

            var mapPointsOffset = w.BaseStream.Position;
            w.WriteObjectList(MapPoints);

            var triggerPointsOffset = w.BaseStream.Position;
            w.WriteObjectList(TriggerPoints);

            var intersectionsOffset = w.BaseStream.Position;
            w.WriteObjectList(Intersections);

            var navNodesOffset = w.BaseStream.Position;
            w.WriteObjectList(NavNodes);

            // Jump back to the header and fill in the offsets
            w.BaseStream.Position = 48;
            w.Write((int)nodesOffset);
            w.Write((int)navCurvesOffset);
            w.Write((int)signsOffset);
            w.Write((int)semaphoresOffset);
            w.Write((int)spawnPointsOffset);
            w.Write((int)terrainPointPositionsOffset);
            w.Write((int)terrainPointNormalsOffset);
            w.Write((int)terrainPointVariantsOffset);
            w.Write((int)mapPointsOffset);
            w.Write((int)triggerPointsOffset);
            w.Write((int)intersectionsOffset);
            w.Write((int)navNodesOffset);
        }

        /// <summary>
        /// Writes the prefab descriptor to a file.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        public void Save(string path)
        {
            using var fs = File.Create(path);
            using var w = new BinaryWriter(fs);
            Serialize(w);
        }
    }
}
