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
        /// ノード数制限とカウンター情報
        /// </summary>
        public int MaxNodeCount { get; private set; } = 10000; // デフォルト値
        public int CurrentNodeCount { get; private set; } = 0;

        /// <summary>
        /// MaxNodeCountを設定
        /// </summary>
        public void SetMaxNodeCount(int maxNodes)
        {
            MaxNodeCount = maxNodes;
        }

        /// <summary>
        /// ノードカウントを増加させる
        /// </summary>
        /// <returns>増加後のカウント。maxNodesを超えた場合は-1を返す</returns>
        public int IncrementNodeCount()
        {
            if (CurrentNodeCount >= MaxNodeCount)
            {
                return -1; // 制限に達している
            }
            CurrentNodeCount++;
            return CurrentNodeCount;
        }

        /// <summary>
        /// ノードカウントをリセット
        /// </summary>
        public void ResetNodeCount()
        {
            CurrentNodeCount = 0;
        }

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
            CreateModelLoadScheduler();
            CreateJsonLoadScheduler();
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
        /// ただし、このメソッドは ModelLoadScheduler にタスクを登録して終了する。
        /// あとは ModelLoadScheduler が実際のロード処理を行う。
        /// </summary>
        /// <param name="maxLevels"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async UniTask Load3DModels(int maxLevels, CancellationToken token)
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
                AddLoad3DModelTaskForNode(node, token);
            }

            foreach (Transform child in trans)
            {
                await Load3DModelsRecursive(child, maxLevels - 1, token);
            }
        }

        /// <summary>
        /// ModelLoadScheduler用タスク
        /// </summary>
        public class ModelLoadTask : GeoTile.TaskScheduler.Task
        {
            TileSetNodeComponent node;

            public ModelLoadTask(TileSetNodeComponent node)
            {
                this.node = node;
            }

            public async UniTask<(bool result, object artifact)> Do(CancellationToken token)
            {
                var result = await node.LoadModel(token);
                return (result, null);
            }
        }

        private void AddLoad3DModelTaskForNode(TileSetNodeComponent node, CancellationToken token)
        {
            if (node.GetContentExtension() != ".b3dm" && node.GetContentExtension() != ".glb")
            {
                return;
            }

            // refine が REPLACEの場合、親ノードのモデルは重複して邪魔になるのでロードしない。
            if (node.TileSetNode.refine == "REPLACE" && node.HasActiveChildNode())
            {
                Debug.Log($"Skipped loading {node.gameObject.name} because there's children.");
                return;
            }

            // ノードの範囲が大きすぎなければモデルをロードする
            if (node.GetBoundingSphere().radiusMeters >=
                node.TileSetInfoProvider.LoaderConfig?.CullingInfo.cullingRadiusMeters * 50)
            {
                Debug.Log($"Skipped loading {node.gameObject.name} because this tile's bounding sphere radius is {node.GetBoundingSphere().radiusMeters / 1000.0:F1}km");
                return;
            }

            // 既にあったらロードしない
            if (node.IsModelLoaded())
            {
                return;
            }

            // ModelLoadScheduler にタスクを登録して終了！

            // 中心からの距離に基づいて優先度を計算
            float priority = CalculateNodePriority(node);
            ModelLoadScheduler.Instance.AddTask(node.name, token, priority, new ModelLoadTask(node));
        }

        /// <summary>
        /// ノードの優先度を計算する（中心からの距離に基づく）
        /// 距離が近いほど高い優先度（大きな値）を返す
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <returns>優先度（距離が近いほど大きな値）</returns>
        private float CalculateNodePriority(TileSetNodeComponent node)
        {
            if (node.NodeBasePos == null || node.NodeBasePos.Length < 3)
            {
                return 0.0f; // NodeBasePosが無効な場合は最低優先度
            }

            // NodeBasePosをVectorD3に変換（ECEF座標）
            var nodePosition = new VectorD3(node.NodeBasePos[0], node.NodeBasePos[1], node.NodeBasePos[2]);
            
            // 中心座標との距離を計算
            var centerPosition = ModelCenterEcefCoordinate;
            var distance = (nodePosition - centerPosition).magnitude;
            
            // 距離に基づく優先度を計算（近いほど高い優先度）
            // 最大距離を100kmと仮定して正規化
            const double maxDistance = 100000.0; // 100km in meters
            var normalizedDistance = Math.Min(distance / maxDistance, 1.0);
            
            // 1.0から正規化距離を引いて、近いほど高い値になるようにする
            return (float)(1.0 - normalizedDistance);
        }

        /// <summary>
        /// 全体的にサブツリーをロードする 
        /// </summary>
        /// <param name="maxLevels"></param>
        /// <returns></returns>
        public async UniTask LoadSubTrees(int maxLevels, int maxNodes, CancellationToken token)
        {
            // ノードカウンターをリセットしてmaxNodesを設定
            ResetNodeCount();
            SetMaxNodeCount(maxNodes);
            
            // 最初のタスクを追加
            JsonLoadScheduler.Instance.AddLoadSubTreeTask(
                "RootSubTree",
                this,
                transform,
                maxLevels,
                token,
                0
            );

            // すべてのタスクが完了するまで待つ
            await UniTask.WaitWhile(() => JsonLoadScheduler.Instance.RemainingTasksCount > 0, cancellationToken: token);
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

        private void CreateModelLoadScheduler()
        {
            if (ModelLoadScheduler.Instance == null)
            {
                ModelLoadScheduler.CreateGameObject();
            }
        }

        private void CreateJsonLoadScheduler()
        {
            if (JsonLoadScheduler.Instance == null)
            {
                JsonLoadScheduler.CreateGameObject();
            }
        }
    }
}