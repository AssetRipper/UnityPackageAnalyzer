﻿using AssetRipper.UnityPackageAnalyzer.Primitives;

namespace AssetRipper.UnityPackageAnalyzer.Comparer;

public class CompareResults
{
	public Dictionary<string, PackageCompareResult> PackageResults { get; init; } = new();
}

public class PackageCompareResult
{
	public Dictionary<PackageVersion, double> ProbabilityByVersion { get; init; } = new();

	public PackageCompareResult() { }

	public PackageCompareResult(PackageVersion version, double probability)
	{
		ProbabilityByVersion[version] = probability;
	}
}
