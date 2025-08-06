using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace chunk_events {


    public struct chunk_event
    {
        public Vector3 position;
        public int id;

        public chunk_event(Vector3 _pos, int _id) { 
            position = _pos;
            id = _id;
        }
    }
}
