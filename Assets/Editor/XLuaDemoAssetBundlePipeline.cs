using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using XLuaDemo.ResourceUpdate;

public sealed class XLuaDemoAssetBundlePipelineWindow : EditorWindow
{
    private const string DefaultAnalyzeRoots = "Assets/XLuaDemoGenerated/ABSource";
    private const string DefaultRemoteRoot = "Assets/StreamingAssets/RemoteServer";
    private const string SettingsKey = "XLuaDemo.AssetBundlePipeline.Settings";

    private Vector2 scroll;
    private PipelineWindowSettings settings;
    private XLuaDemoAssetBundlePipeline.DependencyReport report;
    private XLuaDemoAssetBundlePipeline.BundleBuildPlan plan;

    [MenuItem("XLua Demo/AssetBundle Pipeline")]
    public static void Open()
    {
        XLuaDemoAssetBundlePipelineWindow window = GetWindow<XLuaDemoAssetBundlePipelineWindow>("AB Pipeline");
        window.minSize = new Vector2(760, 520);
        window.Show();
    }

    private void OnEnable()
    {
        settings = LoadSettings();
    }

    private void OnDisable()
    {
        SaveSettings(settings);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AssetBundle Automation Pipeline", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Builds a dependency graph from AssetDatabase, previews automatic bundle packing, supports manual overrides, incremental rebuild, versioned manifest, and report output.", MessageType.Info);

        settings.analyzeRoots = EditorGUILayout.TextField("Analyze Roots", settings.analyzeRoots);
        settings.remoteRoot = EditorGUILayout.TextField("Remote Root", settings.remoteRoot);
        settings.version = EditorGUILayout.TextField("Version", settings.version);
        settings.largeAssetThresholdKb = EditorGUILayout.IntField("Large Asset KB", Mathf.Max(1, settings.largeAssetThresholdKb));
        settings.enableLargeAssetSplit = EditorGUILayout.Toggle("Split Large Assets", settings.enableLargeAssetSplit);

        DrawOverrides();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze Dependencies", GUILayout.Height(28)))
        {
            AnalyzeAndPlan();
        }

        if (GUILayout.Button("Build Incremental", GUILayout.Height(28)))
        {
            AnalyzeAndPlan();
            XLuaDemoAssetBundlePipeline.BuildIncremental(plan, settings.remoteRoot, settings.version);
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Open Report", GUILayout.Height(28)))
        {
            string path = XLuaDemoAssetBundlePipeline.ReportPath;
            if (File.Exists(path))
            {
                EditorUtility.OpenWithDefaultApp(path);
            }
        }

        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawReport();
        DrawPlan();
        EditorGUILayout.EndScrollView();
    }

    private void DrawOverrides()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Manual Overrides", EditorStyles.boldLabel);

        if (settings.overrides == null)
        {
            settings.overrides = new List<XLuaDemoAssetBundlePipeline.ManualBundleOverride>();
        }

        int removeIndex = -1;
        for (int i = 0; i < settings.overrides.Count; i++)
        {
            XLuaDemoAssetBundlePipeline.ManualBundleOverride item = settings.overrides[i];
            EditorGUILayout.BeginHorizontal();
            item.assetPath = EditorGUILayout.TextField(item.assetPath);
            item.bundleName = EditorGUILayout.TextField(item.bundleName, GUILayout.Width(220));
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                removeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            settings.overrides.RemoveAt(removeIndex);
        }

