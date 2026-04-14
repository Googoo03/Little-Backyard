using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FloatingOrigin;
using System;

public abstract class Manager : MonoBehaviour
{
    // It is assumed that every manager will control objects en masse.
    public Floating_Origin_Transform floating_origin_transform;
    public Manager()
    {

    }

}
