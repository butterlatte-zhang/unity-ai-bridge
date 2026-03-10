#nullable enable

using System.Threading;

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// Thread-safe boolean wrapper.
    /// </summary>
    public class ThreadSafeBool
    {
        private int _value;

        public bool Value => Interlocked.CompareExchange(ref _value, 0, 0) == 1;

        public ThreadSafeBool(bool initialValue = false)
        {
            _value = initialValue ? 1 : 0;
        }

        /// <summary>
        /// Attempts to set value to true. Returns true if it was previously false.
        /// </summary>
        public bool TrySetTrue()
        {
            return Interlocked.CompareExchange(ref _value, 1, 0) == 0;
        }

        /// <summary>
        /// Attempts to set value to false. Returns true if it was previously true.
        /// </summary>
        public bool TrySetFalse()
        {
            return Interlocked.CompareExchange(ref _value, 0, 1) == 1;
        }
    }
}
