using AssetRipper.AnalyzeUnityPackages.Analyzer;

namespace AssetRipper.AnalyzeUnityPackages.Comparer;

public interface ICompareStrategy
{
    public double CompareAnalyzeData(AnalyzeData src, AnalyzeData target);
}
