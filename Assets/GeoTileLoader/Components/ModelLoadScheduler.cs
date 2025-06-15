using UnityEngine;
using System.Threading;

namespace GeoTile
{
    /// <summary>
    /// モデルロードをスロットリングするクラス
    /// TaskSchedulerを内部的に使用するMonoBehaviourラッパー
    /// </summary>
    public class ModelLoadScheduler : MonoBehaviour
    {
        private static ModelLoadScheduler instance;
        public static ModelLoadScheduler Instance => instance;

        private TaskScheduler taskScheduler;

        const int concurrencyLimit = 8;

        public int RemainingTasksCount => taskScheduler?.RemainingTasksCount ?? 0;

        public static void CreateGameObject()
        {
            var go = new GameObject("ModelLoadScheduler");
            go.AddComponent<ModelLoadScheduler>();
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

            taskScheduler = new TaskScheduler(concurrencyLimit, "ModelLoadScheduler");
        }

        void Start()
        {
        }

        void Update()
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
        /// タスクをキューに追加する
        /// </summary>
        /// <param name="name"></param>
        /// <param name="token"></param>
        /// <param name="priority"></param>
        /// <param name="task"></param>
        /// <returns></returns>
        public TaskScheduler.TaskCarrier AddTask(string name, CancellationToken token, float priority, TaskScheduler.Task task)
        {
            return taskScheduler.AddTask(name, token, priority, task);
        }

        /// <summary>
        /// キューイングされたすべてのタスクを止めて、タスクキューをクリアする。
        /// </summary>
        public void StopAllTasks()
        {
            taskScheduler?.StopAllTasks();
        }
    }
}