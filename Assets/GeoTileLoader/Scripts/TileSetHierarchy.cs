using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GeoTile;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// タイルセット全体の情報を提供する
    /// </summary>
    public interface ITileSetInfoProvider
    {
        public CullingInfo CullingInfo { get; }
        
        /// <summary>
        /// モデル（リアルサイズ地球）のどこをUnity座標系の原点とするかの情報。
        /// Unityはfloat精度の問題のため地球サイズのものをそのまま表現できない。
        /// 表現したい部分の近くをUnity座標系原点にしなくてはならない
        /// 座標はECEF座標をUnity座標系に変換したもの (Zの正負を反転）で表現する。
        /// </summary>
        public VectorD3 ModelCenterEcefCoordinate { get; }

        /// <summary>
        /// ローダーを提供
        /// サブノードをロードする場合用
        /// </summary>
        public TileSetHierarchyLoaderConfig LoaderConfig { get; }
    }

    /// <summary>
    /// タイルセットの最上位につけて、タイルセット全体の操作や情報を扱う
    /// </summary>
    public class TileSetHierarchy : MonoBehaviour, ITileSetInfoProvider
    {
        [SerializeField]
        public TileSetNodeComponent RootNode { get; set; }

        public string TileSetName { get; set; }

        [field: SerializeField]
        public CullingInfo CullingInfo { get; set; }

        [field: SerializeField]
        public VectorD3 ModelCenterEcefCoordinate { get; set; }

        public TileSetHierarchyLoaderConfig LoaderConfig { get; set; }

        /// <summary>
        /// 著作権表示文字列
        /// </summary>
        public string CopyrightAttributionText { get; private set; }

        /// <summary>
        /// 著作権表示文字列の更新を通知するEvent
        /// </summary>
        public event Action<string> OnCopyrightAttributionTextChanged;

        private readonly TileCopyrightCollector copyrightCollector = new TileCopyrightCollector();

        private void Start()
        {
            StartCoroutine(LoopCollectCopyright());
        }

        /// <summary>
        /// 一定時間おきに著作権表示を収集する
        /// </summary>
        private IEnumerator LoopCollectCopyright()
        {
            while (true)
            {
                yield return new WaitForSeconds(1.0f);
                CollectCopyrightAttribution();
            }
        }

        /// <summary>
        /// 全体的にGLTFをロードする
        /// </summary>
        /// <param name="maxLevels"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async UniTask  Load3DModels(int maxLevels, CancellationToken token)
        {
            await Load3DModelsRecursive(transform, maxLevels, token);
        }

        private async UniTask Load3DModelsRecursive(Transform trans, int maxLevels, CancellationToken token)
        {
            if (maxLevels <= 0)
            {
                return;
            }
            var node = trans.GetComponent<TileSetNodeComponent>();
            if (node != null)
            {
                await Load3DModelForNode(node, token);
            }

            foreach (Transform child in trans)
            {
                await Load3DModelsRecursive(child, maxLevels - 1, token);
            }
        }

        private async UniTask Load3DModelForNode(TileSetNodeComponent node, CancellationToken token)
        {
            if (node.GetContentExtension() != ".b3dm" && node.GetContentExtension() != ".glb")
            {
                return;
            }

            // refine が REPLACEの場合、親ノードのモデルは重複して邪魔になるのでロードしない。
            if (node.TileSetNode.refine == "REPLACE" && node.HasChildNode())
            {
                Debug.Log($"Skipped loading {node.gameObject.name} because there's children.");
                return;
            }

            // ノードの範囲が大きすぎなければモデルをロードする
            if (node.GetBoundingSphere().radiusMeters >=
                node.TileSetInfoProvider.LoaderConfig?.CullingInfo.cullingRadiusMeters * 50)
            {
                Debug.Log($"Skipped loading {node.gameObject.name} because this tile's bounding sphere radius is {node.GetBoundingSphere().radiusMeters / 1000.0:F1}km");
            }

            // 既にあったらロードしない
            if (node.IsModelLoaded())
            {
                return;
            }

            await node.LoadModel();
            await UniTask.Delay(20, cancellationToken: token);
        }

        /// <summary>
        /// 全体的にサブツリーをロードする 
        /// </summary>
        /// <param name="maxLevels"></param>
        /// <returns></returns>
        public async UniTask LoadSubTrees(int maxLevels, int maxNodes, CancellationToken token)
        {
            await LoadSubTreesRecursive(transform, maxLevels, maxNodes, token);
        }

        /// <summary>
        /// 再帰的にサブツリーをロードする
        /// return maxNodes
        /// </summary>
        /// <param name="trans"></param>
        /// <param name="maxLevels"></param>
        /// <param name="maxNodes"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async UniTask<int> LoadSubTreesRecursive(Transform trans, int maxLevels, int maxNodes, CancellationToken token)
        {
            maxNodes--;
            if (maxNodes <= 0)
            {
                Debug.LogWarning("maxNodes reached.");
                return maxNodes;
            }

            {
                // サブツリーが存在して、既にロードされていなければロードする
                var node = trans.GetComponent<TileSetNodeComponent>();
                if (node != null
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

                    var loader = new TileSetHierarchyLoader(new TileSetHierarchyLoaderConfig()
                    {
                        TileSetJsonUrl = newUri.ToString(),
                        TileSetName = "",
                        GoogleSessionId = sessionId,
                        GoogleMapTileApiKey = node.TileSetInfoProvider.LoaderConfig.GoogleMapTileApiKey,
                        CullingInfo = node.CullingInfo,
                        RootParent = node.TileSetInfoProvider.LoaderConfig.RootParent,
                    });
                    try
                    {
                        await loader.ReadJson(this, node.transform, node.TileSetInfoProvider.LoaderConfig.CullingInfo.cullCollider, token);
                        Debug.Log($"ReadJson at {trans.name} success.");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"ReadJson at {trans.name} failed. e: " + e);
                    }
                }
            }

            foreach (Transform child in trans)
            {
                var node = child.GetComponent<TileSetNodeComponent>();
                if (node != null)
                {
                    maxNodes = await LoadSubTreesRecursive(child, maxLevels - 1, maxNodes, token);
                    if (maxNodes <= 0)
                    {
                        break;
                    }
                }
            }

            return maxNodes;
        }

        /// <summary>
        /// ヒエラルキー内に存在するタイルの著作権表示を収集し、CopyrightAttributionText に保存する。
        /// 変化時のイベントも投げる。
        /// </summary>
        private void CollectCopyrightAttribution()
        {
            if (RootNode == null)
            {
                return;
            }
            var text = copyrightCollector.Collect(RootNode);
            bool changed = CopyrightAttributionText != text;
            CopyrightAttributionText = text;
            if (changed)
            {
                OnCopyrightAttributionTextChanged?.Invoke(CopyrightAttributionText);
            }
        }
    }
}