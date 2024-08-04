//#define USE_UNIGLTF

using GLTFast;
using GLTFast.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

#if USE_UNIGLTF
using UniGLTF;
using VRMShaders;
#endif

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

        [field: SerializeField]
        /// <summary>
        /// Googleの3D TilesではjsonURLのsessionパラメータを引き継ぐ必要がある。
        /// しかしこれは3D Tilesの仕様外。
        /// https://github.com/CesiumGS/3d-tiles/issues/746
        /// </summary>
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

        //[field: SerializeField]
        //public VectorD3 ModelOffset;

        private ITileSetInfoProvider tileSetInfoProvider;

        public string GetContentExtension()
        {
            string fileName = TileSetNode?.content?.contentUrlFileName;
            return string.IsNullOrEmpty(fileName) ? null : Path.GetExtension(fileName.ToLower());
        }

        /// <summary>
        /// タイルセットの情報を教えてくれる
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

#if !USE_UNIGLTF
            // GLTFastでロード
            LoadGltfWithGLTFast(gltfData, component.transform, new Uri(component.BaseJsonUrl), instance =>
            {
                Vector3 pos = Vector3.zero;
                Quaternion rot = Quaternion.identity;
                Debug.Log("BoundingBoxType: " + component.BoundingBoxType);
                VectorD3 modelOffset = - component.TileSetInfoProvider.ModelCenterEcefCoordinate;
                if (component.BoundingBoxType == BoundingBoxType.Box)
                {
                    if (center != null)
                    {
                        pos = new Vector3(
                            (float)(center[0] + modelOffset.x),
                            (float)(center[1] + modelOffset.y),
                            (float)(center[2] + modelOffset.z)
                        );
                    }
                    else
                    {
                        pos = new Vector3(
                            (float)(- component.GlobalBasePos[0] + modelOffset.x),
                            (float)(- component.GlobalBasePos[1] + modelOffset.y),
                            (float)(- component.GlobalBasePos[2] + modelOffset.z)
                        );
                    }
                }
                else if (component.BoundingBoxType == BoundingBoxType.Region)
                {
                    if (component.RootCenterLatLng == null)
                    {
                        Debug.LogError("component.RootCenterLatLng is null");
                    }
                    var RootBasePos = CoordinateUtil.Vector3FromLatLngAlt(component.RootCenterLatLng);
                    Debug.Log("RootBasePos: " + string.Join(", ", RootBasePos));

                    if (center != null)
                    {
                        // cullingCenterの緯度経度を地図中央にするテスト
                        pos = new Vector3(
                            (float)(center[0] + modelOffset.x),
                            (float)(center[1] + modelOffset.y),
                            (float)(center[2] + modelOffset.z)
                        );
                    }
                    else
                    {
                        pos = new Vector3(
                            (float)(- RootBasePos[0] + modelOffset.x),
                            (float)(- RootBasePos[1] + modelOffset.y),
                            (float)(- RootBasePos[2] + modelOffset.z)
                        );
                    }
                }
                instance.transform.localPosition = pos;
                instance.transform.localRotation = Quaternion.Euler(90, 180, 0); // GLTFast conversion (Y: 180) * 3DTiles conversion (X: -90)
            });
#endif // !USE_UNIGLTF

#if USE_UNIGLTF
            // UniGLTFでロード
            LoadGltfWithUniGLTF(gltfData, component.transform, instance =>
            {
                var pos = new Vector3(
                    (float)(center[0] - component.GlobalBasePos[0]),
                    (float)(center[1] - component.GlobalBasePos[1]),
                    (float)(center[2] - component.GlobalBasePos[2])
                    );
                instance.transform.localPosition = pos;
                instance.transform.localRotation = Quaternion.Euler(-90, 0, 0); // 3DTiles conversion (X: -90)
            });
#endif // USE_UNIGLTF
        }

#if USE_UNIGLTF
        async void LoadGltfWithUniGLTF(byte[] data, Transform parent, Action<GameObject> onResult)
        {
            var filePath = "sample.glb";
            File.WriteAllBytes(filePath, data);
            Debug.Log($"GLTF written to <{filePath}>");

            var gltfData = new GlbBinaryParser(data, "GLTF").Parse();
            using (var loader = new UniGLTF.ImporterContext(gltfData))
            {
                var instance = await loader.LoadAsync(new ImmediateCaller());
                instance.EnableUpdateWhenOffscreen();
                instance.ShowMeshes();
                instance.transform.SetParent(parent, false);
                onResult?.Invoke(instance.gameObject);
            }
        }
#endif // USE_UNIGLTF

        /// <summary>
        /// GLTFast と PlateauのGLTFの相性に問題があり、実質ロードできない！！！
        /// 
        /// CESIUM_RTC がRequired Extensionになっているが、GLTFastがサポートしておらず、
        /// そもそもGLTF2.0の仕様にCESIUM_RTCは存在しない！（1.0にはある。。。）
        /// </summary>
        /// <param name="data"></param>
        /// <param name="parent"></param>
        /// <param name="baseUri"></param>
        async void LoadGltfWithGLTFast(byte[] data, Transform parent, Uri baseUri, Action<GameObject> onResult)
        {
            GltFastInitializeDeferAgent();

            var filePath = "sample.glb";
            File.WriteAllBytes(filePath, data);
            Debug.Log($"GLTF written to <{filePath}>");

            // 強引な手法
            // CESIUM_RTC拡張がないといわれてロードできない問題の対策
            // gltf(json形式)内のキー名 extensionsRequired をいじってしまうことで無効化する。
            //
            var result = SearchBinaryBlock.IndexOfSequence(data, Encoding.ASCII.GetBytes("extensionsRequired"), 0);
            if (result.Count > 0)
            {
                data[result[0]] = (byte)'X';
            }

            var gltf = new GltfImport(logger: new UnityLogger());
            try
            {
                bool success = await gltf.LoadGltfBinary(data, baseUri);
                if (!success)
                {
                    Debug.LogError("load gltf failed.");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("load gltf error. " + e);
                return;
            }

            var go = new GameObject("GLTF");
            go.transform.SetParent(parent, false);
            {
                bool success = gltf.InstantiateMainScene(go.transform);
                Debug.Log("load gltf result: " + success);
            }

            onResult?.Invoke(go);
        }

        void GltFastInitializeDeferAgent()
        {
            if (Application.isEditor)
            {
                GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());
            }
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

        public class UnityLogger : ICodeLogger
        {

            public void Log(LogType logType, LogCode code, params string[] messages)
            {
                switch (logType)
                {
                    case LogType.Log:
                        Debug.Log("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    case LogType.Error:
                        Debug.LogError("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    case LogType.Warning:
                        Debug.LogWarning("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    case LogType.Exception:
                        Debug.LogError("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    default:
                        Debug.Log("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                }
            }

            public void Error(LogCode code, params string[] messages)
            {
                Debug.LogError(string.Join("\n, ", messages));
            }

            public void Error(string message)
            {
                Debug.LogError(message);
            }

            public void Info(LogCode code, params string[] messages)
            {
                Debug.Log(string.Join("\n, ", messages));
            }

            public void Info(string message)
            {
                Debug.Log(message);
            }

            public void Warning(LogCode code, params string[] messages)
            {
                Debug.LogWarning(string.Join("\n, ", messages));
            }

            public void Warning(string message)
            {
                Debug.LogWarning(message);
            }
        }
    }
}
