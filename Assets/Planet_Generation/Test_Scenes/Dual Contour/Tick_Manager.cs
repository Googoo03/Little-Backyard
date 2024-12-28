using DualContour;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tick_Manager : MonoBehaviour
{
    private float timeToNextFrame = 0.05f;
    private float elapsedTime = 0.0f;

    //Testing
    [SerializeField] private DC_Chunk chunk;

    //Event queue
    private Queue<List<Vector3>> events = new Queue<List<Vector3>>();

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= timeToNextFrame) Tick();
    }

    public void pushEvent(List<Vector3> ev) {
        events.Enqueue(ev);
    }

    private void Tick() {
        //chunk.
        List<List<Vector3>> points = new List<List<Vector3>>();

        while (events.Count > 0)
        {
            List<Vector3> currentEvent = events.Dequeue();
            points.Add(currentEvent);
        }
        if(points.Count > 0) chunk.UpdateChunk(ref points);
    }
}
