﻿using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

        [field: SerializeField]
        public List<string> Copyright { get; private set; }


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

        public bool SubTreeExists()
        {
            return GetContentExtension() == ".json";
        }

        public bool SubTreeAlreadyLoaded()
        {
            return SubTreeExists() && transform.Find("0") != null;
        }

        public bool HasActiveChildNode()
        {
            return transform.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy && t.GetComponent<TileSetNodeComponent>() != null).Any();
        }

        private void PrepareGltfInstantiator()
        {
            if (gltfInstantiator != null)
            {
                return;
            }
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
        /// ※注意 タイルの詳細度などを気にせずすべてロードするので、適切な表示を行うためのメソッドではないので注意すること。
        /// 主にこのコンポーネントのインスペクタから利用する。
        /// </summary>
        /// <param name="depth">現在のデプス。最初は0で呼ぶこと。</param>
        /// <param name="depthLimit"></param>
        /// <returns></returns>
        public async UniTask<bool> LoadModelRecursive(int depth, int depthLimit, CancellationToken token)
        {
            if (depth >= depthLimit)
            {
                Debug.Log($"depth limit exceeded. GameObject: {transform.gameObject.name}");
                return false;
            }
            Debug.Log($"LoadModelRecursive GameObject: {transform.gameObject.name} depth: {depth} depthLimit: {depthLimit}");
            if (TileSetNode != null && TileSetNode.content != null)
            {
                if (Path.GetExtension(TileSetNode.content.contentUrlFileName) != ".json")
                {
                    await LoadModelIfNotLoaded(token);
                }
            }
            foreach (Transform child in transform)
            {
                var tileSetNode = child.GetComponent<TileSetNodeComponent>();
                if (tileSetNode)
                {
                    var result = await tileSetNode.LoadModelRecursive(depth + 1, depthLimit, token);
                    if (!result)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool IsModelLoaded()
        {
            return transform.Find("GLTF") != null;
        }

        async UniTask<bool> LoadModelIfNotLoaded(CancellationToken token)
        {
            if (IsModelLoaded())
            {
                return false;
            }
            return await LoadModel(token);
        }

        /// <summary>
        /// このノードの3Dモデルを読み込み、instantiateする
        /// 直接GLBファイルを読める場合と、B3DM形式で包まれている場合がある
        /// </summary>
        /// <returns></returns>
        public async UniTask<bool> LoadModel(CancellationToken token)
        {
            string session = null;
            var queries = (new Uri(BaseJsonUrl)).Query.Replace("?","").Split('&').Select(s => s.Split('='));
            var sessionPair = queries.Where(p => p[0] == "session");
            if (sessionPair.Count() > 0)
            {
                session = sessionPair.ToList()[0][1];
            }

            var modelRelativeUrl = TileSetNode?.content?.Url;
            if (string.IsNullOrEmpty(modelRelativeUrl))
            {
                Debug.LogError("model relative url is null");
                return false;
            }
            if (!string.IsNullOrEmpty(TileSetInfoProvider.LoaderConfig?.GoogleMapTileApiKey))
            {
                modelRelativeUrl += (modelRelativeUrl.Contains("?") ? "&" : "?") + "key=" + TileSetInfoProvider.LoaderConfig.GoogleMapTileApiKey + "&session=" + session;
            }
            var modelUri = new Uri(new Uri(BaseJsonUrl), modelRelativeUrl);
            var ModelAbsoluteUri = modelUri.AbsoluteUri;

            var ext = Path.GetExtension(modelUri.PathAndQuery.Split('?')[0]);

            ModelData modelData = null;
            if (ext?.ToLower() == ".glb")
            {
                // glbをロード
                modelData = await LoadGLB(ModelAbsoluteUri, token);
                if (modelData == null)
                {
                    return false;
                }
            }
            else
            {
                // b3dmファイルをロード
                // b3dmはメタデータとglbを合わせたようなフォーマット
                byte[] result = await LoadB3DM(ModelAbsoluteUri, token);
                if (result == null)
                {
                    return false;
                }
                modelData = OnB3DMResult(result);
                if (modelData == null)
                {
                    return false;
                }
            }
            await InstatiateGltf(modelData.Data, modelData.CenterPosition);
            return true;
        }

        class ModelData
        {
            /// <summary>
            /// バイナリデータ
            /// </summary>
            public byte[] Data { get; set; }

            /// <summary>
            /// 中心座標 (3要素)
            /// </summary>
            public double[] CenterPosition { get; set; }
        }

        async UniTask<ModelData> LoadGLB(string url, CancellationToken token)
        {
            Debug.Log("loading GLB: " + url);
            using (var req = new UnityWebRequest(url))
            using (var buf = new DownloadHandlerBuffer())
            {
                req.downloadHandler = buf;
                await req.SendWebRequest().ToUniTask(cancellationToken: token);
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("load error: " + req.error);
                    return null;
                }
                return new ModelData()
                {
                    Data = buf.data,
                    CenterPosition = new double[]{0,0,0},
                };
            }
        }

        async UniTask<byte[]> LoadB3DM(string url, CancellationToken token)
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
                await req.SendWebRequest().ToUniTask(cancellationToken: token);
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("load error: " + req.error);
                    return null;
                }
                Debug.Log("loading success.");
                return buf.data;
            }
        }

        ModelData OnB3DMResult(byte[] data)
        {
            var b3dm = new B3DM();
            if (!b3dm.Read(data))
            {
                Debug.LogError("B3DM読み取りエラー");
                return null;
            }

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
            return new ModelData()
            {
                Data = b3dm.GltfData.ToArray(),
                CenterPosition = center,
            };
        }

        async UniTask<bool> InstatiateGltf(byte[] gltfData, double[] center)
        {
            var component = this;
            this.gltfCenter = center;

            PrepareGltfInstantiator();
            if (component == null)
            {
                return false;
            }
            var (result, metadata) = await gltfInstantiator.Instantiate(gltfData, center, this, this.GetCancellationTokenOnDestroy());

            // Copyrightを格納する
            if (!string.IsNullOrEmpty(metadata?.Copyright))
            {
                Copyright = metadata.Copyright.Split(";").ToList();
            }
            return result;
        }

        /// <summary>
        /// カリング判定用の球体を返す
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

        /// <summary>
        /// UnityのCollider作成
        /// 現状 BoundingVolumeTypeが Box の場合のみ。
        /// つまりGoogle 3D Tilesでは動くがPLATEAU Streamingでは動かない (2024/8現在の状況)
        /// </summary>
        public void GenerateCollider()
        {
            if (BoundingBoxType == BoundingBoxType.Box)
            {
                GenerateColliderForBox();
            }
            else if (BoundingBoxType == BoundingBoxType.Region)
            {
                GenerateColliderForRegion();
            }
        }

        /// <summary>
        /// BoxタイプのBoundingVolumeに対応するUnityのBoxColliderを作成する
        /// できるだけdouble精度で計算し、最後にUnityのColliderの座標を設定する
        /// </summary>
        private void GenerateColliderForBox()
        {
            var (colliderTrans, collider) = GetOrCreateColliderGameObject<BoxCollider>();
            var box = TileSetNode.boundingVolume.box;
            var centerPos = new VectorD3(box[0], box[1], -box[2]);
            var halfExtentX = new VectorD3(box[3], box[4], -box[5]);
            var halfExtentY = new VectorD3(box[6], box[7], -box[8]);
            var halfExtentZ = new VectorD3(box[9], box[10], -box[11]);
            double halfLengthX = halfExtentX.magnitude;
            double halfLengthY = halfExtentY.magnitude;
            double halfLengthZ = halfExtentZ.magnitude;

            var normalY = halfExtentY / halfLengthY;
            var normalZ = halfExtentZ / halfLengthZ;

            VectorD3 modelOffset = -TileSetInfoProvider.ModelCenterEcefCoordinate;

            colliderTrans.localRotation = Quaternion.LookRotation(normalZ.ToVector3(), normalY.ToVector3());
            colliderTrans.localPosition = (centerPos + modelOffset).ToVector3();
            collider.center = Vector3.zero;
            collider.size = new VectorD3(halfLengthX * 2, halfLengthY * 2, halfLengthZ * 2).ToVector3();
        }

        /// <summary>
        /// Region用のコライダー生成
        /// 処理をサボっており、タイルを包含する球体コライダーにしている。
        /// 本来はもう少し無駄の少ないコライダーにできるはず。
        /// </summary>
        private void GenerateColliderForRegion()
        {
            var sphere = GetBoundingSphere();
            var (colliderTrans, collider) = GetOrCreateColliderGameObject<SphereCollider>();
            VectorD3 modelOffset = -TileSetInfoProvider.ModelCenterEcefCoordinate;
            colliderTrans.localPosition = (sphere.center + modelOffset).ToVector3();
            collider.center = Vector3.zero;
            collider.radius = (float) sphere.radiusMeters;
        }

        /// <summary>
        /// Collider用GameObjectを取得または生成して返す
        /// </summary>
        /// <typeparam name="T">Colliderの派生タイプ</typeparam>
        /// <returns></returns>
        private (Transform trans, T collider) GetOrCreateColliderGameObject<T>() where T: Collider
        {
            var colliderTrans = transform.Find(NodeCulling.ColliderGameObjectName);
            if (colliderTrans == null)
            {
                var colliderGO = new GameObject(NodeCulling.ColliderGameObjectName);
                colliderTrans = colliderGO.transform;
                colliderTrans.SetParent(transform, false);
            }
            var collider = colliderTrans.GetComponent<T>();
            if (collider == null)
            {
                collider = colliderTrans.gameObject.AddComponent<T>();
            }
            return (colliderTrans, collider);
        }
    }
}
