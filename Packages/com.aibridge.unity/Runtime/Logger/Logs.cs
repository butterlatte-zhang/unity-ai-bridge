
#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityAiBridge.Logger
{
    /// <summary>
    /// Simple log accumulator for tracking modifications and warnings.
    /// </summary>
    public class Logs : IEnumerable<string>
    {
        private readonly List<string> _entries = new();

        public int Count => _entries.Count;

        public void Add(string message)
        {
            _entries.Add(message);
        }

        public void Warning(string message)
        {
            _entries.Add($"[Warning] {message}");
        }

        public void Error(string message)
        {
            _entries.Add($"[Error] {message}");
        }

        public IEnumerator<string> GetEnumerator() => _entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
