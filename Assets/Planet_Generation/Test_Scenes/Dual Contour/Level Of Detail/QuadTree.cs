using DualContour;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


namespace QuadTree
{

    public struct ChunkConfig
    {
        public int lodLevel;
        public int dir; //contains the UAxis, VAxis, and their signs. WAxis can be derived by their cross product
        public Vector3 lodOffset;
        public Vector3Int scale;
        public float baseLength;
        public bool seam;
        public Transform transform;

        public ChunkConfig(int ilodLevel, int idir, Vector3 ilodOffset, Vector3Int iscale, bool iseam, Transform itransform, float ibaseLength)
        {
            lodLevel = ilodLevel;
            dir = idir;
            lodOffset = ilodOffset;
            scale = iscale;
            seam = iseam;
            transform = itransform;
            baseLength = ibaseLength;
        }

    }




    public class QuadTreeNode
    {
        private ChunkConfig chunkConfig;
        private List<QuadTreeNode> children;
        private QuadTreeNode parent;
        private bool empty;

        private GameObject prefab;
        private GameObject go;
        private GameObject seamgo;

        private static Vector3[] binaryOperator = { Vector3.zero, Vector3.right, new Vector3(1, 0, 1), new Vector3(1, 1, 0), Vector3.up, new Vector3(0, 1, 1), Vector3.one, Vector3.forward };

        //Constructors
        public QuadTreeNode(ChunkConfig ichunkConfig)
        {
            chunkConfig = ichunkConfig;
        }

        public QuadTreeNode(ChunkConfig ichunkConfig, GameObject igo, QuadTreeNode iparent)
        {
            //Initialize all relevant internal information, being the children, parent, and prefab
            children = new List<QuadTreeNode>();
            chunkConfig = ichunkConfig;
            prefab = igo;
            parent = iparent;
            empty = true;

            //Determine the position to place the prefab based on if it's the root or not
            Vector3 pos = chunkConfig.lodLevel == 0 ? Vector3.zero : parent.GetGameObject().transform.position;
            go = Object.Instantiate(igo, pos, Quaternion.identity);
            go.transform.name = "Chunk_" + ichunkConfig.lodLevel;
            go.transform.parent = ichunkConfig.transform;
            DC_Chunk gonodeDC = go.GetComponent<DC_Chunk>();

            Vector3 newOffset = (parent != null) ? parent.go.GetComponent<DC_Chunk>().GetDC().GetOffset() : Vector3.one * -chunkConfig.baseLength / 2;

            gonodeDC.SetChunkConfig(chunkConfig);
            gonodeDC.InitializeDualContourBounds(newOffset);
            gonodeDC.InitializeDualContour();


            seamgo = Object.Instantiate(igo, pos, Quaternion.identity);
            seamgo.transform.name = "Seam_" + ichunkConfig.lodLevel;
            seamgo.transform.parent = ichunkConfig.transform;
            DC_Chunk seamnodeDC = seamgo.GetComponent<DC_Chunk>();


            chunkConfig.seam = true;

            seamnodeDC.SetChunkConfig(chunkConfig);
            seamnodeDC.InitializeDualContourBounds(newOffset);

            //For testing purposes only
            seamnodeDC.SetOffset(seamnodeDC.GetDC().GetOffset());
            gonodeDC.SetOffset(gonodeDC.GetDC().GetOffset());

            //find highest resolution (smallest cell_size) of its NEIGHBORS
            QuadTreeNode root = GetRoot(this);
            int maxLOD = Mathf.Max(chunkConfig.lodLevel, GetHighestLOD(root, seamnodeDC.GetDC().GetMax(), seamnodeDC.GetDC().GetMin()));

            //set resolution to 2^ number of times the max lod is ahead of the current lod
            if (maxLOD != chunkConfig.lodLevel) seamnodeDC.GetDC().SetResolution(1 << (maxLOD - chunkConfig.lodLevel));
            seamnodeDC.InitializeDualContour();
        }

        public void AddChild(QuadTreeNode newchild) { children.Add(newchild); }

