﻿using CSharpLua;
using Pidgin;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using War3Net.Build;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using War3Net.Build.Info;
using War3Net.Build.Script;
using War3Net.Build.Widget;
using War3Net.CodeAnalysis.Decompilers;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.IO.Mpq;
using Camera = War3Net.Build.Environment.Camera;
using Region = War3Net.Build.Environment.Region;

namespace WC3MapDeprotector
{

    public class IndexedJassCompilationUnitSyntax
    {
        public JassCompilationUnitSyntax CompilationUnit { get; }
        public Dictionary<string, JassFunctionDeclarationSyntax> IndexedFunctions { get; }
        public IndexedJassCompilationUnitSyntax(JassCompilationUnitSyntax compilationUnit)
        {
            CompilationUnit = compilationUnit;
            IndexedFunctions = CompilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>().GroupBy(x => x.FunctionDeclarator.IdentifierName.Name).ToDictionary(x => x.Key, x => x.First());
        }
    }

    public class DecompilationMetaData
    {
        public Dictionary<UnitData, UnitDataDecompilationMetaData> Units { get; set; }
        public Dictionary<Camera, ObjectManagerDecompilationMetaData> Cameras { get; set; }
        public Dictionary<Sound, ObjectManagerDecompilationMetaData> Sounds { get; set; }
        public Dictionary<Region, ObjectManagerDecompilationMetaData> Regions { get; set; }

        public List<ObjectManagerDecompilationMetaData> AllMetaData
        {
            get
            {
                var result = new List<ObjectManagerDecompilationMetaData>();
                if (Units?.Values?.Any() == true)
                {
                    result.AddRange(Units.Values);
                }
                if (Cameras?.Values?.Any() == true)
                {
                    result.AddRange(Cameras.Values);
                }
                if (Sounds?.Values?.Any() == true)
                {
                    result.AddRange(Sounds.Values);
                }
                if (Regions?.Values?.Any() == true)
                {
                    result.AddRange(Regions.Values);
                }
                return result;
            }
        }
    }

    public class ScriptMetaData
    {
        private MapSounds sounds;

        public MapInfo Info { get; set; }
        public MapSounds Sounds
        {
            get
            {
                var result = sounds ?? new MapSounds(MapSoundsFormatVersion.v3);
                if (!result.Sounds.Any(x => x.FilePath == "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76"))
                {
                    result.Sounds.Add(new Sound() { FilePath = "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76", Name = "\x67\x67\x5F\x73\x6E\x64\x5F\x77\x61\x72\x33\x6D\x61\x70" });
                }
                return result;
            }
            set => sounds = value;
        }
        public MapCameras Cameras { get; set; }
        public MapRegions Regions { get; set; }
        public MapTriggers Triggers { get; set; }
        public MapCustomTextTriggers CustomTextTriggers { get; set; }
        public TriggerStrings TriggerStrings { get; set; }
        public MapUnits Units { get; set; }
        public List<string> Destructables { get; set; }

        public List<MpqKnownFile> ConvertToFiles()
        {
            var map = new Map() { Info = Info, Units = Units, Sounds = Sounds, Cameras = Cameras, Regions = Regions, Triggers = Triggers, TriggerStrings = TriggerStrings, CustomTextTriggers = CustomTextTriggers };
            try
            {
                return map.GetAllFiles();
            }
            catch
            {
                return new List<MpqKnownFile>();
            }
        }
    }

    public static partial class JassTriggerExtensions
    {
        public static HashSet<string> NativeEditorFunctions
        {
            get
            {
                return new HashSet<string>(_nativeEditorFunctions);
            }
        }

