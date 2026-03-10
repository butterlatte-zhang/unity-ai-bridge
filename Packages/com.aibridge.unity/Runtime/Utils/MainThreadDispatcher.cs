#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// MonoBehaviour for dispatching actions to the main thread at runtime.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static int _mainThreadId = -1;

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditor()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
#endif

        public static void Enqueue(Action action)
        {
            _queue.Enqueue(action);
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