        if (GUILayout.Button("Add Override"))
        {
            settings.overrides.Add(new XLuaDemoAssetBundlePipeline.ManualBundleOverride
            {
                assetPath = "Assets/XLuaDemoGenerated/ABSource/FestivalActivityPanel.prefab",
                bundleName = "bundles/festivalui"
            });
        }
    }

    private void DrawReport()
    {
        if (report == null)
        {
            return;
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Dependency Report", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Assets", report.nodes.Count.ToString());
        EditorGUILayout.LabelField("Edges", report.edgeCount.ToString());
        EditorGUILayout.LabelField("Cycles", report.cycles.Count.ToString());
        EditorGUILayout.LabelField("Shared/Redundant Candidates", report.redundantResources.Count.ToString());

        foreach (XLuaDemoAssetBundlePipeline.DependencyCycle cycle in report.cycles.Take(6))
        {
            EditorGUILayout.HelpBox(string.Join(" -> ", cycle.assets.ToArray()), MessageType.Warning);
        }

        foreach (XLuaDemoAssetBundlePipeline.RedundantResource item in report.redundantResources.Take(8))
        {
            EditorGUILayout.LabelField(item.assetPath, "referenced by " + item.referrers.Count + " assets");
        }
    }

    private void DrawPlan()
    {
        if (plan == null)
        {
            return;
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Bundle Plan", EditorStyles.boldLabel);
        foreach (XLuaDemoAssetBundlePipeline.BundleBuildEntry entry in plan.bundles)
        {
            EditorGUILayout.LabelField(entry.bundleName, entry.assets.Count + " assets, " + EditorUtility.FormatBytes(entry.totalSize));
        }
    }

    private void AnalyzeAndPlan()
    {
        SaveSettings(settings);
        string[] roots = SplitRoots(settings.analyzeRoots);
        report = XLuaDemoAssetBundlePipeline.AnalyzeDependencies(roots);
        plan = XLuaDemoAssetBundlePipeline.CreateBundlePlan(
            report,
            roots,
            settings.overrides,
            settings.enableLargeAssetSplit,
            settings.largeAssetThresholdKb * 1024L);
        XLuaDemoAssetBundlePipeline.WriteDependencyReport(report, plan);
        Debug.Log("XLua demo dependency report written to: " + XLuaDemoAssetBundlePipeline.ReportPath);
    }

    private static string[] SplitRoots(string roots)
    {
        return (roots ?? string.Empty)
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().Replace("\\", "/"))
            .Where(item => !string.IsNullOrEmpty(item))
            .ToArray();
    }

    private static PipelineWindowSettings LoadSettings()
    {
        string json = EditorPrefs.GetString(SettingsKey, string.Empty);
        PipelineWindowSettings loaded = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PipelineWindowSettings>(json);
        if (loaded == null)
        {
            loaded = new PipelineWindowSettings
            {
                analyzeRoots = DefaultAnalyzeRoots,
                remoteRoot = DefaultRemoteRoot,
                version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                enableLargeAssetSplit = true,
                largeAssetThresholdKb = 512,
                overrides = new List<XLuaDemoAssetBundlePipeline.ManualBundleOverride>()
            };
        }

        if (loaded.overrides == null)
        {
            loaded.overrides = new List<XLuaDemoAssetBundlePipeline.ManualBundleOverride>();
        }

        if (string.IsNullOrEmpty(loaded.version))
        {
            loaded.version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        }

        return loaded;
    }

    private static void SaveSettings(PipelineWindowSettings value)
    {
        EditorPrefs.SetString(SettingsKey, JsonUtility.ToJson(value));
    }

    [Serializable]
    private sealed class PipelineWindowSettings
    {
        public string analyzeRoots;
        public string remoteRoot;
        public string version;
        public bool enableLargeAssetSplit;
        public int largeAssetThresholdKb;
        public List<XLuaDemoAssetBundlePipeline.ManualBundleOverride> overrides;
    }
}

public static class XLuaDemoAssetBundlePipeline
{
    public const string GeneratedRoot = "Assets/XLuaDemoGenerated";
    public const string ReportPath = GeneratedRoot + "/Reports/assetbundle_dependency_report.md";

