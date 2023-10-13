using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;
using System.Reflection;
using System.Runtime.Loader;

namespace AssetRipper.AnalyzeUnityPackages.Analyzer;

public static class AssemblyAnalyzer
{
	private static readonly Lazy<AssemblyLoadContext> assemblyContext = new(() =>
	{
		AssemblyLoadContext loadContext = new("AssetRipper.GameAssemblyAnalyzerContext");
		loadContext.Resolving += (context, name) =>
		{
			string dir = Path.GetDirectoryName(context.Assemblies.First().Location) ?? string.Empty;
			string file = Path.Combine(dir, name.Name + ".dll");
			return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
		};
		return loadContext;
	});

	public static AnalyzeData? AnalyzeAssembly(string packageId, string srcFile)
	{
		if (!File.Exists(srcFile))
		{
			Logger.Error($"No directory found to analyze at: {srcFile}");
			return null;
		}

		AnalyzeData analyzeData = new(packageId, new PackageVersion(), UnityVersion.MinVersion);
		Assembly assembly = assemblyContext.Value.LoadFromAssemblyPath(srcFile);

		AnalyzeAssembly(analyzeData, assembly);

		return analyzeData;
	}

	private static void AnalyzeAssembly(AnalyzeData analyzeData, Assembly assembly)
	{
		Dictionary<string, List<EnumData>> localEnumData = new();

		foreach (TypeInfo type in assembly.GetTypes().Select(t => t.GetTypeInfo()))
		{
			if (type.IsCompilerGenerated())
			{
				continue;
			}

			if (type.IsEnum)
            {
                EnumData enumData = new EnumData
                {
                    ProtectionLevel = type.GetProtectionLevel(),
                    Name = type.Name,
                    Values = type.DeclaredMembers.Select(mdField => mdField.Name).Where(name => name != "value__").ToList()
                };

                if (type.DeclaringType == null)
				{
					if (enumData.ProtectionLevel == ProtectionLevel.PRIVATE)
					{
						enumData.ProtectionLevel = ProtectionLevel.INTERNAL;
					}

					analyzeData.GlobalEnums.Add(enumData.Name, enumData);
				}
				else
				{
					string declaringName = type.DeclaringType.GetCleanName();
					if (!localEnumData.TryGetValue(declaringName, out List<EnumData> classEnums))
					{
						localEnumData[declaringName] = classEnums = new List<EnumData>();
					}

					classEnums.Add(enumData);
				}

				continue;
			}

			ClassData classData = new()
			{
				Namespace = type.Namespace ?? string.Empty,
				ProtectionLevel = type.GetProtectionLevel(),
				Modifier = type.GetModifiers(),
				Name = type.GetCleanName(type.DeclaringType),
				Inheritors = type.GetBaseAndAllInterfaceNames()
			};
			analyzeData.ClassesByName.Add(classData.Name, classData);

			if (type.IsValueType)
			{
				classData.Modifier &= ~Modifier.SEALED; // Compiled structs are sealed by default
				classData.Type = ClassType.STRUCT;
			}
			else if (type.IsInterface)
			{
				classData.Modifier &= ~Modifier.ABSTRACT; // Compiled interfaces are abstract by default
				classData.Type = ClassType.INTERFACE;
			}
			else if (typeof(Delegate).IsAssignableFrom(type.AsType()))
			{
				classData.Modifier &= ~Modifier.SEALED; // Delegates are sealed by default
				classData.Type = ClassType.DELEGATE;
				classData.Inheritors.Clear();
				AnalyzeMember(classData, type, type.DeclaredMembers.First(m => m.Name == "Invoke"));
				continue;
			}
			else
			{
				classData.Type = ClassType.CLASS;
			}

			foreach (MemberInfo memberInfo in type.DeclaredMembers)
			{
				AnalyzeMember(classData, type, memberInfo);
			}
		}

		foreach (KeyValuePair<string, List<EnumData>> pair in localEnumData)
		{
			if (analyzeData.ClassesByName.TryGetValue(pair.Key, out ClassData classData))
			{
				classData.Enums.AddRange(pair.Value);
			}
			else
			{
				ClassData? fallbackClassData = analyzeData.ClassesByName.Values.FirstOrDefault(cd => cd.Name.Split('.').Contains(pair.Key));
				if (fallbackClassData != null)
				{
					fallbackClassData.Enums.AddRange(pair.Value);
				}
				else
				{
					Logger.Error($"No class with name {pair.Key} was found for {pair.Value.Count} local enums");
					return;
				}
			}
		}
	}

	private static void AnalyzeMember(ClassData classData, TypeInfo baseType, MemberInfo memberInfo)
	{
		switch (memberInfo)
		{
			case PropertyInfo propertyInfo:
				ParameterInfo[] indexParams = propertyInfo.GetIndexParameters();
				if (indexParams.Length > 0)
				{
					classData.Indexer.Add(new IndexerData
					{
						Protection = EnumExtension.Max(propertyInfo.GetMethod.GetProtectionLevel(), propertyInfo.SetMethod.GetProtectionLevel()),
						HasGetter = propertyInfo.GetMethod != null,
						HasSetter = propertyInfo.SetMethod != null,
						Parameter = indexParams.Select(p => p.GetParameterData()).ToArray(),
						Return = propertyInfo.PropertyType.GetCleanName()
					});

                    break;
                }

                PropertyData propertyData = new()
                {
                    Getter = null,
                    Setter = null,
                    Name = propertyInfo.Name.Split('.').Last(),
                    Type = propertyInfo.PropertyType.GetCleanName()
                };

                if (propertyInfo.GetMethod != null)
                {
					propertyData.Getter = propertyInfo.GetMethod.GetProtectionLevel();
					propertyData.Modifier |= propertyInfo.GetMethod.Attributes.GetModifiers(baseType.IsInterface);
				}

				if (propertyInfo.SetMethod != null)
				{
					propertyData.Setter = propertyInfo.SetMethod.GetProtectionLevel();
					propertyData.Modifier |= propertyInfo.SetMethod.Attributes.GetModifiers(baseType.IsInterface);
				}

				classData.Properties.Add(propertyData);
				break;
			case FieldInfo fieldInfo:
				if (fieldInfo.Name.Contains(">k__BackingField"))
				{
					break;
				}

                classData.Fields.Add(new FieldData
                {
                    Protection = fieldInfo.Attributes.GetProtectionLevel(),
                    Modifier = fieldInfo.Attributes.GetModifiers(),
                    Name = fieldInfo.Name,
                    Type = fieldInfo.FieldType.GetCleanName()
                });
                break;
            case MethodInfo methodInfo:
				if (methodInfo.IsConstructor ||
				    methodInfo.Attributes.HasFlag(MethodAttributes.SpecialName) ||
				    methodInfo.Attributes.HasFlag(MethodAttributes.RTSpecialName))
				{
					break;
				}

				classData.Methods.Add(new MethodData
				{
					Protection = methodInfo.GetProtectionLevel(),
					Modifier = methodInfo.Attributes.GetModifiers(baseType.IsInterface),
					Name = methodInfo.GetCleanName().Split('.').Last(),
					Parameter = methodInfo.GetParameters().Select(p => p.GetParameterData()).ToArray(),
					Return = methodInfo.ReturnType.GetCleanName()
				});
				break;
		}
	}
}
