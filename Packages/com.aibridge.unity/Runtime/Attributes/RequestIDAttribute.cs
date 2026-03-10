#nullable enable

using System;

namespace UnityAiBridge
{
    /// <summary>
    /// Marks a parameter as the request ID for deferred tool responses.
    /// Used by tools that trigger domain reloads (script create/delete, package add/remove).
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RequestIDAttribute : Attribute
    {
    }
}
