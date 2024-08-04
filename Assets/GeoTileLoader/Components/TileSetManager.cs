using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GeoTile
{
    /*
     * 3Dタイルのjsonをパースして、GameObjectのHierarchyを作成する起点となるコンポーネント
     * 実際の処理はTileSetHierarchyLoaderが行う
     * 現状では、cullingInfo の緯度経度高度の場所をUnity座標系原点にあわせるようにロードされる。(GLTFロード時）
     */
    public class TileSetManager : MonoBehaviour
    {
        [SerializeField]
        public string tileSetJsonUrl;
        
        [SerializeField]
        public string tileSetTitle = "";

        [SerializeField]
        private GeoTileLoaderSettings settings;

        [SerializeField]
        public Transform parent;

        public CullingInfo cullingInfo = new CullingInfo()
        {
            cullingLatDegree = 35.6581,
            cullingLonDegree = 139.7017,
            cullingRadiusMeters = 1000,
        };
        
        /// <summary>
        /// TileSetのルートJSONファイルを読み込む（コールバック版）
        /// ネストされた下位のjsonファイルまでは読み込まない。
        /// </summary>
        /// <param name="parentTrans"></param>
        /// <param name="onResult"></param>
        public void ReadJson(Transform parentTrans, Action<Exception> onResult)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await ReadJsonAsync(parentTrans, this.GetCancellationTokenOnDestroy());
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    onResult?.Invoke(e);
                }
            });
        }
        
        /// <summary>
        /// TileSetのルートJSONファイルを読み込む（async版）
        /// ネストされた下位のjsonファイルまでは読み込まない。
        /// </summary>
        /// <param name="parentTrans"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async UniTask<Transform> ReadJsonAsync(Transform parentTrans, CancellationToken token)
        {
            var loader = new TileSetHierarchyLoader(new TileSetHierarchyLoaderConfig()
            {
                TileSetName = tileSetTitle,
                TileSetJsonUrl = tileSetJsonUrl,
                GoogleMapTileApiKey = settings?.GoogleApiKeyFor3dMapTiles,
                RootParent = parent,
                CullingInfo = cullingInfo,
            });
            return await loader.ReadJson(true, parentTrans, token);
        }
    }
}