using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// Generic task scheduler that manages concurrent execution of tasks with configurable limits
    /// </summary>
    public class TaskScheduler
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
        /// Interface for tasks that can be executed by the scheduler
        /// </summary>
        public interface Task
        {
            UniTask<(bool result, object artifact)> Do(CancellationToken token);
        }

        /// <summary>
        /// Container for task metadata and execution state
        /// </summary>
        public class TaskCarrier : IDisposable
        {
            public string Name { get; }
            public float Priority { get; set; }
            
            private CancellationToken sourceCancellationToken;
            public CancellationToken CancellationToken { get; private set; }
            public CancellationTokenSource LinkedCancellationTokenSource { get; private set; }

            public TaskState State { get; set; }
            public Exception Exception { get; set; }
            public Task Task { get; }

            public TaskCarrier(string name, CancellationToken token, Task task)
            {
                Name = name;
                State = TaskState.Waiting;
                Task = task;
                sourceCancellationToken = token;
            }

            public void MakeLinkedCancellationTokenSource()
            {
                if (LinkedCancellationTokenSource != null)
                {
                    Debug.LogError("LinkedCancellationTokenSource already initialized.");
                    return;
                }
                LinkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(sourceCancellationToken);
                CancellationToken = LinkedCancellationTokenSource.Token;
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

        private readonly List<TaskCarrier> waitingTasks = new List<TaskCarrier>();
        private readonly List<TaskCarrier> runningTasks = new List<TaskCarrier>();
        private readonly int concurrencyLimit;
        private readonly string schedulerName;

        public int RemainingTasksCount => waitingTasks.Count + runningTasks.Count;

        public TaskScheduler(int concurrencyLimit = 8, string schedulerName = "TaskScheduler")
        {
            this.concurrencyLimit = concurrencyLimit;
            this.schedulerName = schedulerName;
        }

        /// <summary>
        /// Add a task to the execution queue
        /// </summary>
        public TaskCarrier AddTask(string name, CancellationToken token, float priority, Task task)
        {
            var carrier = new TaskCarrier(name, token, task);
            carrier.Priority = priority;
            waitingTasks.Add(carrier);
            return carrier;
        }

        /// <summary>
        /// Execute next available tasks if there are free slots
        /// Should be called regularly (e.g., from Update method)
        /// </summary>
        public void ProcessTasks()
        {
            while (runningTasks.Count < concurrencyLimit && waitingTasks.Count > 0)
            {
                var carrier = waitingTasks[0];
                waitingTasks.RemoveAt(0);
                if (carrier.CancellationToken.IsCancellationRequested)
                {
                    Debug.Log($"{schedulerName}: Didn't run the task {carrier.Name} because it is already cancelled.");
                    continue;
                }
                RunTask(carrier);
            }
        }

        /// <summary>
        /// Execute a specific task
        /// </summary>
        private void RunTask(TaskCarrier carrier)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    runningTasks.Add(carrier);
                    carrier.MakeLinkedCancellationTokenSource();
                    carrier.State = TaskState.Running;
                    Debug.Log($"{schedulerName}: ++++ start task {carrier.Name} concurrency: {runningTasks.Count}");
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
                catch (OperationCanceledException)
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
                    Debug.Log($"{schedulerName}: ---- end task {carrier.Name} state: {carrier.State} concurrency: {runningTasks.Count}");
                }
            });
        }

        /// <summary>
        /// Stop all queued and running tasks
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
}