using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using SignedDistanceFields;
using SparseVoxelOctree;
using System.Runtime.CompilerServices;
using faces;


namespace DualContour
{

    public struct Quad
    {
        public int v0;
        public int v1;
        public int v2;
        public int v3;

        public int sign;
        public int dir;
    };


    public class Dual_Contour
    {

        bool block_voxel;
        private List<Vector3> vertices;

        readonly float[] vertValues = new float[8];
        readonly Vector3[] vertPos = new Vector3[8];

        //NOISE FUNCTIONS TEMPORARY
        Texture3D noiseTexture = Resources.Load("PlanetTexture") as Texture3D;
        private int width;
        private Vector3 global;
        private float radius;

        //helper variables to speed up vertex placement
        Func<Vector3, float, Vector3> coordTransformFunction;

        public List<Quad> quads = new();
        public List<BISDF> sdfs;


        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir)
        {
            global = _global;
            block_voxel = mode;
            radius = iradius;
        }
        public Dual_Contour()
        {
            width = noiseTexture.width;
        }

        private float Function(Vector3 pos)
        {


            Vector3 sphereCenter = Vector3.one * this.radius;
            Vector3 spherePos = pos - sphereCenter;

            float sphereSDF = spherePos.magnitude - this.radius * 0.8f;
            float frequency = .05f;
            float amplitude = 50.0f;
            spherePos /= width; //even dimensions
            float contVal = 0;//(1f - Mathf.Abs(noiseTexture.GetPixelBilinear(spherePos.x * .01f, spherePos.y * .01f, spherePos.z * .01f).r) * 100);

            float value = -(noiseTexture.GetPixelBilinear(spherePos.x * frequency, spherePos.y * frequency, spherePos.z * frequency).r * amplitude) - sphereSDF + contVal;

            int sdfCount = sdfs.Count;
            for (int i = 0; i < sdfCount; ++i)
            {
                value = Mathf.Min(value, sdfs[i].Evaluate(pos));
            }

            return value;
        }

        ///INITIALIZE GRID INFORMATION UPON STARTUP/----------------------------------------------------------------------------

        public void SetVertexList(List<Vector3> v) { vertices = v; }

        public void SetSDFEditList(List<BISDF> sdfs_) { sdfs = sdfs_; }

        public void SetRadius(float r) { radius = r; }
        public Vector3 FindTransformedCoord(Vector3 pos, int elevation) { return coordTransformFunction(pos, elevation); }

        public void SetGlobal(Vector3 g) { global = g; }
        public void SetBlockVoxel(bool b) { block_voxel = b; }

        float Adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void SVOVertex(SVONode node)
        {
            //these are being allocated every frame, which is bad
            const float eps = 1e-9f;

            float xPos, yPos, zPos;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;

            for (int i = 0; i < 8; ++i)
            {
                //eulerian coordinate grid at resolution node.size
                xPos = node.position.x + ((i >> 2) & 0x01) * node.size;
                yPos = node.position.y + ((i >> 1) & 0x01) * node.size;
                zPos = node.position.z + (i & 0x01) * node.size;

                //evaluate the position and value of each vertex in the unit cube
                vertPos[i] = new Vector3(xPos, yPos, zPos);

                vertValues[i] = Function(vertPos[i]); //base SDF
                if (i == 0 && vertValues[i] > 2 * node.size) return;
                max = vertValues[i] > max ? vertValues[i] : max;
                min = vertValues[i] < min ? vertValues[i] : min;
            }
            //calculate the adapt of only the edges that cross, rather than the whole thing
            //calculate the positions of the edges itself

            node.minSDF = min;
            node.maxSDF = max;

            Vector3 avg = Vector3.zero;
            int count = 0;

            //replace Vector3 with dynamic sizing

            //we then identify where changes in the function are (sign changes)
            bool signChange = false;
            bool xCross;
            bool yCross;
            bool zCross;

            //set sign change if any edge is crossed
            signChange |= (vertValues[0] > eps) != (vertValues[4] > eps);
            signChange |= (vertValues[0] > eps) != (vertValues[1] > eps);
            signChange |= (vertValues[5] > eps) != (vertValues[1] > eps);
            signChange |= (vertValues[5] > eps) != (vertValues[4] > eps);
            signChange |= (vertValues[4] > eps) != (vertValues[6] > eps);
            signChange |= (vertValues[1] > eps) != (vertValues[3] > eps);
            signChange |= (vertValues[0] > eps) != (vertValues[2] > eps);
            signChange |= yCross = (vertValues[5] > eps) != (vertValues[7] > eps);
            signChange |= zCross = (vertValues[6] > eps) != (vertValues[7] > eps);
            signChange |= xCross = (vertValues[3] > eps) != (vertValues[7] > eps);
            signChange |= (vertValues[2] > eps) != (vertValues[3] > eps);
            signChange |= (vertValues[2] > eps) != (vertValues[6] > eps);

            if (!signChange) return;

            //X EDGES NEW 
            for (int i = 0; i < 4; ++i)
            {
                float a = vertValues[i];
                float b = vertValues[i | 0x04];
                if (a > 0 != b > 0)
                {
                    avg += vertPos[i] + Adapt(a, b) * (vertPos[i | 0x04] - vertPos[i]);
                    count++;
                }
            }

            //Y EDGES
            int j = 0;
            for (int i = 0; i < 4; ++i)
            {

                if (i < 2)
                {
                    j = i;
                }
                else
                {
                    j = i == 2 ? 4 : 5;
                }
                float a = vertValues[j];
                float b = vertValues[j | 0x02];
                if (a > 0 != b > 0)
                {
                    avg += vertPos[j] + Adapt(a, b) * (vertPos[j | 0x02] - vertPos[j]);
                    count++;
                }
            }

            //Z EDGES
            for (int i = 0; i < 4; ++i)
            {
                float a = vertValues[(i << 1)];
                float b = vertValues[(i << 1) | 0x01];
                if (a > 0 != b > 0)
                {
                    avg += vertPos[i << 1] + Adapt(a, b) * (vertPos[(i << 1) | 0x01] - vertPos[i << 1]);
                    count++;
                }
            }

            avg /= count > 1 ? count : 1;
            //convert the average position to a sphere

            //figure out what edge was crossed (axis)

            //Assign the sign of the edge according to how the flip occurs

            //The normals shouldn't have x,y,z as their parameters, but should instead reflect
            //the positions of the intermediate point on the edge.

            int newedge = 0;

            if (xCross)
            {
                newedge |= 1 << 5;
                newedge |= (((vertValues[3] > 0) && !(vertValues[7] > 0)) ? 1 : 0) << 2;
            }
            if (yCross)
            {
                newedge |= 1 << 4;
                newedge |= ((!(vertValues[5] > 0) && (vertValues[7] > 0)) ? 1 : 0) << 1;
            }
            if (zCross)
            {
                newedge |= 1 << 3;
                newedge |= ((vertValues[6] > 0) && !(vertValues[7] > 0)) ? 1 : 0;
            }

            //block_voxel = true;
            Vector3 vertex = block_voxel ? vertPos[0] : avg;

            node.vertex = vertex;
            node.edge = newedge;


        }

