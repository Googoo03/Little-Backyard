using DualContour;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using chunk_events;
using System;

public class Tick_Manager : MonoBehaviour
{
    private float timeToNextFrame = 0.05f;
    private float elapsedTime = 0.0f;

    //Testing
    [SerializeField] private List<DC_Chunk> chunks;
    [SerializeField ]private Dictionary<int,int> chunk_hashmap = new Dictionary<int,int>();

    //Event queue
    private Queue<chunk_event> events = new Queue<chunk_event>();

    private void Start()
    {
        for (int i = 0; i < chunks.Count; ++i)
        {
            var item = chunks[i];
            chunk_hashmap.Add(item.GetInstanceID(), i);
        }
    }


    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= timeToNextFrame) Tick();
    }

    public void pushEvent(chunk_event ev) {
        events.Enqueue(ev);
    }

    private void Tick() {


        List<chunk_event> points = events.ToList();

        if (points.Count == 0) return;

        points.Sort((chunk_event a, chunk_event b) => a.id.CompareTo(b.id));

        //2 pointer trick
        int begin = 0;
        int i = chunk_hashmap[points.ElementAt(0).id];
        int end = 0;

        //need a way to identify which chunk we're clicking on.
        while (end < points.Count) {
            if (points.ElementAt(end).id != points.ElementAt(begin).id || end == points.Count-1) {
                List<chunk_event> subevent = points.GetRange(begin, end == points.Count-1 ? end-begin : (end-1)-begin);
                chunks[i].UpdateChunk(ref subevent);

                begin = end;
                i = chunk_hashmap[points.ElementAt(begin).id];
            }
            end++;
        }
        //right now we have a list of events and which chunk we're in. We should sort based on id.
        //Then have points be a list of events tied to a specific chunk, then update all at once.
        events.Clear();
    }

    public int findChunk(int id) {
        return chunks.Find(i => i.transform.GetInstanceID() == id).GetInstanceID();
    }
}
