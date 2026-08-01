using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Models.Ppd
{
    /// <summary>
    /// Represents a navigation curve, used to define AI traffic paths and GPS navigation.
    /// </summary>
    public class NavCurve : IBinarySerializable
    {
        public Token Name { get; set; }

        public (byte EndNode, byte EndLane, byte StartNode, byte StartLane) LeadsToNodes { get; set; }

        public Vector3 StartPosition { get; set; }

        public Vector3 EndPosition { get; set; }

        public Quaternion StartRotation { get; set; }

        public Quaternion EndRotation { get; set; }

        public float Length { get; set; }

        private const int linesCount = 4;

        public LimitedList<int> NextLines { get; private set; } = new(linesCount);

        public LimitedList<int> PreviousLines { get; private set; } = new(linesCount);

        public int SemaphoreId { get; set; }

        public Token TrafficRule { get; set; }

        public uint NavNodeIndex { get; set; } = uint.MaxValue;

        private FlagField flags = new();

        public BlinkerType Blinker
        {
            get => (BlinkerType)flags.GetBitString(2, 3);
            set => flags.SetBitString(2, 3, (uint)value);
        }

        /// <summary>
        /// Determines which AI vehicles can use this curve.
        /// <para>AI vehicles will try to go into most suitable curve, 
        /// but if there will be none, they can also use any other 
        /// even if they are not allowed to.</para>
        /// </summary>
        public AllowedVehicles AllowedVehicles
        {
            get => (AllowedVehicles)flags.GetBitString(5, 2);
            set => flags.SetBitString(5, 2, (uint)value);
        }

        /// <summary>
        /// Determines if the probability of AI vehicles entering this (prefab? nav. path?) is lowered.
        /// </summary>
        public bool LowProbability
        {
            get => flags[13];
            set => flags[13] = value;
        }

        /// <summary>
        /// Property defining extra limited displacement for AI vehicles.
        /// </summary>
        public bool LimitDisplacement
        {
            get => flags[14];
            set => flags[14] = value;
        }

        /// <summary>
        /// Determines if the PriorityModifier value will be added 
        /// to already existing priority for this lane.
        /// </summary>
        public bool AdditivePriority
        {
            get => flags[15];
            set => flags[15] = value;
        }

        public Nibble PriorityModifier
        {
            get => (Nibble)flags.GetBitString(16, 4);
            set => flags.SetBitString(16, 4, (uint)value);
        }

        public void Deserialize(BinaryReader r, uint? version = null)
        {
            switch (version)
            {
                case 0x15:
                    Deserialize15(r);
                    break;
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                    Deserialize16to19(r);
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported.");
            }
        }

        public void Deserialize15(BinaryReader r)
        {
            Name = r.ReadToken();
            flags = new FlagField(r.ReadUInt32());

            LeadsToNodes = (r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());

            StartPosition = r.ReadVector3();
            EndPosition = r.ReadVector3();

            StartRotation = r.ReadQuaternion();
            EndRotation = r.ReadQuaternion();

            Length = r.ReadSingle();

            var nextLines = new int[linesCount];
            for (int i = 0; i < linesCount; i++)
            {
                nextLines[i] = r.ReadInt32();
            }

            var previousLines = new int[linesCount];
            for (int i = 0; i < linesCount; i++)
            {
                previousLines[i] = r.ReadInt32();
            }

            var nextUsed = r.ReadUInt32();
            var previousUsed = r.ReadUInt32();

            NextLines = new(linesCount, nextLines[0..(int)nextUsed]);
            PreviousLines = new(linesCount, previousLines[0..(int)previousUsed]);

            SemaphoreId = r.ReadInt32();

            TrafficRule = r.ReadToken();
        }

        public void Deserialize16to19(BinaryReader r)
        {
            Name = r.ReadToken();
            flags = new FlagField(r.ReadUInt32());

            LeadsToNodes = (r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());

            StartPosition = r.ReadVector3();
            EndPosition = r.ReadVector3();

            StartRotation = r.ReadQuaternion();
            EndRotation = r.ReadQuaternion();

            Length = r.ReadSingle();

            var nextLines = new int[linesCount];
            for (int i = 0; i < linesCount; i++)
            {
                nextLines[i] = r.ReadInt32();
            }

            var previousLines = new int[linesCount];
            for (int i = 0; i < linesCount; i++)
            {
                previousLines[i] = r.ReadInt32();
            }

            var nextUsed = r.ReadUInt32();
            var previousUsed = r.ReadUInt32();

            NextLines = new(linesCount, nextLines[0..(int)nextUsed]);
            PreviousLines = new(linesCount, previousLines[0..(int)previousUsed]);

            SemaphoreId = r.ReadInt32();

            TrafficRule = r.ReadToken();

            NavNodeIndex = r.ReadUInt32();
        }

        public void Serialize(BinaryWriter w)
        {
            w.Write(Name);
            w.Write(flags.Bits);

            w.Write(LeadsToNodes.EndNode);
            w.Write(LeadsToNodes.EndLane);
            w.Write(LeadsToNodes.StartNode);
            w.Write(LeadsToNodes.StartLane);

            w.Write(StartPosition);
            w.Write(EndPosition);

            w.Write(StartRotation);
            w.Write(EndRotation);

            w.Write(Length);

            // used
            foreach (var idx in NextLines)
            {
                w.Write(idx);
            }
            // unused
            for (int i = 0; i < linesCount - NextLines.Count; i++)
            {
                w.Write(-1);
            }

            // used
            foreach (var idx in PreviousLines)
            {
                w.Write(idx);
            }
            // unused
            for (int i = 0; i < linesCount - PreviousLines.Count; i++)
            {
                w.Write(-1);
            }

            w.Write(NextLines.Count);
            w.Write(PreviousLines.Count);

            w.Write(SemaphoreId);

            w.Write(TrafficRule);

            w.Write(NavNodeIndex);
        }
    }
}
