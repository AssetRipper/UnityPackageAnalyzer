using AssetRipper.AnalyzeUnityPackages.Helper;
using AssetRipper.AnalyzeUnityPackages.PackageDownloader;
using AssetRipper.AnalyzeUnityPackages.Primitives;
using AssetRipper.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AssetRipper.AnalyzeUnityPackages.Analyzer;

public static class PackageAnalyzer
{
    private static readonly string analyzeResultPath =
        Path.Combine(Path.GetTempPath(), "AssetRipper", "AnalyzeUnityPackages", "AnalyzedPackages");

    public static bool HasAnyAnalyzedPackages(string packageId)
    {
        string packageDir = Path.Combine(analyzeResultPath, packageId);
        return Directory.Exists(packageDir) && Directory.EnumerateFileSystemEntries(packageDir).Any();
    }

    public static bool HasAnalyzedPackage(string packageId, string version)
    {
        string analyzeFile = Path.Combine(analyzeResultPath, packageId, $"{version}.json");
        return File.Exists(analyzeFile);
    }

    public static List<AnalyzeData> GetAnalyzeResults(string packageId, UnityVersion unityVersion)
    {
        List<AnalyzeData> result = new();

        string packageDir = Path.Combine(analyzeResultPath, packageId);
        foreach (string filePath in Directory.EnumerateFiles(packageDir))
        {
            AnalyzeData? analyzeData = Serializer.DeserializeData<AnalyzeData>(filePath);

            if (analyzeData != null && analyzeData.MinUnityVersion < unityVersion)
            {
                result.Add(analyzeData);
            }
        }

        return result;
    }

