using AssetRipper.UnityPackageAnalyzer.Primitives;

namespace AssetRipper.UnityPackageAnalyzer.Comparer;

public class BalancedCompareStrategy : ICompareStrategy
{
	public double CompareAnalyzeData(AnalyzeData src, AnalyzeData target)
	{
		double value = 0;

		foreach (KeyValuePair<string, EnumData> pair in src.GlobalEnums)
		{
			if (target.GlobalEnums.TryGetValue(pair.Key, out EnumData targetEnumData))
			{
				value += CompareEnumData(pair.Value, targetEnumData);
			}
		}

		foreach (KeyValuePair<string, ClassData> pair in src.ClassesByFullName)
		{
			if (target.ClassesByFullName.TryGetValue(pair.Key, out ClassData targetClassData))
			{
				value += CompareClassData(pair.Value, targetClassData);
			}
		}

		return value / (src.GlobalEnums.Count + src.ClassesByFullName.Count);
	}

	private static double CompareEnumData(EnumData src, EnumData target)
	{
		double value = 0d;

		for (int i = 0; i < src.Values.Count; i++)
		{
			if (i < target.Values.Count && src.Values[i].Equals(target.Values[i], StringComparison.Ordinal))
			{
				value += 1;
			}
		}

		value /= src.Values.Count;


		if (src.ProtectionLevel != target.ProtectionLevel)
		{
			value *= 0.9d;
		}

		return value;
	}

	private static double CompareClassData(ClassData src, ClassData target)
	{
		double value = 0d;

		Dictionary<string, EnumData> targetEnums = target.Enums.ToDictionary(f => f.Name);
		foreach (EnumData enumData in src.Enums)
		{
			if (targetEnums.TryGetValue(enumData.Name, out EnumData targetEnumData))
			{
				value += CompareEnumData(enumData, targetEnumData);
			}
		}

		Dictionary<string, FieldData> targetFields = target.Fields.ToDictionary(f => f.ToString());
		foreach (FieldData fieldData in src.Fields)
		{
			if (targetFields.TryGetValue(fieldData.ToString(), out FieldData targetFieldData))
			{
				value += CompareFieldData(fieldData, targetFieldData);
			}
		}

		Dictionary<string, PropertyData> targetProperties = target.Properties.ToDictionary(f => f.ToString());
		foreach (PropertyData propertyData in src.Properties)
		{
			if (targetProperties.TryGetValue(propertyData.ToString(), out PropertyData targetPropertyData))
			{
				value += ComparePropertyData(propertyData, targetPropertyData);
			}
		}

		Dictionary<string, IndexerData> targetIndexers = target.Indexer.ToDictionary(f => f.ToString());
		foreach (IndexerData indexerData in src.Indexer)
		{
			if (targetIndexers.TryGetValue(indexerData.ToString(), out IndexerData targetIndexerData))
			{
				value += CompareIndexerData(indexerData, targetIndexerData);
			}
		}

		Dictionary<string, MethodData> targetMethods = target.Methods.ToDictionary(f => f.ToString());
		foreach (MethodData methodData in src.Methods)
		{
			if (targetMethods.TryGetValue(methodData.ToString(), out MethodData targetMethodData))
			{
				value += CompareMethodData(methodData, targetMethodData);
			}
		}

		int listCount = src.Enums.Count + src.Fields.Count + src.Properties.Count + src.Indexer.Count + src.Methods.Count;
		if (listCount > 0)
		{
			value /= listCount;

			if (value > 1)
			{
				throw new ArithmeticException("Compare value over 1");
			}
		}
		else
		{
			value = 1d;
		}


		if (!src.Namespace.Equals(target.Namespace, StringComparison.Ordinal))
		{
			value *= 0.8d;
		}

		if (src.ProtectionLevel != target.ProtectionLevel)
		{
			value *= 0.95d;
		}

		if (src.Modifier != target.Modifier)
		{
			value *= 0.95d;
		}

		if (src.Type != target.Type)
		{
			value *= 0.8d;
		}

		return value;
	}

