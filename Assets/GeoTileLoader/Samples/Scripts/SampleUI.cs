using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GeoTile
{
    /// <summary>
    /// 3D TileをロードするサンプルUI
    /// </summary>
    public class SampleUI : MonoBehaviour
    {
        [SerializeField] private TileSetManager manager;

        [SerializeField] private Button loadHierarchyButton;
        [SerializeField] private Button load3DModelButton;
        [SerializeField] private Button stopButton;
        
        [SerializeField] private Text messageText;

        private bool isBusy;

        private Transform hierarchyTransform;

        private CancellationTokenSource cts;

        // Start is called before the first frame update
        void Start()
        {
            loadHierarchyButton.onClick.AddListener(OnLoadHierarchyButtonClicked);
            load3DModelButton.onClick.AddListener(OnLoad3DModelButtonClicked);
            stopButton.onClick.AddListener(OnStopButtonClicked);
        }

        void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
            loadHierarchyButton.onClick.RemoveListener(OnLoadHierarchyButtonClicked);
            load3DModelButton.onClick.RemoveListener(OnLoad3DModelButtonClicked);
            stopButton.onClick.RemoveListener(OnStopButtonClicked);
        }

        void Update()
        {
            loadHierarchyButton.interactable = !isBusy;
            load3DModelButton.interactable = !isBusy;
            stopButton.interactable = isBusy;
        }

        /// <summary>
        /// ルートノードをロードしたあと、ネストされたサブツリーがあれば読み込む
        /// カリングの範囲と重複するような領域をもつノードだけが残る。
        /// 読み込むノード階層の深さ、合計ノード数の制限をつけているので想定以上の大量に読み込むことはない。
        /// </summary>
        /// <exception cref="Exception"></exception>
        void OnLoadHierarchyButtonClicked()
        {
            UniTask.Void(async () =>
            {
                try
                {
                    cts = new CancellationTokenSource();
                    isBusy = true;
                    messageText.text = "Loading Hierarchy...";
                    if (hierarchyTransform != null)
                    {
                        Destroy(hierarchyTransform.gameObject);
                        hierarchyTransform = null;
                    }
                    hierarchyTransform = await manager.ReadJsonAsync(null, cts.Token);
                    messageText.text = "Loading Subtree Nodes...";
                    var hierarchy = hierarchyTransform?.GetComponent<TileSetHierarchy>();
                    if (hierarchy == null)
                    {
                        throw new Exception("No Hierarchy exist.");
                    }
                    await hierarchy.LoadSubTrees(100, 1000, cts.Token);
                    messageText.text = "Loading Subtree Nodes Success!";
                }
                catch (Exception e)
                {
                    messageText.text = "Loading Hierarchy Error: "+ e;
                }
                finally
                {
                    isBusy = false;
                    cts?.Dispose();
                    cts = null;
                }
            });
        }

        /// <summary>
        /// GLTFモデルをロードする
        /// カリングはノード時点で行われている前提のため、ここでは全てのノードについて読み込む。
        /// </summary>
        /// <exception cref="Exception"></exception>
        void OnLoad3DModelButtonClicked()
        {
            UniTask.Void(async () =>
            {
                try
                {
                    cts = new CancellationTokenSource();
                    isBusy = true;
                    messageText.text = "Loading 3D Models";
                    var hierarchy = hierarchyTransform?.GetComponent<TileSetHierarchy>();
                    if (hierarchy == null)
                    {
                        throw new Exception("No Hierarchy exist.");
                    }
                    await hierarchy.Load3DModels(1000, cts.Token);
                    messageText.text = "Loading 3D Models Success!";
                }
                catch (Exception e)
                {
                    messageText.text = "Loading 3D Models Error: "+ e;
                }
                finally
                {
                    isBusy = false;
                    cts?.Dispose();
                    cts = null;
                }
            });
        }

        void OnStopButtonClicked()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

    }
}