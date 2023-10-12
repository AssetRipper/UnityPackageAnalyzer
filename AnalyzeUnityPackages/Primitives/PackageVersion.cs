using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AssetRipper.AnalyzeUnityPackages.Primitives;

[JsonConverter(typeof(PackageVersionJsonConverter))]
public readonly struct PackageVersion : IComparable<PackageVersion?>, IEquatable<PackageVersion?>
{
	private static readonly Regex intRegex = new Regex(@"\d+");

	public Version Version { get; init; }
	public string? Separator { get; init; }
	public int? PreviewVersion { get; init; }
	public char? PreviewSuffix { get; init; }

	public PackageVersion()
	{
		Version = new Version();
	}

	public PackageVersion(Version version, string? separator, int? previewVersion, char? previewSuffix)
	{
		Version = version;
		Separator = separator;
		PreviewVersion = previewVersion;
		PreviewSuffix = previewSuffix;
	}

	public PackageVersion(string value)
	{
		string[] values = value.Split('-');
		Version = new Version(values[0]);

		if (values.Length > 1)
		{
			string[] previewSuffix = values[1].Split('.');
			Separator = previewSuffix[0];

			if (previewSuffix.Length > 1)
			{
				string previewSuffixValue = intRegex.Match(previewSuffix[1]).Value;
				PreviewVersion = int.Parse(previewSuffixValue, NumberStyles.Integer);
				char lastElement = previewSuffix[1].Last();
				PreviewSuffix = char.IsAsciiLetter(lastElement) ? lastElement : null;
			}
		}
	}

	public static PackageVersion Zero => new PackageVersion(new Version(0, 0, 0, 0), null, null, null);

	public static PackageVersion Parse(string versionString) => new(versionString);

	public int CompareTo(PackageVersion? other)
	{
		if (other == null)
		{
			return 1;
		}

		if (Equals(other, this))
		{
			return 0;
		}

		int versionComp = Version.CompareTo(other.Value.Version);
		if (versionComp != 0)
		{
			return versionComp;
		}

		if (Separator == null || other.Value.Separator == null)
		{
			return Separator != null ? -1 : 1;
		}

		if (!PreviewVersion.HasValue || !other.Value.PreviewVersion.HasValue)
		{
			return 0;
		}

		int previewVersionComp = PreviewVersion.Value - other.Value.PreviewVersion.Value;
		if (previewVersionComp != 0)
		{
			return previewVersionComp;
		}

		if (PreviewSuffix.HasValue && other.Value.PreviewSuffix.HasValue)
		{
			return PreviewSuffix.Value - other.Value.PreviewSuffix.Value;
		}

		return 0;
	}

	public override string ToString()
	{
		return Separator == null ? Version.ToString() :
			PreviewVersion == null ? $"{Version}-{Separator}" :
			PreviewSuffix == null ? $"{Version}-{Separator}.{PreviewVersion.Value}" :
			$"{Version}-{Separator}.{PreviewVersion.Value}{PreviewSuffix}";
	}

	public bool Equals(PackageVersion? other)
	{
		return other.HasValue &&
		       Version.Equals(other.Value.Version) &&
		       Separator == other.Value.Separator &&
		       PreviewVersion == other.Value.PreviewVersion &&
		       PreviewSuffix == other.Value.PreviewSuffix;
	}

	public override bool Equals(object? obj)
	{
		return obj != null &&
		       GetType() == obj.GetType() &&
		       Equals((PackageVersion)obj);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Version, Separator, PreviewVersion, PreviewSuffix);
	}
}
