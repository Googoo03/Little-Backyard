using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FloatingOrigin
{
    [System.Serializable]
    public struct Floating_Origin_Transform
    {
        public Matrix4x4 TRS;

        public Floating_Origin_Transform(Vector3 position_, Quaternion rotation_, Vector3 scale_)
        {
            TRS = Matrix4x4.TRS(position_, rotation_, scale_);
        }

        public void ApplyTransformation(Matrix4x4 transformation)
        {
            TRS = transformation * TRS;
        }

        public Vector3 GetPosition() { return TRS.GetColumn(3); }
    };
}
