using AssetRipper.AnalyzeUnityPackages.Helper;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Nodes;

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

		Stream rawData = await downloadPackageClient.GetStreamAsync(packageId, ct);
		JsonNode? json = JsonNode.Parse(rawData);
		if (json == null)
		{
			throw new SerializationException($"Failed to parse downloaded PackageDomainInfo");
		}

		PackageDomainInfo domainInfo = new PackageDomainInfo();
		foreach (KeyValuePair<string, JsonNode?> versionJson in json["versions"].AsObject())
		{
			PackageInfo packageInfo = new PackageInfo();
			packageInfo.MinUnity = versionJson.Value["unity"]?.GetValue<string>();
			packageInfo.DistTarball = versionJson.Value["dist"]["tarball"].GetValue<string>();

			domainInfo.Versions.Add(versionJson.Key, packageInfo);
		}

		Directory.CreateDirectory(tempListPath);
		Serializer.SerializeDataAsync(cacheFilePath, domainInfo, ct);
		return domainInfo;
	}


	public static async Task DownloadAndExtractPackageAsync(string tarballUrl, string packageId, string version, CancellationToken ct)
	{
		string destinationDir = GetExtractPath(packageId, version);
		if (Directory.Exists(destinationDir) && Directory.EnumerateFileSystemEntries(destinationDir).Any())
		{
			Logger.Debug($"Package {packageId}@{version} already downloaded");
			return;
		}

		Logger.Debug($"Downloading package {packageId}@{version}");
		TarArchive? tarArchive = null;
		try
		{
			using HttpClient client = new();
			using HttpResponseMessage response = await client.GetAsync(tarballUrl, ct);
			await using Stream stream = await response.Content.ReadAsStreamAsync(ct);

			await using GZipInputStream gzipStream = new(stream);
			tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.Default);

			Directory.CreateDirectory(destinationDir);
			tarArchive.ExtractContents(destinationDir);

		}
		catch (Exception ex)
		{
			Logger.Error(ex, $"An Error occured while downloading {packageId}@{version}");
		}
		finally
		{
			tarArchive?.Close();
		}
	}

	public static string GetExtractPath(string packageId, string packageVersion) => Path.Combine(tempExtractPath, packageId, packageVersion);
}
