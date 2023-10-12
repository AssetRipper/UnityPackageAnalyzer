using AssetRipper.AnalyzeUnityPackages.Helper;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Runtime.Serialization;
using System.Text;

namespace AssetRipper.AnalyzeUnityPackages.PackageDownloader;

public static class DownloadManager
{
	private static readonly string tempListPath = Path.Combine(Path.GetTempPath(), "AssetRipper", "AnalyzeUnityPackages", "PackageDomainInfo");
	private static readonly string tempExtractPath = Path.Combine(Path.GetTempPath(), "AssetRipper", "AnalyzeUnityPackages", "ExtractedPackages");
	private static readonly HttpClient downloadPackageClient = new() { BaseAddress = new Uri("https://download.packages.unity.com/") };


	public static async Task<PackageDomainInfo> DownloadVersionListAsync(string packageId, CancellationToken ct)
	{
		string cacheFilePath = Path.Combine(tempListPath, $"{packageId}.json");
		if (File.Exists(cacheFilePath))
		{
			return await Serializer.DeserializeDataAsync<PackageDomainInfo>(cacheFilePath, ct) ?? throw new SerializationException();
		}

		string rawData = await downloadPackageClient.GetStringAsync(packageId, ct);
		Directory.CreateDirectory(tempListPath);
		await File.WriteAllTextAsync(cacheFilePath, rawData, ct);
		return await Serializer.DeserializeDataAsync<PackageDomainInfo>(rawData, ct) ?? throw new SerializationException();
	}


	public static async Task DownloadAndExtractPackageAsync(PackageDistributionInfo packageDistribution, string packageId, string version, CancellationToken ct)
	{
		string destinationDir = GetExtractPath(packageId, version);
		if (Directory.Exists(destinationDir) && Directory.EnumerateFileSystemEntries(destinationDir).Any())
		{
			Logger.Debug($"Package {packageId}@{version} already downloaded");
			return;
		}

		Logger.Debug($"Downloading package {packageId}@{version}");
		using HttpClient client = new();
		using HttpResponseMessage response = await client.GetAsync(packageDistribution.tarball, ct);
		await using Stream stream = await response.Content.ReadAsStreamAsync(ct);

		await using GZipInputStream gzipStream = new(stream);
		TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.Default);

		Directory.CreateDirectory(destinationDir);
		tarArchive.ExtractContents(destinationDir);
		tarArchive.Close();
	}

	public static string GetExtractPath(string packageId, string packageVersion) => Path.Combine(tempExtractPath, packageId, packageVersion);
}