        private static readonly HashSet<string> _nativeEditorFunctions;
        private static readonly Dictionary<Type, List<MemberInfo>> _jassParserASTNodeChildren;
        static JassTriggerExtensions()
        {
            _nativeEditorFunctions = new HashSet<string>() { "config", "main", "CreateAllUnits", "CreateAllItems", "CreateNeutralPassiveBuildings", "CreateNeutralHostileBuildings", "CreatePlayerBuildings", "CreatePlayerUnits", "InitCustomPlayerSlots", "InitGlobals", "InitCustomTriggers", "RunInitializationTriggers", "CreateRegions", "CreateCameras", "InitSounds", "InitCustomTeams", "InitAllyPriorities", "CreateNeutralPassive", "CreateNeutralHostile", "InitUpgrades", "InitTechTree", "CreateAllDestructables", "InitBlizzard" };
            for (var playerIdx = 0; playerIdx <= 23; playerIdx++)
            {
                _nativeEditorFunctions.Add($"InitUpgrades_Player{playerIdx}");
                _nativeEditorFunctions.Add($"InitTechTree_Player{playerIdx}");
                _nativeEditorFunctions.Add($"CreateBuildingsForPlayer{playerIdx}");
                _nativeEditorFunctions.Add($"CreateUnitsForPlayer{playerIdx}");
            }

            var jassParserSyntaxTypes = new HashSet<Type>(typeof(JassCompilationUnitSyntax).Assembly.GetTypes().Where(x => x.Namespace.Equals("War3Net.CodeAnalysis.Jass.Syntax", StringComparison.InvariantCultureIgnoreCase)));
            _jassParserASTNodeChildren = jassParserSyntaxTypes.ToDictionary(x => x, astNodeType => astNodeType.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(property =>
            {
                var memberType = (property as FieldInfo)?.FieldType ?? (property as PropertyInfo)?.PropertyType;
                if (memberType == null)
                {
                    return false;
                }

                if (jassParserSyntaxTypes.Contains(memberType))
                {
                    return true;
                }

                if (memberType.IsGenericType && memberType.GenericTypeArguments.Any(x => jassParserSyntaxTypes.Contains(x)))
                {
                    return true;
                }

                return false;
            }).ToList());
        }

        [GeneratedRegex(@"^\s*function\s+(\w+)\s+takes", RegexOptions.IgnoreCase)]
        public static partial Regex Regex_JassFunctionDeclaration();

        [GeneratedRegex(@"^\s*call\s+(\w+)\s*\(", RegexOptions.IgnoreCase)]
        public static partial Regex Regex_JassFunctionCall();

        [GeneratedRegex(@"\s*(constant\s*)?(\S+)\s+(array\s*)?([^ \t=]+)\s*(=)?\s*(.*)", RegexOptions.IgnoreCase)]
        public static partial Regex Regex_ParseJassVariableDeclaration();

        public static Dictionary<IJassSyntaxToken, IJassSyntaxToken> JassAST_CreateChildToParentMapping(JassCompilationUnitSyntax compilationUnit)
        {
            var result = new Dictionary<IJassSyntaxToken, IJassSyntaxToken>();
            var allTokens = compilationUnit.GetChildren_RecursiveDepthFirst();
            foreach (var parent in allTokens)
            {
                foreach (var child in parent.GetChildren())
                {
                    result[child] = parent;
                }
            }

            return result;
        }

        private static void JassASTNode_ReplaceChild(object parent_jassASTNode, object oldChild_jassASTNode, object replacementChild_jassASTNode)
        {
            var parentType = parent_jassASTNode.GetType();

            if (!_jassParserASTNodeChildren.TryGetValue(parentType, out var childrenProperties))
            {
                return;
            }

            foreach (var childProperty in childrenProperties)
            {
                object value = null;
                if (childProperty is PropertyInfo propertyInfo)
                {
                    value = propertyInfo.GetValue(parent_jassASTNode);
                    if (value == oldChild_jassASTNode)
                    {
                        propertyInfo.SetValue(parent_jassASTNode, replacementChild_jassASTNode);
                    }
                }
                if (childProperty is FieldInfo fieldInfo)
                {
                    value = fieldInfo.GetValue(parent_jassASTNode);
                    if (value == oldChild_jassASTNode)
                    {
                        fieldInfo.SetValue(parent_jassASTNode, replacementChild_jassASTNode);
                    }
                }

                if (value is System.Collections.IList list)
                {
                    var valueType = value.GetType();
                    var isImmutable = valueType.Name.StartsWith("Immutable") || valueType.Name.StartsWith("ReadOnly");
                    if (isImmutable)
                    {
                        list = list.Cast<object>().ToList();
                    }

                    var oldIndex = list.IndexOf(oldChild_jassASTNode);
                    if (oldIndex != -1)
                    {
                        list.RemoveAt(oldIndex);
                        list.Insert(oldIndex, replacementChild_jassASTNode);
                    }

                    if (isImmutable)
                    {
                        if (valueType.IsGenericType && valueType.Name.StartsWith(nameof(ImmutableArray) + "`"))
                        {
                            var childGenericType = valueType.GenericTypeArguments[0];
                            var genericList = list.ToGenericListOfType(childGenericType);
                            var methodInfo_toImmutableArray = typeof(ImmutableArray).GetMethods(BindingFlags.Public | BindingFlags.Static).First(x => x.Name == nameof(ImmutableArray.ToImmutableArray) && x.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)).MakeGenericMethod(childGenericType);
                            var immutableArray = methodInfo_toImmutableArray.Invoke(null, new[] { genericList });

                            if (childProperty is PropertyInfo listPropertyInfo)
                            {
                                listPropertyInfo.SetValue(parent_jassASTNode, immutableArray);
                            }
                            if (childProperty is FieldInfo listFieldInfo)
                            {
                                listFieldInfo.SetValue(parent_jassASTNode, immutableArray);
                            }
                        }
                        else
                        {
                            DebugSettings.Warn("Unknown readonly type");
                        }
                    }
                }
            }
        }

