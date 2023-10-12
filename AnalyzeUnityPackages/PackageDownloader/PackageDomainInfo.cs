namespace AssetRipper.AnalyzeUnityPackages.PackageDownloader;

public class PackageDomainInfo
{
	public string name;
	public Dictionary<string, PackageInfo> versions;
}

public struct PackageInfo
{
	public string name;
	public string unity;
	public PackageDistributionInfo dist;
}

public struct PackageDistributionInfo
{
	public string tarball;
}
