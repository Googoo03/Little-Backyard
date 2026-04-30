using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using SignedDistanceFields;
using Unity.Collections;
using chunk_events;
using Unity.Jobs;
using Unity.Burst;
using SparseVoxelOctree;
using System.Runtime.CompilerServices;
using faces;
using UnityEditor.Experimental.GraphView;


namespace DualContour
{
    public struct Trirule
    {
        public int axis;
        public int sign;
        //public (int dx, int dy, int dz)[] vertpos;
        public int v0;
        public int v1;
        public int v2;
        public int v3;
        public int v4;
        public int v5;

        // indexer to access like an array
        public int this[int i]
        {
            get => i switch
            {
                0 => v0,
                1 => v1,
                2 => v2,
                3 => v3,
                4 => v4,
                5 => v5,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

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

        [SerializeField] private float Ground;

        //GLOBAL VARS
        Vector3 step;
        Vector3 offset;

        int dir;
        bool block_voxel;
        private List<Vector3> vertices;

        readonly float[] vertValues = new float[8];
        readonly Vector3[] vertPos = new Vector3[8];
        Vector3 uaxis;
        Vector3 vaxis;
        Vector3 wAxis;

        readonly Trirule[] rules = {
            new() { //x axis
                axis = 0x20,
                sign = 0x10,

                v0 = 0,
                v1 = 2,
                v2 = 3,
                v3 = 3,
                v4 = 1,
                v5 = 0
            },
            new () { //y axis
                axis = 0x08,
                sign = 0x04,

                v0 = 0,
                v1 = 4,
                v2 = 5,
                v3 = 5,
                v4 = 1,
                v5 = 0

            },
            new () { //z axis
                axis = 0x02,
                sign = 0x01,

                v0 = 0,
                v1 = 4,
                v2 = 6,
                v3 = 6,
                v4 = 2,
                v5 = 0

            },
        };

        //NOISE FUNCTIONS TEMPORARY
        Texture3D noiseTexture = Resources.Load("PlanetTexture") as Texture3D;
        Noise simplexNoise = new();
        private Vector3 global;
        private float radius;

        //helper variables to speed up vertex placement
        Func<Vector3, float, Vector3> coordTransformFunction;

        public List<Quad> quads = new();


        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir)
        {
            global = _global;
            Ground = 32;
            block_voxel = mode;
            radius = iradius;
            dir = idir;

        }
        public Dual_Contour() { }

        private float Function(Vector3 pos)
        {
            Vector3 sphereCenter = Vector3.one * this.radius;
            Vector3 spherePos = pos - sphereCenter;

            float sdf = spherePos.magnitude - this.radius * 0.8f;
            float frequency = .05f;
            float amplitude = 50.0f;
            spherePos /= noiseTexture.width; //even dimensions
            float contVal = (1f - Mathf.Abs(noiseTexture.GetPixelBilinear(spherePos.x * .01f, spherePos.y * .01f, spherePos.z * .01f).r) * 100);


            float value = -(1f - Mathf.Abs(noiseTexture.GetPixelBilinear(spherePos.x * frequency, spherePos.y * frequency, spherePos.z * frequency).r * amplitude)) - sdf + contVal;

            return value;
        }

        ///INITIALIZE GRID INFORMATION UPON STARTUP/----------------------------------------------------------------------------

        public void SetVertexList(List<Vector3> v) { vertices = v; }

        public void SetRadius(float r) { radius = r; }
        public Vector3 FindTransformedCoord(Vector3 pos, int elevation) { return coordTransformFunction(pos, elevation); }

        private Vector3 ShellElevate(Vector3 pos) { return new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z); }
        public void SetGlobal(Vector3 g) { global = g; }
        public void SetBlockVoxel(bool b) { block_voxel = b; }
        public void SetDir(int d) { dir = d; }

