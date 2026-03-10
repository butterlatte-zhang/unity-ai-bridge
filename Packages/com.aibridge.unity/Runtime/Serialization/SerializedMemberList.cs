#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Serialization
{
    /// <summary>
    /// Typed list of SerializedMember.
    /// Supports LINQ operations, indexing, and parameter enhancement.
    /// </summary>
    public class SerializedMemberList : List<SerializedMember>
    {
        public SerializedMemberList() { }

        public SerializedMemberList(int capacity) : base(capacity) { }

        public SerializedMemberList(SerializedMember item) : base(1)
        {
            Add(item);
        }

        public SerializedMemberList(IEnumerable<SerializedMember> collection) : base(collection) { }

        /// <summary>
        /// Get a field by name.
        /// </summary>
        public SerializedMember? GetField(string name)
        {
            return this.FirstOrDefault(x => x.name == name);
        }

        /// <summary>
        /// Enhance parameter names from a MethodInfo's parameters.
        /// Fills in empty names using positional matching.
        /// </summary>
        public void EnhanceNames(MethodInfo method)
        {
            if (Count == 0)
                return;

            var methodParams = method.GetParameters();
            for (int i = 0; i < Count && i < methodParams.Length; i++)
            {
                var member = this[i];
                if (string.IsNullOrEmpty(member.name))
                {
                    member.name = methodParams[i].Name;
                }
            }
        }

        /// <summary>
        /// Enhance parameter type names from a MethodInfo's parameters.
        /// Fills in empty type names using positional matching.
        /// </summary>
        public void EnhanceTypes(MethodInfo method)
        {
            if (Count == 0)
                return;

            var methodParams = method.GetParameters();
            for (int i = 0; i < Count && i < methodParams.Length; i++)
            {
                var member = this[i];
                if (string.IsNullOrEmpty(member.typeName))
                {
                    var typeId = methodParams[i]?.ParameterType?.GetTypeId();
                    if (typeId != null)
                    {
                        member.typeName = typeId;
                    }
                }
            }
        }

        public override string ToString()
        {
            if (Count == 0)
                return "No items";

            var sb = new StringBuilder();
            sb.AppendLine($"Items total amount: {Count}");
            for (int i = 0; i < Count; i++)
            {
                sb.AppendLine($"Item[{i}] {this[i]}");
            }
            return sb.ToString();
        }
    }
}
