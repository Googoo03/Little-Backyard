using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UnityEditor.PackageManager.Requests;


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
        IRON_ORE = 3
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
        public void setCube(GameObject c) {
            cube = c;
        }

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

        public Dual_Contour(Vector3 _global, Vector3Int scale, int length, bool mode) {
            global = _global;
            sizeX = scale.x;
            sizeY = scale.y;
            sizeZ = scale.z;
            CELL_SIZE = length;
            LOD_Level = 0;
            Ground = 2;
            amplitude = 5;
            block_voxel = mode;

            //determines the "step" between vertices to cover 1 unit total
            step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;

            //sets the offset of the grid by a power of two based on the LOD level
            offset = -Vector3.one * (1f / (1 << (LOD_Level + 1))) * (CELL_SIZE);

            
        }

        private float function(Vector3 pos, float elevation)
        {


            float domainWarp = simplexNoise.CalcPixel3D(pos.x * 5, pos.y * 5, pos.z * 5) * 2f;

            if (elevation >= sizeY - 1) { return -1; }
            if (elevation <= 1) { return 1; }

            float value = (1 - Mathf.Abs(simplexNoise.CalcPixel3D(pos.x + domainWarp, pos.y + domainWarp, pos.z + domainWarp))) * amplitude + Ground - elevation;

            return value;
        }

        private ushort DetermineVoxelData(Vector3 pos, int elevation)
        {
            float oreVal = simplexNoise.CalcPixel3D(pos.x * 3, pos.y * 3, pos.z * 3) * 2f;
            //Ground/Stone pass
            BLOCKID val = BLOCKID.GRASS;
            //These should include IDs for the given blocks by enumeration, rather than the plain ids themselves
            val = elevation < Ground ? BLOCKID.STONE : BLOCKID.GRASS;

            //Ore pass
            val = oreVal > 0.8f && val == BLOCKID.STONE ? BLOCKID.COPPER_ORE : val;
            val = oreVal < 0.3f && val == BLOCKID.STONE ? BLOCKID.IRON_ORE : val;

            //Other passes once the time comes
            return (ushort)val;
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
                        InitializeCell(function, Grid, x, y, z);
                    }
                }
            }
        }

        public void Generate(ref List<Vector3> verts, ref List<int> ind, ref UInt16[] v_data)
        {
            //initialize both the dual grid and the vertex grid

            indices = ind;
            vertices = verts;

            


            //This computes all the vertices except the edge cases (quite literally)
            for (int x = 0; x < sizeX; ++x)
            {
                for (int y = 0; y < sizeY; ++y)
                {
                    for (int z = 0; z < sizeZ; ++z)
                    {
                        dualGrid[x + sizeX * (y + sizeY * z)] = -1;
                        Find_Best_Vertex(x, y, z);
                    }
                }
            }

            for (int x = 0; x < sizeX - 1; ++x)
            {
                for (int y = 0; y < sizeY - 1; ++y)
                {
                    for (int z = 0; z < sizeZ - 1; ++z)
                    {
                        CreateQuad(x, y, z);
                    }
                }
            }

            v_data = voxel_data;
        }

        public void UpdateDC(ref List<List<Vector3>> points) {
            //Given a list of update points, that are assumed to be in range.

            //Find the index of said points.
            points.ForEach(item => {

                Vector3 localPos = (item[0] - global - offset); //undo transformation to get grid position

                if (item[0].y == 0) return;

                int y = item[1].y < 0 ? Mathf.CeilToInt((localPos.y * (sizeY / CELL_SIZE))) : Mathf.FloorToInt((localPos.y * (sizeY / CELL_SIZE)));

                int x = Mathf.RoundToInt((localPos.x * (sizeX / CELL_SIZE)));
                int z = Mathf.RoundToInt((localPos.z * (sizeZ / CELL_SIZE)));

                //cube.transform.position = new Vector3(x, y, z)+offset;
                UpdateCellValueEntry(x, y, z);
            });

            //Change the corresponding dualgrid entries
            //Add/remove vertex indices as necessary

            //Reassign indices
        }

        private void UpdateCellValueEntry(int x, int y, int z) {
            if (x < 0 || x > sizeX || y < 1 || y > sizeY || z < 0 || z > sizeZ) return;

            cellvalues[(uint)(x + sizeX * (y + sizeY * z))] = -1;
            //cant directly affect the dualgrid. Will lead to excess vertices.
        }
        //////////////////////////////////////////////////////////////////////////////

        private void InitializeCell(Func<Vector3, float, float> f, Func<Vector3, int, Vector3> spaceTransform, int x, int y, int z) {
            uint index = (uint)(x + sizeX * (y + sizeY * z));
            Vector3 pos = offset + new Vector3(x * step.x, y * step.y, z * step.z);

            cellpos[index] = spaceTransform(pos, y);
            cellvalues[index] = f(global + cellpos[index], y);
            voxel_data[index] = (ushort)DetermineVoxelData(global + cellpos[index], y);
        }

        private void Find_Best_Vertex(int x, int y, int z)
        {
            uint index = (uint)(x + sizeX * (y + sizeY * z));


            float[] vertValues = new float[8];
            Vector3[] vertPos = new Vector3[8];

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

            avg /= count > 1 ? count : 1;//Mathf.Max(1, count);

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

            //This should reference a biome texture and current elevation
            UInt16 voxelData = 0;// (ushort)UnityEngine.Random.Range(0, 65535);


            //voxel data, if we want to add more functionality, should be determined by the noise function, not by the vertex function
            //voxel_data[index] = (y <= Ground+2) ? (ushort)1 : (ushort)0;

            dualGrid[index] = (newedge | ((vertices.Count-1) << 6) | (voxelData << 21));
            
        }

        void CreateQuad(int x, int y, int z)
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
            }
        }

        private Vector3 Grid(Vector3 pos, int elevation) { return pos; }

        private Vector3 CartesianToSphere(Vector3 pos, int elevation)
        {

            //Given a grid position, convert the point into a shell point
            //dir contains the u and v direction, which are masks for the x and z positions.
            Vector3 step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;


            int uSign = ((dir & 0x80) != 0) ? 1 : -1;
            int vSign = ((dir & 0x08) != 0) ? 1 : -1;
            Vector3 uaxis = new Vector3(((dir >> 6 & 0x7FFF) & 0x01), (dir >> 5) & 0x01, (dir >> 4) & 0x01) * (uSign);
            Vector3 vaxis = new Vector3(((dir >> 2) & 0x01), (dir >> 1) & 0x01, (dir) & 0x01) * (vSign);
            Vector3 wAxis = Vector3.Cross(vaxis, uaxis);
            float radius = CELL_SIZE / 2;

            //no idea why the step.x / 2. Why would it need to be pushed back half a unit? BECAUSE THE QUADS ARE CENTERED BY DEFAULT
            Vector3 newpos = uaxis * pos.x + vaxis * pos.z + wAxis * (radius - step.x / 2);

            return newpos.normalized * (radius + (elevation * step.y));
            //return newpos;

        }

        private Vector3 ShellElevate(Vector3 pos)
        {
            //simplexNoise.Seed = 1642;
            // Vector3 newpos = new Vector3 (pos.x,pos.y+ simplexNoise.CalcPixel3D(pos.x / 5, 0, pos.z / 5)*1, pos.z);
            //Assert.IsTrue(newpos.x != 0);
            Vector3 newpos = new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z);
            return newpos;
        }


        


        //returns the interpolation t value for points x0 and x1 where sign(x0) != sign(x1)
        private float adapt(float x0, float x1)
        {
            return (-x0) / (x1 - x0);
        }
    }
}
