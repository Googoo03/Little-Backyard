using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SignedDistanceFields;
using SparseVoxelOctree;
using UnityEngine.Rendering;
using TerrainEditing;

public class TerrainEditor : MonoBehaviour
{
    //Has a variety of shapes

    //Can lock shape "down" to planet center, or free in space

    //Can left click to delete, right click to add
    private const int X_AXIS = 4;
    private const int Y_AXIS = 2;
    private const int Z_AXIS = 1;

    [SerializeField] private float fireRate = 0.1f;
    private float fireTimer = 0f;
    private GameObject selectedShape;
    private enum STATES { EDIT, NOEDIT };
    private STATES state;

    void Start()
    {
        state = STATES.NOEDIT;
        selectedShape = TerrainShapeManager.Instance.GetShapeObject(SHAPES.SPHERE); //default sphere
    }

    void FixedUpdate()
    {
        if (state == STATES.NOEDIT) return;
        CalculateFire();
        fireTimer += Time.fixedDeltaTime;
    }

    void Update()
    {
        StateMachineTick();
    }

    void StateMachineTick()
    {
        bool buttonPress = Input.GetKeyDown(KeyCode.V);
        if (!buttonPress) return;
        switch (state)
        {
            case STATES.EDIT:
                state = STATES.NOEDIT;
                selectedShape.SetActive(false);
                break;
            case STATES.NOEDIT:
                state = STATES.EDIT;
                selectedShape.SetActive(true);
                break;
            default:
                break;
        }
    }

    void DisplayTerrainShape(Vector3 pos, bool inReach = true)
    {
        selectedShape.transform.position = pos;
        selectedShape.transform.localScale = new Vector3(25, 25, 25);
        selectedShape.GetComponent<Renderer>().material.color = inReach ? new Color(0f, 1f, 0f, 5f) : new Color(1f, 0f, 0f, 5f);
    }

    void ProcessRaycast(RaycastHit hit)
    {
        if (hit.transform.CompareTag("Chunk"))
        {

            SVOTest svoTest = hit.transform.parent.GetComponent<SVOTest>();
            SVO svo = svoTest.GetSVO();


            Vector3 relativePosition = hit.point - hit.transform.position;
            int additive = Input.GetKey(KeyCode.Mouse1) ? 1 : 0;

            SphereSDF sdf = new(relativePosition, 25, additive);
            svo.AddSDFEdit(sdf);

            SVONode node = svo.TraversePath(relativePosition); //get the target node
            node = svo.GetChunkFromNode(node); //get chunk that the node is under

            //need to enqueue changes for when the GPU is done
            //svoTest.GatherLeavesBuffer(node, forceGenerate: true, sdf);
            svo.MarkChunk(node);
            svoTest.FlagRefreshChunks();
        }
    }

    void CalculateFire()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 100))
        {
            DisplayTerrainShape(hit.point);

            //If we didn't click, don't count it
            if (!Input.GetKey(KeyCode.Mouse0) && !Input.GetKey(KeyCode.Mouse1)) return;

            if (fireTimer > fireRate)
            {
                fireTimer = 0f;
                ProcessRaycast(hit);
            }
        }
        else
        {
            DisplayTerrainShape(transform.forward * 100, false);
        }
    }
}
