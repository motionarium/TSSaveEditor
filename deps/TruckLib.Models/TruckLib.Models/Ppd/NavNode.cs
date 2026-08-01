using System;
using System.Collections.Generic;
using System.IO;

namespace TruckLib.Models.Ppd
{
    // See https://modding.scssoft.com/wiki/Games/ETS2/Modding_guides/1.30#Prefabs

    public class NavNode : IBinarySerializable
    {
        public NavNodeType Type { get; set; }

        public ushort Index { get; set; }

        private const byte connectionsCount = 8;

        public LimitedList<NavNodeConnectionInfo> Connections { get; private set; } = new(connectionsCount);

        private static readonly NavNodeConnectionInfo empty = new()
        {
            TargetNodeIndex = ushort.MaxValue,
            Length = float.MaxValue,
        };

        public void Deserialize(BinaryReader r, uint? version = null)
        {
            Type = (NavNodeType)r.ReadByte();
            Index = r.ReadUInt16();

            var used = r.ReadByte();
            var connections = r.ReadObjectList<NavNodeConnectionInfo>(connectionsCount);
            Connections = new(connectionsCount, connections[0..used]);
        }

        public void Serialize(BinaryWriter w)
        {
            w.Write((byte)Type);
            w.Write(Index);

            w.Write((byte)Connections.Count);
            // used
            foreach (var connection in Connections)
            {
                connection.Serialize(w);
            }
            // unused
            for (int i = 0; i < connectionsCount - Connections.Count; i++)
            {
                empty.Serialize(w);
            }
        }
    }

    public class NavNodeConnectionInfo : IBinarySerializable
    {
        public ushort TargetNodeIndex { get; set; }

        public float Length { get; set; }

        private const byte curveIndicesCount = 8;

        public LimitedList<ushort> CurveIndices { get; private set; } = new(curveIndicesCount);

        public void Deserialize(BinaryReader r, uint? version = null)
        {
            TargetNodeIndex = r.ReadUInt16();
            Length = r.ReadSingle();

            var used = r.ReadByte();
            var curveIndices = r.ReadObjectList<ushort>(curveIndicesCount);
            CurveIndices = new(curveIndicesCount, curveIndices[0..used]);
        }

        public void Serialize(BinaryWriter w)
        {
            w.Write(TargetNodeIndex);
            w.Write(Length);

            w.Write((byte)CurveIndices.Count);
            // used
            foreach (var idx in CurveIndices)
            {
                w.Write(idx);
            }
            // unused
            for (int i = 0; i < curveIndicesCount - CurveIndices.Count; i++)
            {
                w.Write(ushort.MaxValue);
            }
        }
    }
}
