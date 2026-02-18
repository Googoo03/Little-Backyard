using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;

namespace faces
{
    public struct Face
    {
        public SVO U;
        public SVO NU;
        public SVO V;
        public SVO NV;
        public Face(SVO U_, SVO NU_, SVO V_, SVO NV_) //assign U and V neighbors
        {
            U = U_;
            NU = NU_;
            V = V_;
            NV = NV_;
        }
    };

    public class CubeSphereSVOWrapper : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField] SVO XFace;
        [SerializeField] SVO NXFace;
        [SerializeField] SVO YFace;
        [SerializeField] SVO NYFace;
        [SerializeField] SVO ZFace;
        [SerializeField] SVO NZFace;

        public Face[] neighbors;

        void Awake()
        {
            if (neighbors != null) return; // only initialize once

            neighbors = new Face[]
            {
            new Face(XFace, NXFace, YFace, NYFace),
            new Face(NXFace, XFace, YFace, NYFace),
            new Face(YFace, NYFace, XFace, NXFace),
            new Face(NYFace, YFace, XFace, NXFace),
            new Face(ZFace, NZFace, XFace, NXFace),
            new Face(NZFace, ZFace, XFace, NXFace)
            };
        }
    }
}
