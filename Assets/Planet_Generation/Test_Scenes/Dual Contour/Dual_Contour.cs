using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UnityEditor.PackageManager.Requests;
using chunk_events;


namespace DualContour
{

    public struct Edge
    {
        public bool crossed;
        public bool sign;

        public Edge(bool _crossed, bool _sign)
        {
            crossed = _crossed;
            sign = _sign;
        }
    };

    public struct cell
    {
        public int vertIndex;
        public Edge[] edges;

    };

    enum BLOCKID : ushort { 
        GRASS = 0,
        STONE = 1,
        COPPER_ORE = 2,
        IRON_ORE = 3,
        AIR = 15
    }


    public class Dual_Contour
    {
        //MESH DIMENSIONS
        [SerializeField] private int sizeX;
        [SerializeField] private int sizeY;
        [SerializeField] private int sizeZ;
        [SerializeField] private float T;
        [SerializeField] private float lastT;
        [SerializeField] private float amplitude;
        [SerializeField] private int LOD_Level;
        [SerializeField] private int dir;

        //MESH MODE
        [SerializeField] private bool block_voxel;

        [SerializeField] private float CELL_SIZE;

        [SerializeField] private float Ground;

        //Testing

        [SerializeField] private GameObject cube;

        //GLOBAL VARS
        Vector3 step;
        Vector3 offset;

        //VOXEL DATA
        UInt16[] voxel_data;

        List<Vector3> vertices;
        List<int> indices;

        private int[] dualGrid;
        private Vector3[] cellpos;
        private float[] cellvalues;

        //NOISE FUNCTIONS TEMPORARY
        Noise simplexNoise = new Noise();

        //
        private Vector3 global;

        //EDIT VARS
        private float radius;

        //helper variables to speed up vertex placement
        float[] vertValues = new float[8];
        Vector3[] vertPos = new Vector3[8];
        Func<Vector3, int, Vector3> coordTransformFunction;



        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir) {
            global = _global;
            sizeX = scale.x;
            sizeY = scale.y;
            sizeZ = scale.z;
            CELL_SIZE = length;
            LOD_Level = ilodLevel;
            Ground = 2;
            amplitude = 5;
            block_voxel = mode;
            radius = iradius;
            dir = idir;
            coordTransformFunction = Grid;

            //determines the "step" between vertices to cover 1 unit total
            step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;
            

            //sets the offset of the grid by a power of two based on the LOD level
            //offset = -Vector3.one * (1f / (1 << (LOD_Level + 1))) * (CELL_SIZE);
            offset = -Vector3.one * CELL_SIZE * 0.5f;
            offset.x += ioffset.x * CELL_SIZE;
            offset.y += ioffset.y * CELL_SIZE;
            offset.z += ioffset.z * CELL_SIZE;
            
        }

        private float function(Vector3 pos, float elevation)
        {


            float domainWarp = simplexNoise.CalcPixel3D(pos.x * 5, pos.y * 5, pos.z * 5) * 2f;

            if (elevation >= sizeY - 1) { return -1; }
            if (elevation <= 1) { return 1; }

            float value = (1 - Mathf.Abs(simplexNoise.CalcPixel3D(pos.x + domainWarp, pos.y + domainWarp, pos.z + domainWarp))) * amplitude + Ground - pos.y;

            return value;
        }

        ///INITIALIZE GRID INFORMATION UPON STARTUP/----------------------------------------------------------------------------

        private ushort DetermineVoxelData(Vector3 pos, int x,int elevation,int z)
        {
            float oreVal = simplexNoise.CalcPixel3D(pos.x * 3, pos.y * 3, pos.z * 3) * 2f;
            //Ground/Stone pass
            BLOCKID val;
            //These should include IDs for the given blocks by enumeration, rather than the plain ids themselves
            float cellvalue = cellvalues[x + sizeX * (elevation + sizeY * z)];

            val = cellvalue > 0 ? BLOCKID.STONE : BLOCKID.AIR;
            if (val == BLOCKID.AIR) return (ushort)val;

            val = elevation < Ground ? BLOCKID.STONE : BLOCKID.GRASS;

            //Ore pass
            val = oreVal > 0.8f && val == BLOCKID.STONE ? BLOCKID.COPPER_ORE : val;
            val = oreVal < 0.3f && val == BLOCKID.STONE ? BLOCKID.IRON_ORE : val;

            //Other passes once the time comes
            return (ushort)val;
        }



        private void InitializeCell(Func<Vector3, float, float> f, Func<Vector3, int, Vector3> spaceTransform, int x, int y, int z)
        {
            uint index = (uint)(x + sizeX * (y + sizeY * z));
            Vector3 pos = offset + new Vector3(x * step.x, y * step.y, z * step.z);

            cellpos[index] = coordTransformFunction(pos, y);
            cellvalues[index] = f(global + cellpos[index], y);
            voxel_data[index] = (ushort)DetermineVoxelData(global + cellpos[index], x, y, z);
        }




