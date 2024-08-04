using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoTile
{
    public enum BoundingBoxType
    {
        Box,
        Region,
        Unknown,
    }

    public class LatLngAlt
    {
        public double LatRad { get; set; }
        public double LngRad { get; set; }
        public double AltitudeMeter { get; set; }

        public LatLngAlt() { }

        public LatLngAlt(double latRad, double lngRad, double altMeters)
        {
            LatRad = latRad;
            LngRad = lngRad;
            AltitudeMeter = altMeters;
        }
    }

    public class TileSetNodeComponent : MonoBehaviour
    {
        [field: SerializeField]
        public TileSetNode TileSetNode { get; set; }
        [field: SerializeField]
        public string BaseJsonUrl { get; set; }

        /// <summary>
        /// Googleの3D TilesではjsonURLのsessionパラメータを引き継ぐ必要がある。
        /// しかしこれは3D Tilesの仕様外。
        /// https://github.com/CesiumGS/3d-tiles/issues/746
        /// </summary>
        [field: SerializeField]
        public string GoogleSessionId { get; set; }

        [field: SerializeField]
        public BoundingBoxType BoundingBoxType { get; set; } = BoundingBoxType.Unknown;

        [field: SerializeField]
        public double[] GlobalBasePos { get; set; } = null;
        [field: SerializeField]
        public double[] NodeBasePos { get; set; } = null;
        [field: SerializeField]
        public double[] NodeContentBasePos { get; set; } = null;

        [field: SerializeField]
        public LatLngAlt NodeCenterLatLng { get; set; } = null;

        [field: SerializeField]
        public LatLngAlt RootCenterLatLng { get; set; } = null;


        // GLTFのCESIUM_RTC extension に入っているデータ
        [field: SerializeField]
        public double[] gltfCenter;

        public CullingInfo CullingInfo => TileSetInfoProvider.CullingInfo;

        private ITileSetInfoProvider tileSetInfoProvider;
        
        /// <summary>
        /// GLTFを実体化する手法を指定
        /// </summary>
        private IGltfInstantiator gltfInstantiator;

        public string GetContentExtension()
        {
            string fileName = TileSetNode?.content?.contentUrlFileName;
            return string.IsNullOrEmpty(fileName) ? null : Path.GetExtension(fileName.ToLower());
        }

        private void Awake()
        {
#if GEOTILE_USE_UNIGLTF
            gltfInstantiator = new GltfInstantiatorWithUniGltf();
#else
            gltfInstantiator = new GltfInstantiatorWithGltFast();
#endif
        }

        /// <summary>
        /// タイルセットの情報を教えてくれるインタフェース
        /// </summary>
        public ITileSetInfoProvider TileSetInfoProvider
        {
            get
            {
                if (tileSetInfoProvider != null)
                {
                    return tileSetInfoProvider;
                }

                tileSetInfoProvider = transform.GetComponentInParent<TileSetHierarchy>();
                return tileSetInfoProvider;
            }
            set
            {
                tileSetInfoProvider = value;
            }
        }

        /// <summary>
        /// 複数段GLTFモデル生成メソッド
        /// 主にこのコンポーネントのインスペクタから利用する。
        /// </summary>
        /// <param name="depth"></param>
        /// <param name="depthLimit"></param>
        /// <returns></returns>
        public IEnumerator LoadModelRecursive(int depth, int depthLimit)
        {
            if (depth >= depthLimit)
            {
                Debug.Log($"depth limit exceeded. GameObject: {transform.gameObject.name}");
                yield break;
            }
            Debug.Log($"LoadModelRecursive GameObject: {transform.gameObject.name} depth: {depth} depthLimit: {depthLimit}");
            if (TileSetNode == null || TileSetNode.content == null)
            {
                Debug.LogWarning($"TileSet or content is null for GameObject: {transform.gameObject.name}");
                yield break;
            }
            if (Path.GetExtension(TileSetNode.content.contentUrlFileName) != ".json")
            {
                yield return LoadModelIfNotLoaded();
            }
            foreach (Transform child in transform)
            {
                var tileSetNode = child.GetComponent<TileSetNodeComponent>();
                if (tileSetNode)
                {
                    yield return tileSetNode.LoadModelRecursive(depth + 1, depthLimit);
                }
            }
        }

        public bool IsModelLoaded()
        {
            return transform.Find("GLTF") != null;
        }

        IEnumerator LoadModelIfNotLoaded()
        {
            if (IsModelLoaded())
            {
                yield break;
            }
            yield return LoadModel();
        }

        /// <summary>
        /// このノードの3Dモデルを読み込み、instantiateする
        /// 直接GLBファイルを読める場合と、B3DM形式で包まれている場合がある
        /// </summary>
        /// <returns></returns>
        public IEnumerator LoadModel()
        {
            string session = null;
            var queries = (new Uri(BaseJsonUrl)).Query.Replace("?","").Split('&').Select(s => s.Split('='));
            var sessionPair = queries.Where(p => p[0] == "session");
            if (sessionPair.Count() > 0)
            {
                session = sessionPair.ToList()[0][1];
            }
            Debug.Log("session :" + session);

            var modelRelativeUrl = TileSetNode?.content?.Url;
            if (string.IsNullOrEmpty(modelRelativeUrl))
            {
                Debug.LogError("model relative url is null");
                yield break;
            }
            if (!string.IsNullOrEmpty(TileSetInfoProvider.LoaderConfig?.GoogleMapTileApiKey))
            {
                modelRelativeUrl += (modelRelativeUrl.Contains("?") ? "&" : "?") + "key=" + TileSetInfoProvider.LoaderConfig.GoogleMapTileApiKey + "&session=" + session;
            }
            Debug.Log("modelRelativeUrl: " + modelRelativeUrl);
            var modelUri = new Uri(new Uri(BaseJsonUrl), modelRelativeUrl);
            var ModelAbsoluteUri = modelUri.AbsoluteUri;

            var ext = Path.GetExtension(modelUri.PathAndQuery.Split('?')[0]);
            Debug.Log("ext: " + ext);
            if (ext?.ToLower() == ".glb")
            {
                yield return LoadGLB(ModelAbsoluteUri, OnGLTFReceived);
                yield break;
            }

            yield return LoadB3DM(ModelAbsoluteUri, OnGLTFReceived);
        }

        IEnumerator LoadGLB(string url, Action<byte[], double[]> onGLTFReceived)
        {
            Debug.Log("loading GLB: " + url);
            using (var req = new UnityWebRequest(url))
            using (var buf = new DownloadHandlerBuffer())
            {
                req.downloadHandler = buf;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("load error: " + req.error);
                    yield break;
                }
                Debug.Log("loading success.");
                double[] center = {0,0,0};
                onGLTFReceived(buf.data, center);
            }
        }

        IEnumerator LoadB3DM(string url, Action<byte[], double[]> onGLTFReceived)
        {
            if (!string.IsNullOrEmpty(TileSetInfoProvider.LoaderConfig?.GoogleMapTileApiKey))
            {
                url += (url.Contains("?") ? "&" : "?") + "key=" + TileSetInfoProvider.LoaderConfig.GoogleMapTileApiKey;
            }
            Debug.Log("loading B3DM: " + url);
            using (var req = new UnityWebRequest(url))
            using (var buf = new DownloadHandlerBuffer())
            {
                req.downloadHandler = buf;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("load error: " + req.error);
                    yield break;
                }
                Debug.Log("loading success.");
                OnB3DMResult(buf.data, onGLTFReceived);
            }
        }

        void OnB3DMResult(byte[] data, Action<byte[], double[]> onGLTFReceived)
        {
            var b3dm = new B3DM();
            b3dm.Read(data);

            /* debug output
            // save feature table json
            {
                var jsonFilePath = "b3dm_feature.json";
                File.WriteAllText(jsonFilePath, b3dm.FeatureJsonText);
                Debug.Log($"feature json written to <{jsonFilePath}>");
            }

            // save batch table json
            {
                var jsonFilePath = "b3dm_batch.json";
                File.WriteAllText(jsonFilePath, b3dm.BatchJsonText);
                Debug.Log($"batch json written to <{jsonFilePath}>");
            }

            // save gltf json
            {
                var jsonFilePath = "b3dm_gltf.json";
                File.WriteAllText(jsonFilePath, b3dm.GltfJsonText);
                Debug.Log($"gltf json written to <{jsonFilePath}>");
            }
            */

            var metadata = JsonConvert.DeserializeObject<GltfMetadata>(b3dm.GltfJsonText);
            double[] center = null;
            if (metadata.extensions.CESIUM_RTC != null)
            {
                Debug.Log("metadata extensions.CESIUM_RTC.center: " + string.Join(", ", metadata.extensions?.CESIUM_RTC.center));
                center = CoordinateUtil.TileCoordToUnity(metadata.extensions.CESIUM_RTC.center);
            }
            onGLTFReceived(b3dm.GltfData.ToArray(), center);
        }

        void OnGLTFReceived(byte[] gltfData, double[] center)
        {
            var component = this;
            this.gltfCenter = center;

            Debug.Log("OnGLTFReceived: gltfData len: " + gltfData.Length);

            gltfInstantiator.Instantiate(gltfData, center, this);
        }

        /// <summary>
        /// 衝突判定用の球体を返す
        /// </summary>
        /// <returns></returns>
        public SphereCoordsUnity GetBoundingSphere()
        {
            switch (BoundingBoxType)
            {
                case BoundingBoxType.Box:
                    return CoordinateUtil.GetBoundingSphereFromBoxCoords(TileSetNode.boundingVolume.box);
                case BoundingBoxType.Region:
                    return CoordinateUtil.GetBoundingSphereFromRegionCoords(TileSetNode.boundingVolume.region);
                default:
                    return null;
            }
        }
    }
}
