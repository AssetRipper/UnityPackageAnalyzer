using AssetRipper.AnalyzeUnityPackages.Primitives;

namespace AssetRipper.AnalyzeUnityPackages.Comparer;

public class EqualCompareStrategy : ICompareStrategy
{
	public double CompareAnalyzeData(AnalyzeData src, AnalyzeData target)
	{
		double value = 0;
		double maxValue = 0;

		foreach (KeyValuePair<string, EnumData> pair in src.GlobalEnums)
		{
			if (target.GlobalEnums.TryGetValue(pair.Key, out EnumData targetEnumData))
			{
				CompareEnumData(pair.Value, targetEnumData, ref value, ref maxValue);
			}
			else
			{
				CompareEnumData(pair.Value, null, ref value, ref maxValue);
			}
		}

		foreach (KeyValuePair<string, ClassData> pair in src.ClassesByName)
		{
			if (target.ClassesByName.TryGetValue(pair.Key, out ClassData? targetClassData))
			{
				CompareClassData(pair.Value, targetClassData, ref value, ref maxValue);
			}
		}

		return value / maxValue;
	}

	private static void CompareEnumData(EnumData src, EnumData? target, ref double value, ref double maxValue)
	{
		for (int i = 0; i < src.Values.Count; i++)
		{
			if (i < target?.Values.Count && src.Values[i].Equals(target.Value.Values[i], StringComparison.Ordinal))
			{
				value += 1;
			}
		}

		if (src.ProtectionLevel != target?.ProtectionLevel)
		{
			value += 1;
		}

		maxValue += src.Values.Count + 1;
	}

	private static void CompareClassData(ClassData src, ClassData? target, ref double value, ref double maxValue)
	{
		Dictionary<string, EnumData> targetEnums = target?.Enums.ToDictionary(f => f.Name) ?? new Dictionary<string, EnumData>();
		foreach (EnumData enumData in src.Enums)
		{
			if (targetEnums.TryGetValue(enumData.Name, out EnumData targetEnumData))
			{
				CompareEnumData(enumData, targetEnumData, ref value, ref maxValue);
			}
			else
			{
				CompareEnumData(enumData, null, ref value, ref maxValue);
			}
		}

		Dictionary<string, FieldData> targetFields = target?.Fields.ToDictionary(f => f.ToString()) ?? new Dictionary<string, FieldData>();
		foreach (FieldData fieldData in src.Fields)
		{
			if (targetFields.TryGetValue(fieldData.ToString(), out FieldData targetFieldData))
			{
				CompareFieldData(fieldData, targetFieldData, ref value, ref maxValue);
			}
			else
			{
				CompareFieldData(fieldData, null, ref value, ref maxValue);
			}
		}

		Dictionary<string, PropertyData> targetProperties = target?.Properties.ToDictionary(f => f.ToString()) ?? new Dictionary<string, PropertyData>();
		foreach (PropertyData propertyData in src.Properties)
		{
			if (targetProperties.TryGetValue(propertyData.ToString(), out PropertyData targetPropertyData))
			{
				ComparePropertyData(propertyData, targetPropertyData, ref value, ref maxValue);
			}
			else
			{
				ComparePropertyData(propertyData, null, ref value, ref maxValue);
			}
		}

		Dictionary<string, IndexerData> targetIndexers = target?.Indexer.ToDictionary(f => f.ToString()) ?? new Dictionary<string, IndexerData>();
		foreach (IndexerData indexerData in src.Indexer)
		{
			if (targetIndexers.TryGetValue(indexerData.ToString(), out IndexerData targetIndexerData))
			{
				CompareIndexerData(indexerData, targetIndexerData, ref value, ref maxValue);
			}
			else
			{
				CompareIndexerData(indexerData, null, ref value, ref maxValue);
			}
		}

		Dictionary<string, MethodData> targetMethods = target?.Methods.ToDictionary(f => f.ToString()) ?? new Dictionary<string, MethodData>();
		foreach (MethodData methodData in src.Methods)
		{
			if (targetMethods.TryGetValue(methodData.ToString(), out MethodData targetMethodData))
			{
				CompareMethodData(methodData, targetMethodData, ref value, ref maxValue);
			}
			else
			{
				CompareMethodData(methodData, null, ref value, ref maxValue);
			}
		}

		if (!src.Namespace.Equals(target?.Namespace, StringComparison.Ordinal))
		{
			value += 1;
		}

		if (src.ProtectionLevel != target?.ProtectionLevel)
		{
			value += 1;
		}

		if (src.Modifier != target?.Modifier)
		{
			value += 1;
		}

		maxValue += 3;
	}

	private static void CompareFieldData(FieldData src, FieldData? target, ref double value, ref double maxValue)
	{
		if (src.Protection == target?.Protection)
		{
			value += 1;
		}

		if (src.Modifier == target?.Modifier)
		{
			value += 1;
		}

		if (src.Type.Equals(target?.Type, StringComparison.Ordinal))
		{
			value += 1;
		}

		maxValue += 3;
	}

	private static void ComparePropertyData(PropertyData src, PropertyData? target, ref double value, ref double maxValue)
	{
		if (src.Getter == target?.Getter)
		{
			value += 1;
		}

		if (src.Setter == target?.Setter)
		{
			value += 1;
		}

		if (src.Modifier == target?.Modifier)
		{
			value += 1;
		}

		if (src.Type.Equals(target?.Type, StringComparison.Ordinal))
		{
			value += 1;
		}

		maxValue += 4;
	}

	private static void CompareIndexerData(IndexerData src, IndexerData? target, ref double value, ref double maxValue)
	{
		if (src.Protection == target?.Protection)
		{
			value += 1;
		}

		if (src.Modifier == target?.Modifier)
		{
			value += 1;
		}

		if (src.HasGetter == target?.HasGetter)
		{
			value += 1;
		}

		if (src.HasSetter == target?.HasSetter)
		{
			value += 1;
		}

		if (src.Return.Equals(target?.Return, StringComparison.Ordinal))
		{
			value += 1;
		}

		for (int i = 0; i < src.Parameter.Length; i++)
		{
			ParameterData? parameterData = target?.Parameter.GetValue(i) as ParameterData?;
			CompareParameterData(src.Parameter[i], parameterData, ref value, ref maxValue);
		}

		maxValue += 5;
	}

	private static void CompareMethodData(MethodData src, MethodData? target, ref double value, ref double maxValue)
	{
		if (src.Protection == target?.Protection)
		{
			value += 1;
		}

		if (src.Modifier == target?.Modifier)
		{
			value += 1;
		}

		if (src.Return.Equals(target?.Return, StringComparison.Ordinal))
		{
			value += 1;
		}

		for (int i = 0; i < src.Parameter.Length; i++)
		{
			ParameterData? parameterData = target?.Parameter.GetValue(i) as ParameterData?;
			CompareParameterData(src.Parameter[i], parameterData, ref value, ref maxValue);
		}

		maxValue += 3;
	}

	private static void CompareParameterData(ParameterData src, ParameterData? target, ref double value, ref double maxValue)
	{
		if (src.Modifier == target?.Modifier)
		{
			value += 1;
		}

		if (src.Type.Equals(target?.Type, StringComparison.Ordinal))
		{
			value += 1;
		}

		maxValue += 2;
	}
}