        public static List<IStatementSyntax> ExtractStatements_IncludingEnteringFirstExecutionOfEachFunctionCall(IndexedJassCompilationUnitSyntax indexedCompilationUnit, string startingFunctionName)
        {
            var alreadyVisitedFunctions = new HashSet<string>();

            if (!indexedCompilationUnit.IndexedFunctions.TryGetValue(startingFunctionName, out var function))
            {
                return new List<IStatementSyntax>();
            }

            var result = new List<IJassSyntaxToken>();
            var stack = new Stack<IJassSyntaxToken>();
            stack.Push(function);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                result.Add(current);

                var children = current.GetChildren();
                if (children != null)
                {
                    foreach (var child in children.Reverse())
                    {
                        stack.Push(child);

                        if (child is JassFunctionReferenceExpressionSyntax functionReference)
                        {
                            if (indexedCompilationUnit.IndexedFunctions.TryGetValue(functionReference.IdentifierName.Name, out var nestedFunctionCall))
                            {
                                if (!alreadyVisitedFunctions.Contains(functionReference.IdentifierName.Name))
                                {
                                    alreadyVisitedFunctions.Add(functionReference.IdentifierName.Name);
                                    stack.Push(nestedFunctionCall);
                                }
                            }
                        }
                        else if (child is JassCallStatementSyntax callStatement)
                        {
                            if (indexedCompilationUnit.IndexedFunctions.TryGetValue(callStatement.IdentifierName.Name, out var nestedFunctionCall))
                            {
                                if (!alreadyVisitedFunctions.Contains(callStatement.IdentifierName.Name))
                                {
                                    alreadyVisitedFunctions.Add(callStatement.IdentifierName.Name);
                                    stack.Push(nestedFunctionCall);
                                }
                            }
                            else if (string.Equals(callStatement.IdentifierName.Name, "ExecuteFunc", StringComparison.InvariantCultureIgnoreCase) && callStatement.Arguments.Arguments.FirstOrDefault() is JassStringLiteralExpressionSyntax execFunctionName)
                            {
                                if (indexedCompilationUnit.IndexedFunctions.TryGetValue(execFunctionName.Value, out var execNestedFunctionCall))
                                {
                                    if (!alreadyVisitedFunctions.Contains(execFunctionName.Value))
                                    {
                                        alreadyVisitedFunctions.Add(execFunctionName.Value);
                                        stack.Push(nestedFunctionCall);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return result.OfType<IStatementSyntax>().ToList();
        }

        public static void SetTriggersFromDecompiledJass(this ScriptMetaData result, Map map, JassCompilationUnitSyntax jassParsed, DecompilationMetaData decompilationMetaData)
        {
            //todo: review all non-commented lines in _old functions after deprotection to find pieces I'm not decompiling (example: "CreateAllDestructables", "InitTechTree")

            var commentReplacements = new List<(object parent, object originalStatement, object commentedStatement)>();
            var astNodeToParent = JassAST_CreateChildToParentMapping(jassParsed);
            var allDecompiledFromStatements = decompilationMetaData.AllMetaData.SelectMany(x => x.DecompiledFromStatements).ToList();
            foreach (var statement in allDecompiledFromStatements)
            {
                if (!astNodeToParent.TryGetValue(statement, out var parent))
                {
                    continue;
                }

                using (var writer = new StringWriter())
                {
                    var renderer = new JassRenderer(writer);
                    renderer.Render(statement);
                    var statementAsString = writer.GetStringBuilder().ToString();

                    commentReplacements.Add((parent, statement, new JassCommentSyntax(statementAsString)));
                }
            }

            //Note: We compare objects by-reference between JassCompilationUnitSyntax & DecompilationMetaData to find statements to comment, so we can't clone JassCompilationUnitSyntax. By not cloning, we break the original JassCompilationUnitSyntax when adding comments, so we revert the changes afterwards.
            foreach (var replacement in commentReplacements)
            {
                JassASTNode_ReplaceChild(replacement.parent, replacement.originalStatement, replacement.commentedStatement);
            }

            var jassScript = jassParsed.RenderScriptAsString();

            foreach (var replacement in commentReplacements)
            {
                JassASTNode_ReplaceChild(replacement.parent, replacement.commentedStatement, replacement.originalStatement);
            }

            //todo: re-code this string manipulation to use AST
            var lines = jassScript.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

            var functionDeclarations = lines.Select((x, y) => new { lineIdx = y, match = Regex_JassFunctionDeclaration().Match(x) }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).ToDictionary(x => x.Key, x => x.ToList());
            var functionCalls = lines.Select((x, y) => new { lineIdx = y, match = Regex_JassFunctionCall().Match(x) }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).ToDictionary(x => x.Key, x => x.ToList());
            var functions = functionDeclarations.Keys.Concat(functionCalls.Keys).ToHashSet();

            var nativeEditorFunctionIndexes = new Dictionary<string, Tuple<int, int>>();
            var nativeEditorFunctionsRenamed = new Dictionary<string, string>();
            foreach (var nativeEditorFunction in _nativeEditorFunctions)
            {
                var renamed = nativeEditorFunction;
                do
                {
                    renamed += "_old";
                } while (functions.Contains(renamed));

                nativeEditorFunctionsRenamed[nativeEditorFunction] = renamed;

                if (functionDeclarations.TryGetValue(nativeEditorFunction, out var declarationMatches))
                {
                    foreach (var declaration in declarationMatches)
                    {
                        lines[declaration.lineIdx] = lines[declaration.lineIdx].Replace(nativeEditorFunction, renamed);
                    }
                }

                if (functionCalls.TryGetValue(nativeEditorFunction, out var callMatches))
                {
                    foreach (var call in callMatches)
                    {
                        lines[call.lineIdx] = lines[call.lineIdx].Replace(nativeEditorFunction, renamed);
                    }
                }
            }

            var startGlobalsLineIdx = lines.FindIndex(x => x.Trim() == "globals");
            var endGlobalsLineIdx = lines.FindIndex(x => x.Trim() == "endglobals");
            var globalLines = lines.Skip(startGlobalsLineIdx + 1).Take(endGlobalsLineIdx - startGlobalsLineIdx - 1).ToArray();
            var userGlobalLines = new List<string>();
            if (startGlobalsLineIdx != -1)
            {
                foreach (var globalLine in globalLines)
                {
                    bool userGenerated = true;

                    var match = Regex_ParseJassVariableDeclaration().Match(globalLine);
                    if (match.Success)
                    {
                        var name = (match.Groups[4].Value ?? "").Trim();
                        if (name.StartsWith("gg_", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (name.StartsWith("gg_trg_"))
                            {
                                var editorName = name.Substring(3);
                                var userGeneratedName = "udg_" + editorName;
                                jassScript = jassScript.Replace(name, userGeneratedName);
                            }
                            else
                            {
                                userGenerated = false;
                            }
                        }
                        else if (!name.StartsWith("udg_", StringComparison.InvariantCultureIgnoreCase))
                        {
                            DebugSettings.Warn("Unknown variable prefix");
                        }
                    }
                    else
                    {
                        DebugSettings.Warn("Unable to parse variable declaration");
                    }

                    if (userGenerated)
                    {
                        userGlobalLines.Add(globalLine);
                    }
                }
            }

            lines.RemoveRange(startGlobalsLineIdx, endGlobalsLineIdx - startGlobalsLineIdx + 1);
            lines.InsertRange(startGlobalsLineIdx, userGlobalLines);
            lines.Insert(startGlobalsLineIdx, "globals");
            lines.Insert(startGlobalsLineIdx, "//If you get compiler errors, Ensure vJASS is enabled");
            lines.Insert(startGlobalsLineIdx + userGlobalLines.Count + 2, "endglobals");
            jassScript = new StringBuilder().AppendJoin("\r\n", lines.ToArray()).ToString();

            var triggerItemIdx = 0;
            var rootCategoryItemIdx = triggerItemIdx++;
            var triggersCategoryItemIdx = triggerItemIdx++;

            result.Triggers = new MapTriggers(MapTriggersFormatVersion.v7, MapTriggersSubVersion.v4) { GameVersion = 2 };
            result.Triggers.TriggerItems.Add(new TriggerCategoryDefinition(TriggerItemType.RootCategory) { Id = rootCategoryItemIdx, ParentId = -1, Name = "script.w3x" });

            result.Triggers.TriggerItems.Add(new TriggerCategoryDefinition() { Id = triggersCategoryItemIdx, ParentId = rootCategoryItemIdx, Name = "Triggers", IsExpanded = true });
            var mainRenamed = nativeEditorFunctionsRenamed["main"];
            result.Triggers.TriggerItems.Add(new TriggerDefinition() { Id = triggerItemIdx++, ParentId = triggersCategoryItemIdx, Name = "MainDeprotected", Functions = new List<TriggerFunction>() { new TriggerFunction() { IsEnabled = true, Type = TriggerFunctionType.Event, Name = "MapInitializationEvent" }, new TriggerFunction() { IsEnabled = true, Type = TriggerFunctionType.Action, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = $"call {mainRenamed}()" } } } }, IsInitiallyOn = true, IsEnabled = true, RunOnMapInit = true, Description = $"Call {mainRenamed} which was extracted from protected map in case it had extra code that failed to decompile into GUI" });

            if (decompilationMetaData.Units?.Any() == true)
            {
                result.Triggers ??= new MapTriggers(MapTriggersFormatVersion.v7, MapTriggersSubVersion.v4) { GameVersion = 2 };
                var maxTriggerItemId = result.Triggers.TriggerItems?.Any() == true ? result.Triggers.TriggerItems.Select(x => x.Id).Max() + 1 : 0;
                var rootCategory = result.Triggers.TriggerItems.FirstOrDefault(x => x.Type == TriggerItemType.RootCategory);
                if (rootCategory == null)
                {
                    rootCategory = new TriggerCategoryDefinition(TriggerItemType.RootCategory) { Id = maxTriggerItemId++, ParentId = -1, Name = "script.w3x" };
                    result.Triggers.TriggerItems.Add(rootCategory);
                }
                var category = result.Triggers.TriggerItems.FirstOrDefault(x => x.Type == TriggerItemType.Category);
                if (category == null)
                {
                    category = new TriggerCategoryDefinition(TriggerItemType.Category) { Id = maxTriggerItemId++, ParentId = rootCategory.Id, Name = "Deprotect ObjectManager Variables", IsExpanded = true };
                    result.Triggers.TriggerItems.Add(category);
                }

                result.Destructables = new List<string>();
                var emptyVariableTrigger = new TriggerDefinition() { Description = "Disabled GUI trigger with fake code, just to convert ObjectManager units/items/cameras to global generated variables", Name = "GlobalGeneratedObjectManagerVariables", ParentId = category.Id, IsEnabled = true, IsInitiallyOn = false };
                var variables = (result.Units?.Units?.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).Concat(result.Cameras?.Cameras?.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).Concat(map.Doodads?.Doodads.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).ToList();
                foreach (var variable in variables)
                {
                    var isUnit = variable.StartsWith("gg_unit_");
                    var isItem = variable.StartsWith("gg_item_");
                    var isDestructable = variable.StartsWith("gg_dest_");

                    var jassVariableSearchString = isUnit || isItem ? variable.Substring(0, variable.Length - 5) : variable; // Removes _#### (CreationNumber) suffix since it changes after deprotection & having extra variables won't break anything

                    if (!jassScript.Contains(jassVariableSearchString))
                    {
                        continue;
                    }

                    if (isUnit)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "ResetUnitAnimation", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                    else if (isDestructable)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "SetDestAnimationSpeedPercent", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = "100" } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                        result.Destructables.Add(variable);
                    }
                    else if (isItem)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "UnitDropItemSlotBJ", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Function, Value = "GetLastCreatedUnit", Function = new TriggerFunction() { Name = "GetLastCreatedUnit", Type = TriggerFunctionType.Call, IsEnabled = true } } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = "1" } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                    else if (variable.StartsWith("gg_cam_"))
                    {
                        var triggerFunction = new TriggerFunction() { Name = "BlzCameraSetupGetLabel", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                }
                result.Triggers.TriggerItems.Add(emptyVariableTrigger);
            }

            var reformatted = JassSyntaxFactory.ParseCompilationUnit(jassScript).RenderScriptAsString();

            result.CustomTextTriggers.GlobalCustomScriptCode.Code = reformatted.Replace("%", "%%");
        }
    }
}