    private const string BuildStatePath = GeneratedRoot + "/assetbundle_build_state.json";
    private const string BuildCacheRoot = GeneratedRoot + "/AssetBundleCache";
    private const string TempBuildRoot = GeneratedRoot + "/AssetBundleTemp";
    private static readonly HashSet<string> IgnoredExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".asmdef", ".dll", ".meta", ".unity", ".js"
    };

    public static DependencyReport AnalyzeDependencies(IEnumerable<string> rootFolders)
    {
        DependencyReport report = new DependencyReport();
        Queue<string> queue = new Queue<string>();
        HashSet<string> queued = new HashSet<string>();

        foreach (string root in rootFolders)
        {
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
            {
                continue;
            }

            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (IsAnalyzableAsset(assetPath) && queued.Add(assetPath))
                {
                    queue.Enqueue(assetPath);
                    report.rootAssets.Add(assetPath);
                }
            }
        }

        while (queue.Count > 0)
        {
            string assetPath = queue.Dequeue();
            DependencyNode node = GetOrCreateNode(report, assetPath);
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            foreach (string dependency in dependencies)
            {
                if (dependency == assetPath || !IsAnalyzableAsset(dependency))
                {
                    continue;
                }

                node.dependencies.Add(dependency);
                report.edgeCount++;
                DependencyNode dependencyNode = GetOrCreateNode(report, dependency);
                dependencyNode.referrers.Add(assetPath);
                if (queued.Add(dependency))
                {
                    queue.Enqueue(dependency);
                }
            }
        }

        report.cycles = DetectCycles(report);
        report.redundantResources = DetectRedundantResources(report);
        return report;
    }

    public static BundleBuildPlan CreateBundlePlan(
        DependencyReport report,
        IEnumerable<string> rootFolders,
        IEnumerable<ManualBundleOverride> overrides,
        bool splitLargeAssets,
        long largeAssetThresholdBytes)
    {
        Dictionary<string, string> overrideMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (overrides != null)
        {
            foreach (ManualBundleOverride item in overrides)
            {
                if (item != null && IsAnalyzableAsset(item.assetPath) && !string.IsNullOrEmpty(item.bundleName))
                {
                    overrideMap[item.assetPath.Replace("\\", "/")] = NormalizeBundleName(item.bundleName);
                }
            }
        }

        HashSet<string> roots = new HashSet<string>((rootFolders ?? Array.Empty<string>()).Select(item => item.Replace("\\", "/")), StringComparer.OrdinalIgnoreCase);
        HashSet<string> rootAssets = new HashSet<string>(report.rootAssets, StringComparer.OrdinalIgnoreCase);
        HashSet<string> sharedCandidates = new HashSet<string>(report.redundantResources.Select(item => item.assetPath), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BundleBuildEntry> bundleMap = new Dictionary<string, BundleBuildEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (DependencyNode node in report.nodes)
        {
            bool shouldPack = rootAssets.Contains(node.assetPath) || sharedCandidates.Contains(node.assetPath) || overrideMap.ContainsKey(node.assetPath);
            if (!shouldPack)
            {
                continue;
            }

            string bundleName;
            if (!overrideMap.TryGetValue(node.assetPath, out bundleName))
            {
                bundleName = sharedCandidates.Contains(node.assetPath)
                    ? "bundles/shared/" + StableAssetName(node.assetPath)
                    : InferBundleName(node.assetPath, roots, splitLargeAssets, largeAssetThresholdBytes);
            }

            BundleBuildEntry entry;
            if (!bundleMap.TryGetValue(bundleName, out entry))
            {
                entry = new BundleBuildEntry { bundleName = bundleName };
                bundleMap.Add(bundleName, entry);
            }

            if (!entry.assets.Contains(node.assetPath))
            {
                entry.assets.Add(node.assetPath);
                entry.totalSize += node.size;
            }
        }

        BundleBuildPlan plan = new BundleBuildPlan();
        plan.bundles = bundleMap.Values.OrderBy(item => item.bundleName, StringComparer.OrdinalIgnoreCase).ToList();
        plan.assetToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (BundleBuildEntry entry in plan.bundles)
        {
            entry.assets.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string asset in entry.assets)
            {
                plan.assetToBundle[asset] = entry.bundleName;
            }
        }

        foreach (BundleBuildEntry entry in plan.bundles)
        {
            HashSet<string> dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string asset in entry.assets)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(asset, true))
                {
                    string dependencyBundle;
                    if (dependency != asset && plan.assetToBundle.TryGetValue(dependency, out dependencyBundle) && dependencyBundle != entry.bundleName)
                    {
                        dependencies.Add(dependencyBundle);
                    }
                }
            }

            entry.dependencies = dependencies.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return plan;
    }

    public static BuildResult BuildIncremental(BundleBuildPlan plan, string remoteRoot, string version)
    {
        if (plan == null || plan.bundles == null || plan.bundles.Count == 0)
        {
            throw new InvalidOperationException("No bundle plan available. Run dependency analysis first.");
        }

        Directory.CreateDirectory(GeneratedRoot);
        Directory.CreateDirectory(remoteRoot);

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        BuildState oldState = LoadBuildState();
        BuildState newState = new BuildState
        {
            buildTarget = target.ToString(),
            bundles = new List<BundleState>()
        };

        string cacheRoot = Path.Combine(BuildCacheRoot, target.ToString()).Replace("\\", "/");
        string tempRoot = Path.Combine(TempBuildRoot, target.ToString()).Replace("\\", "/");
        string fullCacheRoot = ToFullPath(cacheRoot);
        string fullTempRoot = ToFullPath(tempRoot);
        string fullRemoteRoot = ToFullPath(remoteRoot);

        Directory.CreateDirectory(fullCacheRoot);
        if (Directory.Exists(fullTempRoot))
        {
            Directory.Delete(fullTempRoot, true);
        }

        Directory.CreateDirectory(fullTempRoot);

        List<AssetBundleBuild> allBuilds = new List<AssetBundleBuild>();
        BuildResult result = new BuildResult();
        result.changedBundles = new List<string>();
        result.skippedBundles = new List<string>();

        foreach (BundleBuildEntry entry in plan.bundles)
        {
            string inputHash = CalculateInputHash(entry.assets);
            string cachePath = Path.Combine(fullCacheRoot, entry.bundleName.Replace("/", Path.DirectorySeparatorChar.ToString()));
            BundleState oldBundle = oldState.FindBundle(entry.bundleName);
            bool changed = oldBundle == null ||
                oldBundle.inputHash != inputHash ||
                !File.Exists(cachePath) ||
                !string.Equals(oldState.buildTarget, target.ToString(), StringComparison.OrdinalIgnoreCase);

            newState.bundles.Add(new BundleState
            {
                bundleName = entry.bundleName,
                inputHash = inputHash,
                assets = entry.assets.ToList(),
                dependencies = entry.dependencies.ToList()
            });

            if (changed)
            {
                result.changedBundles.Add(entry.bundleName);
            }
            else
            {
                result.skippedBundles.Add(entry.bundleName);
            }

            allBuilds.Add(new AssetBundleBuild
            {
                assetBundleName = entry.bundleName,
                assetNames = entry.assets.ToArray()
            });
        }

        if (result.changedBundles.Count > 0)
        {
            BuildPipeline.BuildAssetBundles(
                fullTempRoot,
                allBuilds.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression,
                target);

            foreach (AssetBundleBuild build in allBuilds)
            {
                CopyBundleArtifact(fullTempRoot, fullCacheRoot, build.assetBundleName);
                CopyBundleArtifact(fullTempRoot, fullCacheRoot, build.assetBundleName + ".manifest");
            }
        }

        PruneCacheArtifacts(fullCacheRoot, plan);
        MirrorDirectory(fullCacheRoot, fullRemoteRoot, "bundles");
        WriteBuildState(newState);
        WriteManifest(remoteRoot, plan, version, target.ToString());

        result.version = version;
        Debug.Log(string.Format("AssetBundle incremental build finished. Changed={0}, skipped={1}, manifest={2}",
            result.changedBundles.Count,
            result.skippedBundles.Count,
            Path.Combine(remoteRoot, "manifest.json")));
        return result;
    }

    public static void WriteDependencyReport(DependencyReport report, BundleBuildPlan plan)
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# XLua Demo AssetBundle Dependency Report");
        builder.AppendLine();
        builder.AppendLine("- Generated UTC: " + DateTime.UtcNow.ToString("u"));
        builder.AppendLine("- Assets: " + report.nodes.Count);
        builder.AppendLine("- Edges: " + report.edgeCount);
        builder.AppendLine("- Cycles: " + report.cycles.Count);
        builder.AppendLine("- Shared/redundant candidates: " + report.redundantResources.Count);
        builder.AppendLine();
        builder.AppendLine("## Cycles");
        if (report.cycles.Count == 0)
        {
            builder.AppendLine("No cycles detected.");
        }
        else
        {
            foreach (DependencyCycle cycle in report.cycles)
            {
                builder.AppendLine("- " + string.Join(" -> ", cycle.assets.ToArray()));
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Redundant Resources");
        if (report.redundantResources.Count == 0)
        {
            builder.AppendLine("No redundant shared dependency candidates detected.");
        }
        else
        {
            foreach (RedundantResource item in report.redundantResources)
            {
                builder.AppendLine("- " + item.assetPath + " referenced by " + item.referrers.Count + " assets");
                foreach (string referrer in item.referrers.Take(6))
                {
                    builder.AppendLine("  - " + referrer);
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Bundle Plan");
        if (plan != null)
        {
            foreach (BundleBuildEntry entry in plan.bundles)
            {
                builder.AppendLine("- " + entry.bundleName + " (" + entry.assets.Count + " assets, " + EditorUtility.FormatBytes(entry.totalSize) + ")");
                foreach (string dependency in entry.dependencies)
                {
                    builder.AppendLine("  - depends on: " + dependency);
                }
            }
        }

        File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(ReportPath);
    }

    public static ResourceManifest WriteManifest(string remoteRoot, BundleBuildPlan plan, string version, string buildTarget)
    {
        ResourceManifest manifest = new ResourceManifest
        {
            version = string.IsNullOrEmpty(version) ? DateTime.UtcNow.ToString("yyyyMMddHHmmss") : version,
            buildTarget = buildTarget,
            generatedAtUtc = DateTime.UtcNow.ToString("u"),
            files = CollectManifestFiles(remoteRoot).ToArray(),
            bundles = CollectBundleRecords(remoteRoot, plan).ToArray()
        };

        File.WriteAllText(Path.Combine(remoteRoot, "manifest.json"), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
        return manifest;
    }

    private static List<ResourceFile> CollectManifestFiles(string remoteRoot)
    {
        List<ResourceFile> files = new List<ResourceFile>();
        string fullRoot = ToFullPath(remoteRoot);
        if (!Directory.Exists(fullRoot))
        {
            return files;
        }

        foreach (string path in Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = ToRelativePath(fullRoot, path);
            FileInfo info = new FileInfo(path);
            files.Add(new ResourceFile
            {
                path = relative,
                url = relative,
                md5 = CalculateFileMd5(path),
                size = info.Length
            });
        }

        return files.OrderBy(item => item.path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<AssetBundleRecord> CollectBundleRecords(string remoteRoot, BundleBuildPlan plan)
    {
        List<AssetBundleRecord> records = new List<AssetBundleRecord>();
        if (plan == null)
        {
            return records;
        }

        string fullRoot = ToFullPath(remoteRoot);
        foreach (BundleBuildEntry entry in plan.bundles)
        {
            string bundlePath = Path.Combine(fullRoot, entry.bundleName.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(bundlePath))
            {
                continue;
            }

            FileInfo info = new FileInfo(bundlePath);
            records.Add(new AssetBundleRecord
            {
                name = entry.bundleName,
                md5 = CalculateFileMd5(bundlePath),
                hash = CalculateInputHash(entry.assets),
                size = info.Length,
                assets = entry.assets.ToArray(),
                dependencies = entry.dependencies.ToArray()
            });
        }

        return records;
    }

    private static List<DependencyCycle> DetectCycles(DependencyReport report)
    {
        Dictionary<string, int> state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Stack<string> stack = new Stack<string>();
        List<DependencyCycle> cycles = new List<DependencyCycle>();

        foreach (DependencyNode node in report.nodes)
        {
            Visit(node.assetPath, report, state, stack, cycles);
        }

        return cycles;
    }

    private static void Visit(string assetPath, DependencyReport report, Dictionary<string, int> state, Stack<string> stack, List<DependencyCycle> cycles)
    {
        int currentState;
        if (state.TryGetValue(assetPath, out currentState))
        {
            return;
        }

        state[assetPath] = 1;
        stack.Push(assetPath);

        DependencyNode node = report.FindNode(assetPath);
        if (node != null)
        {
            foreach (string dependency in node.dependencies)
            {
                int dependencyState;
                if (!state.TryGetValue(dependency, out dependencyState))
                {
                    Visit(dependency, report, state, stack, cycles);
                }
                else if (dependencyState == 1)
                {
                    List<string> cycle = new List<string>();
                    foreach (string item in stack)
                    {
                        cycle.Add(item);
                        if (item == dependency)
                        {
                            break;
                        }
                    }

                    cycle.Reverse();
                    cycle.Add(dependency);
                    string signature = string.Join("|", cycle.ToArray());
                    if (!cycles.Any(item => string.Join("|", item.assets.ToArray()) == signature))
                    {
                        cycles.Add(new DependencyCycle { assets = cycle });
                    }
                }
            }
        }

        stack.Pop();
        state[assetPath] = 2;
    }

    private static List<RedundantResource> DetectRedundantResources(DependencyReport report)
    {
        HashSet<string> roots = new HashSet<string>(report.rootAssets, StringComparer.OrdinalIgnoreCase);
        return report.nodes
            .Where(item => !roots.Contains(item.assetPath) && item.referrers.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(item => new RedundantResource
            {
                assetPath = item.assetPath,
                referrers = item.referrers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList(),
                size = item.size
            })
            .OrderByDescending(item => item.referrers.Count)
            .ThenByDescending(item => item.size)
            .ToList();
    }

    private static DependencyNode GetOrCreateNode(DependencyReport report, string assetPath)
    {
        DependencyNode node = report.FindNode(assetPath);
        if (node != null)
        {
            return node;
        }

        node = new DependencyNode
        {
            assetPath = assetPath,
            guid = AssetDatabase.AssetPathToGUID(assetPath),
            size = GetAssetFileSize(assetPath),
            dependencies = new List<string>(),
            referrers = new List<string>(),
            labels = GetAssetLabels(assetPath)
        };
        report.nodes.Add(node);
        return node;
    }

    private static string InferBundleName(string assetPath, HashSet<string> roots, bool splitLargeAssets, long largeAssetThresholdBytes)
    {
        long size = GetAssetFileSize(assetPath);
        if (splitLargeAssets && size >= largeAssetThresholdBytes)
        {
            return "bundles/large/" + StableAssetName(assetPath);
        }

        string labelBundle = InferBundleNameFromLabels(assetPath);
        if (!string.IsNullOrEmpty(labelBundle))
        {
            return labelBundle;
        }

        string matchedRoot = roots
            .Where(root => assetPath.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(root => root.Length)
            .FirstOrDefault();
        string relative = string.IsNullOrEmpty(matchedRoot)
            ? assetPath.Substring("Assets/".Length)
            : assetPath.Substring(matchedRoot.TrimEnd('/').Length).TrimStart('/');
        string directory = Path.GetDirectoryName(relative);
        if (string.IsNullOrEmpty(directory))
        {
            directory = "root";
        }

        return NormalizeBundleName("bundles/dir/" + directory.ToLowerInvariant().Replace("\\", "/"));
    }

    private static string InferBundleNameFromLabels(string assetPath)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset == null)
        {
            return null;
        }

        string label = AssetDatabase.GetLabels(asset)
            .FirstOrDefault(item => item.StartsWith("ab_", StringComparison.OrdinalIgnoreCase) || item.StartsWith("bundle_", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(label))
        {
            return null;
        }

        label = label.Replace("bundle_", string.Empty).Replace("ab_", string.Empty);
        return NormalizeBundleName("bundles/label/" + label);
    }

    private static string[] GetAssetLabels(string assetPath)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        return asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
    }

    private static bool IsAnalyzableAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return false;
        }

        return !IgnoredExtensions.Contains(Path.GetExtension(assetPath));
    }

    private static string NormalizeBundleName(string value)
    {
        return value.Trim().Replace("\\", "/").TrimStart('/').ToLowerInvariant();
    }

    private static string StableAssetName(string assetPath)
    {
        string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrEmpty(guid) && guid.Length > 8)
        {
            name += "_" + guid.Substring(0, 8);
        }

        return name;
    }

    private static long GetAssetFileSize(string assetPath)
    {
        string fullPath = ToFullPath(assetPath);
        return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0L;
    }

    private static string CalculateInputHash(IEnumerable<string> assets)
    {
        StringBuilder input = new StringBuilder();
        foreach (string dependency in assets.SelectMany(asset => AssetDatabase.GetDependencies(asset, true)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            input.Append(dependency).Append('|');
            input.Append(AssetDatabase.AssetPathToGUID(dependency)).Append('|');
            input.Append(AssetDatabase.GetAssetDependencyHash(dependency).ToString()).AppendLine();
        }

        return CalculateStringMd5(input.ToString());
    }

    private static void CopyBundleArtifact(string sourceRoot, string targetRoot, string relativePath)
    {
        string source = Path.Combine(sourceRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        string target = Path.Combine(targetRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!File.Exists(source))
        {
            return;
        }

        string directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(source, target, true);
    }

    private static void MirrorDirectory(string sourceRoot, string targetRoot, string subDirectory)
    {
        string source = Path.Combine(sourceRoot, subDirectory);
        string target = Path.Combine(targetRoot, subDirectory);
        if (!Directory.Exists(source))
        {
            return;
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = ToRelativePath(source, file);
            string targetFile = Path.Combine(target, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
            File.Copy(file, targetFile, true);
        }
    }

    private static void PruneCacheArtifacts(string cacheRoot, BundleBuildPlan plan)
    {
        string bundlesRoot = Path.Combine(cacheRoot, "bundles");
        if (!Directory.Exists(bundlesRoot))
        {
            return;
        }

        HashSet<string> keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (BundleBuildEntry entry in plan.bundles)
        {
            keep.Add(entry.bundleName.Replace("\\", "/"));
            keep.Add(entry.bundleName.Replace("\\", "/") + ".manifest");
        }

        foreach (string file in Directory.GetFiles(bundlesRoot, "*", SearchOption.AllDirectories))
        {
            string relative = "bundles/" + ToRelativePath(bundlesRoot, file);
            if (!keep.Contains(relative.Replace("\\", "/")))
            {
                File.Delete(file);
            }
        }

        foreach (string directory in Directory.GetDirectories(bundlesRoot, "*", SearchOption.AllDirectories).OrderByDescending(item => item.Length))
        {
            if (Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
    }

    private static BuildState LoadBuildState()
    {
        if (!File.Exists(BuildStatePath))
        {
            return new BuildState { bundles = new List<BundleState>() };
        }

        BuildState state = JsonUtility.FromJson<BuildState>(File.ReadAllText(BuildStatePath));
        if (state == null)
        {
            state = new BuildState();
        }

        if (state.bundles == null)
        {
            state.bundles = new List<BundleState>();
        }

        return state;
    }

    private static void WriteBuildState(BuildState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BuildStatePath));
        File.WriteAllText(BuildStatePath, JsonUtility.ToJson(state, true), Encoding.UTF8);
    }

    private static string ToFullPath(string projectRelativePath)
    {
        if (Path.IsPathRooted(projectRelativePath))
        {
            return projectRelativePath;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath));
    }

    private static string ToRelativePath(string root, string path)
    {
        Uri rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        Uri pathUri = new Uri(path);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace("\\", "/");
    }

    private static string CalculateFileMd5(string path)
    {
        using (MD5 md5 = MD5.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = md5.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    private static string CalculateStringMd5(string text)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    [Serializable]
    public sealed class ManualBundleOverride
    {
        public string assetPath;
        public string bundleName;
    }

    public sealed class DependencyReport
    {
        public List<string> rootAssets = new List<string>();
        public List<DependencyNode> nodes = new List<DependencyNode>();
        public List<DependencyCycle> cycles = new List<DependencyCycle>();
        public List<RedundantResource> redundantResources = new List<RedundantResource>();
        public int edgeCount;

        public DependencyNode FindNode(string assetPath)
        {
            return nodes.FirstOrDefault(item => string.Equals(item.assetPath, assetPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class DependencyNode
    {
        public string assetPath;
        public string guid;
        public long size;
        public string[] labels;
        public List<string> dependencies;
        public List<string> referrers;
    }

    public sealed class DependencyCycle
    {
        public List<string> assets = new List<string>();
    }

    public sealed class RedundantResource
    {
        public string assetPath;
        public long size;
        public List<string> referrers = new List<string>();
    }

    public sealed class BundleBuildPlan
    {
        public List<BundleBuildEntry> bundles = new List<BundleBuildEntry>();
        public Dictionary<string, string> assetToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class BundleBuildEntry
    {
        public string bundleName;
        public long totalSize;
        public List<string> assets = new List<string>();
        public List<string> dependencies = new List<string>();
    }

    public sealed class BuildResult
    {
        public string version;
        public List<string> changedBundles = new List<string>();
        public List<string> skippedBundles = new List<string>();
    }

    [Serializable]
    private sealed class BuildState
    {
        public string buildTarget;
        public List<BundleState> bundles = new List<BundleState>();

        public BundleState FindBundle(string bundleName)
        {
            return bundles == null ? null : bundles.FirstOrDefault(item => string.Equals(item.bundleName, bundleName, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    private sealed class BundleState
    {
        public string bundleName;
        public string inputHash;
        public List<string> assets = new List<string>();
        public List<string> dependencies = new List<string>();
    }
}