	private static double CompareFieldData(FieldData src, FieldData target)
	{
		if (src.Protection < ProtectionLevel.PROTECTED)
		{
			return 1d;
		}

		double value = 0d;

		if (src.Modifier == target.Modifier)
		{
			value += 0.5d;
		}

		if (src.Type.Equals(target.Type, StringComparison.Ordinal))
		{
			value += 0.5d;
		}

		if (value > 1)
		{
			throw new ArithmeticException("Compare value over 1");
		}

		return value;
	}

	private static double ComparePropertyData(PropertyData src, PropertyData target)
	{
		if (src.Getter < ProtectionLevel.PROTECTED && src.Setter < ProtectionLevel.PROTECTED)
		{
			return 1d;
		}

		double value = 0;

		if (src.Getter == null || src.Getter <= target.Getter)
		{
			value += 0.25d;
		}

		if (src.Setter == null || src.Setter <= target.Setter)
		{
			value += 0.25d;
		}


		if (src.Modifier == target.Modifier)
		{
			value += 0.2d;
		}

		if (src.Type.Equals(target.Type, StringComparison.Ordinal))
		{
			value += 0.3d;
		}

		if (value > 1)
		{
			throw new ArithmeticException("Compare value over 1");
		}

		return value;
	}

	private static double CompareIndexerData(IndexerData src, IndexerData target)
	{
		if (src.Protection < ProtectionLevel.PROTECTED)
		{
			return 1d;
		}

		double value = 0;

		if (src.Parameter.Length > 0)
		{
			for (int i = 0; i < src.Parameter.Length; i++)
			{
				if (i < target.Parameter.Length)
				{
					value += CompareParameterData(src.Parameter[i], target.Parameter[i]);
				}
			}

			value /= src.Parameter.Length * 4;

			if (value > 0.25f)
			{
				throw new ArithmeticException("Compare value over 1");
			}
		}
		else
		{
			value = 0.25d;
		}

		if (src.HasGetter == target.HasGetter)
		{
			value += 0.2d;
		}

		if (src.HasSetter == target.HasSetter)
		{
			value += 0.2d;
		}


		if (src.Modifier == target.Modifier)
		{
			value += 0.1d;
		}

		if (src.Return.Equals(target.Return, StringComparison.Ordinal))
		{
			value += 0.25d;
		}

		if (value > 1)
		{
			throw new ArithmeticException("Compare value over 1");
		}

		return value;
	}

	private static double CompareMethodData(MethodData src, MethodData target)
	{
		if (src.Protection < ProtectionLevel.PROTECTED)
		{
			return 1d;
		}

		double value = 0d;

		if (src.Parameter.Length > 0)
		{
			for (int i = 0; i < src.Parameter.Length; i++)
			{
				if (i < target.Parameter.Length)
				{
					value += CompareParameterData(src.Parameter[i], target.Parameter[i]);
				}
			}

			value /= src.Parameter.Length * 2;

			if (value > 0.5d)
			{
				throw new ArithmeticException("Compare value over 1");
			}
		}
		else
		{
			value = 0.5d;
		}


		if (src.Modifier == target.Modifier)
		{
			value += 0.2d;
		}

		if (src.Return.Equals(target.Return, StringComparison.Ordinal))
		{
			value += 0.3d;
		}

		if (value > 1)
		{
			throw new ArithmeticException("Compare value over 1");
		}

		return value;
	}

	private static double CompareParameterData(ParameterData src, ParameterData target)
	{
		double value = 0d;

		if (src.Modifier == target.Modifier)
		{
			value += 0.5d;
		}

		if (src.Type.Equals(target.Type, StringComparison.Ordinal))
		{
			value += 0.5d;
		}

		if (value > 1)
		{
			throw new ArithmeticException("Compare value over 1");
		}

		return value;
	}
}
