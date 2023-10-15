using System.Text.Json;

namespace AssetRipper.AnalyzeUnityPackages.Helper;

public static class Serializer
{
	private static readonly JsonSerializerOptions jsonSettings = new() { IncludeFields = true };

	public static async Task SerializeDataAsync<T>(string path, T value, CancellationToken ct)
	{
		try
		{
			await using FileStream stream = File.Create(path);
			await JsonSerializer.SerializeAsync(stream, value, jsonSettings, ct);
		}
		catch (Exception ex)
		{
			Logger.Error(ex, $"An error was throw while asynchronously serializing {path}");
			throw;
		}
	}

	public static async Task<T?> DeserializeDataAsync<T>(string path, CancellationToken ct)
	{
		try
		{
			await using FileStream stream = File.OpenRead(path);
			return await JsonSerializer.DeserializeAsync<T>(stream, jsonSettings, ct);
		}
		catch (Exception ex)
		{
			Logger.Error(ex, $"An error was throw while asynchronously deserializing {path}");
			throw;
		}
	}

	public static T? DeserializeData<T>(string path)
	{
		try
		{
			using FileStream stream = File.OpenRead(path);
			return JsonSerializer.Deserialize<T>(stream, jsonSettings);
		}
		catch (Exception ex)
		{
			Logger.Error(ex, $"An error was throw while deserializing {path}");
			throw;
		}
	}
}