        // axis bit constants used throughout the method
        public const int X_AXIS = 4;
        public const int Y_AXIS = 2;
        public const int Z_AXIS = 1;



        //Something to fix here. The faceproc is assuming node1 to be the higher node, even though this doesnt make sense per say.
        //Can reverse the order, and redo the edgeproc calls to reflect it. This is becuase edgeproc assumes the preservation of
        //local order and bit coordinates
        public void CellProcHelper(SVONode root)
        {
            CellProc(root);
        }

        public void RefreshNeighborChunk(SVONode node1, SVONode node2, int direction)
        {
            FaceProcedure(node1, node2, direction);
        }

        public void RefreshNeighborCorner(SVONode node1, SVONode node2, SVONode node3, SVONode node4, int MergedDirection)
        {
            //finds the common axis among all 4 nodes, even if a pair of axis is flipped
            EdgeProc(node1, node2, node3, node4, MergedDirection ^ 0b111);
        }

        private void CellProc(SVONode node)
        {
            if (node.isLeaf) return;
            for (int n = 0; n < 8; ++n)
                CellProc(node.children[n]);

            FaceProcedure(node.children[0], node.children[1], Z_AXIS);
            FaceProcedure(node.children[0], node.children[4], X_AXIS);
            FaceProcedure(node.children[2], node.children[6], X_AXIS);
            FaceProcedure(node.children[2], node.children[3], Z_AXIS);
            FaceProcedure(node.children[4], node.children[5], Z_AXIS);
            FaceProcedure(node.children[1], node.children[5], X_AXIS);
            FaceProcedure(node.children[6], node.children[7], Z_AXIS);
            FaceProcedure(node.children[3], node.children[7], X_AXIS);
            FaceProcedure(node.children[0], node.children[2], Y_AXIS);
            FaceProcedure(node.children[4], node.children[6], Y_AXIS);
            FaceProcedure(node.children[1], node.children[3], Y_AXIS);
            FaceProcedure(node.children[5], node.children[7], Y_AXIS);
            //all winding the same way, will change winding based on sign
            EdgeProc(node.children[2], node.children[3], node.children[7], node.children[6], Y_AXIS); // +y face
            EdgeProc(node.children[0], node.children[1], node.children[5], node.children[4], Y_AXIS); // -y face

            EdgeProc(node.children[0], node.children[1], node.children[3], node.children[2], X_AXIS); // -x face
            EdgeProc(node.children[4], node.children[5], node.children[7], node.children[6], X_AXIS); // +x face

            EdgeProc(node.children[0], node.children[2], node.children[6], node.children[4], Z_AXIS); // -z face
            EdgeProc(node.children[1], node.children[3], node.children[7], node.children[5], Z_AXIS); // +z face



        }
        private static readonly byte[,,] indices =
        {
            {{0,0,0,0},{0,0,0,0}},
            { { 1, 0, 2, 3 }, { 1, 0, 4, 5 }}, //z dir
            { { 2, 3, 1, 0 }, { 2, 0, 4, 6 }}, //y dir
            {{0,0,0,0},{0,0,0,0}},
            { { 4, 5, 1, 0 }, { 4, 6, 2, 0 }}, //x dir
        };

