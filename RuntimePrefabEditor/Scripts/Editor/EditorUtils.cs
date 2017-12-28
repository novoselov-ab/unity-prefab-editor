using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace RuntimePrefabEditor
{
    public static class EditorUtils
    {
        public static bool IsAnimationCurvesEqual(AnimationCurve curve1, AnimationCurve curve2)
        {
            if(curve1.postWrapMode != curve2.postWrapMode) return false;
            if(curve1.preWrapMode != curve2.preWrapMode) return false;
            if(curve1.length != curve2.length) return false;
            return curve1.keys.SequenceEqual(curve2.keys);
        }
    	
    	public static string GetPathForObjectInHierarchy(GameObject childGO, GameObject baseGO)
    	{
            bool found = false;
    		string path = "";
    		Transform t = childGO != null ? childGO.transform : null;
    		while(t != null)
    		{
    			if(t == baseGO.transform)
    			{
                    found = true;
    				break;
    			}
    			if(string.IsNullOrEmpty(path))
    				path = t.name;
    			else
    				path = t.name + "/" + path;
    			t = t.parent;
    		}
    		return found ? path : null;
    	}
        
        public static GameObject GetChildByPath(this GameObject gameObject, string path)
        {
            if(path != null)
            {
                Transform t = path.Length > 0 ? gameObject.transform.Find(path) : gameObject.transform;
                if(t != null)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        public static string GetParentPath(string path)
        {
            path = path.TrimEnd('/');
            int i = path.LastIndexOf("/");
            if(i > 0)
            {
                return path.Substring(0, i);
            }
            return "";
        }
    		
        public static GameObject GetParent (this GameObject obj)
        {
            Transform tr = obj.transform.parent;
            return tr? tr.gameObject: null;
        }
        
        public static bool TryRemovePostfix(ref string str, string postfix)
        {
            if(!str.EndsWith(postfix))
            {   
                return false; 
            }
            else
            {   
                str = str.Substring(0, str.Length - postfix.Length);
                return true; 
            }
        }
        
        public static string WithoutClonePostfix(string name)
        {
            TryRemovePostfix(ref name, "(Clone)");
            return name; 
        }
        
        public static string ToString255(this Color c)
        {
            return string.Format("({0:0},{1:0},{2:0},{3:0})", c.r * 255, c.g * 255, c.b * 255, c.a * 255);
        }
    }
}