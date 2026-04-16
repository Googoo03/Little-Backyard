using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using faces;

public class PlanetWrapper : MonoBehaviour
{
    //Faces must be assigned in this order X -X Y -Y Z -Z
    [SerializeField] List<SVOTest> Faces;
    [SerializeField] private int planetRadius;

    public Face[] neighbors;

    void Awake()
    {
        SVOTest XFace = Faces[0];// = XFaceSVOTest;
        SVOTest NXFace = Faces[1];// = NXFaceSVOTest;
        SVOTest YFace = Faces[2];// = YFaceSVOTest;
        SVOTest NYFace = Faces[3];// = NYFaceSVOTest;
        SVOTest ZFace = Faces[4];// = ZFaceSVOTest;
        SVOTest NZFace = Faces[5];// = NZFaceSVOTest;

        foreach (SVOTest face in Faces)
        {
            face.SetPatchSize(planetRadius);
        }

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
