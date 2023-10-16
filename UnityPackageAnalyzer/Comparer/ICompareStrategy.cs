using AssetRipper.UnityPackageAnalyzer.Primitives;

namespace AssetRipper.UnityPackageAnalyzer.Comparer;

public interface ICompareStrategy
{
	public double CompareAnalyzeData(AnalyzeData src, AnalyzeData target);
}
