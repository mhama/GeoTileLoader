using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections;
using Cysharp.Threading.Tasks;
using Unity.EditorCoroutines.Editor;

namespace GeoTile
{
    /// <summary>
    /// TileSetNodeComponentの操作用UI
    /// </summary>
    [CustomEditor(typeof(TileSetNodeComponent))]
    public class TileSetNodeComponentEditor : UnityEditor.Editor
    {
        TileSetNodeComponent Target { get; set; }

        private void OnEnable()
        {
            Target = (TileSetNodeComponent)target;
        }

        public override void OnInspectorGUI()
        {
            var contentUrl = Target.TileSetNode?.content?.Url;
            var contentFound = !string.IsNullOrEmpty(contentUrl);
            var alreadyLoaded = Target.transform.Find("GLTF") != null;
            var contentExtension = Target.GetContentExtension();
            var jsonFound = contentExtension != null && contentExtension.EndsWith(".json");

            base.OnInspectorGUI();

            EditorGUILayout.TextField("content extension:", contentExtension);

            if (GUILayout.Button("Load Nested JSON " + (jsonFound ? "" : "(not available)")))
            {
                var newUri = new Uri(new Uri(Target.BaseJsonUrl), contentUrl);
                var query = Target.TileSetNode?.content?.contentUrlQuery;
                Debug.Log("query:" + query);
                var sessionId = Target.GoogleSessionId;
                var sessionKeyValue = query.Split("&").Select(v => v.Split("=")).Where(kv => kv[0] == "session").ToList();
                if (sessionKeyValue.Count > 0)
                {
                    sessionId = sessionKeyValue[0][1];
                }

                var loader = new TileSetHierarchyLoader(new TileSetHierarchyLoaderConfig()
                {
                    TileSetJsonUrl = newUri.ToString(),
                    TileSetName = "",
                    GoogleSessionId = sessionId,
                    GoogleMapTileApiKey = Target.TileSetInfoProvider.LoaderConfig.GoogleMapTileApiKey,
                    CullingInfo = Target.CullingInfo,
                    RootParent = Target.TileSetInfoProvider.LoaderConfig.RootParent,
                });

                var hierarchy = Target.transform.GetComponentInParent<TileSetHierarchy>();
                UniTask.Void(async () =>
                {
                    try
                    {
                        await loader.ReadJson(hierarchy, Target.transform, Target.GetCancellationTokenOnDestroy());
                        Debug.Log("ReadJson success.");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("ReadJson failed. e: " + e);
                    }
                });
            }

            // GLTFロード可能かどうか拡張子で判断
            bool gltfFound = contentFound && (contentExtension == ".b3dm" || contentExtension == ".glb");

            if (GUILayout.Button("Load GLTF " + (gltfFound ? "":"(not available)") + (alreadyLoaded ? "(already loaded)" : "" )))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModel(), this);
            }

            if (GUILayout.Button("Load GLTF Under This (2 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 2), this);
            }
            if (GUILayout.Button("Load GLTF Under This (3 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 3), this);
            }
            if (GUILayout.Button("Load GLTF Under This (4 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 4), this);
            }
            if (GUILayout.Button("Load GLTF Under This (5 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 5), this);
            }
            if (GUILayout.Button("Load GLTF Under This (6 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 6), this);
            }
            if (GUILayout.Button("Load GLTF Under This (7 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 7), this);
            }
            if (GUILayout.Button("Load GLTF Under This (8 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 8), this);
            }
            if (GUILayout.Button("Load GLTF Under This (9 Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 9), this);
            }
            if (GUILayout.Button("Load GLTF Under This (Unlimited(100) Level)"))
            {
                EditorCoroutineUtility.StartCoroutine(Target.LoadModelRecursive(0, 100), this);
            }
        }
    }
}
