#nullable enable

#if !UNITY_EDITOR
using System;
using System.Threading.Tasks;

namespace UnityAiBridge.Utils
{
    public static class MainThreadInstaller
    {
        public static void Init()
        {
            MainThread.Instance ??= new PlayerMainThread();
        }
    }

    public class PlayerMainThread : MainThread
    {
        public override bool IsMainThread => MainThreadDispatcher.IsMainThread;

        public override Task RunAsync(Action action)
        {
            if (MainThreadDispatcher.IsMainThread)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public override Task<T> RunAsync<T>(Func<T> func)
        {
            if (MainThreadDispatcher.IsMainThread)
                return Task.FromResult(func());

            var tcs = new TaskCompletionSource<T>();
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public override Task RunAsync(Task task)
        {
            if (MainThreadDispatcher.IsMainThread)
                return task;

            var tcs = new TaskCompletionSource<bool>();
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    task.Wait();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public override Task<T> RunAsync<T>(Task<T> task)
        {
            if (MainThreadDispatcher.IsMainThread)
                return task;

            var tcs = new TaskCompletionSource<T>();
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(task.Result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }
    }
}
#endif
