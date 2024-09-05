using ObjPool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Object_Pool_Manager : MonoBehaviour
{
    //Type of object for pool
    [SerializeField] private GameObject obj;

    //Num of objects in pool
    [SerializeField] private const int num = 250;

    //Object pool itself
    Object_Pool<GameObject> objPool = new Object_Pool<GameObject>(num);

    //Queue of requesters to be resolved each frame
    Queue<Tuple<List<GameObject>, int>> queue = new Queue<Tuple<List<GameObject>, int>>();


    //Awake runs before the game starts
    void Awake()
    {
        if (!obj) obj = new GameObject("Foliage_Object_");

        for (int i = 0; i < num; ++i)
        {
            GameObject newObj = Instantiate(obj, Vector3.zero, Quaternion.identity);
            newObj.transform.name += i.ToString();
            newObj.AddComponent<MeshFilter>();
            newObj.AddComponent<MeshRenderer>();
            //newObj.transform.position = Vector3.zero;

            objPool.addPoolObj(newObj);
        }
    }

    // Update is called once per frame
    void Update()
    {
        while (queue.Count > 0)
        {

            //Get request
            Tuple<List<GameObject>, int> request = queue.Dequeue();

            //use find Subpool to get objects if available. Apply to list
            objPool.findSubPool(request);
        }
    }

    public void requestPoolObjs(ref List<GameObject> list, int num)
    {

        //need to enqueue the pointer of the list so it can be modified later
        queue.Enqueue(new Tuple<List<GameObject>, int>(list, num));
    }
}
