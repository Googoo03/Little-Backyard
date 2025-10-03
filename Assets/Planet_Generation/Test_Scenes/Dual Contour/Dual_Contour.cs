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



namespace DualContour
{

    enum BLOCKID : ushort
    {
        GRASS = 0,
        STONE = 1,
        COPPER_ORE = 2,
        IRON_ORE = 3,
        AIR = 15
    }

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
        private Dictionary<Vector3, Vector3> verticesDict;



        float[] vertValues = new float[8];
        Vector3[] vertPos = new Vector3[8];

        //Quad local vars
        SVONode[] neighbors = new SVONode[8];
        Trirule[] rules = {
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
        Noise simplexNoise = new Noise();

        //
        private Vector3 global;
        private Vector3 min;
        private Vector3 max;

        //EDIT VARS
        private float radius;

        //helper variables to speed up vertex placement
        Func<Vector3, int, Vector3> coordTransformFunction;


        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir)
        {
            global = _global;
            Ground = 32;
            block_voxel = mode;
            radius = iradius;
            dir = idir;
            coordTransformFunction = CartesianToSphere;

        }

        public Dual_Contour()
        {


        }

        private float function(Vector3 pos, float y)
        {

            float frequency = .1f;


            Vector3 spherePos = new Vector3(pos.x, 0, pos.z);

            float radius = pos.y;
            float value = -radius + 20;
            float amplitude = 20;
            float domainWarp = simplexNoise.CalcPixel3D(pos.x, pos.y, pos.z) * amplitude;
            int octaves = 5;
            float lacunarity = 2;
            float persistence = 0.5f;
            float totalValue = 0;
            for (int i = 0; i < octaves; ++i)
            {
                value += simplexNoise.CalcPixel3D((spherePos.x + domainWarp) * frequency, (spherePos.y + domainWarp) * frequency, (spherePos.z + domainWarp) * frequency) * amplitude;
                totalValue += amplitude;
                frequency *= lacunarity;
                amplitude *= persistence;
            }

            return value / totalValue;
        }

        ///INITIALIZE GRID INFORMATION UPON STARTUP/----------------------------------------------------------------------------

        public void SetVertexList(List<Vector3> v) { vertices = v; }

        [BurstCompile]
        public void InitializeGrid(bool seam, ref NativeList<Vector3> verts, ref NativeList<int> ind) { }
        public Vector3 GetMin() { return min; }
        public Vector3 GetMax() { return max; }

        //returns the center based on base cartestian grid coordinates
        public Vector3 GetCenter() { return (min + max) / 2; }

        //////////////////////////////////////////////////////////////////////////////

        private Vector3 CartesianToSphere(Vector3 pos, int elevation)
        {

            //Given a grid position, convert the point into a shell point
            //dir contains the u and v direction, which are masks for the x and z positions.
            dir = 65;
            float radius = 1024;

            int uSign = ((dir & 0x80) != 0) ? 1 : -1;
            int vSign = ((dir & 0x08) != 0) ? 1 : -1;
            Vector3 uaxis = new Vector3(((dir >> 6 & 0x7FFF) & 0x01), (dir >> 5) & 0x01, (dir >> 4) & 0x01) * (uSign);
            Vector3 vaxis = new Vector3(((dir >> 2) & 0x01), (dir >> 1) & 0x01, (dir) & 0x01) * (vSign);
            Vector3 wAxis = Vector3.Cross(vaxis, uaxis);


            //no idea why the step.x / 2. Why would it need to be pushed back half a unit? BECAUSE THE QUADS ARE CENTERED BY DEFAULT
            Vector3 newpos = uaxis * pos.x + vaxis * pos.z + wAxis * (radius - step.y / 2);

            return newpos.normalized * (radius + (elevation * step.y) + offset.y);
        }

        Vector3 CubeToSphereDirection(Vector3 p)
        {
            float x = p.x;
            float y = p.y;
            float z = p.z;

            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;

            return new Vector3(
                x * Mathf.Sqrt(1f - (y2 / 2f) - (z2 / 2f) + (y2 * z2) / 3f),
                y * Mathf.Sqrt(1f - (z2 / 2f) - (x2 / 2f) + (x2 * z2) / 3f),
                z * Mathf.Sqrt(1f - (x2 / 2f) - (y2 / 2f) + (x2 * y2) / 3f)
            );
        }

        private Vector3 GridToSphere(Vector3 pos, int elevation)
        {
            float r = pos.magnitude; // radial distance
            return CubeToSphereDirection(pos.normalized) * r;
        }

        //returns in radians the polar coords of a given cartesian coordinate
        private Vector3 CartesianToPolarCoords(Vector3 Relativecartesian)
        {
            float r = Relativecartesian.magnitude;
            float theta = Mathf.Atan2(Relativecartesian.y, Relativecartesian.x);
            float phi = Mathf.Acos(Relativecartesian.z / r);
            return new Vector3(r, theta, phi);
        }

        private Vector3 Grid(Vector3 pos, int elevation) { return pos; }

        public Vector3 FindTransformedCoord(Vector3 pos, int elevation) { return coordTransformFunction(pos, elevation); }

