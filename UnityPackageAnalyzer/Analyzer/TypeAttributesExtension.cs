using AssetRipper.UnityPackageAnalyzer.Primitives;
using System.Reflection;
using System.Runtime.CompilerServices;
using ParameterModifier = AssetRipper.UnityPackageAnalyzer.Primitives.ParameterModifier;

namespace AssetRipper.UnityPackageAnalyzer.Analyzer;

public static class TypeAttributesExtension
{
	public static ProtectionLevel GetProtectionLevel(this TypeInfo type)
	{
		if (type.IsPublic || type.IsNestedPublic)
		{
			return ProtectionLevel.PUBLIC;
		}

		if (type.IsNested && type.IsNestedFamORAssem)
		{
			return ProtectionLevel.PROTECTED_INTERNAL;
		}

		if (type.IsNested && type.IsNestedFamily)
		{
			return ProtectionLevel.PROTECTED;
		}

		if (type.IsNotPublic || (type.IsNested && type.IsNestedAssembly))
		{
			return ProtectionLevel.INTERNAL;
		}

		if (type.IsNested && type.IsNestedFamANDAssem)
		{
			return ProtectionLevel.PRIVATE_PROTECTED;
		}

		if (type.IsNested && type.IsNestedPrivate)
		{
			return ProtectionLevel.PRIVATE;
		}

		return ProtectionLevel.UNKNOWN;
	}

	public static ProtectionLevel GetProtectionLevel(this MethodInfo? methodInfo)
	{
		if (methodInfo == null)
		{
			return ProtectionLevel.UNKNOWN;
		}

		MethodAttributes attributes = methodInfo.Attributes;
		if (attributes.HasFlag(MethodAttributes.Public))
		{
			return ProtectionLevel.PUBLIC;
		}

		if (attributes.HasFlag(MethodAttributes.FamORAssem))
		{
			return ProtectionLevel.PROTECTED_INTERNAL;
		}

		if (attributes.HasFlag(MethodAttributes.Family))
		{
			return ProtectionLevel.PROTECTED;
		}

		if (attributes.HasFlag(MethodAttributes.Assembly))
		{
			return ProtectionLevel.INTERNAL;
		}

		if (attributes.HasFlag(MethodAttributes.FamANDAssem))
		{
			return ProtectionLevel.PRIVATE_PROTECTED;
		}

		if (attributes.HasFlag(MethodAttributes.Private))
		{
			return ProtectionLevel.PRIVATE;
		}

		return ProtectionLevel.UNKNOWN;
	}

	public static ProtectionLevel GetProtectionLevel(this FieldAttributes attributes)
	{
		if (attributes.HasFlag(FieldAttributes.Public))
		{
			return ProtectionLevel.PUBLIC;
		}

		if (attributes.HasFlag(FieldAttributes.FamORAssem))
		{
			return ProtectionLevel.PROTECTED_INTERNAL;
		}

		if (attributes.HasFlag(FieldAttributes.Family))
		{
			return ProtectionLevel.PROTECTED;
		}

		if (attributes.HasFlag(FieldAttributes.Assembly))
		{
			return ProtectionLevel.INTERNAL;
		}

		if (attributes.HasFlag(FieldAttributes.FamANDAssem))
		{
			return ProtectionLevel.PRIVATE_PROTECTED;
		}

		if (attributes.HasFlag(FieldAttributes.Private))
		{
			return ProtectionLevel.PRIVATE;
		}

		return ProtectionLevel.UNKNOWN;
	}

	public static Modifier GetModifiers(this Type type)
	{
		Modifier modifiers = Modifier.NONE;

		if (type.IsAbstract && type.IsSealed)
		{
			modifiers |= Modifier.STATIC;
		}
		else if (type.IsAbstract)
		{
			modifiers |= Modifier.ABSTRACT;
		}
		else if (type.IsSealed)
		{
			modifiers |= Modifier.SEALED;
		}

		return modifiers;
	}

	public static Modifier GetModifiers(this MethodAttributes attributes, bool isInsideInterface)
	{
		Modifier modifiers = Modifier.NONE;

		if (attributes.HasFlag(MethodAttributes.Static))
		{
			modifiers |= Modifier.STATIC;
		}

		if (!isInsideInterface && attributes.HasFlag(MethodAttributes.Abstract))
		{
			modifiers |= Modifier.ABSTRACT;
		}

		return modifiers;
	}

	public static Modifier GetModifiers(this FieldAttributes attributes)
	{
		Modifier modifiers = Modifier.NONE;

		if (attributes.HasFlag(FieldAttributes.Static))
		{
			modifiers |= Modifier.STATIC;
		}

		if (attributes.HasFlag(FieldAttributes.InitOnly))
		{
			modifiers |= Modifier.READONLY;
		}

		if (attributes.HasFlag(FieldAttributes.Literal))
		{
			return Modifier.CONST;
		}

		return modifiers;
	}

	public static string GetCleanName(this Type type, Type? declaringType = null)
	{
		string name = type.Name.Split('`')[0].TrimEnd('&');

		if (declaringType != null)
		{
			name = $"{declaringType.GetCleanName()}.{name}";
		}

		Type[] genericArguments = type.GetGenericArguments(); // Can't use t.IsGenericType => doesn't work on out parameters
		if (genericArguments.Length > 0)
		{
			name += "<" + string.Join(',', genericArguments.Select(t => t.GetCleanName())) + ">";
		}

		if (type.IsArray && !name.EndsWith("[]"))
		{
			name += "[]";
		}

		return name;
	}

	public static string GetCleanName(this MethodInfo info)
	{
		Type[] genericArguments = info.GetGenericArguments(); // Can't use t.IsGenericType => doesn't work on out parameters
		if (genericArguments.Length > 0)
		{
			return info.Name + "<" + string.Join(',', genericArguments.Select(t => t.GetCleanName())) + ">";
		}

		return info.Name;
	}

	public static bool IsCompilerGenerated(this Type type)
	{
		return type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
	}

	public static SortedSet<string> GetBaseAndAllInterfaceNames(this TypeInfo type)
	{
		List<Type> inheritors = new();

		if (type.BaseType != null &&
		    type.BaseType != typeof(object) &&
		    type.BaseType != typeof(ValueType))
		{
			inheritors.Add(type.BaseType);
		}

		inheritors.AddRange(type.ImplementedInterfaces.ToList());

		SortedSet<string> result = new(inheritors.Select(i => i.GetCleanName()));
		foreach (Type i in inheritors)
		{
			result.ExceptWith(i.GetTypeInfo().ImplementedInterfaces.Select(ii => ii.GetCleanName()));
		}

		return result;
	}

    public static ParameterData GetParameterData(this ParameterInfo parameter)
    {
        ParameterData data = new()
        {
            Type = parameter.ParameterType.GetCleanName(),
            Name = parameter.Name ?? string.Empty
        };

        if (parameter.IsOut)
        {
			data.Modifier = ParameterModifier.OUT;
		}
		else if (parameter.ParameterType.IsByRef)
		{
			data.Modifier = ParameterModifier.REF;
		}

		return data;
	}
}
