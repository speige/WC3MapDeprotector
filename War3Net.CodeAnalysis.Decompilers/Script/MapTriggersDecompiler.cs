// ------------------------------------------------------------------------------
// <copyright file="MapTriggersDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using War3Net.Build.Script;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler
    {
        public static HashSet<string> validGUIVariableTypes = new HashSet<string>() { "ability", "abilcode", "animtype", "attacktype", "boolean", "buffcode", "camerasetup", "weapontype", "commandbuttoneffect", "damagetype", "defeatcondition", "destructable", "destructablecode", "dialog", "button", "effecttype", "texttag", "gamecache", "gamespeed", "handle", "hashtable", "image", "imagetype", "integer", "item", "itemtype", "itemcode", "leaderboard", "lightning", "lightningtype", "minimapicon", "mousebuttontype", "multiboard", "ordercode", "pathingtype", "player", "playercolor", "force", "location", "quest", "questitem", "race", "real", "rect", "sound", "soundtype", "effect", "string", "subanimtype", "techcode", "terraindeformation", "terrainshape", "terraintype", "timer", "timerdialog", "trigger", "ubersplat", "ubersplattype", "unit", "group", "unitcode", "fogmodifier", "weathereffect" };
        public bool TryDecompileMapTriggers(MapTriggersFormatVersion formatVersion, MapTriggersSubVersion? subVersion, [NotNullWhen(true)] out MapTriggers? mapTriggers)
        {
            var initGlobals = GetFunction("InitGlobals");
            var initCustomTriggers = GetFunction("InitCustomTriggers");
            var runInitializationTriggers = GetFunction("RunInitializationTriggers");

            if (TryDecompileMapTriggers(
                initGlobals?.FunctionDeclaration,
                initCustomTriggers?.FunctionDeclaration,
                runInitializationTriggers?.FunctionDeclaration,
                formatVersion,
                subVersion,
                out mapTriggers))
            {
                if (initGlobals is not null)
                {
                    initGlobals.Handled = true;
                }

                if (initCustomTriggers is not null)
                {
                    initCustomTriggers.Handled = true;
                }

                if (runInitializationTriggers is not null)
                {
                    runInitializationTriggers.Handled = true;
                }

                return true;
            }

            mapTriggers = null;
            return false;
        }

        public bool TryDecompileMapTriggers(
            JassFunctionDeclarationSyntax? initGlobalsFunction,
            JassFunctionDeclarationSyntax? initCustomTriggersFunction,
            JassFunctionDeclarationSyntax? runInitializationTriggersFunction,
            MapTriggersFormatVersion formatVersion,
            MapTriggersSubVersion? subVersion,
            [NotNullWhen(true)] out MapTriggers? mapTriggers)
        {
            const int RootCategoryId = (int)TriggerItemTypeId.RootCategory << 24;

            var categoryId = subVersion.HasValue ? (int)TriggerItemTypeId.Category << 24 : 0;
            var variableId = (int)TriggerItemTypeId.Variable << 24;
            var triggerId = (int)TriggerItemTypeId.Gui << 24;

            mapTriggers = new MapTriggers(formatVersion, subVersion)
            {
                GameVersion = 2,
            };

            if (subVersion.HasValue)
            {
                mapTriggers.TriggerItems.Add(new TriggerCategoryDefinition(TriggerItemType.RootCategory)
                {
                    Name = "Untitled",
                    Id = RootCategoryId,
                    ParentId = -1,
                });
            }

            if (Context.VariableDeclarations.Any(declaration => declaration.Value.GlobalDeclaration.Declarator.IdentifierName.Name.StartsWith("udg_", StringComparison.Ordinal)))
            {
                var variablesCategoryId = categoryId++;

                mapTriggers.TriggerItems.Add(new TriggerCategoryDefinition(TriggerItemType.Category)
                {
                    Name = "Variables",
                    Id = variablesCategoryId,
                    ParentId = RootCategoryId,
                });

                foreach (var declaration in Context.VariableDeclarations)
                {
                    var globalDeclaration = declaration.Value.GlobalDeclaration;
                    if (globalDeclaration.Declarator.IdentifierName.Name.StartsWith("udg_", StringComparison.Ordinal))
                    {
                        var variableDefinition = new VariableDefinition
                        {
                            Name = globalDeclaration.Declarator.IdentifierName.Name["udg_".Length..],
                            Type = validGUIVariableTypes.Contains(globalDeclaration.Declarator.Type.TypeName.Name) ? globalDeclaration.Declarator.Type.TypeName.Name : "handle",
                            Unk = 1,
                            IsArray = declaration.Value.IsArray,
                            ArraySize = 1,
                            IsInitialized = false,
                            InitialValue = string.Empty,
                            Id = variableId++,
                            ParentId = variablesCategoryId,
                        };

                        declaration.Value.VariableDefinition = variableDefinition;

                        if (globalDeclaration.Declarator is JassVariableDeclaratorSyntax variableDeclarator)
                        {
                            if (variableDeclarator.Value is not null &&
                                TryDecompileVariableDefinitionInitialValue(variableDeclarator.Value.Expression, variableDefinition.Type, out var initialValue))
                            {
                                variableDefinition.IsInitialized = true;
                                variableDefinition.InitialValue = initialValue;
                            }
                        }

                        mapTriggers.Variables.Add(variableDefinition);

                        if (subVersion.HasValue)
                        {
                            var triggerVariableDefinition = new TriggerVariableDefinition();
                            triggerVariableDefinition.Name = variableDefinition.Name;
                            triggerVariableDefinition.Id = variableDefinition.Id;
                            triggerVariableDefinition.ParentId = variableDefinition.ParentId;
                            mapTriggers.TriggerItems.Add(triggerVariableDefinition);
                        }
                    }
                }
            }

            if (initGlobalsFunction is not null)
            {
                foreach (var statement in initGlobalsFunction.Body.Statements)
                {
                    if (statement is JassLoopStatementSyntax loopStatement)
                    {
                        if (loopStatement.Body.Statements.Length == 3 &&
                            loopStatement.Body.Statements[0] is JassExitStatementSyntax exitStatement &&
                            loopStatement.Body.Statements[1] is JassSetStatementSyntax setVariableStatement &&
                            loopStatement.Body.Statements[2] is JassSetStatementSyntax &&
                            setVariableStatement.Indexer is JassVariableReferenceExpressionSyntax i &&
                            string.Equals(i.IdentifierName.Name, "i", StringComparison.Ordinal))
                        {
                            var variableName = setVariableStatement.IdentifierName.Name["udg_".Length..];
                            if (!((JassBinaryExpressionSyntax)exitStatement.Condition.Deparenthesize()).Right.TryGetIntegerExpressionValue_New(out var arraySize))
                            {
                                continue;
                            }

                            var variableDefinition = mapTriggers.Variables.Single(v => string.Equals(v.Name, variableName, StringComparison.Ordinal));

                            variableDefinition.ArraySize = arraySize;

                            if (TryDecompileVariableDefinitionInitialValue(setVariableStatement.Value.Expression, variableDefinition.Type, out var initialValue))
                            {
                                variableDefinition.IsInitialized = true;
                                variableDefinition.InitialValue = initialValue;
                            }
                        }
                    }
                }
            }

            if (initCustomTriggersFunction is not null)
            {
                var triggersCategoryId = categoryId++;

                mapTriggers.TriggerItems.Add(new TriggerCategoryDefinition(TriggerItemType.Category)
                {
                    Name = "Untitled Category",
                    Id = triggersCategoryId,
                    ParentId = RootCategoryId,
                });

                var decompiledTriggers = TryDecompileTriggerDefinitions(initCustomTriggersFunction);
                
                decompiledTriggers.ForEach(x =>
                {
                    x.Id = triggerId++;
                    x.ParentId = triggersCategoryId;
                });
                var triggers = decompiledTriggers.ToDictionary(x => x.Name, x => x);
                mapTriggers.TriggerItems.AddRange(decompiledTriggers);


                if (runInitializationTriggersFunction is not null)
                {
                    foreach (var statement in runInitializationTriggersFunction.Body.Statements)
                    {
                        if (statement is JassCallStatementSyntax callStatement &&
                            callStatement.Arguments.Arguments.Length == 1 &&
                            string.Equals(callStatement.IdentifierName.Name, "ConditionalTriggerExecute", StringComparison.Ordinal) &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax triggerVariableReferenceExpression &&
                            triggerVariableReferenceExpression.IdentifierName.Name.StartsWith("gg_trg_", StringComparison.Ordinal) &&
                            triggers.TryGetValue(triggerVariableReferenceExpression.IdentifierName.Name["gg_trg_".Length..].Replace('_', ' '), out var triggerDefinition))
                        {
                            triggerDefinition.Functions.Add(new TriggerFunction
                            {
                                Type = TriggerFunctionType.Event,
                                IsEnabled = true,
                                Name = "MapInitializationEvent",
                            });
                        }
                    }
                }
            }
            
            return true;
        }

        private List<TriggerDefinition> TryDecompileTriggerDefinitions(JassFunctionDeclarationSyntax initTrigFunction)
        {
            if (initTrigFunction is null)
            {
                throw new ArgumentNullException(nameof(initTrigFunction));
            }

            var result = new List<TriggerDefinition>();
            TriggerDefinition currentTrigger = null;

            foreach (var statement in initTrigFunction.Body.Statements)
            {
                if (statement is JassSetStatementSyntax setStatement)
                {
                    if (setStatement.Value.Expression is JassInvocationExpressionSyntax invocationExpression &&
                        invocationExpression.Arguments.Arguments.IsEmpty &&
                        setStatement.Indexer is null &&
                        setStatement.IdentifierName.Name.StartsWith("gg_trg_", StringComparison.Ordinal) &&
                        string.Equals(invocationExpression.IdentifierName.Name, "CreateTrigger", StringComparison.Ordinal))
                    {
                        currentTrigger = new TriggerDefinition();
                        currentTrigger.IsEnabled = true;
                        currentTrigger.IsInitiallyOn = true;
                        currentTrigger.Name = setStatement.IdentifierName.Name.Substring("gg_trg_".Length);
                        result.Add(currentTrigger);
                    }
                }
                
                if (currentTrigger is null)
                {
                    continue;
                }

                if (statement is JassCallStatementSyntax callStatement)
                {
                    if (string.Equals(callStatement.IdentifierName.Name, "TriggerAddAction", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax variableReferenceExpression &&
                            callStatement.Arguments.Arguments[1] is JassFunctionReferenceExpressionSyntax functionReferenceExpression &&
                            string.Equals(variableReferenceExpression.IdentifierName.Name, "gg_trg_" + currentTrigger.Name, StringComparison.Ordinal) &&
                            Context.FunctionDeclarations.TryGetValue(functionReferenceExpression.IdentifierName.Name, out var actionsFunctionDeclaration) &&
                            actionsFunctionDeclaration.IsActionsFunction)
                        {
                            var actionsFunction = actionsFunctionDeclaration.FunctionDeclaration;
                            if (TryDecompileActionStatementList(actionsFunction.Body, out var actionFunctions))
                            {
                                currentTrigger.Functions.AddRange(actionFunctions);
                            }
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "TriggerAddCondition", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax variableReferenceExpression &&
                            callStatement.Arguments.Arguments[1] is JassInvocationExpressionSyntax conditionInvocationExpression &&
                            string.Equals(conditionInvocationExpression.IdentifierName.Name, "Condition", StringComparison.Ordinal) &&
                            conditionInvocationExpression.Arguments.Arguments.Length == 1 &&
                            conditionInvocationExpression.Arguments.Arguments[0] is JassFunctionReferenceExpressionSyntax functionReferenceExpression &&
                            string.Equals(variableReferenceExpression.IdentifierName.Name, "gg_trg_" + currentTrigger.Name, StringComparison.Ordinal) &&
                            Context.FunctionDeclarations.TryGetValue(functionReferenceExpression.IdentifierName.Name, out var conditionsFunctionDeclaration) &&
                            conditionsFunctionDeclaration.IsConditionsFunction)
                        {
                            var conditionsFunction = conditionsFunctionDeclaration.FunctionDeclaration;
                            if (TryDecompileConditionStatementList(conditionsFunction.Body, out var conditionFunctions))
                            {
                                currentTrigger.Functions.AddRange(conditionFunctions);
                            }
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "DisableTrigger", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 1 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax variableReferenceExpression &&
                            string.Equals(variableReferenceExpression.IdentifierName.Name, "gg_trg_" + currentTrigger.Name, StringComparison.Ordinal))
                        {
                            currentTrigger.IsInitiallyOn = false;
                        }
                    }
                    else
                    {
                        if (Context.TriggerData.TriggerData.TriggerEvents.TryGetValue(callStatement.IdentifierName.Name, out var triggerEvent) &&
                            callStatement.Arguments.Arguments.Length == triggerEvent.ArgumentTypes.Length + 1 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax variableReferenceExpression &&
                            string.Equals(variableReferenceExpression.IdentifierName.Name, "gg_trg_" + currentTrigger.Name, StringComparison.Ordinal))
                        {
                            var function = new TriggerFunction
                            {
                                Type = TriggerFunctionType.Event,
                                IsEnabled = true,
                                Name = callStatement.IdentifierName.Name,
                            };

                            for (var i = 1; i < callStatement.Arguments.Arguments.Length; i++)
                            {
                                if (TryDecompileTriggerFunctionParameter(callStatement.Arguments.Arguments[i], triggerEvent.ArgumentTypes[i - 1], out var functionParameter))
                                {
                                    function.Parameters.Add(functionParameter);
                                }
                            }

                            currentTrigger.Functions.Add(function);
                        }
                    }
                }
            }

            result.ForEach(x => x.Name = x.Name.Replace('_', ' '));
            return result;
        }

        private bool TryDecompileVariableDefinitionInitialValue(IExpressionSyntax expression, string type, [NotNullWhen(true)] out string? initialValue)
        {
            if (expression is not JassNullLiteralExpressionSyntax &&
                (!Context.TriggerData.TriggerData.TriggerTypeDefaults.TryGetValue(type, out var typeDefault) ||
                 !string.Equals(typeDefault.ScriptText, expression.ToString(), StringComparison.Ordinal)) &&
                TryDecompileTriggerFunctionParameter(expression, type, out var functionParameter))
            {
                initialValue = functionParameter.Value;
                return true;
            }

            initialValue = null;
            return false;
        }
    }
}