using AssetRipper.AnalyzeUnityPackages.Analyzer;
using AssetRipper.AnalyzeUnityPackages.Comparer;
using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.PackageDownloader;
using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;
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

	public static async Task<CompareResults> CompareGameAssembliesAsync(string managedPath, ICompareStrategy strategy, UnityVersion gameVersion, CancellationToken ct)
	{
		CompareResults analyzeResults = new();
		foreach (string dllFile in Directory.EnumerateFiles(managedPath, "Unity.*.dll"))
		{
			if (!CanCompareDll(dllFile, out string packageId))
			{
				continue;
			}

			await DownloadAndAnalyzeMissingPackagesAsync(packageId, gameVersion, ct);

			PackageCompareResult? result = AnalyzePackageDll(dllFile, strategy, gameVersion);
			if (result != null)
			{
				analyzeResults.PackageResults.Add(packageId, result);
			}
		}

		return analyzeResults;
	}

	private static async Task DownloadAndAnalyzeMissingPackagesAsync(string packageId, UnityVersion gameVersion, CancellationToken ct)
	{
		if (packageId == "com.unity.addressables") // com.unity.addressables version analyzing doesn't need source files
		{
			return;
		}

		List<(string url, string version)> tarballsToDownload = new();
		List<(PackageVersion Version, UnityVersion MinUnityVersion)> packagesToAnalyze = new();

		PackageDomainInfo domainInfo = await DownloadManager.DownloadVersionListAsync(packageId, ct);
		foreach ((string version, PackageInfo packageInfo) in domainInfo.Versions)
		{
			if (PackageAnalyzer.HasAnalyzedPackage(packageId, version))
			{
				continue;
			}

			UnityVersion minUnityVersion = string.IsNullOrEmpty(packageInfo.MinUnity) ? UnityVersion.MinVersion : UnityVersion.Parse(packageInfo.MinUnity);
			if (minUnityVersion > gameVersion)
			{
				continue;
			}

			if (!DownloadManager.IsPackageExtracted(packageId, version))
			{
				tarballsToDownload.Add((packageInfo.DistTarball, version));
			}

			packagesToAnalyze.Add((PackageVersion.Parse(version), minUnityVersion));
		}

		if (tarballsToDownload.Count > 0)
		{
			Logger.Info($"Downloading {tarballsToDownload.Count} missing packages of {packageId}");

			if (packageId.Equals("com.unity.burst", StringComparison.Ordinal)) // Burst packages are >500mb in size and should be downloaded one after the other
			{
				foreach ((string url, string version) in tarballsToDownload)
				{
					await DownloadManager.DownloadAndExtractPackageAsync(packageId, version, url, ct);
				}
			}
			else
			{
				await DownloadManager.DownloadAndExtractPackagesAsync(packageId, tarballsToDownload, 5, ct);
			}
		}

		if (packagesToAnalyze.Count > 0)
		{
			Logger.Info($"Analyzing {packagesToAnalyze.Count} missing packages of {packageId}");
			await PackageAnalyzer.AnalyzePackagesAsync(packageId, packagesToAnalyze, ct);
		}
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

		AnalyzeData? assemblyAnalyzeData = AssemblyAnalyzer.AnalyzeAssembly(packageId, dllFile, gameVersion);
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
			try
			{
				compareResults.ProbabilityByVersion[targetData.Version] = strategy.CompareAnalyzeData(assemblyAnalyzeData, targetData);
			}
			catch (Exception ex)
			{
				Logger.Error(ex, $"An error occured while comparing {packageId} assembly against {packageId}@{targetData.Version}");
				throw;
			}
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

		AnalyzeData? assemblyAnalyzeData = AssemblyAnalyzer.AnalyzeAssembly(packageId, dllFile, gameVersion);
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
