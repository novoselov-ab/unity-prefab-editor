using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace RuntimePrefabEditor
{
    public class RuntimePrefabEditor : EditorWindow
    {
        static int MinWidth = 455;
        
        bool _saveAssetsRecommended = false;
        
        Vector2 _scrollPos = new Vector2 (0, 0);
        
        PrefabSearchDB _searchDB = null;
        
        
        private PrefabSearchDB.PrefabCandidate _selectedPrefabCandidate;
        private HashSet<GameObjectDiff.Diff.Id> _selectedDiffs = new HashSet<GameObjectDiff.Diff.Id>();
        private HashSet<GameObjectDiff.Diff.Id> _deselectedDiffs = new HashSet<GameObjectDiff.Diff.Id>();
        
        [MenuItem ("Window/Prefab Editor")]
        static void Init ()
        {  
            RuntimePrefabEditor window = (RuntimePrefabEditor)EditorWindow.GetWindow (typeof(RuntimePrefabEditor));
            window.minSize = new Vector2 (MinWidth, 300);
        }
        
        private void InitDB()
        {
            if(_searchDB == null)
            {
                _searchDB = new PrefabSearchDB();
            }
        }
        
        private void OnSelectionChange()
        {
            Repaint();
        }
    
        private void OnInspectorUpdate()
        {
            Repaint();
        }
        
        void OnGUI ()
        {
            InitDB();
            
            bool needRepaint = false;

            // current selection
            GameObject instance = Selection.activeGameObject;
            if(instance != null && EditorUtility.IsPersistent(instance))
            {
                instance = null;
            }

            EditorGUILayout.BeginVertical();

            DrawSelectedObject(instance);
            
            DrawPrefabCandidatesSelection(instance);
            
            DrawDiffs(instance);
            
            DrawSettings();

            EditorGUILayout.EndVertical();
            
            if(needRepaint)
            {
                GUI.changed = true;
            }
        }
        
        private void DrawSelectedObject(GameObject instance)
        {
            EditorGUILayout.Space();
            
            // selected
            if(instance != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Selected object:", EditorStyles.boldLabel, GUILayout.Width(120f));
                EditorGUILayout.ObjectField(instance, typeof(GameObject), false);
                EditorGUILayout.EndHorizontal();
                
                DrawSeparator();
            }
            else 
            {
                EditorGUILayout.HelpBox("Select GameObject in scene", MessageType.Info, true);
                //EditorGUILayout.LabelField("Select GameObject in scene.");
            }
        }
        
        private void DrawPrefabCandidatesSelection(GameObject instance)
        {
            if(instance == null)
            {
                _selectedPrefabCandidate.Reset();
                return;
            }
            
            // find candidates
            List<PrefabSearchDB.PrefabCandidate> prefabCandidates = _searchDB.GetPrefabCandidatesForSceneObject(instance);
            if(!_selectedPrefabCandidate.IsValid() || !prefabCandidates.Contains(_selectedPrefabCandidate))
            {
                _selectedPrefabCandidate = FindBestCandidate(prefabCandidates);
            }

            EditorGUILayout.LabelField("Prefab to edit:", EditorStyles.boldLabel);
            
            // draw candidates
            if(prefabCandidates.Count == 0)
            {
                EditorGUILayout.HelpBox("Can't find prefab for selected object.", MessageType.Warning, true);
            }
            else if(prefabCandidates.Count == 1)
            {
            }
            else if(prefabCandidates.Count > 1)
            {
                EditorGUILayout.LabelField("Few prefabs candidates found (select one that you want to edit):");
            }
                
            foreach(var candidate in prefabCandidates)
            {
                EditorGUILayout.BeginHorizontal();
                bool selected = (_selectedPrefabCandidate.Equals(candidate));
                if(prefabCandidates.Count > 1)
                {
                    if(selected != EditorGUILayout.Toggle(selected, GUILayout.Width(50)))
                    {
                        _selectedPrefabCandidate = candidate;
                        GUI.changed = true;
                    }
                }
                EditorGUILayout.ObjectField(candidate.prefabRoot, typeof(GameObject), false, GUILayout.Width(200));
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(candidate.prefabRoot), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }
            
            if(prefabCandidates.Count == 0 || !prefabCandidates.Contains(_selectedPrefabCandidate))
            {
                _selectedPrefabCandidate.Reset();
            }
            
            if(_selectedPrefabCandidate.IsValid())
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Path in prefab:", EditorStyles.boldLabel);
                string path = _selectedPrefabCandidate.prefabRoot.name + (_selectedPrefabCandidate.prefabPath.Length > 0 ? "/" + _selectedPrefabCandidate.prefabPath : " (root)");
                EditorGUILayout.LabelField(path, GUILayout.ExpandWidth(true));
            }
            
            DrawSeparator();
        }
        
        private void DrawDiffs(GameObject instance)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            
            // selected prefab diff
            if(_selectedPrefabCandidate.IsValid())
            {
                List<GameObjectDiff.Diff> _diffs = GameObjectDiff.GetDiffs(instance, _selectedPrefabCandidate.instanceRoot,
                    _selectedPrefabCandidate.prefab, _selectedPrefabCandidate.prefabRoot);

                EditorGUILayout.LabelField("Changes:", EditorStyles.boldLabel);
                
                if(_diffs.Count > 0)
                {
                    _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, false, false);
                    
                    bool anyDiffSelected = false;
                    foreach(var diff in _diffs)
                    {
                        EditorGUILayout.BeginHorizontal();
                        bool diffSelected = IsDiffSelected(diff);
                        bool selected = EditorGUILayout.Toggle(diffSelected, GUILayout.Width(15));
                        if(diffSelected != selected)
                        {
                            SetDiffSelected(diff, selected);
                            GUI.changed = true;
                        }
                        anyDiffSelected |= diffSelected;
                        diff.OnGUI();
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView ();
                
                    // apply ?
                    GUI.enabled = anyDiffSelected;
                    GUI.color = anyDiffSelected ? Color.green : Color.grey;
                    if(GUILayout.Button("Apply Changes To Prefab", GUILayout.Height(25)))
                    {
                        Undo.RecordObject(_selectedPrefabCandidate.prefabRoot, "Apply Changes");

                        AssetDatabase.StartAssetEditing();
                        foreach(var diff in _diffs)
                        {
                            if(IsDiffSelected(diff))
                            {
                                diff.Apply();
                            }
                        }
                        
                        AssetDatabase.StopAssetEditing();
                        EditorUtility.SetDirty( _selectedPrefabCandidate.prefabRoot );
                        _saveAssetsRecommended = true;
                        if(saveAssetsOnApply)
                        {
                            SaveAssets();
                        }
                        _diffs = null;
                        GUI.changed = true;
                    }
                    GUI.color = Color.white;
                    GUI.enabled = true;
                }
                else 
                {
                    EditorGUILayout.HelpBox("No changes found.", MessageType.Info, true);
                }
            }
            
            EditorGUILayout.EndVertical();
            
            // save assets recommendation
            if(_saveAssetsRecommended)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                GUI.color = new Color(1, 1, 0.3f);
                if(GUILayout.Button("Save Assets", GUILayout.Height(37)))
                {
                    SaveAssets();
                    GUI.changed = true;
                }
                GUI.color = Color.white;
                EditorGUILayout.HelpBox("Assets have been changed, it is recommended to save assets.", MessageType.Info, true);
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawSettings()
        {
            DrawSeparator();
            
            EditorGUILayout.LabelField("Preferences:", EditorStyles.boldLabel);
            selectAllByDefault = EditorGUILayout.Toggle("Select All by default:", selectAllByDefault, GUILayout.Width(200));
            saveAssetsOnApply = EditorGUILayout.Toggle("Save on apply (slower):", saveAssetsOnApply, GUILayout.Width(200));
            if (GUILayout.Button("Refresh DB", GUILayout.Height(25), GUILayout.Width(90)))
            {
                _searchDB = null;
                InitDB();
            }
        }
        
        private bool IsDiffSelected(GameObjectDiff.Diff diff)
        {
            return selectAllByDefault ? !_deselectedDiffs.Contains(diff.id) : _selectedDiffs.Contains(diff.id);
        }
        
        private void SetDiffSelected(GameObjectDiff.Diff diff, bool selected)
        {
            if(selected)
            {
                _selectedDiffs.Add(diff.id);
                _deselectedDiffs.Remove(diff.id);
            }
            else 
            {
                _selectedDiffs.Remove(diff.id);
                _deselectedDiffs.Add(diff.id);
            }
        }
    
        private PrefabSearchDB.PrefabCandidate FindBestCandidate(List<PrefabSearchDB.PrefabCandidate> candidates)
        {
            return candidates.FirstOrDefault();
    	}
        
        void DrawSeparator()
        {
            EditorGUILayout.Space();
            if (Event.current.type == EventType.Repaint)
            {
                Texture2D tex = EditorGUIUtility.whiteTexture;
                Rect rect = GUILayoutUtility.GetLastRect();
                GUI.color = new Color(0f, 0f, 0f, 0.25f);
                GUI.DrawTexture(new Rect(0f, rect.yMin + 6f, rect.width, 4f), tex);
                GUI.DrawTexture(new Rect(0f, rect.yMin + 6f, rect.width, 1f), tex);
                GUI.DrawTexture(new Rect(0f, rect.yMin + 9f, rect.width, 1f), tex);
                GUI.color = Color.white;
            }                   
            EditorGUILayout.Space();
        }
        
        static bool DrawFoldout(bool foldout, string content, GUIStyle style)
        {
            Rect position = GUILayoutUtility.GetRect(40f, 40f, 16f, 16f, style);
            // EditorGUI.kNumberW == 40f but is internal
            return EditorGUI.Foldout(position, foldout, new GUIContent(content), true, style);
        }
        
        void SaveAssets()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _saveAssetsRecommended = false;
        }
        
        
        // settings:
        public bool selectAllByDefault
        {
            get { return EditorPrefs.GetBool("RPE/selectAllByDefault", true); }
            set { EditorPrefs.SetBool("RPE/selectAllByDefault", value); }
        }
        
        public bool saveAssetsOnApply
        {
            get { return EditorPrefs.GetBool("RPE/saveAssetsOnApply", false); }
            set { EditorPrefs.SetBool("RPE/saveAssetsOnApply", value); }
        }
        
   }
}