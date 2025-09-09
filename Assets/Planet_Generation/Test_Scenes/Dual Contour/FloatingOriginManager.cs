using System.Collections;
using System.Collections.Generic;
using DualContour;
using UnityEngine;

public class FloatingOriginManager : MonoBehaviour
{
    [SerializeField] private BaseFreeCam playerDelta;

    [SerializeField] private List<Transform> objectsToMove = new List<Transform>();
    [SerializeField] private Vector3 lastPlayerPosition;
    private float epsilon = 1e-4f;
    // Start is called before the first frame update
    void Start()
    {
        GameObject[] plaents = GameObject.FindGameObjectsWithTag("Planet");
        foreach (var item in plaents)
        {
            objectsToMove.Add(item.transform);
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 delta = playerDelta.GetDelta();
        if (delta.sqrMagnitude > epsilon * epsilon)
        {
            foreach (var item in objectsToMove)
            {
                item.position -= delta;

                //must update each chunk's global position
                DC_Chunk[] chunks = item.GetComponentsInChildren<DC_Chunk>();
                foreach (var chunk in chunks)
                {
                    chunk.GetDC().SetGlobal(item.position);

                }
            }
        }
    }
}
