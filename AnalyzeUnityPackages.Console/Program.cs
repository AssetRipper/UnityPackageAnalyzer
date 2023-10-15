// See https://aka.ms/new-console-template for more information

using AssetRipper.AnalyzeUnityPackages;
using AssetRipper.AnalyzeUnityPackages.Comparer;
using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;
using System.Text;

Logger.Init();

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

UnityVersion? gameVersion = null;
if (args.Length > 2)
{
	try
	{
		gameVersion = UnityVersion.Parse(args[2]);
	}
	catch (Exception _)
	{
		// ignored
	}
}

while (gameVersion == null)
{
	Logger.Info("Please enter the game unity version");
	string input = Console.ReadLine() ?? string.Empty;
	Console.WriteLine();

	try
	{
		gameVersion = UnityVersion.Parse(input);
	}
	catch (Exception _)
	{
		// ignored
	}
}

Logger.Info($"Strategy: {strategy.GetType().Name}");
Logger.Info($"Unity Version: {gameVersion}\n");

CancellationToken ct = new();
CompareResults analyzeResults = await AnalyzeUnityPackages.CompareGameAssembliesAsync(managedPath, strategy, gameVersion.Value, ct);

StringBuilder sb = new("\n\nAnalyze Results:\n");
foreach ((string packageId, PackageCompareResult packageResult) in analyzeResults.PackageResults)
{
	sb.Append($"{packageId,35}: ");
	foreach (KeyValuePair<PackageVersion, double> pair in packageResult.ProbabilityByVersion.OrderByDescending(entry => entry.Value).ThenByDescending(entry => entry.Key).Take(5))
	{
		sb.Append($" {pair.Key,18} {$"({pair.Value * 100:#0.000} %)",11} -> ");
	}

	sb.AppendLine();
}

Logger.Info(sb.ToString());
Logger.Info("Finished! Press any Key do exit.");
Console.ReadKey();
