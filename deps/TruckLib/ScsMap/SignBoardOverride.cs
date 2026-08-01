using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// Overrides sign board attributes of a sign template.
    /// </summary>
    public class SignBoardOverride : IBinarySerializable
    {
        /// <summary>
        /// The name of the sign locator where the board will be placed.
        /// </summary>
        public Token AreaName { get; set; }

        /// <summary>
        /// Offset of the board. Coordinates must be between -1 and 1 and will be clamped
        /// to this range on serialization.
        /// </summary>
        public Vector2 Offset { get; set; }

        /// <summary>
        /// The board to use, as defined in <c>/def/sign/boards.sii</c>.
        /// If not set, the template default will be used.
        /// </summary>
        public Token Board { get; set; }

        private const float offsetFactor = 127f;

        /// <inheritdoc/>
        public void Deserialize(BinaryReader r, uint? version = null)
        {
            AreaName = r.ReadToken();

            var flags = new FlagField(r.ReadByte());

            if (flags.Bits > 3 || flags.Bits == 0)
            {
                Debugger.Break();
            }

            if (flags[0]) // Offset fields are present
            {
                var x = r.ReadSByte() / offsetFactor;
                var y = r.ReadSByte() / offsetFactor;
                Offset = new(x, y);
            }

            if (flags[1]) // Board definition is present
            {
                Board = r.ReadToken();
            }
        }

        /// <inheritdoc/>
        public void Serialize(BinaryWriter w)
        {
            w.Write(AreaName);

            var flags = new FlagField();
            flags[0] = Offset.X != 0 && Offset.Y != 0;
            flags[1] = Board.Value != 0;
            w.Write((byte)flags.Bits);

            if (flags[0])
            {
                w.Write((sbyte)Math.Clamp(Offset.X * offsetFactor, -1, 1));
                w.Write((sbyte)Math.Clamp(Offset.Y * offsetFactor, -1, 1));
            }

            if (flags[1])
            {
                w.Write(Board);
            }
        }
    }
}
