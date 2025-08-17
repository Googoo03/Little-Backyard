using DualContour;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


namespace QuadTree
{

    public struct ChunkConfig {
        public int lodLevel;
        public int dir; //contains the UAxis, VAxis, and their signs. WAxis can be derived by their cross product
        public Vector3 lodOffset;
        public Vector3Int scale;
        public bool seam;

        public ChunkConfig(int ilodLevel, int idir, Vector3 ilodOffset, Vector3Int iscale, bool iseam) {
            lodLevel = ilodLevel;
            dir = idir;
            lodOffset = ilodOffset;
            scale = iscale;
            seam = iseam;
        }

    }




    public class QuadTreeNode
    {
        private ChunkConfig chunkConfig;
        private List<QuadTreeNode> children;
        private QuadTreeNode parent;

        private GameObject prefab;
        private GameObject go;
        private GameObject seamgo;

        

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
            go = Object.Instantiate(igo,pos,Quaternion.identity);
            go.transform.name = "Chunk_" + ichunkConfig.lodLevel;
            DC_Chunk gonodeDC = go.GetComponent<DC_Chunk>();

            gonodeDC.SetChunkConfig(chunkConfig);
            gonodeDC.InitializeDualContourBounds();
            gonodeDC.InitializeDualContour();


            seamgo = Object.Instantiate(igo, pos, Quaternion.identity);
            seamgo.transform.name = "Seam_" + ichunkConfig.lodLevel;
            DC_Chunk seamnodeDC = seamgo.GetComponent<DC_Chunk>();


            chunkConfig.seam = true;

            seamnodeDC.SetChunkConfig(chunkConfig);
            seamnodeDC.InitializeDualContourBounds();

            //find highest resolution (smallest cell_size) of its NEIGHBORS
            QuadTreeNode root = GetRoot(this);
            int maxLOD = Mathf.Max(chunkConfig.lodLevel, GetHighestLOD(root, seamnodeDC.GetDC().GetMax(), seamnodeDC.GetDC().GetMin()));

            //set resolution to 2^ number of times the max lod is ahead of the current lod
            if (maxLOD != chunkConfig.lodLevel) seamnodeDC.GetDC().SetResolution(1 << (maxLOD - chunkConfig.lodLevel));
            seamnodeDC.InitializeDualContour();


        }

        public void AddChild(QuadTreeNode newchild) { children.Add(newchild); }

