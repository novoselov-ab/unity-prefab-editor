using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;

namespace RuntimePrefabEditor
{
    public class PrefabSearchDB
    {
        public PrefabSearchDB()
        {
            RefreshIndex();
        }
        
        public struct PrefabCandidate
        {
            public bool IsValid() { return prefabRoot != null; }
            public void Reset() { prefabRoot = null; }
            
            public GameObject instanceRoot;
            public GameObject prefabRoot;
            public GameObject prefab;
            public string prefabPath;
        }
        
        public List<PrefabCandidate> GetPrefabCandidatesForSceneObject(GameObject instance)
        {
            List<PrefabCandidate> candidates = new List<PrefabCandidate>();
    
            RefreshIndex();
            
            GameObject instanceRoot = instance;
            while(instanceRoot != null)
            {
                string candidateName = EditorUtils.WithoutClonePostfix(instanceRoot.name);
                
                List<string> paths;
                if(_index.TryGetValue(candidateName, out paths))
                {                
                    foreach(var path in paths)
                    {
                        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
                        if(!prefabRoot) continue;

                        var prefabPath = EditorUtils.GetPathForObjectInHierarchy(instance, instanceRoot);
                        GameObject prefab = prefabRoot.GetChildByPath(prefabPath);

                        if(prefab == null)
                        {
                            // ATTENTION:
                            // new gameobjects unsupported yet, comment "continue" to enable
                            continue;

                            // try find prefab for parent 
                            prefabPath = EditorUtils.GetParentPath(prefabPath);
                            GameObject prefabParent = prefabRoot.GetChildByPath(prefabPath);
                            if(prefabParent == null)
                                continue;
                        }

                        PrefabCandidate candidate;
                        candidate.instanceRoot = instanceRoot;
                        candidate.prefabRoot = prefabRoot;
                        candidate.prefab = prefab;
                        candidate.prefabPath = prefabPath;
                
                        candidates.Add(candidate);
                    }
                }
                
                instanceRoot = instanceRoot.GetParent();
            }
            
            // longer path is preferable
            if(candidates.Count > 0)
            {
                candidates.Sort((c1, c2) => c2.prefabPath.Length.CompareTo(c1.prefabPath.Length));
            }

            return candidates;
        }
        
        private void RefreshIndex()
        {
            string[] assetGUIDs = AssetDatabase.FindAssets("t:prefab");
            if(_assetsProcessedCount != assetGUIDs.Length)
            {
                _index = new Dictionary<string, List<string>>();
                foreach(string guid in assetGUIDs)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                    List<string> paths = null;
                    if(!_index.TryGetValue(filename, out paths))
                    {
                        paths = new List<string>();
                        _index[filename] = paths;
                    }
                    paths.Add(path);
                }
                _assetsProcessedCount = assetGUIDs.Length;
            }
        }
        
        private static GameObject FindRootObject(GameObject gameObject, GameObject prefab)  
        {
            GameObject result = gameObject;
            while( result )
            {
                if(EditorUtils.WithoutClonePostfix(result.name).ToLower() == EditorUtils.WithoutClonePostfix(prefab.name).ToLower()) break;
                result = result.GetParent();
            }
            
            return result;
        }
        
        Dictionary<string, List<string>> _index = new Dictionary<string, List<string>>();
        int _assetsProcessedCount = 0;
    }
}