        private static readonly byte[,] AlternateDimensions = {
            { 0, 0 },
            { X_AXIS, Y_AXIS }, //z
            { X_AXIS, Z_AXIS }, //y
            { 0, 0 },
            { Y_AXIS, Z_AXIS } //x
        };

        private void FaceProcedure(SVONode node1, SVONode node2, int direction)
        {
            if (node1.isLeaf && node2.isLeaf) return;

            SVONode flip = node1, flip1 = node2;

            if (direction == Z_AXIS)
            {
                flip = node2;
                flip1 = node1;
            }

            for (byte i = 0; i < 4; ++i)
            {
                int axisIndex = (i & 2) >> 1;
                byte axis = AlternateDimensions[direction, axisIndex];

                byte top = (byte)((i & 1) * axis);

                byte i0 = (byte)(indices[direction, axisIndex, 0] | top);
                byte i1 = (byte)(indices[direction, axisIndex, 1] | top);
                byte i2 = (byte)(indices[direction, axisIndex, 2] | top);
                byte i3 = (byte)(indices[direction, axisIndex, 3] | top);

                if (direction == Y_AXIS)
                {
                    if (axis == X_AXIS)
                    {
                        flip = node1;
                        flip1 = node2;
                    }
                    else
                    {
                        flip = node2;
                        flip1 = node1;
                    }
                }

                SVONode term1 = GetChild(node1, i0);
                SVONode term2 = GetChild(flip, i1);

                SVONode term3 = GetChild(node2, i2);
                SVONode term4 = GetChild(flip1, i3);

                EdgeProc(term1, term2, term3, term4, axis);
            }

            int offset;
            for (int a = 0; a < 2; a++)
            {
                for (int b = 0; b < 2; b++)
                {
                    offset = offsets[direction, (a << 1) | b];

                    FaceProcedure(GetChild(node1, offset | direction), GetChild(node2, offset), direction);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SVONode GetChild(SVONode node, int index)
        {
            return node.isLeaf ? node : node.children[index];
        }

        private static readonly byte[,] offsets = {
            {0,0,0,0},
            {0,2,4,6}, //z
            {0,1,4,5}, //y
            {0,0,0,0},
            {0,1,2,3}, //x,
        };

        private static readonly byte[,] edgeNewDirs =
        {
            {0,0,0,0},
             { X_AXIS | Y_AXIS, X_AXIS, 0, Y_AXIS }, //z
             { X_AXIS | Z_AXIS, X_AXIS, 0, Z_AXIS }, //y
            {0,0,0,0},
             { Y_AXIS | Z_AXIS, Y_AXIS, 0, Z_AXIS }, //x
        };

        private void EdgeProc(SVONode node1, SVONode node2, SVONode node3, SVONode node4, int direction)
        {
            //if one of them is a leaf and is higher on the octree than the others, then bit order is not preserved
            //gotta figure out which axis we're working with (x,y,z)

            //once we figure that out, we consider the case where if any one of the nodes is not a leaf, then we split intp
            //two edgeproc groups, one for top half, one for bottom half
            if (node1.isLeaf && node2.isLeaf && node3.isLeaf && node4.isLeaf)
            {
                if (node1.localIndex < 0 || node2.localIndex < 0 || node3.localIndex < 0 || node4.localIndex < 0) return;

                //base case, return, make quad, do what we need to do

                quads.Add(new Quad
                {
                    v0 = node1.localIndex,
                    v1 = node2.localIndex,
                    v2 = node3.localIndex,
                    v3 = node4.localIndex,
                    sign = node1.edge,
                    dir = direction
                });
            }
            else
            {
                //only works iff the space orientations are obeyed when passed in as nodes, otherwise this falls apart

                EdgeProc(GetChild(node1, edgeNewDirs[direction, 0] | direction),
                            GetChild(node2, edgeNewDirs[direction, 1] | direction),
                            GetChild(node3, edgeNewDirs[direction, 2] | direction),
                            GetChild(node4, edgeNewDirs[direction, 3] | direction), direction
                             );

                EdgeProc(GetChild(node1, edgeNewDirs[direction, 0]),
                            GetChild(node2, edgeNewDirs[direction, 1]),
                            GetChild(node3, edgeNewDirs[direction, 2]),
                            GetChild(node4, edgeNewDirs[direction, 3]), direction
                             );

            }


        }

    }
}