    public static async Task AnalyzePackageAsync(string packageId, PackageVersion packageVersion, UnityVersion minUnityVersion, CancellationToken ct)
    {
        string packageDir = Path.Combine(analyzeResultPath, packageId);
        if (!Directory.Exists(packageDir))
        {
            Directory.CreateDirectory(packageDir);
        }

        string dstFile = Path.Combine(packageDir, packageVersion + ".json");
        if (File.Exists(dstFile))
        {
            Logger.Debug($"Analyze result already exist: {packageId}@{packageVersion}");
            return;
        }

        string srcDir = DownloadManager.GetExtractPath(packageId, packageVersion.ToString());
        if (!Directory.Exists(srcDir))
        {
            Logger.Error($"No directory found to analyze at: {srcDir}");
            return;
        }

        Logger.Debug($"Analyzing package {packageId}@{packageVersion}");
        AnalyzeData analyzeData = new(packageId, packageVersion, minUnityVersion);

        foreach (string file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            string fileDir = Path.GetDirectoryName(file) ?? string.Empty;
            if (fileDir.Contains("Editor") || fileDir.Contains("Test"))
            {
                continue;
            }

            // Excluded files and folders: https://docs.unity3d.com/Manual/SpecialFolders.html
            string fileName = Path.GetFileName(file);
            string relativePath = file[file.IndexOf("package", StringComparison.Ordinal)..];
            if (relativePath.Contains(@"\.") || relativePath.Contains(@"~\") ||relativePath.Contains(@"\cvs\") || fileName.EndsWith(".tmp"))
            {
                continue;
            }

            try
            {
                await AnalyzeFile(analyzeData, file, ct);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed analyzing {fileName}");
                throw;
            }
        }

        Serializer.SerializeAnalyzerDataAsync(analyzeData, dstFile, ct);
    }

    private static async Task AnalyzeFile(AnalyzeData analyzeData, string path, CancellationToken ct)
    {
	    UnityGuid unityGuid = UnityGuid.Zero;
        string metaFile = path + ".meta";
        if (File.Exists(metaFile))
        {
            await foreach (string line in File.ReadLinesAsync(metaFile, ct))
            {
                if (line.StartsWith("guid: "))
                {
                    unityGuid = UnityGuid.Parse(line[5..]);
                    break;
                }
            }
        }

        string context = await File.ReadAllTextAsync(path, ct);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(context, cancellationToken: ct);
        string namespaceText = string.Empty;
        Dictionary<string, string> usingAlias = new();

        Queue<SyntaxNode> workQueue = new((await syntaxTree.GetRootAsync(ct)).ChildNodes());
        while (workQueue.Count > 0)
        {
            SyntaxNode node = workQueue.Dequeue();
            switch (node)
            {
                case EnumDeclarationSyntax enumSyntax:
                    string enumName = enumSyntax.Identifier.GetClassName(enumSyntax.Parent, null);
                    analyzeData.GlobalEnums.Add(enumName, new EnumData
                    {
                        ProtectionLevel = enumSyntax.GetProtectionLevel(enumSyntax.Modifiers),
                        Name = enumName,
                        Values = enumSyntax.Members.Select(declaration => declaration.Identifier.Text).ToList()
                    });
                    break;
                case ClassDeclarationSyntax classSyntax:
                    string className = classSyntax.Identifier.GetClassName(classSyntax.Parent, classSyntax.TypeParameterList);
                    if (!analyzeData.ClassesByName.TryGetValue(className, out ClassData classData))
                    {
                        classData = new ClassData
                        {
                            Namespace = namespaceText,
                            ProtectionLevel = classSyntax.GetProtectionLevel(classSyntax.Modifiers),
                            Modifier = classSyntax.Modifiers.GetModifiers(),
                            Type = ClassType.CLASS,
                            Name = className,
                            UnityGuid = unityGuid
                        };

                        analyzeData.ClassesByName.Add(className, classData);
                    }

                    classData.Modifier |= classSyntax.Modifiers.GetModifiers();

                    if (classSyntax.BaseList != null)
                    {
                        foreach (string inheritor in classSyntax.BaseList.Types.Select(baseType => baseType.Type.GetTypeText(usingAlias)))
                        {
                            classData.Inheritors.Add(inheritor);
                        }
                    }

                    AnalyzeClass(workQueue, classSyntax, classData, usingAlias, ProtectionLevel.UNKNOWN, classData.Modifier & Modifier.STATIC);

                    break;
                case StructDeclarationSyntax structSyntax:
                    string structName = structSyntax.Identifier.GetClassName(structSyntax.Parent, structSyntax.TypeParameterList);
                    if (!analyzeData.ClassesByName.TryGetValue(structName, out ClassData structData))
                    {
                        structData = new ClassData
                        {
                            Namespace = namespaceText,
                            ProtectionLevel = structSyntax.GetProtectionLevel(structSyntax.Modifiers),
                            Type = ClassType.STRUCT,
                            Name = structName,
                            UnityGuid = unityGuid
                        };

                        analyzeData.ClassesByName.Add(structName, structData);
                    }

                    structData.Modifier |= structSyntax.Modifiers.GetModifiers();


                    if (structSyntax.BaseList != null)
                    {
                        foreach (string inheritor in structSyntax.BaseList.Types.Select(baseType => baseType.Type.GetTypeText(usingAlias)))
                        {
                            structData.Inheritors.Add(inheritor);
                        }
                    }

                    AnalyzeClass(workQueue, structSyntax, structData, usingAlias, ProtectionLevel.UNKNOWN, Modifier.NONE);
                    break;
                case InterfaceDeclarationSyntax interfaceSyntax:
                    ProtectionLevel interfaceProtection = interfaceSyntax.GetProtectionLevel(interfaceSyntax.Modifiers);
                    ClassData interfaceData = new()
                    {
                        Namespace = namespaceText,
                        ProtectionLevel = interfaceProtection,
                        Type = ClassType.INTERFACE,
                        Name = interfaceSyntax.Identifier.GetClassName(interfaceSyntax.Parent, interfaceSyntax.TypeParameterList),
                        UnityGuid = unityGuid
                    };

                    interfaceData.Modifier |= interfaceSyntax.Modifiers.GetModifiers();

                    if (interfaceSyntax.BaseList != null)
                    {
                        interfaceData.Inheritors = new SortedSet<string>(interfaceSyntax.BaseList.Types.Select(baseType => baseType.Type.GetTypeText(usingAlias)));
                    }

                    AnalyzeClass(workQueue, interfaceSyntax, interfaceData, usingAlias, ProtectionLevel.PUBLIC, Modifier.NONE);
                    analyzeData.ClassesByName.Add(interfaceData.Name, interfaceData);
                    break;
                case DelegateDeclarationSyntax delegateSyntax:
                    ClassData delegateData = new()
                    {
                        Namespace = namespaceText,
                        ProtectionLevel = delegateSyntax.GetProtectionLevel(delegateSyntax.Modifiers),
                        Modifier = delegateSyntax.Modifiers.GetModifiers(),
                        Type = ClassType.DELEGATE,
                        Name = delegateSyntax.Identifier.GetClassName(delegateSyntax.Parent, delegateSyntax.TypeParameterList),
                        UnityGuid = unityGuid
                    };

                    delegateData.Methods.Add(new MethodData
                    {
                        Protection = ProtectionLevel.PUBLIC,
                        Name = "Invoke",
                        Parameter = delegateSyntax.ParameterList.Parameters.Select(p => p.GetParameterData(usingAlias)).ToArray(),
                        Return = delegateSyntax.ReturnType.GetTypeText(usingAlias)
                    });

                    analyzeData.ClassesByName.Add(delegateData.Name, delegateData);
                    break;
                case NamespaceDeclarationSyntax namespaceSyntax:
                    namespaceText = namespaceSyntax.Name.ToString(); // Namespaces need full name and not only right side
                    workQueue.EnqueueRange(node.ChildNodes());
                    break;
                case UsingDirectiveSyntax usingSyntax:
                    if (usingSyntax.Alias != null)
                    {
                        usingAlias.Add(usingSyntax.Alias.Name.GetTypeText(new()) , usingSyntax.Name.GetTypeText(new()));
                    }
                    break;
            }
        }
    }

    private static void AnalyzeClass(Queue<SyntaxNode> namespaceWorkQueue, SyntaxNode classSyntax, ClassData classData, Dictionary<string, string> usingAlias, ProtectionLevel classInheritingProtection, Modifier classInheritingModifiers)
    {
        Queue<SyntaxNode> workQueue = new(classSyntax.ChildNodes());

        while (workQueue.Count > 0)
        {
            SyntaxNode node = workQueue.Dequeue();
            switch (node)
            {
                case FieldDeclarationSyntax fieldSyntax:
                    ProtectionLevel fieldProtectLevel = EnumExtension.Max(fieldSyntax.GetProtectionLevel(fieldSyntax.Modifiers), classInheritingProtection);
                    Modifier fieldModifiers = fieldSyntax.Modifiers.GetModifiers(classInheritingModifiers) & ~Modifier.SEALED;
                    string type = fieldSyntax.Declaration.Type.GetTypeText(usingAlias);

                    foreach (VariableDeclaratorSyntax declaratorSyntax in fieldSyntax.Declaration.Variables)
                    {
                        classData.Fields.Add(new FieldData
                        {
                            Protection = fieldProtectLevel,
                            Modifier = fieldModifiers,
                            Name = declaratorSyntax.Identifier.Text,
                            Type = type
                        });
                    }

                    break;
                case PropertyDeclarationSyntax propertySyntax:
                    ProtectionLevel baseProtection = EnumExtension.Max(propertySyntax.GetProtectionLevel(propertySyntax.Modifiers), classInheritingProtection);

                    if (propertySyntax.ExpressionBody != null) //Special case without AccessorList
                    {
                        classData.Properties.Add(new PropertyData
                        {
                            Getter = baseProtection,
                            Setter = null,
                            Modifier = propertySyntax.Modifiers.GetModifiers(classInheritingModifiers) & ~Modifier.SEALED,
                            Name = propertySyntax.Identifier.Text.Split('.').Last(),
                            Type = propertySyntax.Type.GetTypeText(usingAlias)
                        });
                        break;
                    }

                    SyntaxList<AccessorDeclarationSyntax> propertyAccessors = propertySyntax.AccessorList.Accessors;
                    AccessorDeclarationSyntax? getModifiers = propertyAccessors.FirstOrDefault(ac => ac.Keyword.ValueText == "get");
                    AccessorDeclarationSyntax? setModifiers = propertyAccessors.FirstOrDefault(ac => ac.Keyword.ValueText == "set");

                    classData.Properties.Add(new PropertyData
                    {
                        Getter = getModifiers switch
                        {
                            null => null,
                            { Modifiers.Count: > 0 } => getModifiers.GetProtectionLevel(getModifiers.Modifiers),
                            _ => baseProtection
                        },
                        Setter = setModifiers switch
                        {
                            null => null,
                            { Modifiers.Count: > 0 } => setModifiers.GetProtectionLevel(setModifiers.Modifiers),
                            _ => baseProtection
                        },
                        Modifier = propertySyntax.Modifiers.GetModifiers(classInheritingModifiers) & ~Modifier.SEALED,
                        Name = propertySyntax.Identifier.Text,
                        Type = propertySyntax.Type.GetTypeText(usingAlias)
                    });
                    break;
                case MethodDeclarationSyntax methodSyntax:
                    classData.Methods.Add(new MethodData
                    {
                        Protection = EnumExtension.Max(methodSyntax.GetProtectionLevel(methodSyntax.Modifiers), classInheritingProtection),
                        Modifier = methodSyntax.Modifiers.GetModifiers(classInheritingModifiers) & ~Modifier.SEALED,
                        Name = methodSyntax.Identifier.GetCleanName(methodSyntax.TypeParameterList).Split('.').Last(),
                        Parameter = methodSyntax.ParameterList.Parameters.Select(p => p.GetParameterData(usingAlias)).ToArray(),
                        Return = methodSyntax.ReturnType.GetTypeText(usingAlias)
                    });
                    break;
                case EnumDeclarationSyntax enumSyntax:
                    classData.Enums.Add(new EnumData
                    {
                        ProtectionLevel = enumSyntax.GetProtectionLevel(enumSyntax.Modifiers),
                        Name = enumSyntax.Identifier.Text,
                        Values = enumSyntax.Members.Select(declaration => declaration.Identifier.Text).ToList()
                    });
                    break;
                case IndexerDeclarationSyntax indexerSyntax:
                    SyntaxList<AccessorDeclarationSyntax> indexerAccessors = indexerSyntax.AccessorList.Accessors;
                    classData.Indexer.Add(new IndexerData
                    {
                        Protection = EnumExtension.Max(indexerSyntax.GetProtectionLevel(indexerSyntax.Modifiers), classInheritingProtection),
                        HasGetter = indexerAccessors.Any(ac => ac.Keyword.ValueText == "get"),
                        HasSetter = indexerAccessors.Any(ac => ac.Keyword.ValueText == "set"),
                        Modifier = indexerSyntax.Modifiers.GetModifiers(classInheritingModifiers),
                        Parameter = indexerSyntax.ParameterList.Parameters.Select(p => p.GetParameterData(usingAlias)).ToArray(),
                        Return = indexerSyntax.Type.GetTypeText(usingAlias)
                    });
                    break;

                case ClassDeclarationSyntax:
                case StructDeclarationSyntax:
                case InterfaceDeclarationSyntax:
                case DelegateDeclarationSyntax:
                    namespaceWorkQueue.Enqueue(node);
                    break;
            }
        }
    }
}