        public void NextLOD() {

            Vector3[] binaryOperator = { Vector3.zero, Vector3.right, new Vector3(1,0,1), new Vector3(1,1,0),  Vector3.up, new Vector3(0,1,1),  Vector3.one, Vector3.forward };
            float powerof2Frac = 1f / (1 << (chunkConfig.lodLevel + 1));
            Vector3 p2fracVec = new Vector3(powerof2Frac, powerof2Frac, powerof2Frac);

            for (int i = 0; i < 8; ++i) {


                Vector3 childLODOffset = chunkConfig.lodOffset + new Vector3(p2fracVec.x * binaryOperator[i].x, p2fracVec.y * binaryOperator[i].y, p2fracVec.z * binaryOperator[i].z);
                Vector3Int childScale = chunkConfig.scale;

                ChunkConfig childChunkConfig = new ChunkConfig(chunkConfig.lodLevel+1,chunkConfig.dir,childLODOffset, childScale, false); 

                QuadTreeNode newchild = new QuadTreeNode(childChunkConfig, prefab, this);

                //after instantiation of gameObject
                

                AddChild(newchild);
            }

            //Segments the mesh generation job into key steps
            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(8,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
            JobHandle combinedjobs;

            //Calculate vertex positions
            for (int i = 0; i < 8; ++i) {
                var vertJob = children[i].go.GetComponent<DC_Chunk>().GetDC().GetVertexParallel();
                JobHandle newHandle = vertJob.Schedule();
                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();

            //Gather Vertices from surrounding chunks into seams
            for (int i = 0; i < 8; ++i)
            {
                DC_Chunk seamDC = children[i].seamgo.GetComponent<DC_Chunk>();
                DC_Chunk goDC = children[i].go.GetComponent<DC_Chunk>();

                //get seams from neighbors
                QuadTreeNode root = GetRoot(this);
                GetSeams(root, seamDC.GetDC() );

                //gather vertices from parent chunk of seam
                goDC.GetDC().ConstructSeamFromParentChunk(seamDC.GetDC());
            }

            //////////////////SEAM JOBS

            //Calculate intermediary vertices of seam
            
            
            for (int i = 0; i < 8; ++i)
            {
                Dual_Contour seamdc = children[i].seamgo.GetComponent<DC_Chunk>().GetDC();
                var quadJob = seamdc.GetSeamVertexParallel();
                JobHandle newHandle = quadJob.Schedule();

                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();
            


            //Calculate quads / tris of seams
            for (int i = 0; i < 8; ++i)
            {
                Dual_Contour seamdc = children[i].seamgo.GetComponent<DC_Chunk>().GetDC();
                var quadJob = seamdc.GetSeamQuadParallel();
                JobHandle newHandle = quadJob.Schedule();

                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();

            //////////////////////////////

            //Calculate quads / tris
            for (int i = 0; i < 8; ++i)
            {
                var quadJob = children[i].go.GetComponent<DC_Chunk>().GetDC().GetQuadParallel();
                JobHandle newHandle = quadJob.Schedule();

                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();

            //Apply mesh details
            foreach (var child in children) {
                child.go.GetComponent<DC_Chunk>().GenerateDCMesh();
                child.seamgo.GetComponent<DC_Chunk>().GenerateDCMesh();
            }
            ////////////////////////////////////////////////

        }

        public void PrevLOD() {
            foreach (QuadTreeNode child in children)
            {
                child.DestroyGO();
            }
            children.Clear();
        }

        //Reverse DFS to get root of tree
        public QuadTreeNode GetRoot(QuadTreeNode node) {
            if (node.GetParent() != null) { 
                return GetRoot(node.GetParent());
            }
            return node;
        }


        public void GetSeams(QuadTreeNode node, Dual_Contour current) {

            //if outside the max range, stop
            Vector3 omin = node.go.GetComponent<DC_Chunk>().GetDC().GetMin();
            Vector3 omax = node.go.GetComponent<DC_Chunk>().GetDC().GetMax();
            Vector3 cmin = current.GetMin();
            Vector3 cmax = current.GetMax();

            //min and max, for each of the 7 neighbors, should satisfy at least 1 dimension where min.xyz == max.xyz

            //consider floating point arithmetic
            if (cmax.x < omin.x && cmax.y < omin.y && cmax.z < omin.z) return;
            if (cmin.x > omax.x && cmin.y > omax.y && cmin.z > omax.z) return;

            if (node.HasChildren())
            {
                foreach (QuadTreeNode child in node.GetChildren())
                {
                    GetSeams(child, current);
                }
            }
            else {
                //is leaf, gather nodes
                node.go.GetComponent<DC_Chunk>().GetDC().ConstructSeamNodes( current);
            }



        }

        public int GetHighestLOD(QuadTreeNode node, Vector3 cmax, Vector3 cmin) {
            int lodLevel = 0;

            //if outside the max range, stop
            Vector3 omin = node.go.GetComponent<DC_Chunk>().GetDC().GetMin();
            Vector3 omax = node.go.GetComponent<DC_Chunk>().GetDC().GetMax();

            //min and max, for each of the 7 neighbors, should satisfy at least 1 dimension where min.xyz == max.xyz

            //consider floating point arithmetic
            if (cmax.x < omin.x && cmax.y < omin.y && cmax.z < omin.z) return -1;
            if (cmin.x > omax.x && cmin.y > omax.y && cmin.z > omax.z) return -1;

            if (node.HasChildren())
            {

                foreach (QuadTreeNode child in children) lodLevel = Mathf.Max(lodLevel, GetHighestLOD(child, cmax, cmin));

                return lodLevel;
            }
            else {
                if (Dual_Contour.GetNeighborRule(cmin, cmax, omin, omax) == 0) return -1;

                lodLevel = node.go.GetComponent<DC_Chunk>().GetDC().GetLODLevel();
                return lodLevel;
            }
        }

        public void DestroyGO() { GameObject.Destroy(go); }

        public GameObject GetGameObject() { return go; }

        public bool HasChildren() { return children.Count > 0; }

        public QuadTreeNode GetParent() {
            return parent;
        }

        public QuadTreeNode GetChild(int i) { 
            if(i > 0 && i < children.Count) return children[i]; 
            return null;
        }

        //In C#, reference types return references when in C++ they are copied.
        public List<QuadTreeNode> GetChildren() { return children;}

        public int GetLODLevel() { return chunkConfig.lodLevel; }

    }
}
