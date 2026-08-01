using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Models.Ppd
{
    /// <summary>
    /// Represents a terrain point.
    /// </summary>
    public struct TerrainPoint
    {
        /// <summary>
        /// The position of the point.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// The normal vector of the point.
        /// </summary>
        public Vector3 Normal { get; set; }

        public TerrainPoint()
        {
            Position = Vector3.Zero;
            Normal = Vector3.Zero;
        }

        public TerrainPoint(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;
        }
    }
}
