using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetRipper.AnalyzeUnityPackages.Helper;

public static class Serializer
{
	private static readonly JsonSerializerOptions jsonSettings = new() { IncludeFields = true };
	private static readonly JsonSerializerOptions jsonDebugSettings = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

	public static async void SerializeAnalyzerDataAsync(AnalyzeData? analyzeData, string path, CancellationToken ct)
	{
		await using FileStream stream = File.Create(path);
		await JsonSerializer.SerializeAsync(stream, analyzeData, jsonSettings, ct);

#if DEBUG
		//await using FileStream debugStream = File.Create(path.Insert(path.Length - 5, "_debug"));
		//await JsonSerializer.SerializeAsync(stream, new OrderedAnalyzeData(analyzeData), jsonDebugSettings, ct);
		//analyzeData = null; // analyzeData gets polluted by encapsulating in OrderedAnalyzeData and should not be used further
#endif
	}

	public static async void SerializeDataAsync<T>(string path, T value, CancellationToken ct)
	{
		await using FileStream stream = File.Create(path);
		await JsonSerializer.SerializeAsync(stream, value, jsonSettings, ct);
	}

	public static async Task<T?> DeserializeDataAsync<T>(string path, CancellationToken ct)
	{
		await using FileStream stream = File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<T>(stream, jsonSettings, ct);
	}

	public static T? DeserializeData<T>(string path)
	{
		using FileStream stream = File.OpenRead(path);
		return JsonSerializer.Deserialize<T>(stream, jsonSettings);
	}

#if DEBUG
	public struct OrderedAnalyzeData
	{
		public string PackageId;
		public PackageVersion Version;
		public UnityVersion MinUnityVersion;
		public EnumData[] GlobalEnums = Array.Empty<EnumData>();
		public ClassData[] Classes = Array.Empty<ClassData>();

		public OrderedAnalyzeData(AnalyzeData? data)
		{
			if (data != null)
			{
				PackageId = data.PackageId;
				Version = data.Version;
				MinUnityVersion = data.MinUnityVersion;
				GlobalEnums = data.GlobalEnums.Values.OrderBy(e => e.Name).ToArray();
				Classes = data.ClassesByName.Values.OrderBy(c => c.Name).ToArray();

				foreach (ClassData classData in Classes)
				{
					classData.DebugSort();
				}
			}
		}
	}
#endif
}
