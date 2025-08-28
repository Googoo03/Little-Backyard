using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignedDistanceFields
{
    public class SDF {
        public static float OctahedronNotExact(Vector3 p, Vector3 global, float s)
        {
            p = new Vector3(
                            Mathf.Abs(global.x - p.x),
                            Mathf.Abs(global.y - p.y),
                            Mathf.Abs(global.z - p.z)
                            );
            return (p.x + p.y + p.z - s) * 0.57735027f;
        }

        public static float Link(Vector3 p, Vector3 global, float le, float r1, float r2)
        {
            Vector3 q = new Vector3(p.x + global.x, Mathf.Max(Mathf.Abs(p.y + global.y) - le, 0.0f), p.z + global.z);
            return new Vector2( new Vector2(q.x,q.y).magnitude - r1, q.z).magnitude - r2;
        }

        public static float CutHollowSphere(Vector3 p, Vector3 global, float r, float h, float t)
        {
            // sampling independent computations (only depend on shape)
            float w = Mathf.Sqrt(r * r - h * h);

            // sampling dependant computations
            Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude, p.y);
            return ((h * q.x < w * q.y) ? (q - new Vector2(w, h)).magnitude : Mathf.Abs((q).magnitude - r)) - t;
        }
    }

}
