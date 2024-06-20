using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Poisson
{
    public class PoissonDisc
    {
        private uint startSeed;
        private const float _2PI = 6.28f;

        void setSeedPRNG(int seed)
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

        void generatePoissonDisc(ref List<Vector3> points, int k, int num) {

            int index = 0;
            int points_placed = 0;

            float rand;
            float x, y;

            while (index != points.Capacity && points_placed < num) { //while we havent placed enough points and havent reached the end of our array
                index++;
                for (int i = 0; i < k; i++) {
                    //generate new random number from 0 to 2pi
                    rand = ((float)PRNG() / 256.0f) * _2PI;
                    //figure out new point x,y position
                    x = Mathf.Cos(rand);
                    y = Mathf.Sin(rand);
                    //check if its valid, if so, add it, if not, skip it
                }
            }
            return;
        }

    }
}
