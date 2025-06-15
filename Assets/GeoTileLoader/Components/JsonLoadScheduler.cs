using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using System.Linq;

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

        // Configure concurrency limit for JSON loading (lower than model loading to avoid overwhelming servers)
        private const int JsonConcurrencyLimit = 10;

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

        private void Update()
        {
            taskScheduler?.ProcessTasks();
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
        /// <param name="maxNodes">Maximum nodes to process</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="priority">Task priority (higher values executed first)</param>
        /// <returns>Task carrier for tracking progress</returns>
        public TaskScheduler.TaskCarrier AddLoadSubTreeTask(
            string name,
            TileSetHierarchy hierarchy,
            Transform trans,
            int maxLevels,
            int maxNodes,
            CancellationToken token,
            float priority = 0)
        {
            var task = new LoadSubTreeTask(hierarchy, trans, maxLevels, maxNodes);
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