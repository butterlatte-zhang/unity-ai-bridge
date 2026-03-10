#nullable enable

using System;

namespace UnityAiBridge
{
    /// <summary>
    /// Marks a class as a Bridge tool type container.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class BridgeToolTypeAttribute : Attribute
    {
    }
}
