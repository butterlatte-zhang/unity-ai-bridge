#nullable enable

using System;
using System.Threading.Tasks;

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// Abstract base for main-thread dispatching.
    /// </summary>
    public abstract class MainThread
    {
        public static MainThread? Instance { get; set; }

        public abstract bool IsMainThread { get; }

        public abstract Task RunAsync(Action action);
        public abstract Task<T> RunAsync<T>(Func<T> func);
        public abstract Task RunAsync(Task task);
        public abstract Task<T> RunAsync<T>(Task<T> task);

        public T Run<T>(Func<T> func)
        {
            if (IsMainThread)
                return func();

            return RunAsync(func).GetAwaiter().GetResult();
        }

        public void Run(Action action)
        {
            if (IsMainThread)
            {
                action();
                return;
            }

            RunAsync(action).GetAwaiter().GetResult();
        }
    }
}
