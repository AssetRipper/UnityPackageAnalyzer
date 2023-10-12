using AssetRipper.Primitives;
using System.Diagnostics;

namespace AssetRipper.AnalyzeUnityPackages.Primitives;

public class AnalyzeData
{
    public string PackageId { get; init; }
    public PackageVersion Version { get; init; }
    public UnityVersion MinUnityVersion { get; init; }
    public Dictionary<string, EnumData> GlobalEnums { get; init; }
    public Dictionary<string, ClassData> ClassesByName { get; init; }

    public AnalyzeData()
    {
	    PackageId = string.Empty;
	    Version = PackageVersion.Zero;
	    MinUnityVersion = UnityVersion.MinVersion;
        GlobalEnums = new Dictionary<string, EnumData>();
		ClassesByName = new Dictionary<string, ClassData>();
	}

	public AnalyzeData(string packageId, PackageVersion version, UnityVersion minUnityVersion) : this()
	{
		PackageId = packageId;
		Version = version;
        MinUnityVersion = minUnityVersion;
    }


}

public class ClassData
{
    public string Namespace = string.Empty;
    public ProtectionLevel ProtectionLevel;
    public Modifier Modifier;
    public ClassType Type = ClassType.UNKNOWN;
    public string Name = string.Empty;
    public SortedSet<string> Inheritors = new();
	public List<EnumData> Enums = new();
	public List<FieldData> Fields = new();
    public List<PropertyData> Properties = new();
	public List<IndexerData> Indexer = new();
	public List<MethodData> Methods = new();
	public UnityGuid UnityGuid = UnityGuid.Zero;

	[Conditional("DEBUG")]
	public void DebugSort()
	{
		Enums = Enums.OrderBy(e => e.Name).ToList();
		Fields = Fields.OrderBy(f => f.Name).ToList();
		Properties = Properties.OrderBy(p => p.Name).ToList();
		Indexer = Indexer.OrderBy(p => p.Return).ThenBy(m => string.Join(',', m.Parameter)).ToList();
		Methods = Methods.OrderBy(m => m.Name).ThenBy(m => string.Join(',', m.Parameter)).ToList();
		UnityGuid = UnityGuid.Zero;
	}
}

public struct FieldData
{
	public ProtectionLevel Protection;
	public Modifier Modifier;
	public string Name;
	public string Type;

	public override string ToString() => $"{Type} {Name}";
}

public struct PropertyData
{
	public ProtectionLevel? Getter;
	public ProtectionLevel? Setter;
	public Modifier Modifier;
	public string Name;
	public string Type;

	public override string ToString() => $"{Type} {Name}";
}

public struct IndexerData
{
	public ProtectionLevel Protection;
	public Modifier Modifier;
	public bool HasGetter;
	public bool HasSetter;
	public ParameterData[] Parameter;
	public string Return;

	public override string ToString() => $"{Return} this[{string.Join(',', Parameter)})]";
}

public struct MethodData
{
	public ProtectionLevel Protection;
	public Modifier Modifier;
	public string Name;
	public ParameterData[] Parameter;
	public string Return;

	public override string ToString() => $"{Return} {Name}({string.Join(',', Parameter)})";
}

public struct ParameterData
{
	public string Name;
	public string Type;
	public ParameterModifier Modifier;

	public override string ToString() => $"{Type} {Name}";
}

public struct EnumData
{
	public ProtectionLevel ProtectionLevel;
	public string Name;
	public List<string> Values;

	public EnumData()
	{
		Name = string.Empty;
		Values = new List<string>();
	}
}

public enum ClassType : byte
{
	UNKNOWN,
	CLASS,
	INTERFACE,
	STRUCT,
	DELEGATE
}

public enum ProtectionLevel : byte
{
	UNKNOWN,
	PRIVATE,
	PRIVATE_PROTECTED,
	INTERNAL,
	PROTECTED,
	PROTECTED_INTERNAL,
	PUBLIC
}

[Flags]
public enum Modifier : byte
{
	NONE = 0,
	STATIC = 1,
	READONLY = 2,
	ABSTRACT = 4,
	SEALED = 8,
	CONST = 16
}

[Flags]
public enum ParameterModifier : byte
{
	NONE = 0,
	REF = 1,
	OUT = 3
}
