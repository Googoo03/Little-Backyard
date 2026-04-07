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


        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir)
        {
            global = _global;
            Ground = 32;
            block_voxel = mode;
            radius = iradius;
            dir = idir;

        }
        public Dual_Contour() { }

        private float Function(Vector3 pos, float y)
        {
            Vector3 spherePos = new(pos.x, pos.y, pos.z);

            float radius = y;
            float frequency = .05f;
            float amplitude = 50.0f;
            int octaves = 4;

            float contVal = (1f - Mathf.Abs(simplexNoise.CalcPixel3D(spherePos.x * .01f, spherePos.y * .01f, spherePos.z * .01f))) * 100;

            spherePos /= noiseTexture.width; //even dimensions
            float value = (1f - Mathf.Abs(noiseTexture.GetPixelBilinear(spherePos.x * frequency, spherePos.y * frequency, spherePos.z * frequency).r * amplitude)) + radius - contVal;

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
                vertPos[i] = CubeToSphere(new Vector3(xPos, yPos, zPos));

                vertValues[i] = Function(vertPos[i], vertPos[i].magnitude - radius); //base SDF
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
                newedge |= (((vertValues[3] > 0) && !(vertValues[7] > 0)) ? 1 : 0) << 4;
            }
            if (yCross)
            {
                newedge |= 1 << 3;
                newedge |= ((!(vertValues[5] > 0) && (vertValues[7] > 0)) ? 1 : 0) << 2;
            }
            if (zCross)
            {
                newedge |= 1 << 1;
                newedge |= ((vertValues[6] > 0) && !(vertValues[7] > 0)) ? 1 : 0;
            }

            //block_voxel = true;
            Vector3 vertex = block_voxel ? vertPos[0] : avg;

            node.vertex = vertex;
            node.edge = newedge;


        }

        // axis bit constants used throughout the method
        private const int X_AXIS = 4;
        private const int Y_AXIS = 2;
        private const int Z_AXIS = 1;

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
        }

    }
}
