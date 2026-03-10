#nullable enable

using System;

namespace UnityAiBridge
{
    /// <summary>
    /// Marks a method as a Bridge tool.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BridgeToolAttribute : Attribute
    {
        public string Name { get; }
        public string Title { get; set; } = "";

        public BridgeToolAttribute(string name)
        {
            Name = name;
        }
    }
}
