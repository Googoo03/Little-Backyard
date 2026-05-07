using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SignedDistanceFields;
using SparseVoxelOctree;

public class TerrainEditor : MonoBehaviour
{
    //Has a variety of shapes

    //Can lock shape "down" to planet center, or free in space

    //Can left click to delete, right click to add

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //raycast out, get planet associated with it, and cause it to update SVO with SDF

    }

    void FixedUpdate()
    {
        CalculateFire();
    }

    void CalculateFire()
    {

        if (!Input.GetKeyDown(KeyCode.Mouse0) && !Input.GetKeyDown(KeyCode.Mouse1)) return;
        bool additive = Input.GetKeyDown(KeyCode.Mouse1);
        // Does the ray intersect any objects excluding the player layer
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out RaycastHit hit, 100))

        {
            if (hit.transform.CompareTag("Chunk"))
            {
                SVO svo = hit.transform.parent.GetComponent<SVOTest>().GetSVO();
                Vector3 relativePosition = hit.point - hit.transform.position;
                SphereSDF sdf = new SphereSDF(relativePosition, 50, additive);
                svo.AddSDFEdit(sdf);

                //ideally, we only force generate the local neighborhood of vertices
                //we dont want nodes to even begin to calculate if its out of range.

                svo.root.GenerateVerticesForLeaves(svo.meshingAlgorithm.SVOVertex, forceGenerate: true, sdf);
                svo.MarkChunk(relativePosition);

                svo.GenerateChunks();
            }

            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            Debug.Log("Did Hit");
        }
        else
        {
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1000, Color.white);
            Debug.Log("Did not Hit");
        }
    }
}
