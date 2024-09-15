using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System;


/// <summary>
/// モデルロードをスロットリングするクラス
/// 汎用の作りをしているので、実はモデルロード以外にも利用可能。
/// </summary>
public class ModelLoadScheduler : MonoBehaviour
{
    public enum TaskState
    {
        Waiting,
        Running,
        Success,
        Failed,
        Cancelled,
    }

    /// <summary>
    /// 実行対象のタスク
    /// </summary>
    public interface Task
    {
        UniTask<(bool result, object artifact)> Do(CancellationToken token);
    }

    /// <summary>
    /// タスクのメタデータを保持するCarrier
    /// </summary>
    public class TaskCarrier : IDisposable
    {
        public string Name { get; }

        // priorityの高い順に実行される。後から変更も可能。
        // 現時点で実行順の調整処理は未実装 (2024/9/16)
        public float Priority { get; set; }
        public CancellationToken CancellationToken { get; }

        // ModelLoadSchedulerからもキャンセルしたいので、元のCancellationTokenとリンクしたCancellationTokenSourceを作成して保持する。
        public CancellationTokenSource LinkedCancellationTokenSource { get; private set; }

        public TaskState State { get; set; }
        public Exception Exception { get; set; }

        public Task Task { get; }

        public TaskCarrier(string name, CancellationToken token, Task task)
        {
            Name = name;
            CancellationToken = token;
            State = TaskState.Waiting;
            Task = task;
        }

        public void MakeLinkedCancellationTokenSource()
        {
            if (LinkedCancellationTokenSource != null)
            {
                Debug.LogError("LinkedCancellationTokenSource already initialized.");
                return;
            }
            LinkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        }

        public void CancelTask()
        {
            LinkedCancellationTokenSource?.Cancel();
        }

        public void Dispose()
        {
            if (LinkedCancellationTokenSource != null)
            {
                ((IDisposable)LinkedCancellationTokenSource).Dispose();
            }
        }
    }

    private static ModelLoadScheduler instance;
    public static ModelLoadScheduler Instance => instance;

    /// <summary>
    /// 待機中のタスクリスト
    /// </summary>
    private List<TaskCarrier> waitingTasks = new List<TaskCarrier>();

    /// <summary>
    /// 実行中のタスクリスト
    /// </summary>
    private List<TaskCarrier> runningTasks = new List<TaskCarrier>();

    const int concurrencyLimit = 8;

    public int RemainingTasksCount => waitingTasks.Count + runningTasks.Count;

    public static void CreateGameObject()
    {
        var go = new GameObject("ModelLoadScheduler");
        go.AddComponent<ModelLoadScheduler>();
    }

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    void Start()
    {
    }

    void Update()
    {
        RunNextTasks();
    }

    private void OnDestroy()
    {
        StopAllTasks();
    }

    /// <summary>
    /// タスクをキューに追加する
    /// </summary>
    /// <param name="name"></param>
    /// <param name="token"></param>
    /// <param name="priority"></param>
    /// <param name="task"></param>
    /// <returns></returns>
    public TaskCarrier AddTask(string name, CancellationToken token, float priority, ModelLoadScheduler.Task task)
    {
        var carrier = new TaskCarrier(name, token, task);
        carrier.Priority = priority;
        waitingTasks.Add(carrier);
        return carrier;
    }

    /// <summary>
    /// スロットの空きがあり、残りタスクがあればタスクを実行する
    /// </summary>
    void RunNextTasks()
    {
        while (runningTasks.Count < concurrencyLimit && waitingTasks.Count > 0)
        {
            var carrier = waitingTasks[0];
            waitingTasks.RemoveAt(0);
            if (carrier.CancellationToken.IsCancellationRequested)
            {
                Debug.Log($"Didn't run the task {carrier.Name} because it is already cancelled.");
                return;
            }
            RunTask(carrier);
        }
    }

    /// <summary>
    /// 該当タスクを実行する
    /// 実行中、TaskCarrierのStateはRunningになり、runningTasks に入る。
    /// </summary>
    /// <param name="task"></param>
    void RunTask(TaskCarrier carrier)
    {
        UniTask.Void(async () =>
        {
            try
            {
                runningTasks.Add(carrier);
                carrier.MakeLinkedCancellationTokenSource();
                carrier.State = TaskState.Running;
                Debug.Log($"++++ start task {carrier.Name} concurrency: {runningTasks.Count}");
                var result = await carrier.Task.Do(carrier.CancellationToken);
                if (result.result)
                {
                    carrier.State = TaskState.Success;
                }
                else
                {
                    carrier.State = TaskState.Failed;
                    carrier.Exception = new Exception("the task returned failure.");
                }
            }
            catch (OperationCanceledException e)
            {
                carrier.State = TaskState.Cancelled;
            }
            catch (Exception e)
            {
                carrier.State = TaskState.Failed;
                carrier.Exception = e;
            }
            finally
            {
                runningTasks.Remove(carrier);
                Debug.Log($"---- end task {carrier.Name} state: {carrier.State} concurrency: {runningTasks.Count}");
            }
        });
    }

    /// <summary>
    /// キューイングされたすべてのタスクを止めて、タスクキューをクリアする。
    /// </summary>
    public void StopAllTasks()
    {
        waitingTasks.Clear();
        foreach (var carrier in runningTasks)
        {
            carrier.CancelTask();
            carrier.Dispose();
        }
        runningTasks.Clear();
    }
}
