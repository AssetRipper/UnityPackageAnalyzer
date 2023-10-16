namespace AssetRipper.UnityPackageAnalyzer.Helper;

public static class EnumExtension
{
	public static T Max<T>(T first, T second) where T : Enum
	{
		return first.CompareTo(second) > 0 ? first : second;
	}
}
