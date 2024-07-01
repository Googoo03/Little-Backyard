using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Poisson
{
    public class PoissonDisc
    {
        private uint startSeed;
        private const float _2PI = 6.28f;

        public void setSeedPRNG(int seed)
        {
            startSeed = (uint)seed;
        }

        uint PRNG()
        {
            //c is the increment, a is the factor, the seed is our starting factor, m is modulus
            uint a = 22695477;
            uint m = 256;
            uint c = 1;
            //if (startSeed == 0) startSeed = seed;
            uint val = ((startSeed * a) + c) % m;
            startSeed = val;
            return val;
        }

        private bool meetsDistanceThreshold(int radius, ref bool[] hashgrid,int startIndex,int maxX, int maxY) {
            int halfradius = radius / 2;
            for (int i = -halfradius; i < halfradius; ++i) {
                for (int j = -halfradius; j < halfradius; ++j)
                {
                    //do bounds check for x and y, if outside, skip

                    ////////////////////////
                    int index = startIndex + (maxX * j) + i;
                    if (index > (maxX * maxY)-1 || index < 0) continue;
                    if (!hashgrid[index])
                    {
                        continue;
                    }
                    else {
                        return false;
                    }
                }
            }
            return true;
        }

        public void generatePoissonDisc(ref List<Vector3> points, int k, int num, int maxX, int maxY, int radius) { // doesn't take in a reference vector field

            //start with empty list, grow as needed
            bool[] hashgrid = new bool[maxX*maxY];
            

            int index = 0; //current index of reference point
            int bool_index; //index of new point in bool list
            int points_placed = 0;

            float rand;
            float x, y;
            

            x = maxX / 2;
            y = maxY / 2;
            points.Add(new Vector3(x, 0, y));
            bool_index = ((int)y * maxX) + (int)x;
            hashgrid[bool_index] = true;

            while (index < points.Count && points_placed < num) { //while we havent placed enough points and havent reached the end of our array

                for (int i = 0; i < k; i++) {
                    //generate new random number from 0 to 2pi
                    rand = ((float)PRNG() / 256.0f) * _2PI;


                    

                    //figure out new point x,y position
                    x = Mathf.Max(points[index].x+(Mathf.Cos(rand)*radius),0);
                    y = Mathf.Max(points[index].z+(Mathf.Sin(rand)*radius),0);
                    x = Mathf.Min(x, maxX);
                    y = Mathf.Min(y, maxY-1);
                    //check if its valid, if so, add it, if not, skip it
                    //float dist = Mathf.Sqrt((x - points[index].x) * (x - points[index].x) + (y - points[index].y) * (y - points[index].y));
                    bool_index = ((int)Mathf.Max(y-1,0) *maxX) + (int)x;
                    if (meetsDistanceThreshold(radius,ref hashgrid,bool_index,maxX,maxY)) {
                        hashgrid[bool_index] = true;
                        Vector3 newpoint = new Vector3(x, 0, y);
                        points.Add(newpoint);
                        points_placed++;
                    }
                    //find out what hash grid it belongs to. If it's already true, then skip, otherwise set it true and add it to points

                }
                index++;
            }
            return;
        }

        private Vector3 interpolate(Vector3 a, Vector3 b, float t) {
            return a + (b - a)*t;
        }

        public void generatePoissonDisc(ref List<Vector3> points, ref Vector3[] vertices, int k, int num, int maxX, int maxY, int radius) //takes in a reference vector field
        { // doesn't take in a reference vector field

            //start with empty list, grow as needed
            bool[] hashgrid = new bool[maxX * maxY];
            


            int index = 0; //current index of reference point
            int bool_index; //index of new point in bool list
            int points_placed = 0;

            float rand;
            float x, y;
            float index_x, index_y;
            float next_x, next_y;

            bool found = false;

            x = maxX / 2;
            y = maxY / 2;
            next_x = x; next_y = y;

            index_x = next_x; index_y = next_y;

            /////CALCULATE FIRST POINT FROM CENTER BUT DONT ADD CENTER/////////////////
            rand = ((float)PRNG() / 256.0f) * _2PI;

            //figure out new point x,y position
            x = Mathf.Max(index_x + (Mathf.Cos(rand) * radius), 0);
            y = Mathf.Max(index_y + (Mathf.Sin(rand) * radius), 0);
            x = Mathf.Min(x, maxX);
            y = Mathf.Min(y, maxY - 1);
            ///////////////////////////////////

            bool_index = ((int)Mathf.Max(y - 1, 0) * maxX) + (int)x;
            points.Add(vertices[bool_index]);
            hashgrid[bool_index] = true;


            while (index < points.Count && points_placed < num)
            { //while we havent placed enough points and havent reached the end of our array
                found = false;
                for (int i = 0; i < k; i++)
                {
                    //generate new random number from 0 to 2pi
                    rand = ((float)PRNG() / 256.0f) * _2PI;




                    //figure out new point x,y position
                    x = Mathf.Max(index_x + (Mathf.Cos(rand) * radius), 0);
                    y = Mathf.Max(index_y + (Mathf.Sin(rand) * radius), 0);
                    x = Mathf.Min(x, maxX);
                    y = Mathf.Min(y, maxY - 1);

                    //check if its valid, if so, add it, if not, skip it
                    bool_index = ((int)Mathf.Max(y - 1, 0) * maxX) + (int)x;
                    int interpolate_index = ((int)Mathf.Max(y, 0) * maxX) + (int)(x+1);

                    if (meetsDistanceThreshold(radius, ref hashgrid, bool_index, maxX, maxY))
                    {
                        if (!found) {
                            next_x = x; next_y = y;
                            found = true;
                        }
                        hashgrid[bool_index] = true;
                        
                        Vector3 newpoint = interpolate_index < (maxX*maxY)-1 ? interpolate( vertices[bool_index], vertices[interpolate_index],x-(int)x) : vertices[bool_index];
                        points.Add(newpoint);
                        points_placed++;
                    }
                    //find out what hash grid it belongs to. If it's already true, then skip, otherwise set it true and add it to points

                }
                index++;
                index_x = next_x;
                index_y = next_y;
            }
            return;
        }

    }
}
