
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public static IEnumerable<Type> AllComponentTypes => TypeUtils.AllTypes
            .Where(type => typeof(UnityEngine.Component).IsAssignableFrom(type) && !type.IsAbstract);

        public const string ComponentListToolId = "gameobject-component-list-all";
        [BridgeTool
        (
            ComponentListToolId,
            Title = "GameObject / Component / List All"
        )]
        [Description("When gameObjectRef is provided: list all components attached to that GameObject (type name + instanceID). " +
            "When gameObjectRef is omitted: list all available C# component type names in the project (for '" + GameObjectComponentAddToolId + "' tool). " +
            "Results are paginated.")]
        public ComponentListResult ListAll
        (
            [Description("Target GameObject. When provided, lists components ON this object. " +
                "When omitted, lists all available component types in the project.")]
            GameObjectRef? gameObjectRef = null,
            [Description("Substring for searching/filtering component names. Could be empty.")]
            string? search = null,
            [Description("Page number (0-based). Default is 0.")]
            int page = 0,
            [Description("Number of items per page. Default is 20. Max is 500.")]
            int pageSize = 20
        )
        {
            // Clamp pageSize to valid range
            pageSize = Math.Clamp(pageSize, 1, 500);
            page = Math.Max(0, page);

            IEnumerable<string> items;

            if (gameObjectRef != null && gameObjectRef.IsValid(out _))
            {
                // 列出指定 GameObject 上的组件
                items = ListComponentsOnGameObject(gameObjectRef);
            }
            else
            {
                // 列出项目中所有可用组件类型（原有行为）
                items = AllComponentTypes
                    .Select(type => type.GetTypeId())
                    .Where(typeName => typeName != null)
                    .Cast<string>();
            }

            if (!string.IsNullOrEmpty(search))
            {
                items = items
                    .Where(name => name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var allItems = items.ToList();
            var totalCount = allItems.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var pagedItems = allItems
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToArray();

            return new ComponentListResult
            {
                Items = pagedItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        private static IEnumerable<string> ListComponentsOnGameObject(GameObjectRef gameObjectRef)
        {
            var go = gameObjectRef.FindGameObject(out var error);
            if (go == null)
                throw new Exception(error ?? "GameObject not found.");

            var components = go.GetComponents<UnityEngine.Component>();
            var result = new List<string>();

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null) continue;

                var typeName = comp.GetType().GetTypeId() ?? comp.GetType().Name;
                var instanceID = comp.GetInstanceID();
                result.Add($"{typeName} (instanceID: {instanceID}, index: {i})");
            }

            return result;
        }

        public class ComponentListResult
        {
            [Description("Array of component type names (or component info when gameObjectRef is provided).")]
            public string[] Items { get; set; } = Array.Empty<string>();

            [Description("Current page number (0-based).")]
            public int Page { get; set; }

            [Description("Number of items per page.")]
            public int PageSize { get; set; }

            [Description("Total number of matching items.")]
            public int TotalCount { get; set; }

            [Description("Total number of pages available.")]
            public int TotalPages { get; set; }
        }
    }
}