        private Vector3 ShellElevate(Vector3 pos) { return new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z); }

        public void SetGlobal(Vector3 g) { global = g; }

        public void SetBlockVoxel(bool b) { block_voxel = b; }

        public Vector3 GetStep() { return step; }

        public float GetSideLength() { return max.x - min.x; }

        public Vector3 GetOffset() { return offset; }

        public void SetVertexDictionary(Dictionary<Vector3, Vector3> dict) { verticesDict = dict; }

        float Adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void SVOVertex(SVONode node)
        {
            //these are being allocated every frame, which is bad


            float xPos, yPos, zPos;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;

            for (int i = 0; i < 8; ++i)
            {
                xPos = node.position.x + ((i >> 2) & 0x01) * node.size;
                yPos = node.position.y + ((i >> 1) & 0x01) * node.size;
                zPos = node.position.z + (i & 0x01) * node.size;

                //evaluate the position and value of each vertex in the unit cube
                vertPos[i] = new Vector3(xPos, yPos, zPos);
                //vertValues[i] = -vertPos[i].y + 10 + Mathf.Sin(vertPos[i].x) * 2;
                vertValues[i] = function(vertPos[i], yPos); //base SDF
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
            signChange |= (vertValues[0] > 0) != (vertValues[4] > 0);
            signChange |= (vertValues[0] > 0) != (vertValues[1] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[1] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[4] > 0);
            signChange |= (vertValues[4] > 0) != (vertValues[6] > 0);
            signChange |= (vertValues[1] > 0) != (vertValues[3] > 0);
            signChange |= (vertValues[0] > 0) != (vertValues[2] > 0);
            signChange |= yCross = (vertValues[5] > 0) != (vertValues[7] > 0);
            signChange |= zCross = (vertValues[6] > 0) != (vertValues[7] > 0);
            signChange |= xCross = (vertValues[3] > 0) != (vertValues[7] > 0);
            signChange |= (vertValues[2] > 0) != (vertValues[3] > 0);
            signChange |= (vertValues[2] > 0) != (vertValues[6] > 0);

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





        public void SVOQuad(SVONode node, List<SVONode> nodes, List<int> indices, Dictionary<Vector3, int> globalToLocal, List<Vector3> chunkVerts)
        {



            SVONode zNeighbor = node.GetNeighborLOD(1);
            SVONode yNeighbor = node.GetNeighborLOD(2);
            SVONode yzNeighbor = yNeighbor?.GetNeighborLOD(1);
            SVONode xNeighbor = node.GetNeighborLOD(4);
            SVONode zxNeighbor = zNeighbor?.GetNeighborLOD(4);
            SVONode xyNeighbor = xNeighbor?.GetNeighborLOD(2);
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

                    int directionAxis = j == 0 ? verts.v1 : verts.v4;

                    List<SVONode> face = baseNeighbors[directionAxis]?.GetFace(directionAxis);
                    if (face == null) continue;
                    for (int k = 0; k < face.Count; ++k)
                    {
                        zNeighbor = directionAxis == 1 ? face[k] : node.GetNeighborLOD(1);
                        yNeighbor = directionAxis == 2 ? face[k] : node.GetNeighborLOD(2);
                        xNeighbor = directionAxis == 4 ? face[k] : node.GetNeighborLOD(4);

                        // choose diagonal neighbors: drive by the *finer* neighbor consistently
                        yzNeighbor = (yNeighbor != null && zNeighbor != null)
                            ? (yNeighbor.size < zNeighbor.size ? yNeighbor.GetNeighborLOD(1) : zNeighbor.GetNeighborLOD(2))
                            : (yNeighbor ?? zNeighbor)?.GetNeighborLOD((yNeighbor != null) ? 1 : 2);

                        zxNeighbor = (zNeighbor != null && xNeighbor != null)
                            ? (zNeighbor.size < xNeighbor.size ? zNeighbor.GetNeighborLOD(4) : xNeighbor.GetNeighborLOD(1))
                            : (zNeighbor ?? xNeighbor)?.GetNeighborLOD((zNeighbor != null) ? 4 : 1);

                        xyNeighbor = (xNeighbor != null && yNeighbor != null)
                            ? (xNeighbor.size < yNeighbor.size ? xNeighbor.GetNeighborLOD(2) : yNeighbor.GetNeighborLOD(4))
                            : (xNeighbor ?? yNeighbor)?.GetNeighborLOD((xNeighbor != null) ? 2 : 4);

                        SVONode[] neighbors = { node, zNeighbor, yNeighbor, yzNeighbor, xNeighbor, zxNeighbor, xyNeighbor };

                        int getfaceVal = verts.v2;
                        List<SVONode> diagonal = neighbors[getfaceVal]?.GetFace(getfaceVal);

                        //if the first neighbor we're working with has disparate sizes with the second neighbor, do diagonal. Else, diagonal should be 1

                        int diagonalSize = diagonal?.Count ?? 0;

                        for (int l = 0; l < diagonalSize; ++l)
                        {
                            SVONode n0 = neighbors[verts[j * 3]];
                            SVONode n1 = neighbors[verts[j * 3 + 1]];
                            SVONode n2 = neighbors[verts[j * 3 + 2]];

                            if (n0.IsEmpty() || n1.IsEmpty() || n2.IsEmpty() || diagonal[l].IsEmpty()) continue;

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
