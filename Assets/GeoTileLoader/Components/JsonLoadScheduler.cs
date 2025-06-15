using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using GeoTile;

/// <summary>
/// Scheduler for JSON loading tasks with concurrent execution support
/// Singleton MonoBehaviour that manages JSON loading operations
/// </summary>
public class JsonLoadScheduler : MonoBehaviour
{
    /// <summary>
    /// Task implementation for subtree loading operations (wraps original LoadSubTreesRecursive logic)
    /// </summary>
    public class LoadSubTreeTask : GeoTile.TaskScheduler.Task
    {
        private readonly TileSetHierarchy hierarchy;
        private readonly Transform trans;
        private readonly int maxLevels;
        private readonly int maxNodes;

        public LoadSubTreeTask(TileSetHierarchy hierarchy, Transform trans, int maxLevels, int maxNodes)
        {
            this.hierarchy = hierarchy;
            this.trans = trans;
            this.maxLevels = maxLevels;
            this.maxNodes = maxNodes;
        }

        public async UniTask<(bool result, object artifact)> Do(CancellationToken token)
        {
            try
            {
                var remainingNodes = await LoadSubTreesRecursiveLogic(trans, maxLevels, maxNodes, token);
                return (true, remainingNodes);
            }
            catch (Exception e)
            {
                Debug.LogError($"LoadSubTreeTask failed for {trans.name}: {e}");
                return (false, null);
            }
        }

        private async UniTask<int> LoadSubTreesRecursiveLogic(Transform trans, int maxLevels, int maxNodes, CancellationToken token)
        {
            maxNodes--;
            if (maxNodes <= 0)
            {
                Debug.LogWarning("maxNodes reached.");
                return maxNodes;
            }

            // サブツリーが存在して、既にロードされていなければロードする
            var node = trans.GetComponent<TileSetNodeComponent>();
            if (node != null
                && node.gameObject.activeInHierarchy
                && node.SubTreeExists()
                && !node.SubTreeAlreadyLoaded())
            {
                var contentUrl = node.TileSetNode?.content?.Url;
                var newUri = new Uri(new Uri(node.BaseJsonUrl), contentUrl);
                var query = node.TileSetNode?.content?.contentUrlQuery;
                Debug.Log("content query:" + query);
                var sessionId = node.GoogleSessionId;
                var sessionKeyValue = query.Split("&").Select(v => v.Split("=")).Where(kv => kv[0] == "session")
                    .ToList();
                if (sessionKeyValue.Count > 0)
                {
                    sessionId = sessionKeyValue[0][1];
                }

                var config = new TileSetHierarchyLoaderConfig()
                {
                    TileSetJsonUrl = newUri.ToString(),
                    TileSetName = "",
                    GoogleSessionId = sessionId,
                    GoogleMapTileApiKey = node.TileSetInfoProvider.LoaderConfig.GoogleMapTileApiKey,
                    CullingInfo = node.CullingInfo,
                    RootParent = node.TileSetInfoProvider.LoaderConfig.RootParent,
                };
                try
                {
                    var loader = new TileSetHierarchyLoader(config);
                    await loader.ReadJsonAsync(hierarchy, node.transform, node.TileSetInfoProvider.LoaderConfig.CullingInfo.cullCollider, token);
                    Debug.Log($"ReadJson at {trans.name} success.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"ReadJson at {trans.name} failed. e: " + e);
                }
            }

            // 子ノードの処理をタスクとして追加（再帰呼び出しの代わり）
            foreach (Transform child in trans)
            {
                var childNode = child.GetComponent<TileSetNodeComponent>();
                if (childNode != null && childNode.gameObject.activeInHierarchy)
                {
                    if (maxLevels > 1 && maxNodes > 0)
                    {
                        float priority = 0;
                        JsonLoadScheduler.Instance.AddLoadSubTreeTask(
                            $"SubTree-{child.name}",
                            hierarchy,
                            child,
                            maxLevels - 1,
                            maxNodes,
                            token,
                            priority
                        );
                        maxNodes--;
                        if (maxNodes <= 0)
                        {
                            break;
                        }
                    }
                }
            }

            return maxNodes;
        }
    }

    private static JsonLoadScheduler instance;
    public static JsonLoadScheduler Instance => instance;

    private GeoTile.TaskScheduler taskScheduler;
    
    // Configure concurrency limit for JSON loading (lower than model loading to avoid overwhelming servers)
    private const int JsonConcurrencyLimit = 10;

    public int RemainingTasksCount => taskScheduler?.RemainingTasksCount ?? 0;

    public static void CreateGameObject()
    {
        var go = new GameObject("JsonLoadScheduler");
        go.AddComponent<JsonLoadScheduler>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        taskScheduler = new GeoTile.TaskScheduler(JsonConcurrencyLimit, "JsonLoadScheduler");
    }

    private void Update()
    {
        taskScheduler?.ProcessTasks();
    }

    private void OnDestroy()
    {
        taskScheduler?.StopAllTasks();
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Add a subtree loading task to the queue
    /// </summary>
    /// <param name="name">Task name for debugging</param>
    /// <param name="hierarchy">TileSetHierarchy instance</param>
    /// <param name="trans">Transform to process</param>
    /// <param name="maxLevels">Maximum levels to process</param>
    /// <param name="maxNodes">Maximum nodes to process</param>
    /// <param name="token">Cancellation token</param>
    /// <param name="priority">Task priority (higher values executed first)</param>
    /// <returns>Task carrier for tracking progress</returns>
    public GeoTile.TaskScheduler.TaskCarrier AddLoadSubTreeTask(
        string name,
        TileSetHierarchy hierarchy,
        Transform trans,
        int maxLevels,
        int maxNodes,
        CancellationToken token,
        float priority = 0)
    {
        var task = new LoadSubTreeTask(hierarchy, trans, maxLevels, maxNodes);
        return taskScheduler.AddTask(name, token, priority, task);
    }

    /// <summary>
    /// Stop all queued and running JSON loading tasks
    /// </summary>
    public void StopAllTasks()
    {
        taskScheduler?.StopAllTasks();
    }
}