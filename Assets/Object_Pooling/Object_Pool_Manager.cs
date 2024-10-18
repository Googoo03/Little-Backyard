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
    [SerializeField] private const int num = 1024;

    //Object pool itself
    Object_Pool<GameObject> objPool = new Object_Pool<GameObject>(num);

    //List of requesters to hold until release
    //Holds 2
    List<Tuple<List<GameObject>, List<int>>> _requestReceipt = new List<Tuple<List<GameObject>, List<int> >>();


    //Awake runs before the game starts
    void Awake()
    {
        if (!obj) obj = new GameObject();

        for (int i = 0; i < num; ++i)
        {
            //Instantiate said object
            GameObject newObj = Instantiate(obj, Vector3.zero, Quaternion.identity);

            newObj.SetActive(false);
            newObj.transform.parent = transform;
            objPool.addPoolObj(newObj);

            //Naming convention and Needed components
            newObj.transform.name = transform.name+"_"+i.ToString();

            
        }
    }



    public void releasePoolObjs(ref List<GameObject> list) {
        for (int i = 0; i < _requestReceipt.Count; ++i) {

            //If all objects have an ordered ID, it might be beneficial to have a binary search here

            if (_requestReceipt[i].Item1 != list) continue;

            _requestReceipt[i].Item2.ForEach(item => { 
                //int index = _requestReceipt[i].Item2.IndexOf(item);
                objPool.releasePool(item); 
                
            });
            
            _requestReceipt.RemoveAt(i);
            break;
        }
    }

    public void requestPoolObjs(ref List<GameObject> list, int num)
    {
        Tuple<List<GameObject>, int> request = new Tuple<List<GameObject>, int>(list, num);
        Tuple<List<GameObject>, List<int>> receipt = new Tuple<List<GameObject>, List<int>>(list, new List<int>());

        //find a subpool of objects that can be allocated, then store the indices at which these are found in a receipt.
        objPool.findSubPool(request,receipt);

        _requestReceipt.Add(receipt); // can be referenced later on to release objects

    }
}
