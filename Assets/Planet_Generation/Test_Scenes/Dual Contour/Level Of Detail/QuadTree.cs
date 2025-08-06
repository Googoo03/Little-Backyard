using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace QuadTree
{

    public struct ChunkConfig {
        public int lodLevel;
        public int dir; //contains the UAxis, VAxis, and their signs. WAxis can be derived by their cross product
        public Vector3 lodOffset;
        public Vector3Int scale;

        public ChunkConfig(int ilodLevel, int idir, Vector3 ilodOffset, Vector3Int iscale) {
            lodLevel = ilodLevel;
            dir = idir;
            lodOffset = ilodOffset;
            scale = iscale;

        }

    }




    public class QuadTreeNode : MonoBehaviour
    {
        private ChunkConfig chunkConfig;
        private List<QuadTreeNode> children;
        private QuadTreeNode parent;

        private GameObject prefab;
        private GameObject go;

        //Constructors
        public QuadTreeNode(ChunkConfig ichunkConfig) {
            chunkConfig = ichunkConfig;
        }

        public QuadTreeNode(ChunkConfig ichunkConfig, GameObject igo, QuadTreeNode iparent)
        {
            //Initialize all relevant internal information, being the children, parent, and prefab
            children = new List<QuadTreeNode>();
            chunkConfig = ichunkConfig;
            prefab = igo;
            parent = iparent;

            //Determine the position to place the prefab based on if it's the root or not
            Vector3 pos = chunkConfig.lodLevel == 0 ? Vector3.zero : parent.GetGameObject().transform.position;

            go = Instantiate(igo,
                             pos,
                             Quaternion.identity);
            go.name = "Chunk_" + chunkConfig.lodLevel;
            go.GetComponent<DC_Chunk>().SetChunkConfig(chunkConfig);
        }

        public void AddChild(QuadTreeNode newchild) { children.Add(newchild); }

        public void NextLOD() {

            Vector3[] binaryOperator = { Vector3.zero, Vector3.right, new Vector3(1,0,1), new Vector3(1,1,0),  Vector3.up, new Vector3(0,1,1),  Vector3.one, Vector3.forward };
            float powerof2Frac = 1f / (1 << (chunkConfig.lodLevel + 1));
            Vector3 p2fracVec = new Vector3(powerof2Frac, powerof2Frac, powerof2Frac);

            for (int i = 0; i < 8; ++i) {


                Vector3 childLODOffset = chunkConfig.lodOffset + new Vector3(p2fracVec.x * binaryOperator[i].x, p2fracVec.y * binaryOperator[i].y, p2fracVec.z * binaryOperator[i].z);
                Vector3Int childScale = chunkConfig.scale;

                ChunkConfig childChunkConfig = new ChunkConfig(chunkConfig.lodLevel+1,chunkConfig.dir,childLODOffset, childScale); 

                QuadTreeNode newchild = new QuadTreeNode(childChunkConfig, prefab, this);
                AddChild(newchild);
            }
        }

        public void PrevLOD() {
            foreach (QuadTreeNode child in children)
            {
                child.DestroyGO();
            }
            children.Clear();
        }

        

        public void DestroyGO() { GameObject.Destroy(go); }

        public GameObject GetGameObject() { return go; }

        public bool HasChildren() { return children.Count > 0; }

        public QuadTreeNode GetChild(int i) { 
            if(i > 0 && i < children.Count) return children[i]; 
            return null;
        }

        //In C#, reference types return references when in C++ they are copied.
        public List<QuadTreeNode> GetChildren() { return children;}

        public int GetLODLevel() { return chunkConfig.lodLevel; }

    }
}