        public void InitializeGrid() {
            cellpos = new Vector3[(sizeX) * (sizeY) * sizeZ];
            cellvalues = new float[(sizeX) * (sizeY) * sizeZ];
            dualGrid = new int[(sizeX) * (sizeY) * sizeZ];
            voxel_data = new UInt16[(sizeX) * (sizeY) * sizeZ];

            for (int x = 0; x < sizeX; ++x)
            {
                for (int y = 0; y < sizeY; ++y)
                {
                    for (int z = 0; z < sizeZ; ++z)
                    {
                        InitializeCell(function, CartesianToSphere, x, y, z);
                    }
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        public void Generate(ref List<Vector3> verts, ref List<int> ind, ref UInt16[] v_data, ref int vert_index, ref int ind_index)
        {
            //initialize both the dual grid and the vertex grid

            indices = ind;
            vertices = verts;
            int index = 0;



            //This computes all the vertices except the edge cases (quite literally)
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //int vert_index = 0;
            for (int x = 0; x < sizeX; ++x)
            {
                for (int y = 0; y < sizeY; ++y)
                {
                    for (int z = 0; z < sizeZ; ++z)
                    {
                        index = x + sizeX * (y + sizeY * z);
                        dualGrid[index] = -1;

                        Find_Best_Vertex(x, y, z,index, ref vert_index);
                    }
                }
            }
            stopwatch.Stop();
            UnityEngine.Debug.Log("Vertices took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

            stopwatch.Reset();
            stopwatch.Start();
            for (int x = 0; x < sizeX - 1; ++x)
            {
                for (int y = 0; y < sizeY - 1; ++y)
                {
                    for (int z = 0; z < sizeZ - 1; ++z)
                    {
                        CreateQuad(x, y, z, ref ind_index);
                    }
                }
            }
            stopwatch.Stop ();
            UnityEngine.Debug.Log("Indices took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

            v_data = voxel_data;
        }

        public void UpdateVoxelData(ref List<chunk_event> points) {
            //Given a list of update points, that are assumed to be in range.

            Vector3 localPos;
            Vector3 currentCellPos;

            //Find the index of said points.
            points.ForEach(item => {

                localPos = (item.position - global - offset); //undo transformation to get grid position
                

                if (item.position.y == 0) return;

                for (int x = 0; x < sizeX; ++x)
                {
                    for (int y = 0; y < sizeY; ++y)
                    {
                        for (int z = 0; z < sizeZ; ++z)
                        {
                            currentCellPos = cellpos[(x + sizeX * (y + sizeY * z))] - offset;
                            if ((localPos - currentCellPos).magnitude < radius)
                            {
                                UpdateCellValueEntry(x, y, z);
                            }
                        }
                    }
                }
            });
        }

        private void UpdateCellValueEntry(int x, int y, int z) {
            if (x < 0 || x > sizeX || y < 1 || y > sizeY || z < 0 || z > sizeZ) return;

            cellvalues[(uint)(x + sizeX * (y + sizeY * z))] = -1;
        }
        //////////////////////////////////////////////////////////////////////////////

       

        private void Find_Best_Vertex(int x, int y, int z, int index,ref int vert_index)
        {
            int xPos, yPos, zPos;

            for (int i = 0; i < 8; ++i)
            {
                xPos = x == sizeX - 1 ? (x - ((i >> 2) & 0x01)) : (x + ((i >> 2) & 0x01));
                yPos = y == sizeY - 1 ? (y - ((i >> 1) & 0x01)) : (y + ((i >> 1) & 0x01));
                zPos = z == sizeZ - 1 ? (z - (i & 0x01)) : (z + (i & 0x01));
                vertPos[i] = cellpos[( xPos + sizeX * (yPos + sizeY * zPos ) ) ];
                vertValues[i] = cellvalues[(xPos + sizeX * (yPos + sizeY * zPos) ) ];
            }
            //calculate the adapt of only the edges that cross, rather than the whole thing
            //calculate the positions of the edges itself

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
            signChange |= xCross = (vertValues[6] > 0) != (vertValues[7] > 0);
            signChange |= zCross = (vertValues[3] > 0) != (vertValues[7] > 0);
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
                    avg += vertPos[i] + adapt(a, b) * (vertPos[i | 0x04] - vertPos[i]);
                    count++;
                }
            }

            //Y EDGES
            int[] yindices = new int[4] { 0, 1, 4, 5 };
            for (int i = 0; i < 4; ++i)
            {
                int j = yindices[i];
                float a = vertValues[j];
                float b = vertValues[j | 0x02];
                if (a > 0 != b > 0)
                {
                    avg += vertPos[j] + adapt(a, b) * (vertPos[j | 0x02] - vertPos[j]);
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
                    avg += vertPos[i << 1] + adapt(a, b) * (vertPos[(i << 1) | 0x01] - vertPos[i << 1]);
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
                newedge |= (((vertValues[6] > 0) && !(vertValues[7] > 0)) ? 1 : 0) << 4;
            }
            if (yCross)
            {
                newedge |= 1 << 3;
                newedge |= ((!(vertValues[5] > 0) && (vertValues[7] > 0)) ? 1 : 0) << 2;
            }
            if (zCross)
            {
                newedge |= 1 << 1;
                newedge |= ((vertValues[3] > 0) && !(vertValues[7] > 0)) ? 1 : 0;
            }

            Vector3 vertex = block_voxel ? vertPos[0] : avg;

            vertices.Add(vertex);
            vert_index++;

            //This should reference a biome texture and current elevation
            UInt16 voxelData = 0;

            dualGrid[index] = (newedge | ((vertices.Count-1) << 6) | (voxelData << 21));
            
        }

        void CreateQuad(int x, int y, int z, ref int ind_index)
        {
            int index = (int)(x + sizeX * (y + sizeY * z));
            int dualGrid_index = dualGrid[index];

            if (dualGrid_index == -1) return;

            if ((dualGrid_index & 0x20) == 0x20) //if edge[0].crossed
            {
                //Make a quad from neighboring x cells
                if ((dualGrid_index & 0x10) == 0x10) //if edge[0].sign
                {
                    
                    indices.Add(dualGrid[index] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[(x + 1) + sizeX * ((y + 1) + sizeY * z)] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)] >> 6 & 0x7FFF);

                }
                else
                {
                    
                    indices.Add(dualGrid[index] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[(x + sizeX * ((y + 1) + sizeY * z))] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[((x + 1) + sizeX * ((y + 1) + sizeY * z))] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[((x + 1) + sizeX * (y + sizeY * z))] >> 6 & 0x7FFF);

                }
                ind_index += 4;
            }
            if ((dualGrid_index & 0x08) == 0x08)
            {
                //Make a quad from neighboring y cells
                if ((dualGrid_index & 0x04) == 0x04)
                {
                    
                    indices.Add(dualGrid[index] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * (z + 1))] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))] >> 6 & 0x7FFF);
                }
                else
                {
                    indices.Add(dualGrid[index] >> 6 & 0x7FFF);
                    indices.Add((dualGrid[(x) + sizeX * (y + sizeY * (z + 1))] >> 6 & 0x7FFF));
                    indices.Add((dualGrid[(x + 1) + sizeX * (y + sizeY * (z + 1))] >> 6 & 0x7FFF));
                    indices.Add((dualGrid[(x + 1) + sizeX * (y + sizeY * z)] >> 6 & 0x7FFF));

                }
                ind_index += 4;
            }
            if ((dualGrid_index & 0x02) == 0x02)
            {
                //Make a quad from neighboring z cells
                if ((dualGrid_index & 0x01) == 0x01)
                {
                    indices.Add(dualGrid[index] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * (z + 1))] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))] >> 6 & 0x7FFF);
                }
                else
                {
                    indices.Add(dualGrid[index] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * (z + 1))] >> 6 & 0x7FFF);
                    indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)] >> 6 & 0x7FFF);
                }
                ind_index += 4;
            }
        }

        private Vector3 Grid(Vector3 pos, int elevation) { return pos; }


        public Vector3 FindTransformedCoord(Vector3 pos, int elevation) {
            return coordTransformFunction(pos, elevation);
        }

        private Vector3 CartesianToSphere(Vector3 pos, int elevation)
        {

            //Given a grid position, convert the point into a shell point
            //dir contains the u and v direction, which are masks for the x and z positions.
            Vector3 step = new Vector3(1f / (sizeX-1), 1f / (sizeY-1), 1f / (sizeZ - 1));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;


            int uSign = ((dir & 0x80) != 0) ? 1 : -1;
            int vSign = ((dir & 0x08) != 0) ? 1 : -1;
            Vector3 uaxis = new Vector3(((dir >> 6 & 0x7FFF) & 0x01), (dir >> 5) & 0x01, (dir >> 4) & 0x01) * (uSign);
            Vector3 vaxis = new Vector3(((dir >> 2) & 0x01), (dir >> 1) & 0x01, (dir) & 0x01) * (vSign);
            Vector3 wAxis = Vector3.Cross(vaxis, uaxis);
            float radius = CELL_SIZE / 2;

            //no idea why the step.x / 2. Why would it need to be pushed back half a unit? BECAUSE THE QUADS ARE CENTERED BY DEFAULT
            Vector3 newpos = uaxis * pos.x + vaxis * pos.z + wAxis * (radius - step.y / 2);

            return newpos.normalized * (radius + (elevation * step.y));
        }

        private Vector3 ShellElevate(Vector3 pos)
        {
            Vector3 newpos = new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z);
            return newpos;
        }





        //returns the interpolation t value for points x0 and x1 where sign(x0) != sign(x1)

        float adapt(float x0, float x1) => (-x0) / (x1 - x0);
    }
}
