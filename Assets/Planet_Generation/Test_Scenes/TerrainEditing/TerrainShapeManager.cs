using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainEditing
{
    public enum SHAPES { SPHERE };
    public class TerrainShapeManager : MonoBehaviour
    {
        // Start is called before the first frame update
        public static TerrainShapeManager Instance { get; private set; }
        public GameObject sphereObj;
        void Awake()
        {
            Instance = this;
        }

        public GameObject GetShapeObject(SHAPES shape)
        {
            GameObject obj = null;
            switch (shape)
            {
                case SHAPES.SPHERE:
                    obj = sphereObj;
                    break;
                default:
                    break;
            }
            return obj;
        }
    }
}
