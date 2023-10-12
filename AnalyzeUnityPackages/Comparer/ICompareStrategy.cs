using AssetRipper.AnalyzeUnityPackages.Primitives;

namespace AssetRipper.AnalyzeUnityPackages.Comparer;

public interface ICompareStrategy
{
	public double CompareAnalyzeData(AnalyzeData src, AnalyzeData target);
}
