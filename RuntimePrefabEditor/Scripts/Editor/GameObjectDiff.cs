using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace RuntimePrefabEditor
{
    /// <summary>
    /// Helper class to find diff between gameobject and it's prefab
    /// </summary>
    public static class GameObjectDiff
    {
        // class to keep resulting diff
        public class Diff
        {
            public enum Operation
            {
                Delete,
                Add,
                Property,
                New
            }
            
            public Diff(Operation operation, Component component, string propertyPath)
            {
                id.operation = operation;
                id.component = component;
                id.propertyPath = propertyPath;
            }
            
            public struct Id
            {
                public Operation operation;
                public Component component;
                public string propertyPath;
            }
            
            public Id id;
            public Action Apply;
            public Action OnGUI;
        }

    	public static List<Diff> GetDiffs(GameObject instance, GameObject instanceRoot, GameObject prefab, GameObject prefabRoot)
    	{
            List<Diff> diffs = new List<Diff>();
            
    		if( prefab )
    		{
    			// compare existing components
    			var prefabComponents = prefab.GetComponents<Component>();
    			var deletedComponents = new List<Component>();
    			foreach( var prefabComponent in prefabComponents )
    			{
    				var instanceComponentsOfType = instance.GetComponents( prefabComponent.GetType() );
                    Component instanceComponent = instanceComponentsOfType.Length > 0 ? instanceComponentsOfType[0] : null;
                    
                    // if we have more than one same component -> try to get the same by order of GetComponents()
                    if(instanceComponentsOfType.Length > 1)
                    {
                        int indexInPrefab = Array.IndexOf(prefab.GetComponents(prefabComponent.GetType()), prefabComponent);
                        instanceComponent = instanceComponentsOfType[indexInPrefab % instanceComponentsOfType.Length];
                    }
                    
    				if( instanceComponent )
    				{
                        FillDiffsFromComponent(ref diffs, instanceComponent, prefabComponent, instanceRoot, prefabRoot );
    				}
    				else
    				{
    					deletedComponents.Add(prefabComponent);
    				}
    			}
    
    			// delete obsolete components 
    			foreach( var deletedComponent in deletedComponents )
    			{
                    Component prefabDeletedComponent = deletedComponent;
                    Diff deleteDiff = new Diff(Diff.Operation.Delete, prefabDeletedComponent, null);
                    deleteDiff.Apply = () => { GameObject.DestroyImmediate( prefabDeletedComponent, true ); };
                    deleteDiff.OnGUI = () => 
                    {
                        GUI.color = new Color(1, 0.7f, 0.7f);
                        EditorGUILayout.LabelField("Deleted component:", prefabDeletedComponent.GetType().Name);
                        GUI.color = Color.white;
                        EditorGUILayout.ObjectField(prefabDeletedComponent, typeof(Component), false);
                    };
                    diffs.Add(deleteDiff);
    			}
    
    			// add new components
    			var instanceComponents = instance.GetComponents<Component>();
    			foreach( var instanceComponent in instanceComponents )
    			{
    				if( !System.Array.Find<Component>( prefabComponents, (x) => { return x.GetType() == instanceComponent.GetType(); } ) )
    				{
                        Component instanceNewComponent = instanceComponent;
                        Diff addDiff = new Diff(Diff.Operation.Add, instanceNewComponent, null);
                        addDiff.Apply = () => { 
                            var prefabComponent = prefab.AddComponent( instanceNewComponent.GetType() );
                            UnityEditor.EditorUtility.CopySerialized( instanceNewComponent, prefabComponent );
                        };
                        addDiff.OnGUI = () => 
                        {
                            GUI.color = new Color(0.7f, 1, 0.7f);
                            EditorGUILayout.LabelField("Added component:", instanceNewComponent.GetType().Name);
                            GUI.color = Color.white;
                            EditorGUILayout.ObjectField(instanceNewComponent, typeof(Component), false);
                        };
                        diffs.Add(addDiff);
    				}
    			}
    		}
            else 
            {
                // probably new gameobject
                GameObjectDiff.Diff d = new GameObjectDiff.Diff(GameObjectDiff.Diff.Operation.New, null, instance.name);
                d.Apply = () => 
                {
                    GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefabRoot) as GameObject;
                    GameObject instanceCopy = new GameObject(instance.name);
                    string path = EditorUtils.GetPathForObjectInHierarchy(instance, instanceRoot);
                    instanceCopy.transform.parent = prefabInstance.GetChildByPath(path).transform;
                    foreach(Component c in instance.GetComponents<Component>())
                    {
                        if(c is Transform)
                            continue;
                        EditorUtility.CopySerialized(c, instanceCopy.AddComponent(c.GetType()));
                    }
                    EditorUtility.CopySerialized(instance.transform, instanceCopy.transform);
                    PrefabUtility.ReplacePrefab(prefabInstance, prefabRoot);
                    GameObject.DestroyImmediate(prefabInstance);
                };
                d.OnGUI = () => 
                {
                    GUI.color = new Color(0, 1f, 0);
                    EditorGUILayout.LabelField("New GameObject:", instance.name);
                    GUI.color = Color.white;
                };
                diffs.Add(d);
            }
            
            return diffs;
    	}
        
        private static void FillDiffsFromComponent(ref List<Diff> diffs, Component instanceComponent, Component prefabComponent, GameObject instanceRoot, GameObject prefabRoot )
        {
            SerializedObject serializedInstance = new SerializedObject(instanceComponent);
            SerializedObject serializedPrefab = new SerializedObject(prefabComponent);
            SerializedProperty instanceProp = serializedInstance.GetIterator(); 
            bool enterChildren = true;
            do
            {
                // Finding same property
                SerializedProperty prefabProp = serializedPrefab.FindProperty(instanceProp.propertyPath);
                if(prefabProp == null)
                    continue;
                
                // black list
                if(propertyBlacklist.Contains(instanceProp.propertyPath))
                {
                    enterChildren = false;
                    continue;
                }
                
                // Compare
                Action apply = null;
                Action onGUI = null;
                if(!CompareSerializedProperty(instanceProp.Copy(), prefabProp, instanceRoot, prefabRoot, ref apply, ref onGUI))
                {
                    Diff propertyDiff = new Diff(Diff.Operation.Property, instanceComponent, instanceProp.propertyPath );
                    propertyDiff.OnGUI = () =>
                    {
                        onGUI();
                        //EditorGUILayout.LabelField("Property changed:" + prop.propertyType.ToString() + prop.propertyPath);
                    };
                    propertyDiff.Apply = () => 
                    { 
                        apply();
                        serializedPrefab.ApplyModifiedProperties();
                    };
                    diffs.Add(propertyDiff);
                }
    
                // look for childs in generic properties (like class, structs)
                enterChildren = (instanceProp.propertyType == SerializedPropertyType.Generic);
                     
             } while(instanceProp.Next(enterChildren));
        }
    
        // ignore list, internal unity properties
        static HashSet<string> propertyBlacklist = new HashSet<string>(new string[] 
        {
            "m_ObjectHideFlags", "m_PrefabParentObject", "m_PrefabInternal", "m_GameObject", "m_EditorHideFlags", "m_FileID", "m_PathID", "m_Children"
        });   

        static bool CompareSerializedProperty(SerializedProperty p1, SerializedProperty p2, GameObject root1, GameObject root2, ref Action apply, ref Action onGUI) 
        {
            if(p1.propertyType != p2.propertyType)
                Debug.LogError("SerializedPropertys have different types!");
    		
            string SIMPLE_FORMAT = string.Format("{0}.{1}: {2}", p1.serializedObject.targetObject.GetType().Name, p1.propertyPath, "{1} -> {0}");
            
            switch (p1.propertyType) 
    		{
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Integer: 
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.ArraySize:
                    apply = () => 
                    { 
                        p2.intValue = p1.intValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.intValue, p2.intValue));
                    };
                    return (p2.intValue == p1.intValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Boolean: 
                    apply = () => 
                    { 
                        p2.boolValue = p1.boolValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.boolValue, p2.boolValue));
                    };
                    return (p2.boolValue == p1.boolValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Float: 
                    apply = () => 
                    { 
                        p2.floatValue = p1.floatValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.floatValue, p2.floatValue));
                    };
                    return (p2.floatValue == p1.floatValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.String: 
                    apply = () => 
                    { 
                        p2.stringValue = p1.stringValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.stringValue, p2.stringValue));
                    };
                    return (p2.stringValue == p1.stringValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Color: 
                    apply = () => 
                    { 
                        p2.colorValue = p1.colorValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.colorValue.ToString255(), p2.colorValue.ToString255()));
                        EditorGUILayout.ColorField(p2.colorValue, GUILayout.Width(45));
                        EditorGUILayout.LabelField(" -> ", GUILayout.Width(35));
                        EditorGUILayout.ColorField(p1.colorValue, GUILayout.Width(45));
                    };
                    return (p2.colorValue == p1.colorValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Enum: 
                    apply = () => 
                    { 
                        p2.enumValueIndex = p1.enumValueIndex; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.enumNames[p1.enumValueIndex], p2.enumNames[p2.enumValueIndex]));
                    };
                    return (p2.enumValueIndex == p1.enumValueIndex);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Vector2: 
                    apply = () => 
                    { 
                        p2.vector2Value = p1.vector2Value; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.vector2Value, p2.vector2Value));
                    };
                    return (p2.vector2Value == p1.vector2Value);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Vector3: 
                    apply = () => 
                    { 
                        p2.vector3Value = p1.vector3Value; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.vector3Value, p2.vector3Value));
                    };
                    return (p2.vector3Value == p1.vector3Value);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Quaternion:
                    apply = () => 
                    { 
                        p2.quaternionValue = p1.quaternionValue;
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.quaternionValue.eulerAngles, p2.quaternionValue.eulerAngles));
                    };
                    return (p2.quaternionValue == p1.quaternionValue);
                    /////////////////////////////////////////////////////////
                case SerializedPropertyType.Rect: 
                    apply = () => 
                    { 
                        p2.rectValue = p1.rectValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.rectValue, p2.rectValue));
                    };
                    return (p2.rectValue == p1.rectValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.AnimationCurve: 
                    
                    apply = () => 
                    { 
                        p2.animationCurveValue = p1.animationCurveValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(p2.serializedObject.targetObject.GetType().Name + "." + p2.propertyPath + ": animation curve changed");
                    };
                    return EditorUtils.IsAnimationCurvesEqual(p2.animationCurveValue, p1.animationCurveValue);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Bounds: 
                    apply = () => 
                    { 
                        p2.boundsValue = p1.boundsValue; 
                    };
                    onGUI = () => 
                    { 
                        EditorGUILayout.LabelField(string.Format(SIMPLE_FORMAT, p1.boundsValue, p2.boundsValue));
                    };
                    return (p2.boundsValue == p1.boundsValue);
                /////////////////////////////////////////////////////////
    			case SerializedPropertyType.ObjectReference: 
                    return CompareSerializedPropertyObjectReference(p1, p2, root1, root2, ref apply, ref onGUI);
                /////////////////////////////////////////////////////////
                case SerializedPropertyType.Generic: 
                    // not implemented
                    return true;
                /////////////////////////////////////////////////////////
                /////////////////////////////////////////////////////////
                default:
                    // not implemented
                    //Debug.LogError("Implement: " + p1.propertyType + " " + p1.propertyPath);
                    return true;
            }
        }
        
        static bool CompareSerializedPropertyObjectReference(SerializedProperty p1, SerializedProperty p2, GameObject root1, GameObject root2, ref Action apply, ref Action onGUI) 
        {
    		// If this is objectReference property, we want to try to find same object in our hierarchy and use it. Otherwise
    		// property is just copied and links same object.

            onGUI = () => 
            { 
                EditorGUILayout.LabelField(p2.serializedObject.targetObject.GetType().Name + "." + p2.propertyPath + ": reference changed");
                EditorGUILayout.ObjectField(p1.objectReferenceValue, typeof(UnityEngine.Object), false, GUILayout.Width(200));
            };

            if(p1.objectReferenceValue == null || EditorUtility.IsPersistent(p1.objectReferenceValue))
            {
                apply = () => 
                { 
                    p2.objectReferenceValue = p1.objectReferenceValue; 
                };
                return (p2.objectReferenceValue == p1.objectReferenceValue);
            }
            else 
            {
    			GameObject referencedGO = null;
    			if(p1.objectReferenceValue is GameObject)
                {
    				referencedGO = p1.objectReferenceValue as GameObject;
                }
    			else if(p1.objectReferenceValue is Component)
                {
    				referencedGO = (p1.objectReferenceValue as Component).gameObject;
                }
    			else 
    			{
    				Debug.LogWarning(string.Format("Unknown object reference type {0}", p1.objectReferenceValue.GetType()), p1.serializedObject.targetObject);
    				return true;
    			}
    				
    			if(referencedGO == null)
    			{
    				Debug.LogError(string.Format("Wrong object reference type {0}", p1.objectReferenceValue.GetType()), p1.serializedObject.targetObject);
    				return true;
    			}
    				
    			string path1 = EditorUtils.GetPathForObjectInHierarchy(referencedGO, root1);
    			if(path1 == null)
    			{
    				// It means it references to some of other assets, leave link as it is.
    				return true;
    			}
    			
    			//Debug.LogWarning("Path: {0} for: {1}", path, fromProp.objectReferenceValue.name);
    			
    			GameObject newGO = root2.GetChildByPath(path1);
    			if(newGO == null)
    			{
    				//Debug.LogWarning(string.Format("Can't find transform for path {0}", path1), p1.serializedObject.targetObject);
    				return true;
    			}
    				
    			if(p1.objectReferenceValue is GameObject)
                {
    				apply = () => { p2.objectReferenceValue = newGO.gameObject; };
                    return path1 == EditorUtils.GetPathForObjectInHierarchy(p2.objectReferenceValue as GameObject, root2);
                }
    			else if(p1.objectReferenceValue is Component)
    			{
    				Component myComp = newGO.gameObject.GetComponent(p1.objectReferenceValue.GetType());
    				if(myComp == null)
                    {
                        // Can't apply this change since we can't find needed component in hierarchy
                        //Debug.LogError(string.Format("Can't find component on object {0}", path1), p1.serializedObject.targetObject);
                    }
                    
                    apply = () => { p2.objectReferenceValue = myComp; };
                    
                    var c2 = p2.objectReferenceValue as Component;
                    if(c2 != null)
                        return path1 == EditorUtils.GetPathForObjectInHierarchy(c2.gameObject, root2);
                    else 
                        return false;
    			}
    		}
            
            return true;
        }	
         
        
    }
}
