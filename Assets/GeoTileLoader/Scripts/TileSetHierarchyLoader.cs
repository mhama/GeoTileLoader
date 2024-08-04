using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace GeoTile
{
    [Serializable]
    public class CullingInfo
    {
        public double cullingLatDegree;
        public double cullingLonDegree;
        public double cullingRadiusMeters;
    }

    public class TileSetHierarchyLoaderConfig
    {
        public string TileSetName { get; set; }
        public string TileSetJsonUrl { get; set; }
        
        public Transform RootParent { get; set; }
        
        public CullingInfo CullingInfo { get; set; }
        
        public string GoogleMapTileApiKey { get; set; }
        public string GoogleSessionId { get; set; }
    }

    /// <summary>
    /// 3DTileのjsonをロード・解釈し、GameObjectのHierarchyを生成する
    /// 各ノードのGameObjectにはTileSetNodeComponentを生成する
    /// ルートGameObjectにはTileSetHierarchyコンポーネントを生成する
    /// </summary>
    public class TileSetHierarchyLoader
    {
        private TileSetHierarchyLoaderConfig config;

        public TileSetHierarchyLoader(TileSetHierarchyLoaderConfig config)
        {
            this.config = config;
        }
        
        // Start is called before the first frame update
        /// <summary>
        /// 3DTileSetのjsonをロードする
        /// </summary>
        /// <param name="isRoot">タイルセットトップのjsonかどうか</param>
        /// <param name="parentTrans">サブツリーを読み込む場合の親Transform</param>
        /// <param name="onResult"></param>
        /// <param name="token"></param>
        /// <returns>生成されたルートGameObject (isRoot = trueの場合), isRoot = false の場合は parentTrans引数の値を返す。エラーの場合はnullを返す</returns>
        public async UniTask<Transform> ReadJson(bool isRoot, Transform parentTrans, CancellationToken token)
        {
            Transform trans = null;
            TileSetHierarchy hierarchy = null;
            if (isRoot)
            {
                var go = new GameObject("Hierarchy: " + config.TileSetName);
                if (config.RootParent != null)
                {
                    go.transform.SetParent(config.RootParent);
                }
                hierarchy = go.AddComponent<TileSetHierarchy>();
                hierarchy.TileSetName = config.TileSetName;
                hierarchy.CullingInfo = config.CullingInfo;
                var centerLatLngAlt = new LatLngAlt(CoordinateUtil.DegreeToRadian(config.CullingInfo.cullingLatDegree), CoordinateUtil.DegreeToRadian(config.CullingInfo.cullingLonDegree), 0);
                hierarchy.ModelCenterEcefCoordinate = new VectorD3(CoordinateUtil.Vector3FromLatLngAlt(centerLatLngAlt));
                hierarchy.LoaderConfig = config;
                trans = go.transform;

                // モデルの傾きが地球全体座標のリアルな傾きになっているため、戻すためにルートGameObjectを傾ける
                var rotation = Quaternion.AngleAxis(90, new Vector3(0, 0, 1)) 
                               * Quaternion.AngleAxis((float) - config.CullingInfo.cullingLatDegree, new Vector3(0, 1, 0)) 
                               * Quaternion.AngleAxis((float) - config.CullingInfo.cullingLonDegree, new Vector3(0, 0, 1));
                go.transform.localRotation = rotation;
            }
            else
            {
                if (parentTrans == null)
                {
                    Debug.LogError("isRoot is false but parent == null.");
                    return null;
                }
                trans = parentTrans;
            }

            var url = config.TileSetJsonUrl;
            if (!string.IsNullOrEmpty(config.GoogleMapTileApiKey))
            {
                url += (url.Contains("?") ? "&" : "?") 
                    + "key=" + config.GoogleMapTileApiKey;
                if (!string.IsNullOrEmpty(config.GoogleSessionId))
                {
                    url += "&session=" + config.GoogleSessionId;
                }
            }

            await ReadJsonFromUrlCoroutine(trans, url, hierarchy, token);
            Debug.Log("ReadJsonFromUrlCoroutine finished!");
            if (trans.childCount > 0)
            {
                var child = trans.GetChild(0);
                var culling = child.gameObject.AddComponent<SphereCulling>();
                var centerLatLngAlt = new LatLngAlt(CoordinateUtil.DegreeToRadian(config.CullingInfo.cullingLatDegree), CoordinateUtil.DegreeToRadian(config.CullingInfo.cullingLonDegree), 0);
                culling.cullSphereUnity = new SphereCoordsUnity()
                {
                    center = new VectorD3(
                        CoordinateUtil.Vector3FromLatLngAlt(centerLatLngAlt)),
                    radiusMeters = config.CullingInfo.cullingRadiusMeters,
                };
                
                // カリング球体の外にあるノードを刈る
                culling.DoCulling();
            }

            return trans;
        }

        async UniTask ReadJsonFromUrlCoroutine(Transform parent, string url, TileSetHierarchy hierarchy, CancellationToken token)
        {
            Debug.Log("loading: " + url);
            using (var req = new UnityWebRequest(url))
            using (var buf = new DownloadHandlerBuffer())
            {
                req.downloadHandler = buf;
                await req.SendWebRequest().WithCancellation(token);
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("load error: " + req.error);
                    throw new Exception("json load error: " + req.error);
                }
                Debug.Log("loading success.");
                DecodeJson(buf.text, url, hierarchy, parent);
            }
        }

        void DecodeJson(string data, string baseJsonUrl, TileSetHierarchy hierarchy, Transform parent)
        {
            Debug.Log("OnJsonResult length: " + data.Length);

            //var jsonFilePath = "tile.json";
            //File.WriteAllText(jsonFilePath, data);
            //Debug.Log($"json written to <{jsonFilePath}>");

            try
            {
                var settings = new JsonSerializerSettings()
                {
                    MaxDepth = 100,
                };
                var tileSet = JsonConvert.DeserializeObject<TileSet>(data, settings);

                // PLATEAUストリーミング用
                if (tileSet.properties != null && tileSet.properties._x != null && tileSet.properties._y != null)
                {
                    // 緯度経度から傾きを求めて、Unity上で水平になるよう一番上のGameObjectのlocalRotationを補正する。
                    float longitude = (float)((tileSet.properties._x.minimum + tileSet.properties._x.maximum) / 2);
                    float latitude = (float)((tileSet.properties._y.minimum + tileSet.properties._y.maximum) / 2);
                    Debug.Log($"lon, lat : ({longitude}, {latitude})");
                    parent.localRotation = Quaternion.AngleAxis(90, new Vector3(0, 0, 1))
                        * Quaternion.AngleAxis(-latitude, new Vector3(0, 1, 0))
                        * Quaternion.AngleAxis(-longitude, new Vector3(0, 0, 1));
                }

                var traverser = new TileSetTraverser(tileSet);
                traverser.Traverse(parent, (node, go) =>
                {
                    var component = go.AddComponent<TileSetNodeComponent>();
                    component.TileSetNode = node.CloneWithoutChildren();
                    component.BaseJsonUrl = baseJsonUrl;
                    component.GoogleSessionId = config.GoogleSessionId;
                    component.TileSetInfoProvider = hierarchy;

                    if (tileSet.root.boundingVolume.box != null)
                    {
                        component.BoundingBoxType = BoundingBoxType.Box;
                        component.GlobalBasePos = CoordinateUtil.TileCoordToUnity(tileSet.root.boundingVolume.box);
                        component.NodeBasePos = CoordinateUtil.TileCoordToUnity(node.boundingVolume.box);
                        if (node.content?.boundingVolume != null)
                        {
                            component.NodeContentBasePos = CoordinateUtil.TileCoordToUnity(node.content.boundingVolume.box);
                        }
                    }
                    else if(tileSet.root.boundingVolume.region != null)
                    {
                        component.BoundingBoxType = BoundingBoxType.Region;
                        component.RootCenterLatLng = CoordinateUtil.CenterLatLngOfRegion(tileSet.root.boundingVolume.region);
                        if (node.content?.boundingVolume != null)
                        {
                            component.NodeCenterLatLng = CoordinateUtil.CenterLatLngOfRegion(node.content.boundingVolume.region);
                        }
                    }
                });
                return;
            }
            catch (Exception e)
            {
                Debug.LogError("json parse error: " + e);
                throw;
            }
        }
        
    }
}