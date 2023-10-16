using AssetRipper.UnityPackageAnalyzer.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AssetRipper.UnityPackageAnalyzer.Analyzer;

public static class SyntaxTreeExtension
{
	public static ProtectionLevel GetProtectionLevel(this SyntaxNode node, SyntaxTokenList tokenList)
	{
		ProtectionLevel protection = ProtectionLevel.UNKNOWN;
		bool isProtected = false;

		foreach (SyntaxToken token in tokenList)
		{
			if (token.Value == null)
			{
				continue;
			}

			switch (token.ValueText)
			{
				case "public":
					return ProtectionLevel.PUBLIC;
				case "protected":
					isProtected = true;
					break;
				case "internal":
					protection = ProtectionLevel.INTERNAL;
					break;
				case "private":
					protection = ProtectionLevel.PRIVATE;
					break;
			}
		}

		if (isProtected)
		{
			switch (protection)
			{
				case ProtectionLevel.INTERNAL:
					return ProtectionLevel.PROTECTED_INTERNAL;
				case ProtectionLevel.PRIVATE:
					return ProtectionLevel.PRIVATE_PROTECTED;
				default:
					return ProtectionLevel.PROTECTED;
			}
		}

		if (protection == ProtectionLevel.UNKNOWN)
		{
			// See https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/access-modifiers#default-access-summary-table
			switch (node)
			{
				case StructDeclarationSyntax:
				case ClassDeclarationSyntax:
				case InterfaceDeclarationSyntax:
				case EnumDeclarationSyntax:
					return node.Parent is NamespaceDeclarationSyntax ? ProtectionLevel.INTERNAL : ProtectionLevel.PRIVATE;
				default:
					return node.Parent is InterfaceDeclarationSyntax ? ProtectionLevel.PUBLIC : ProtectionLevel.PRIVATE;
			}
		}

		return protection;
	}

	public static Modifier GetModifiers(this SyntaxTokenList tokenList, Modifier inheritingModifier = Modifier.NONE)
	{
		Modifier modifiers = inheritingModifier;
		foreach (SyntaxToken token in tokenList)
		{
			if (token.Value == null)
			{
				continue;
			}

			switch (token.ValueText)
			{
				case "static":
					modifiers |= Modifier.STATIC;
					break;
				case "readonly":
					modifiers |= Modifier.READONLY;
					break;
				case "abstract":
					modifiers |= Modifier.ABSTRACT;
					break;
				case "sealed":
					modifiers |= Modifier.SEALED;
					break;
				case "const":
					return Modifier.CONST;
			}
		}

		return modifiers;
	}

	public static string GetTypeText(this TypeSyntax? typeSyntax, Dictionary<string, string> usingAlias)
	{
		string name = typeSyntax switch
		{
			GenericNameSyntax genericName => $"{genericName.Identifier.Text}<{string.Join(',', genericName.TypeArgumentList.Arguments.Select(a => a.GetTypeText(usingAlias)))}>",
			ArrayTypeSyntax arrayType => arrayType.ElementType.GetTypeText(usingAlias) + "[]",
			PointerTypeSyntax refType => refType.ElementType.GetTypeText(usingAlias) + "*",
			NullableTypeSyntax nullableType => $"Nullable<{nullableType.ElementType.GetTypeText(usingAlias)}>",
			QualifiedNameSyntax qualifiedName => qualifiedName.Right.GetTypeText(usingAlias),
			RefTypeSyntax refType => refType.Type.GetTypeText(usingAlias),
			IdentifierNameSyntax idType => idType.Identifier.Text,
			PredefinedTypeSyntax preType => preType.Keyword.Text switch
			{
				"string" => "String",
				"sbyte" => "SByte",
				"byte" => "Byte",
				"short" => "Int16",
				"ushort" => "UInt16",
				"int" => "Int32",
				"uint" => "UInt32",
				"long" => "Int64",
				"ulong" => "UInt64",
				"char" => "Char",
				"float" => "Single",
				"double" => "Double",
				"bool" => "Boolean",
				"decimal" => "Decimal",
				"void" => "Void",
				"object" => "Object",
				_ => preType.Keyword.Text
			},
			_ => "ERROR GETTING TYPE"
		};

		return usingAlias.TryGetValue(name, out string aliasName) ? aliasName : name;
	}

    public static ParameterData GetParameterData(this ParameterSyntax paraSyntax, Dictionary<string, string> usingAlias)
    {
        return new ParameterData
        {
            Type = paraSyntax.Type.GetTypeText(usingAlias),
            Name = paraSyntax.Identifier.ValueText,
            Modifier = paraSyntax.Modifiers.GetParameterModifiers()
        };
    }

    private static ParameterModifier GetParameterModifiers(this SyntaxTokenList tokenList)
	{
		foreach (SyntaxToken token in tokenList)
		{
			if (token.Value == null)
			{
				continue;
			}

			switch (token.ValueText)
			{
				case "ref":
					return ParameterModifier.REF;
				case "out":
					return ParameterModifier.OUT;
			}
		}

		return ParameterModifier.NONE;
	}

	public static string GetClassName(this SyntaxToken identifier, SyntaxNode? inlineParent, TypeParameterListSyntax? genericParameters)
	{
		string name = identifier.GetCleanName(genericParameters);

		// Inline class/struct/etc
		return inlineParent switch
		{
			StructDeclarationSyntax structSyntax => structSyntax.Identifier.GetClassName(structSyntax.Parent, structSyntax.TypeParameterList) + "." + name,
			ClassDeclarationSyntax classSyntax => classSyntax.Identifier.GetClassName(classSyntax.Parent, classSyntax.TypeParameterList) + "." + name,
			InterfaceDeclarationSyntax interfaceSyntax => interfaceSyntax.Identifier.GetClassName(interfaceSyntax.Parent, interfaceSyntax.TypeParameterList) + "." + name,
			EnumDeclarationSyntax enumSyntax => enumSyntax.Identifier.GetClassName(enumSyntax.Parent, null) + "." + name,
			DelegateDeclarationSyntax delegateSyntax => delegateSyntax.Identifier.GetClassName(delegateSyntax.Parent, delegateSyntax.TypeParameterList) + "." + name,
			_ => name
		};
	}

	public static string GetCleanName(this SyntaxToken identifier, TypeParameterListSyntax? genericParameters)
	{
		string name = identifier.Text;

		if (genericParameters != null)
		{
			name += "<" + string.Join(',', genericParameters.Parameters.Select(p => p.Identifier.Text)) + ">";
		}

		return name;
	}
}
