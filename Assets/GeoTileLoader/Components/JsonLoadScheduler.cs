using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Collections;

namespace GeoTile
{
    /// <summary>
    /// Scheduler for JSON loading tasks with concurrent execution support
    /// Singleton MonoBehaviour that manages JSON loading operations
    /// </summary>
    public class JsonLoadScheduler : MonoBehaviour
    {

        private static JsonLoadScheduler instance;
        public static JsonLoadScheduler Instance => instance;

        private TaskScheduler taskScheduler;

        // Configure concurrency limit for JSON loading
        private const int JsonConcurrencyLimit = 20;

        public int RemainingTasksCount => taskScheduler?.RemainingTasksCount ?? 0;

        public static void CreateGameObject()
        {
            var go = new GameObject("JsonLoadScheduler");
            go.AddComponent<JsonLoadScheduler>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            taskScheduler = new TaskScheduler(JsonConcurrencyLimit, "JsonLoadScheduler");
        }

        private void Start()
        {
            // 5秒間隔でrunningTasksの状態をログ出力するコルーチンを開始
            StartCoroutine(LogRunningTasksCoroutine());
        }

        private void Update()
        {
            taskScheduler?.ProcessTasks();
        }

        /// <summary>
        /// 10秒間隔でrunningTasksの状態をログ出力するコルーチン
        /// </summary>
        private IEnumerator LogRunningTasksCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(10.0f);
                if (taskScheduler == null)
                {
                    continue;
                }
                if (taskScheduler.RemainingTasksCount == 0)
                {
                    continue;
                }
                Debug.Log(taskScheduler.GetRunningTasksInfo());
            }
        }

        private void OnDestroy()
        {
            taskScheduler?.StopAllTasks();
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Add a subtree loading task to the queue
        /// </summary>
        /// <param name="name">Task name for debugging</param>
        /// <param name="hierarchy">TileSetHierarchy instance</param>
        /// <param name="trans">Transform to process</param>
        /// <param name="maxLevels">Maximum levels to process</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="priority">Task priority (higher values executed first)</param>
        /// <returns>Task carrier for tracking progress</returns>
        public TaskScheduler.TaskCarrier AddLoadSubTreeTask(
            string name,
            TileSetHierarchy hierarchy,
            Transform trans,
            int maxLevels,
            CancellationToken token,
            float priority = 0)
        {
            var task = new LoadSubTreeTask(hierarchy, trans, maxLevels);
            return taskScheduler.AddTask(name, token, priority, task);
        }

        /// <summary>
        /// Stop all queued and running JSON loading tasks
        /// </summary>
        public void StopAllTasks()
        {
            taskScheduler?.StopAllTasks();
        }
    }
}