        public void NextLOD()
        {

            QuadTreeNode root = GetRoot(this);
            float powerof2Frac = 1f / (1 << (chunkConfig.lodLevel + 1));
            Vector3 p2fracVec = new Vector3(powerof2Frac, powerof2Frac, powerof2Frac);

            for (int i = 0; i < 8; ++i)
            {
                Vector3 childLODOffset = chunkConfig.lodOffset + new Vector3(p2fracVec.x * binaryOperator[i].x, p2fracVec.y * binaryOperator[i].y, p2fracVec.z * binaryOperator[i].z);
                ChunkConfig childChunkConfig = new ChunkConfig(chunkConfig.lodLevel + 1, chunkConfig.dir, childLODOffset, chunkConfig.scale, false, chunkConfig.transform, chunkConfig.baseLength);

                QuadTreeNode newchild = new QuadTreeNode(childChunkConfig, prefab, this);
                AddChild(newchild);
            }

            //Segments the mesh generation job into key steps
            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            JobHandle combinedjobs;

            //Calculate vertex positions
            for (int i = 0; i < 8; ++i)
            {
                var vertJob = children[i].go.GetComponent<DC_Chunk>().GetDC().GetVertexParallel();
                JobHandle newHandle = vertJob.Schedule();
                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();




            ////SEAMS


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


            //Gather Vertices from surrounding chunks into seams
            for (int i = 0; i < 8; ++i)
            {
                DC_Chunk seamDC = children[i].seamgo.GetComponent<DC_Chunk>();
                DC_Chunk goDC = children[i].go.GetComponent<DC_Chunk>();


                //gather vertices from parent chunk of seam
                //goDC.GetDC().ConstructSeamFromParentChunk(seamDC.GetDC());

                //get seams from neighbors
                GetSeams(root, seamDC.GetDC(), false);

            }

            //Grab neighboring seam nodes last
            for (int i = 0; i < 8; ++i)
            {
                DC_Chunk seamDC = children[i].seamgo.GetComponent<DC_Chunk>();

                //get seams from neighbors

                GetSeams(root, seamDC.GetDC(), true);

            }


            //////////////////SEAM JOBS





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
            foreach (var child in children)
            {
                child.go.GetComponent<DC_Chunk>().GenerateDCMesh();
                child.seamgo.GetComponent<DC_Chunk>().GenerateDCMesh();

                child.empty = child.go.GetComponent<DC_Chunk>().HasEmptyMesh();
            }
            ////////////////////////////////////////////////
            ///
        }

        private void RefreshNeighborSeams()
        {


            QuadTreeNode root = GetRoot(this);

            Dual_Contour baseChunk = this.go.GetComponent<DC_Chunk>().GetDC();

            List<QuadTreeNode> negativeNeighbors = new();
            Stack<QuadTreeNode> DFSStack = new();
            DFSStack.Push(root);


            //find -XYZ neighbors
            while (DFSStack.Count > 0)
            {
                //UnityEngine.Debug.Log("neighbors size:" + negativeNeighbors.Count);
                QuadTreeNode current = DFSStack.Pop();
                Dual_Contour currentDC = current.seamgo.GetComponent<DC_Chunk>().GetDC();
                int rule = Dual_Contour.GetPositiveNeighborRule(currentDC.GetMin(), currentDC.GetMax(), baseChunk.GetMin(), baseChunk.GetMax());

                if (current.empty) continue;

                if (!current.HasChildren() && rule != 0)
                {
                    negativeNeighbors.Add(current);
                }

                foreach (var child in current.children)
                {
                    DFSStack.Push(child);
                }
            }

            //clear seam data. (Vertices and indices). Done implicitly in initializer

            //reinitialize the grids with the new resolution
            Dual_Contour refreshNeighborDC;
            ChunkConfig refreshNeighborConfig;
            foreach (QuadTreeNode node in negativeNeighbors)
            {
                refreshNeighborDC = node.seamgo.GetComponent<DC_Chunk>().GetDC();
                refreshNeighborConfig = node.GetChunkConfig();

                int maxLOD = Mathf.Max(refreshNeighborConfig.lodLevel, GetHighestLOD(root, refreshNeighborDC.GetMax(), refreshNeighborDC.GetMin()));
                int LODdifference = (1 << (maxLOD - refreshNeighborConfig.lodLevel));
                if (maxLOD > refreshNeighborConfig.lodLevel)
                {
                    refreshNeighborDC.SetResolution(LODdifference);
                    node.seamgo.GetComponent<DC_Chunk>().relativeResolution = LODdifference;
                    node.seamgo.GetComponent<DC_Chunk>().maxLod = maxLOD;
                    node.go.GetComponent<DC_Chunk>().step = node.go.GetComponent<DC_Chunk>().GetDC().GetStep();
                    node.seamgo.GetComponent<DC_Chunk>().step = refreshNeighborDC.GetStep();
                }
                node.seamgo.GetComponent<DC_Chunk>().InitializeDualContour();
            }

            //gather seam data. 

            ////SEAMS
            NativeArray<JobHandle> jobs = new(negativeNeighbors.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            JobHandle combinedjobs;

            //Calculate intermediary vertices of seam
            for (int i = 0; i < negativeNeighbors.Count; ++i)
            {
                Dual_Contour seamdc = negativeNeighbors[i].seamgo.GetComponent<DC_Chunk>().GetDC();
                var quadJob = seamdc.GetSeamVertexParallel();
                JobHandle newHandle = quadJob.Schedule();

                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();


            //Gather Vertices from surrounding chunks into seams
            for (int i = 0; i < negativeNeighbors.Count; ++i)
            {
                DC_Chunk seamDC = negativeNeighbors[i].seamgo.GetComponent<DC_Chunk>();
                DC_Chunk goDC = negativeNeighbors[i].go.GetComponent<DC_Chunk>();


                //gather vertices from parent chunk of seam
                //goDC.GetDC().ConstructSeamFromParentChunk(seamDC.GetDC());

                //get seams from neighbors
                GetSeams(root, seamDC.GetDC(), false);

            }

            //Grab neighboring seam nodes last
            for (int i = 0; i < negativeNeighbors.Count; ++i)
            {
                DC_Chunk seamDC = negativeNeighbors[i].seamgo.GetComponent<DC_Chunk>();

                //get seams from neighbors

                GetSeams(root, seamDC.GetDC(), true);

            }


            //Calculate quads / tris of seams
            for (int i = 0; i < negativeNeighbors.Count; ++i)
            {
                Dual_Contour seamdc = negativeNeighbors[i].seamgo.GetComponent<DC_Chunk>().GetDC();
                var quadJob = seamdc.GetSeamQuadParallel();
                JobHandle newHandle = quadJob.Schedule();

                jobs[i] = (newHandle);
            }

            combinedjobs = JobHandle.CombineDependencies(jobs);
            combinedjobs.Complete();

            //Apply mesh details
            foreach (var node in negativeNeighbors)
            {
                node.go.GetComponent<DC_Chunk>().GenerateDCMesh();
                node.seamgo.GetComponent<DC_Chunk>().GenerateDCMesh();

                node.empty = node.go.GetComponent<DC_Chunk>().HasEmptyMesh();
            }
            ////////////////////////////////////////////////


        }

        public void RenderChunk()
        {
            //Segments the mesh generation job into key steps

            //Calculate vertex positions
            DC_Chunk goDC = go.GetComponent<DC_Chunk>();
            DC_Chunk seamDC = seamgo.GetComponent<DC_Chunk>();

            var vertJob = goDC.GetDC().GetVertexParallel();
            JobHandle vertHandle = vertJob.Schedule();
            vertHandle.Complete();

            //Calculate intermediary vertices of seam
            var seamVertexJob = seamDC.GetDC().GetSeamVertexParallel();
            JobHandle quadHandle = seamVertexJob.Schedule();
            quadHandle.Complete();


            //Gather Vertices from surrounding chunks into seams

            //get seams from neighbors
            QuadTreeNode root = GetRoot(this);
            GetSeams(root, seamDC.GetDC(), false);

            //gather vertices from parent chunk of seam
            //goDC.GetDC().ConstructSeamFromParentChunk(seamDC.GetDC());

            //Calculate quads / tris of seams
            var seamTriJob = seamDC.GetDC().GetSeamQuadParallel();
            JobHandle seamTriHandle = seamTriJob.Schedule();
            seamTriHandle.Complete();

            //Calculate quads / tris
            var chunkTriJob = go.GetComponent<DC_Chunk>().GetDC().GetQuadParallel();
            JobHandle chunkTriHandle = chunkTriJob.Schedule();
            chunkTriHandle.Complete();

            //Apply mesh details
            go.GetComponent<DC_Chunk>().GenerateDCMesh();
            seamgo.GetComponent<DC_Chunk>().GenerateDCMesh();
            ////////////////////////////////////////////////
            ///
            empty = go.GetComponent<DC_Chunk>().HasEmptyMesh();
        }

        public void PrevLOD()
        {
            foreach (QuadTreeNode child in children)
            {
                child.DestroyGO();
            }
            children.Clear();
        }

        //Reverse DFS to get root of tree
        public QuadTreeNode GetRoot(QuadTreeNode node)
        {
            if (node.GetParent() != null)
            {
                return GetRoot(node.GetParent());
            }
            return node;
        }

        public ChunkConfig GetChunkConfig() { return chunkConfig; }

        public void GetSeams(QuadTreeNode node, Dual_Contour current, bool seam)
        {

            //if outside the max range, stop
            Vector3 omin = node.go.GetComponent<DC_Chunk>().GetDC().GetMin();
            Vector3 omax = node.go.GetComponent<DC_Chunk>().GetDC().GetMax();
            Vector3 cmin = current.GetMin();
            Vector3 cmax = current.GetMax();

            //ignore the same chunk
            //if (cmin == omin && cmax == omax) return;

            if (node.HasChildren())
            {
                foreach (QuadTreeNode child in node.GetChildren())
                {
                    GetSeams(child, current, seam);
                }
            }
            else
            {
                //is leaf, gather nodes
                Dual_Contour neighborDC = seam ? node.seamgo.GetComponent<DC_Chunk>().GetDC() : node.go.GetComponent<DC_Chunk>().GetDC();

                //neighborDC.ConstructSeamNodes(current);
            }
        }

        public int GetHighestLOD(QuadTreeNode node, Vector3 cmax, Vector3 cmin)
        {
            int lodLevel = 0;
            Dual_Contour nodeDC = node.go.GetComponent<DC_Chunk>().GetDC();

            //if outside the max range, stop
            Vector3 omin = nodeDC.GetMin();
            Vector3 omax = nodeDC.GetMax();

            if (node.HasChildren())
            {

                foreach (QuadTreeNode child in children) lodLevel = Mathf.Max(lodLevel, GetHighestLOD(child, cmax, cmin));

                return lodLevel;
            }
            else
            {
                if (Dual_Contour.GetPositiveNeighborRule(cmin, cmax, omin, omax) == 0) return -1;

                lodLevel = nodeDC.GetLODLevel();
                return lodLevel;
            }
        }

        public void DestroyGO() { GameObject.Destroy(go); }

        public GameObject GetGameObject() { return go; }

        public GameObject GetSeamGameObject() { return seamgo; }

        public bool HasChildren() { return children.Count > 0; }

        public bool IsEmpty() { return empty; }

        public QuadTreeNode GetParent()
        {
            return parent;
        }

        public QuadTreeNode GetChild(int i)
        {
            if (i > 0 && i < children.Count) return children[i];
            return null;
        }

        //In C#, reference types return references when in C++ they are copied.
        public List<QuadTreeNode> GetChildren() { return children; }

        public int GetLODLevel() { return chunkConfig.lodLevel; }

    }
}
