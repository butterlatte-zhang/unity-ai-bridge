#nullable enable

using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityEditor;
using UnityEngine;
using UnityAiBridge.Serialization.Converters.Json;

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// Lightweight singleton entry point for the bridge system.
    /// Initializes the tool registry, runner, and reflector on domain reload.
    /// </summary>
    [InitializeOnLoad]
    public static class BridgePlugin
    {
        public static BridgeToolRegistry Registry { get; private set; } = null!;
        public static BridgeToolRunner Runner { get; private set; } = null!;
        public static BridgeReflector Reflector { get; private set; } = null!;

        private static bool _initialized;

        static BridgePlugin()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (_initialized)
                return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Initialize MainThread for editor
            MainThreadInstaller.Init();

            // Create reflector with Unity-specific converters
            Reflector = CreateReflector();

            // Scan assemblies for tools
            Registry = new BridgeToolRegistry();

            // Create the tool runner
            Runner = new BridgeToolRunner(Registry, Reflector);

            _initialized = true;
            stopwatch.Stop();
            Debug.Log($"[BridgePlugin] Initialized in {stopwatch.ElapsedMilliseconds}ms ({Registry.ToolCount} tools)");
        }

        /// <summary>
        /// Ensure the plugin is initialized. Can be called multiple times safely.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        private static BridgeReflector CreateReflector()
        {
            var reflector = new BridgeReflector();

            // Blacklist types that cause issues during serialization
            reflector.Converters.BlacklistType(typeof(System.Delegate));
            reflector.Converters.BlacklistType(typeof(System.EventHandler));
            reflector.Converters.BlacklistType(typeof(System.EventHandler<>));
            reflector.Converters.BlacklistType(typeof(System.MulticastDelegate));
            reflector.Converters.BlacklistType(typeof(System.IntPtr));
            reflector.Converters.BlacklistType(typeof(System.UIntPtr));
            reflector.Converters.BlacklistType(typeof(System.Reflection.FieldInfo));
            reflector.Converters.BlacklistType(typeof(System.Reflection.PropertyInfo));
            reflector.Converters.BlacklistType(typeof(System.Threading.CancellationToken));
            reflector.Converters.BlacklistType(typeof(System.Span<>));
            reflector.Converters.BlacklistType(typeof(System.ReadOnlySpan<>));

#if UNITY_2023_1_OR_NEWER
            reflector.Converters.BlacklistType(typeof(UnityEngine.LowLevelPhysics.GeometryHolder));
#endif
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.Text.FontFeatureTable));
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.Glyph));
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.GlyphRect));
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.GlyphMetrics));

            reflector.Converters.BlacklistTypesInAssembly(
                assemblyNamePrefix: "Unity.TextMeshPro",
                typeFullNames: new[]
                {
                    "TMPro.TMP_TextInfo",
                    "TMPro.TMP_TextElement",
                    "TMPro.TMP_FontFeatureTable",
                    "TMPro.TMP_FontWeightPair",
                    "TMPro.FaceInfo_Legacy"
                }
            );

            reflector.Converters.BlacklistTypeInAssembly(
                assemblyNamePrefix: "Unity.RenderPipelines.Core.Runtime",
                typeFullName: "UnityEngine.Rendering.RTHandle"
            );

            reflector.Converters.BlacklistTypeInAssembly(
                assemblyNamePrefix: "Fusion.Runtime",
                typeFullName: "Fusion.NetworkBehaviourBuffer"
            );

            reflector.Converters.BlacklistTypeInAssembly(
                assemblyNamePrefix: "Unity.ResourceManager",
                typeFullName: "UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle"
            );

            // Add JSON converters for Unity types
            AddJsonConverters(reflector);

            return reflector;
        }

        private static void AddJsonConverters(BridgeReflector reflector)
        {
            // Unity structure converters
            reflector.JsonSerializer.AddConverter(new Color32Converter());
            reflector.JsonSerializer.AddConverter(new ColorConverter());
            reflector.JsonSerializer.AddConverter(new Matrix4x4Converter());
            reflector.JsonSerializer.AddConverter(new QuaternionConverter());
            reflector.JsonSerializer.AddConverter(new Vector2Converter());
            reflector.JsonSerializer.AddConverter(new Vector2IntConverter());
            reflector.JsonSerializer.AddConverter(new Vector3Converter());
            reflector.JsonSerializer.AddConverter(new Vector3IntConverter());
            reflector.JsonSerializer.AddConverter(new Vector4Converter());
            reflector.JsonSerializer.AddConverter(new BoundsConverter());
            reflector.JsonSerializer.AddConverter(new BoundsIntConverter());
            reflector.JsonSerializer.AddConverter(new RectConverter());
            reflector.JsonSerializer.AddConverter(new RectIntConverter());

            // Reference type converters
            reflector.JsonSerializer.AddConverter(new ObjectRefConverter());
            reflector.JsonSerializer.AddConverter(new AssetObjectRefConverter());
            reflector.JsonSerializer.AddConverter(new GameObjectRefConverter());
            reflector.JsonSerializer.AddConverter(new ComponentRefConverter());
            reflector.JsonSerializer.AddConverter(new SceneRefConverter());
        }
    }
}
