using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using faces;

public class CubeSphereSVOWrapper : MonoBehaviour
{
    [SerializeField] SVOTest XFaceSVOTest;
    [SerializeField] SVOTest NXFaceSVOTest;
    [SerializeField] SVOTest YFaceSVOTest;
    [SerializeField] SVOTest NYFaceSVOTest;
    [SerializeField] SVOTest ZFaceSVOTest;
    [SerializeField] SVOTest NZFaceSVOTest;

    public Face[] neighbors;

    void Awake()
    {
        SVOTest XFace = XFaceSVOTest;
        SVOTest NXFace = NXFaceSVOTest;
        SVOTest YFace = YFaceSVOTest;
        SVOTest NYFace = NYFaceSVOTest;
        SVOTest ZFace = ZFaceSVOTest;
        SVOTest NZFace = NZFaceSVOTest;

        neighbors = new Face[6]
        {
            new Face(NYFace,XFace), //z
            new Face(NXFace,YFace),  //-z
            new Face(NXFace,ZFace), //y d
            new Face(NZFace,XFace), //-y d
            new Face(NZFace,YFace), //x
            new Face(NYFace,ZFace) //-x
        };
    }
}
