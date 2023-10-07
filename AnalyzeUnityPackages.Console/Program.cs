// See https://aka.ms/new-console-template for more information

using System.Text;
using AssetRipper.AnalyzeUnityPackages.Analyzer;
using AssetRipper.AnalyzeUnityPackages.Comparer;
using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.PackageDownloader;

Logger.Init();
Logger.Info("Hello, World!");

string gameExePath = string.Empty;
string managedPath = string.Empty;
if (args.Length > 0)
{
    gameExePath = args[0];
    managedPath = Path.Combine(Path.GetDirectoryName(gameExePath) ?? string.Empty, Path.GetFileNameWithoutExtension(gameExePath) + "_Data", "Managed");
}

while (!File.Exists(gameExePath) && !Directory.Exists(managedPath))
{
    Logger.Info("Please enter a valid game exe path to analyze: ");
    gameExePath = Console.ReadLine() ?? string.Empty;
    managedPath = Path.Combine(Path.GetDirectoryName(gameExePath) ?? string.Empty, Path.GetFileNameWithoutExtension(gameExePath) + "_Data", "Managed");
    Console.WriteLine();
}

ICompareStrategy? strategy = null;
if (args.Length > 1)
{
    strategy = args[1] switch
    {
        "BalancedCompareStrategy" => new BalancedCompareStrategy(),
        "EqualCompareStrategy" => new BalancedCompareStrategy()
    };
}

while (strategy == null)
{
    Logger.Info("Choose an analyze strategy: \n1: BalancedCompareStrategy\n2: EqualCompareStrategy");
    string input = Console.ReadLine() ?? string.Empty;
    Console.WriteLine();

    strategy = input switch
    {
        "1" => new BalancedCompareStrategy(),
        "2" => new EqualCompareStrategy(),
        _ => null
    };
}

Version? unityVersion = null;
if (args.Length > 2)
{
    Version.TryParse(args[2], out unityVersion);
}

while (unityVersion == null)
{
    Logger.Info("Please enter the game unity version");
    string input = Console.ReadLine() ?? string.Empty;
    Console.WriteLine();
    Version.TryParse(input, out unityVersion);
}
Logger.Info($"Unity Version: {unityVersion}");

CancellationToken ct = new();
Dictionary<string, Dictionary<PackageVersion, double>> analyzeResults = new();
foreach (string dllFile in Directory.EnumerateFiles(managedPath, "Unity.*.dll"))
{
    if (PackageVersionComparer.CanCompareDll(dllFile, out string packageId))
    {
        Logger.Info($"Downloading and analyzing missing Unity packages of {packageId}");

        PackageDomainInfo domainInfo = await DownloadManager.DownloadVersionListAsync(packageId, ct);

        await Parallel.ForEachAsync(domainInfo.versions, ct, async (pair, ct2) =>
        {
            try
            {
                string version = pair.Key;
                Version minUnityVersion = string.IsNullOrEmpty(pair.Value.unity) ? new Version(0,0,0,0) : new Version(pair.Value.unity);
                if (!PackageAnalyzer.HasAnalyzedPackage(packageId, version) && minUnityVersion < unityVersion)
                {
                    await DownloadManager.DownloadAndExtractPackageAsync(pair.Value.dist, packageId, version, ct2);
                    await PackageAnalyzer.AnalyzePackageAsync(packageId, new PackageVersion(version), minUnityVersion, ct2);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"An Error occured while downloading or analyzing {packageId}@{pair.Key}");
                throw;
            }

        });

        Logger.Info($"Comparing all analyzed versions to {Path.GetFileName(dllFile)}");
        Dictionary<PackageVersion, double>? analyzeResult = PackageVersionComparer.CompareGamePackage(dllFile, strategy, unityVersion);
        if (analyzeResult != null)
        {
            analyzeResults.Add(packageId, analyzeResult);
        }
    }
}

Logger.Info("\n\nAnalyze Results:");
StringBuilder sb = new();
foreach (KeyValuePair<string, Dictionary<PackageVersion, double>> analyzeResult in analyzeResults)
{
    sb.Clear();
    sb.Append($"{analyzeResult.Key,35}: ");
    foreach (KeyValuePair<PackageVersion, double> pair in analyzeResult.Value.OrderByDescending(entry => entry.Value).ThenByDescending(entry => entry.Key).Take(5))
    {
        sb.Append($" {pair.Key,18} {$"({pair.Value * 100:###.##}%)",9} -> ");
    }

    Logger.Info(sb.ToString());
}
Logger.Info("Finished! Press any Key do exit.");
Console.ReadKey();
