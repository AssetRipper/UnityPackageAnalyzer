using AssetRipper.UnityPackageAnalyzer.Helper;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Nodes;

namespace AssetRipper.UnityPackageAnalyzer.PackageDownloader;

public static class DownloadManager
{
	private static readonly string tempListPath = Path.Combine(Path.GetTempPath(), "AssetRipper", "UnityPackageAnalyzer", "PackageDomainInfo");
	private static readonly string tempExtractPath = Path.Combine(Path.GetTempPath(), "AssetRipper", "UnityPackageAnalyzer", "ExtractedPackages");
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
			throw new SerializationException("Failed to parse downloaded PackageDomainInfo");
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

	public static bool IsPackageExtracted(string packageId, string version)
	{
		string destinationDir = GetExtractPath(packageId, version);
		return Directory.Exists(destinationDir) && Directory.EnumerateFiles(destinationDir, "*.*", SearchOption.AllDirectories).Any();
	}

	public static async Task DownloadAndExtractPackagesAsync(string packageId, List<(string url, string version)> packages, int limit, CancellationToken ct)
	{
		using HttpClient client = new HttpClient();
		using SemaphoreSlim semaphore = new SemaphoreSlim(limit, limit);

		List<Task> tasks = new();
		foreach ((string url, string version) in packages)
		{
			tasks.Add(DownloadUrlHelperAsync(packageId, version, url, semaphore, client, ct));
		}
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private static async Task DownloadUrlHelperAsync(string packageId, string version, string url, SemaphoreSlim semaphore, HttpClient client, CancellationToken ct)
	{
		await semaphore.WaitAsync(ct).ConfigureAwait(false);
		Logger.Debug($"Downloading {packageId}@{version}");

		try
		{
			using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				Logger.Error($"An error occured while downloading {packageId}@{version}: {response.StatusCode}");
				return;
			}

			await using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
			await using GZipInputStream gzipStream = new(stream);
			TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.Default);

			string destinationDir = GetExtractPath(packageId, version);
			Directory.CreateDirectory(destinationDir);
			tarArchive.ExtractContents(destinationDir);
		}
		finally
		{
			semaphore.Release();
		}
	}

	public static async Task DownloadAndExtractPackageAsync(string packageId, string version, string tarballUrl, CancellationToken ct)
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
