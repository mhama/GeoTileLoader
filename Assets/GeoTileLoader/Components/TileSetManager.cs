using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

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
        private string tileSetJsonUrl;

        public string TileSetJsonUrl
        {
            get => tileSetJsonUrl;
            set => tileSetJsonUrl = value;
        }
        
        [SerializeField]
        private string tileSetTitle = "";

        public string TileSetTitle
        {
            get => tileSetTitle;
            set => tileSetTitle = value;
        }
        
        [SerializeField]
        private GeoTileLoaderSettings settings;

        public GeoTileLoaderSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        
        [FormerlySerializedAs("parent")] [SerializeField]
        private Transform tileSetParent;

        public Transform TileSetParent
        {
            get => tileSetParent;
            set => tileSetParent = value;
        }

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
                RootParent = tileSetParent,
                CullingInfo = cullingInfo,
            });
            return await loader.ReadJson(null, parentTrans, cullingInfo.cullCollider, token);
        }
    }
}