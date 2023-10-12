using AssetRipper.AnalyzeUnityPackages.Analyzer;
using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;

namespace AssetRipper.AnalyzeUnityPackages.Comparer;

public static class PackageVersionComparer
{
    private static readonly Dictionary<string, string> packageDllNameById = new()
    {
        { "Unity.Addressables.dll", "com.unity.addressables" },
        //{ "Unity.Burst.dll", "com.unity.burst" },
        { "Unity.Collections.dll", "com.unity.collections" },
        { "Unity.Mathematics.dll", "com.unity.mathematics" },
        { "Unity.Recorder.dll", "com.unity.recorder" },
        { "Unity.ResourceManager.dll", "com.unity.resourcemanager" },
        { "Unity.ScriptableBuildPipeline.dll", "com.unity.scriptablebuildpipeline" },
        { "Unity.TextMeshPro.dll", "com.unity.textmeshpro" },
        { "Unity.Timeline.dll", "com.unity.timeline" },
    };

    public static bool CanCompareDll(string dllFile, out string packageId) => packageDllNameById.TryGetValue(Path.GetFileName(dllFile), out packageId);

    public static Dictionary<PackageVersion, double>? CompareGamePackage(string dllFile, ICompareStrategy strategy, UnityVersion unityVersion)
    {
        string dllFileName = Path.GetFileName(dllFile);
        if (!packageDllNameById.TryGetValue(dllFileName, out string packageId))
        {
            Logger.Error($"Could not find packageId for given dll: {dllFileName}");
            return null;
        }

        AnalyzeData? assemblyAnalyzeData = AssemblyAnalyzer.AnalyzeAssembly(packageId, dllFile);

        if (assemblyAnalyzeData == null)
        {
            Logger.Error($"Could not analyze given dll: {dllFileName}");
            return null;
        }

        if (!PackageAnalyzer.HasAnyAnalyzedPackages(packageId))
        {
            Logger.Error($"Could not find analyzed package data for {packageId}. Run an package analysis first.");
            return null;
        }

        List<AnalyzeData> targetAnalyzeDatum = PackageAnalyzer.GetAnalyzeResults(packageId, unityVersion);

        Dictionary<PackageVersion, double> compareResults = new();
        foreach (AnalyzeData targetData in targetAnalyzeDatum)
        {
            compareResults[targetData.Version] = strategy.CompareAnalyzeData(assemblyAnalyzeData, targetData);
        }

        return compareResults;
    }
}
