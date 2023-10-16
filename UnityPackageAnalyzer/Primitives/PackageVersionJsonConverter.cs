using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetRipper.UnityPackageAnalyzer.Primitives;

public sealed class PackageVersionJsonConverter : JsonConverter<PackageVersion>
{
	public override PackageVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return PackageVersion.Parse(reader.GetString() ?? throw new JsonException("String was read as null"));
	}

	public override void Write(Utf8JsonWriter writer, PackageVersion value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString());
	}
}
