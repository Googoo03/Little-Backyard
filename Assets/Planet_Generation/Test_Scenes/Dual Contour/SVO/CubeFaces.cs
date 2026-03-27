using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;

namespace faces
{
    [System.Serializable]
    public class Face
    {
        public SVOTest[] neighbors;
        public Face(SVOTest U_, SVOTest NU_, SVOTest V_, SVOTest NV_) //assign U and V neighbors
        {
            neighbors = new SVOTest[4] { U_, NU_, V_, NV_ };
        }

        public Face(SVOTest X_, SVOTest Z_)
        {
            neighbors = new SVOTest[2] { X_, Z_ };
        }

        static public readonly int[][] CornerGroups = { new int[] { 0, 2, 4 }, new int[] { 1, 3, 5 } };

        static public readonly FaceInfo[] Faces =
        {
            // +Z
            new FaceInfo(
                Vector3.forward,   // normal
                Vector3.down,     // u
                Vector3.right         // v
            ),

            // -Z
            new FaceInfo(
                Vector3.back,      // normal
                Vector3.left,      // u
                Vector3.up         // v
            ),

            // +Y
            new FaceInfo(
                Vector3.up,        // normal
                Vector3.left,   // u
                Vector3.forward      // v
            ),

            // -Y
            new FaceInfo(
                Vector3.down,      // normal
                Vector3.back,      // u
                Vector3.right      // v
            ),

            // +X
            new FaceInfo(
                Vector3.right,     // normal
                Vector3.back,        // u
                Vector3.up       // v
            ),

            // -X
            new FaceInfo(
                Vector3.left,      // normal
                Vector3.down,        // u
                Vector3.forward    // v
            ),
        };

        public struct FaceInfo
        {
            public Vector3 normal;
            public Vector3 uaxis;
            public Vector3 vaxis;

            public FaceInfo(Vector3 n_, Vector3 u_, Vector3 v_)
            {
                normal = n_;
                uaxis = u_;
                vaxis = v_;
            }
        }
    };
}