        public Vector3 GetWAxis() { return wAxis; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 CubeToSphere(Vector3 pos)
        {
            float elevation = pos.y;
            return ((uaxis * pos.x) + (vaxis * pos.z) + (wAxis - offset) * (radius * 0.5f)).normalized * ((radius * 0.5f) + elevation);
        }



        public void SetCubeAxis(int faceNum)
        {
            uaxis = Face.Faces[faceNum].uaxis;
            vaxis = Face.Faces[faceNum].vaxis;
            wAxis = Face.Faces[faceNum].normal;
            offset = (uaxis + vaxis);
        }

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
            foreach (SVONode child in node.children)
                CellProc(child);

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

        private int[] xIndicesNormal = { 2, 3, 1, 0 };
        private int[] xIndicesFlip = { 1, 0, 2, 3 };
        private int[] yIndicesNormal = { 4, 5, 1, 0 };
        private int[] yIndicesFlip = { 1, 0, 4, 5 };
        private int[] zIndicesNormal = { 4, 6, 2, 0 };
        private int[] zIndicesFlip = { 2, 0, 4, 6 };
        private int[] XAlternateDimension = { Y_AXIS, Z_AXIS };
        private int[] yAlternateDimension = { X_AXIS, Z_AXIS };
        private int[] zAlternateDimension = { X_AXIS, Y_AXIS };

        private void FaceProcedure(SVONode node1, SVONode node2, int direction)
        {
            if (node1.isLeaf && node2.isLeaf) return;

            int[] xIndices = null;
            int[] yIndices = null;
            int[] zIndices = null;
            int[] alternateDimension;


            switch (direction)
            {
                case X_AXIS:
                    alternateDimension = XAlternateDimension;
                    yIndices = yIndicesNormal;
                    zIndices = zIndicesNormal;
                    break;
                case Y_AXIS:
                    alternateDimension = yAlternateDimension;
                    xIndices = xIndicesNormal;
                    zIndices = zIndicesFlip;
                    break;
                case Z_AXIS:
                    alternateDimension = zAlternateDimension;
                    xIndices = xIndicesFlip;
                    yIndices = yIndicesFlip;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < 4; ++i)
            {
                int axis = alternateDimension[i < 2 ? 0 : 1];

                int top = (i % 2 == 0) ? 0 : axis;
                int[] indices = axis switch
                {
                    X_AXIS => xIndices,
                    Y_AXIS => yIndices,
                    Z_AXIS => zIndices,
                    _ => throw new ArgumentOutOfRangeException()
                };

                int i0 = indices[0] | top;
                int i1 = indices[1] | top;
                int i2 = indices[2] | top;
                int i3 = indices[3] | top;

                SVONode term1 = node1.isLeaf ? node1 : node1.children[i0];
                SVONode term2 = direction switch
                {
                    X_AXIS => node1.isLeaf ? node1 : node1.children[i1],
                    Y_AXIS => axis == X_AXIS ? (node1.isLeaf ? node1 : node1.children[i1]) : (node2.isLeaf ? node2 : node2.children[i1]),
                    Z_AXIS => node2.isLeaf ? node2 : node2.children[i1],
                    _ => throw new ArgumentOutOfRangeException()
                };
                SVONode term3 = node2.isLeaf ? node2 : node2.children[i2];
                SVONode term4 = direction switch
                {
                    X_AXIS => node2.isLeaf ? node2 : node2.children[i3],
                    Y_AXIS => axis == X_AXIS ? (node2.isLeaf ? node2 : node2.children[i3]) : (node1.isLeaf ? node1 : node1.children[i3]),
                    Z_AXIS => node1.isLeaf ? node1 : node1.children[i3],
                    _ => throw new ArgumentOutOfRangeException()
                };


                EdgeProc(term1, term2, term3, term4, axis);
            }

            int offset;
            for (int a = 0; a < 2; a++)
            {
                for (int b = 0; b < 2; b++)
                {
                    offset = direction switch
                    {
                        X_AXIS => (a << 1) | (b << 0),
                        Y_AXIS => (a << 2) | (b << 0),
                        Z_AXIS => (a << 2) | (b << 1),
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    FaceProcedure(node1.isLeaf ? node1 : node1.children[(offset | direction)],
                            node2.isLeaf ? node2 : node2.children[(offset)], direction);
                }
            }
        }
        //Assume node1 is on the lower end, therefore its nodes are higher axis

        private static readonly int[][] edgeNewDirs =
        {
            new int[4] { Y_AXIS | Z_AXIS, Y_AXIS, 0, Z_AXIS },
            new int[4] { X_AXIS | Z_AXIS, X_AXIS, 0, Z_AXIS },
            new int[4] { X_AXIS | Y_AXIS, X_AXIS, 0, Y_AXIS }
        };

        private void EdgeProc(SVONode node1, SVONode node2, SVONode node3, SVONode node4, int direction)
        {
            //if one of them is a leaf and is higher on the octree than the others, then bit order is not preserved
            //gotta figure out which axis we're working with (x,y,z)

            //once we figure that out, we consider the case where if any one of the nodes is not a leaf, then we split intp
            //two edgeproc groups, one for top half, one for bottom half
            if (node1.isLeaf && node2.isLeaf && node3.isLeaf && node4.isLeaf)
            {
                if (node1.localIndex < 0 || node2.localIndex < 0 || node3.localIndex < 0 || node4.localIndex < 0)
                {
                    //UnityEngine.Debug.Log("Error: Leaf node missing vertex");
                    return;
                }
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
                int[] newdirs = direction switch
                {
                    X_AXIS => edgeNewDirs[0],
                    Y_AXIS => edgeNewDirs[1],
                    Z_AXIS => edgeNewDirs[2],
                    _ => throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be X_AXIS, Y_AXIS, or Z_AXIS")
                };

                EdgeProc(node1.isLeaf ? node1 : node1.children[newdirs[0] | direction],
                            node2.isLeaf ? node2 : node2.children[newdirs[1] | direction],
                            node3.isLeaf ? node3 : node3.children[newdirs[2] | direction],
                            node4.isLeaf ? node4 : node4.children[newdirs[3] | direction], direction
                             );

                EdgeProc(node1.isLeaf ? node1 : node1.children[newdirs[0]],
                            node2.isLeaf ? node2 : node2.children[newdirs[1]],
                            node3.isLeaf ? node3 : node3.children[newdirs[2]],
                            node4.isLeaf ? node4 : node4.children[newdirs[3]], direction
                             );

            }


        }
        /*
                public void SVOQuad(SVONode node, List<SVONode> nodes, List<int> indices, List<Vector3> chunkVerts)
                {
                    // start with the six immediate neighbours; diagonal neighbours are
                    // simply the neighbour in the bitwise-OR direction, which already
                    // handles face crossings and mismatched LOD.
                    SVONode zNeighbor = SVONode.GetNeighborLOD(node, Z_AXIS);
                    SVONode yNeighbor = SVONode.GetNeighborLOD(node, Y_AXIS);
                    SVONode yzNeighbor = SVONode.GetNeighborLOD(node, Y_AXIS | Z_AXIS);
                    SVONode xNeighbor = SVONode.GetNeighborLOD(node, X_AXIS);
                    SVONode zxNeighbor = SVONode.GetNeighborLOD(node, Z_AXIS | X_AXIS);
                    SVONode xyNeighbor = SVONode.GetNeighborLOD(node, X_AXIS | Y_AXIS);
                    SVONode[] baseNeighbors = { node, zNeighbor, yNeighbor, yzNeighbor, xNeighbor, zxNeighbor, xyNeighbor };
                    int edge = node.edge;

                    foreach (Trirule rule in rules)
                    {
                        if ((edge & rule.axis) != rule.axis) continue;

                        var verts = rule;
                        if ((edge & rule.sign) != rule.sign)
                        {
                            verts = new Trirule
                            {
                                axis = rule.axis,
                                sign = rule.sign,
                                v0 = rule[0],
                                v1 = rule[4],
                                v2 = rule[2],
                                v3 = rule[3],
                                v4 = rule[1],
                                v5 = rule[5]
                            };
                        }

                        //two triangles to make a quad
                        for (int j = 0; j < 2; ++j)
                        {
                            int directionAxis = j == 0 ? verts.v1 : verts.v4; //middle vertex index of triangle depending on quad.

                            List<SVONode> face = SVONode.GetFace(baseNeighbors[directionAxis], directionAxis);

                            for (int k = 0; k < face?.Count; ++k)
                            {
                                zNeighbor = directionAxis == Z_AXIS ? face[k] : SVONode.GetNeighborLOD(node, Z_AXIS);
                                yNeighbor = directionAxis == Y_AXIS ? face[k] : SVONode.GetNeighborLOD(node, Y_AXIS);
                                xNeighbor = directionAxis == X_AXIS ? face[k] : SVONode.GetNeighborLOD(node, X_AXIS);

                                // diagonal neighbors are always obtained by asking the tree for
                                // the neighbour in the combined direction; this internally
                                // follows faces and resolves proper LOD for us.
                                zxNeighbor = (node.parentOBJ.faceNum & 1) == 1 ? SVONode.GetNeighborLOD(zNeighbor, X_AXIS, node) : SVONode.GetNeighborLOD(xNeighbor, Z_AXIS, node);
                                yzNeighbor = SVONode.GetNeighborLOD(zNeighbor, Y_AXIS, node);
                                xyNeighbor = SVONode.GetNeighborLOD(yNeighbor, X_AXIS, node);

                                SVONode[] neighbors = { node, zNeighbor, yNeighbor, yzNeighbor, xNeighbor, zxNeighbor, xyNeighbor };

                                int getfaceVal = verts.v2;

                                SVONode n0 = neighbors[verts[j * 3]];
                                SVONode n1 = neighbors[verts[j * 3 + 1]];
                                SVONode n2 = neighbors[verts[j * 3 + 2]];

                                List<SVONode> diagonal = SVONode.GetFace(neighbors[getfaceVal], getfaceVal);

                                //if the first neighbor we're working with has disparate sizes with the second neighbor, do diagonal. Else, diagonal should be 1

                                int diagonalSize = diagonal?.Count ?? 0;

                                for (int l = 0; l < diagonalSize; ++l)
                                {
                                    if (n1.IsEmpty() || diagonal[l].IsEmpty()) continue;
                                    for (int i = 0; i < 3; ++i)
                                    {
                                        var neighbor = neighbors[verts[j * 3 + i]];

                                        if (!neighbor.isLeaf) neighbor = diagonal[l];

                                        //if vertex doesn't exist in chunk yet, add it. Otherwise, change the index to find the vertex
                                        if (neighbor.localIndex == -1)
                                        {
                                            //add to chunk verts
                                            neighbor.localIndex = chunkVerts.Count;
                                            nodes.Add(neighbor);
                                            chunkVerts.Add(neighbor.vertex);
                                        }
                                        int newdualgrid = neighbor.localIndex;
                                        indices.Add(newdualgrid);
                                    }


                                }
                            }

                        }
                    }
                }*/

    }
}
