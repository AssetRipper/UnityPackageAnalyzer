using System.Globalization;
using System.Text.RegularExpressions;

namespace AssetRipper.AnalyzeUnityPackages.Helper;

public class PackageVersion : IComparable<PackageVersion?>, IEquatable<PackageVersion?>
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

    public int CompareTo(PackageVersion? other)
    {
        if (ReferenceEquals(other, this))
        {
            return 0;
        }

        if (other == null)
        {
            return 1;
        }

        int versionComp = Version.CompareTo(other.Version);

        if (versionComp != 0)
        {
            return versionComp;
        }

        if (Separator != null && other.Separator != null)
        {
            if (PreviewVersion.HasValue && other.PreviewVersion.HasValue)
            {
                return PreviewVersion.Value - other.PreviewVersion.Value;
            }

            return 0;
        }

        return Separator != null ? -1 : 1;

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
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Version.Equals(other.Version) && Separator == other.Separator && PreviewVersion == other.PreviewVersion;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (GetType() != obj.GetType()) return false;
        return Equals((PackageVersion)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Version, Separator, PreviewVersion);
    }
}
