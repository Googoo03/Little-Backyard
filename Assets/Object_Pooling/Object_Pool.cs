using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


namespace ObjPool
{
    public class Object_Pool<T>
    {
        private List<Tuple<T,bool>> _pool;
        private int _m; //max value

        private int _indexer;



        //CONSTRUCTORS
        public Object_Pool( int capacity ){
            _pool = new List<Tuple<T,bool>>(capacity);
            _m = capacity;
        }

        public T getPoolObj(int index) { return _pool[index].Item1; }

        public void setPoolObj(int index, T obj) { _pool[index] = new Tuple<T, bool>(obj,false); }

        public void addPoolObj( T obj) {
            Assert.IsTrue(_pool.Count < _m);
            _pool.Add(new Tuple<T, bool>(obj,false));
        }

        public void releasePool(int index) {
            _pool[index] = new Tuple<T, bool>(_pool[index].Item1,false);
        }

        public void lockPool(int index) {
            _pool[index] = new Tuple<T, bool>(_pool[index].Item1, true);
        }

        //returns a list of objects that are free. Does 1 complete rotation in the list
        public void findSubPool( Tuple<List<T>,int> request) {

            List<int> foundIndices = new List<int> ();
            List<T> foundObjs = new List<T> ();
            int found = 0;
            int i = _indexer;
            //int max = _indexer == 0 ? _m-1 : _indexer - 1;

            while (i < _pool.Count && found < request.Item2) {

                int index = (i + _indexer) % _m;
                i++;

                if (_pool[index].Item2) continue; //skip if activated already


                //add to list
                foundObjs.Add(_pool[index].Item1);
                foundIndices.Add(index);
                found++;
                
            }
            /*for( ; i != max && found < request.Item2; ++i)
            {
                i %= _m; //apply wrapping

                if (_pool[i].Item2) continue; //skip if activated already


                //add to list
                foundObjs.Add(_pool[i].Item1);
                foundIndices.Add(i);

                //increment number that is found
                found++;
            }*/
            _indexer = i;

            if (found < request.Item2) return; //return an empty list if quota is not met


            //if quota is met, then lock those who are needed
            for (int j = 0; j < foundIndices.Count; ++j) {
                lockPool(foundIndices[j]);
            }

            //WOOHOO FIRST LAMBDA FUNCTION
            foundObjs.ForEach(item => { request.Item1.Add(item); });

            return; //return the list that we find
        }
    }
}
