using AssetRipper.AnalyzeUnityPackages.Analyzer;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AssetRipper.AnalyzeUnityPackages.Helper;

public static class Serializer
{
    private static readonly JsonSerializerSettings jsonSettings = new()
    {
        Formatting = Formatting.None,
        Converters = { new KeyValuePairConverter(), new VersionConverter() }
    };
    private static readonly JsonSerializerSettings jsonDebugSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter(), new KeyValuePairConverter(), new VersionConverter() }
    };

    public static void SerializeAnalyzerData(AnalyzeData analyzeData, string path)
    {
        string jsonData = JsonConvert.SerializeObject(analyzeData, jsonSettings);
        File.WriteAllText(path, jsonData);

#if DEBUG
        string jsonDebugData = JsonConvert.SerializeObject(new OrderedAnalyzeData(analyzeData), jsonDebugSettings);
        File.WriteAllText(path+".debug.json", jsonDebugData);
        analyzeData = null; // analyzeData gets polluted by encapsulating in OrderedAnalyzeData and should not be used further
#endif
    }

    public static AnalyzeData? DeserializeAnalyzerData(string path)
    {
        string jsonData = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<AnalyzeData>(jsonData, jsonSettings);
    }

    public static T? DeserializeData<T>(string path)
    {
        string jsonData = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<T>(jsonData, jsonSettings);
    }

    public struct OrderedAnalyzeData
    {
        public string PackageId;
        public PackageVersion Version;
        public Version MinUnityVersion;
        public EnumData[] GlobalEnums = Array.Empty<EnumData>();
        public ClassData[] Classes = Array.Empty<ClassData>();

        public OrderedAnalyzeData(AnalyzeData data)
        {
            PackageId = data.PackageId;
            Version = data.Version;
            MinUnityVersion = data.MinUnityVersion;
            GlobalEnums = data.GlobalEnums.Values.OrderBy(e => e.Name).ToArray();
            Classes = data.ClassesByName.Values.OrderBy(c => c.Name).ToArray();

            foreach (ClassData classData in Classes)
            {
                classData.Sort();
            }
        }
    }
}
