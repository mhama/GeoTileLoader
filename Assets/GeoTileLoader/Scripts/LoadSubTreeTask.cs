using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// Task implementation for subtree loading operations (wraps original LoadSubTreesRecursive logic)
    /// </summary>
    public class LoadSubTreeTask : TaskScheduler.Task
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
}