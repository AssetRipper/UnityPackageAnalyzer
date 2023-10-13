namespace AssetRipper.AnalyzeUnityPackages.PackageDownloader;

public class PackageDomainInfo
{
	public Dictionary<string, PackageInfo> Versions;

	public PackageDomainInfo()
	{
		Versions = new Dictionary<string, PackageInfo>();
	}
}

public struct PackageInfo
{
	public string MinUnity;
	public string DistTarball;
}
