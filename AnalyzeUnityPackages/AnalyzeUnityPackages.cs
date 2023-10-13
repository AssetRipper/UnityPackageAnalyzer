using AssetRipper.AnalyzeUnityPackages.Analyzer;
using AssetRipper.AnalyzeUnityPackages.Comparer;
using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.PackageDownloader;
using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;

namespace AssetRipper.AnalyzeUnityPackages;

public static class AnalyzeUnityPackages
{
	private static readonly Dictionary<string, string> specialPackageDllNamesById = new()
	{
		{ "Unity.Formats.Fbx.Runtime", "com.unity.formats.fbx" },
		{ "Unity.InternalAPIEngineBridge.001", string.Empty },
		{ "Unity.ResourceManager", string.Empty }, // Is implicit dependency of Unity.Addressables
	};

	public static bool CanCompareDll(string dllFile, out string packageId)
	{
		string fileName = Path.GetFileNameWithoutExtension(dllFile);

		if (!fileName.StartsWith("Unity."))
		{
			packageId = string.Empty;
			return false;
		}

		if (specialPackageDllNamesById.TryGetValue(fileName, out packageId))
		{
			return !string.IsNullOrEmpty(packageId);
		}

		if (fileName.Count(f => f == '.') > 1) // Sub-Packages
		{
			packageId = string.Empty;
			return false;
		}

		packageId = "com." + fileName.ToLowerInvariant();
		return true;
	}

	public static async Task<CompareResults> DownloadMissingPackagesAndAnalyzeAsync(string managedPath, ICompareStrategy strategy, UnityVersion gameVersion, CancellationToken ct)
	{
		CompareResults analyzeResults = new();
		foreach (string dllFile in Directory.EnumerateFiles(managedPath, "Unity.*.dll"))
		{
			if (!CanCompareDll(dllFile, out string packageId))
			{
				continue;
			}

			await DownloadAndAnalyzeMissingPackagesAsync(dllFile, gameVersion, ct).ContinueWith(_ =>
			{
				PackageCompareResult? result = AnalyzePackageDll(dllFile, strategy, gameVersion);
				if (result != null)
				{
					analyzeResults.PackageResults.Add(packageId, result);
				}
			}, ct);
		}

		return analyzeResults;
	}

	public static async Task DownloadAndAnalyzeMissingPackagesAsync(string dllFile, UnityVersion gameVersion, CancellationToken ct)
	{
		if (!CanCompareDll(dllFile, out string packageId))
		{
			return;
		}

		if (packageId == "com.unity.addressables") // com.unity.addressables version analyzing doesn't need source files
		{
			return;
		}

		Logger.Info($"Downloading and analyzing missing unity packages of {packageId}");

		PackageDomainInfo domainInfo = await DownloadManager.DownloadVersionListAsync(packageId, ct);

		List<Task> analyzeTasks = new List<Task>();
		foreach ((string version, PackageInfo packageInfo) in domainInfo.Versions)
		{
			if (PackageAnalyzer.HasAnalyzedPackage(packageId, version))
			{
				return;
			}


			UnityVersion minUnityVersion = string.IsNullOrEmpty(packageInfo.MinUnity) ? UnityVersion.MinVersion : UnityVersion.Parse(packageInfo.MinUnity);
			if (minUnityVersion <= gameVersion)
			{
				await DownloadManager.DownloadAndExtractPackageAsync(packageInfo.DistTarball, packageId, version, ct).ContinueWith(_ =>
				{
					analyzeTasks.Add(PackageAnalyzer.AnalyzePackageAsync(packageId, new PackageVersion(version), minUnityVersion, ct));
				}, ct);
			}
		}

		await Task.WhenAll(analyzeTasks);
	}

	public static PackageCompareResult? AnalyzePackageDll(string dllFile, ICompareStrategy strategy, UnityVersion gameVersion)
	{
		string dllFileName = Path.GetFileName(dllFile);

		switch (dllFileName)
		{
			case "Unity.Addressables.dll":
				return AnalyzeAddressablesPackage(dllFile);
			case "Unity.Burst.dll":
				return AnalyzeBurstPackage(dllFile, strategy, gameVersion);
		}

		if (!CanCompareDll(dllFileName, out string packageId))
		{
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

		return AnalyzePackage(packageId, assemblyAnalyzeData, strategy, gameVersion);
	}

	private static PackageCompareResult AnalyzePackage(string packageId, AnalyzeData assemblyAnalyzeData, ICompareStrategy strategy, UnityVersion gameVersion)
	{
		List<AnalyzeData> targetAnalyzeDatum = PackageAnalyzer.GetAnalyzeResults(packageId, gameVersion);

		PackageCompareResult compareResults = new();
		foreach (AnalyzeData targetData in targetAnalyzeDatum)
		{
			compareResults.ProbabilityByVersion[targetData.Version] = strategy.CompareAnalyzeData(assemblyAnalyzeData, targetData);
		}

		return compareResults;
	}

	private static PackageCompareResult? AnalyzeAddressablesPackage(string dllFile)
	{
		DirectoryInfo dllDirectory = new(dllFile);
		string configFilePath = Path.Combine(dllDirectory.Parent!.Parent!.FullName, "StreamingAssets", "aa", "settings.json");
		if (!File.Exists(configFilePath))
		{
			Logger.Error($"Could locate addressables config at {configFilePath}");
			return null;
		}

		using FileStream stream = File.OpenRead(configFilePath);
		JsonNode? json = JsonNode.Parse(stream);
		if (json == null)
		{
			throw new SerializationException($"Failed to parse {configFilePath}");
		}

		JsonNode? versionNode = json["m_AddressablesVersion"];
		if (versionNode == null)
		{
			throw new SerializationException($"Property \"m_AddressablesVersion\" was not found in {configFilePath}");
		}

		PackageVersion version = PackageVersion.Parse(versionNode.GetValue<string>());
		return new PackageCompareResult(version, 1d);
	}

	private static PackageCompareResult? AnalyzeBurstPackage(string dllFile, ICompareStrategy strategy, UnityVersion gameVersion)
	{
		const string packageId = "com.unity.burst";

		AnalyzeData? assemblyAnalyzeData = AssemblyAnalyzer.AnalyzeAssembly(packageId, dllFile);
		if (assemblyAnalyzeData == null)
		{
			Logger.Error("Could not analyze Unity.Burst.dll");
			return null;
		}

		if (!PackageAnalyzer.HasAnyAnalyzedPackages(packageId))
		{
			Logger.Error($"Could not find analyzed package data for {packageId}. Run an package analysis first.");
			return null;
		}

		List<AnalyzeData> targetAnalyzeDatum = PackageAnalyzer.GetAnalyzeResults(packageId, gameVersion);

		string managedFolder = Path.GetDirectoryName(dllFile)!;
		PackageCompareResult compareResults = new();
		foreach (AnalyzeData targetData in targetAnalyzeDatum)
		{
			double dllProbabilities = strategy.CompareAnalyzeData(assemblyAnalyzeData, targetData);
			double dllCount = 1;

			string packageFolderFolder = DownloadManager.GetExtractPath(packageId, targetData.Version.ToString());

			CompareAssemblyFiles(managedFolder, packageFolderFolder, "Unity.Burst.Cecil.dll", ref dllProbabilities, ref dllCount);
			CompareAssemblyFiles(managedFolder, packageFolderFolder, "Unity.Burst.Cecil.Mdb.dll", ref dllProbabilities, ref dllCount);
			CompareAssemblyFiles(managedFolder, packageFolderFolder, "Unity.Burst.Cecil.Pdb.dll", ref dllProbabilities, ref dllCount);
			CompareAssemblyFiles(managedFolder, packageFolderFolder, "Unity.Burst.Cecil.Rocks.dll", ref dllProbabilities, ref dllCount);
			CompareAssemblyFiles(managedFolder, packageFolderFolder, "Unity.Burst.Unsafe.dll", ref dllProbabilities, ref dllCount);

			double dllProbability = dllProbabilities / dllCount;
			if (dllProbability > 1)
			{
				throw new ArithmeticException("Compare value over 1");
			}

			compareResults.ProbabilityByVersion[targetData.Version] = dllProbability;
		}

		return compareResults;
	}

	private static void CompareAssemblyFiles(string srcFolder, string targetFolder, string fileName, ref double dllProbabilities, ref double dllCount)
	{
		string srcFilePath = Path.Combine(srcFolder, fileName);
		if (File.Exists(srcFilePath))
		{
			string? packageCecilDll = Directory.EnumerateFiles(targetFolder, fileName, SearchOption.AllDirectories).FirstOrDefault();
			if (packageCecilDll != null)
			{
				dllProbabilities += CompareFileBytes(srcFilePath, packageCecilDll);
			}

			dllCount++;
		}
	}

	private static double CompareFileBytes(string fileName1, string fileName2)
	{
		long file1ByteValues = 0;
		using FileStream fs1 = File.OpenRead(fileName1);
		using (BufferedStream bs1 = new(fs1))
		{
			long length = bs1.Length;
			while (length > 0)
			{
				file1ByteValues += bs1.ReadByte();
				length--;
			}
		}

		long file2ByteValues = 0;
		using FileStream fs2 = File.OpenRead(fileName2);
		using (BufferedStream bs2 = new(fs2))
		{
			long length = bs2.Length;
			while (length > 0)
			{
				file2ByteValues += bs2.ReadByte();
				length--;
			}
		}

		if (file1ByteValues == 0)
		{
			throw new ArithmeticException($"Accumulated bytes of {fileName1} is zero. Exiting before dividing by zero");
		}

		return 1 - (((double)Math.Abs(file1ByteValues - file2ByteValues)) / file1ByteValues);
	}
}
