// ------------------------------------------------------------------------------
// <copyright file="MapUnitsDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using War3Net.Build.Common;
using War3Net.Build.Widget;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.Common.Extensions;

namespace War3Net.CodeAnalysis.Decompilers
{
    public class UnitDataDecompilationMetaData : ObjectManagerDecompilationMetaData
    {
        public string DecompiledFromVariableName { get; set; }
        public string WaygateDestinationRegionName { get; set; }
    }

    public partial class JassScriptDecompiler
    {
        int CreationNumber = 0;

        public bool TryDecompileMapUnits(
            MapWidgetsFormatVersion formatVersion,
            MapWidgetsSubVersion subVersion,
            bool useNewFormat,
            [NotNullWhen(true)] out MapUnits? mapUnits,
            [NotNullWhen(true)] out Dictionary<UnitData, UnitDataDecompilationMetaData>? decompilationMetaData)
        {
            var createAllUnits = GetFunction("CreateAllUnits");
            var createAllItems = GetFunction("CreateAllItems");
            var config = GetFunction("config");
            var initCustomPlayerSlots = GetFunction("InitCustomPlayerSlots");

            if (createAllUnits is null ||
                createAllItems is null ||
                config is null ||
                initCustomPlayerSlots is null)
            {
                mapUnits = null;
                decompilationMetaData = null;
                return false;
            }

            if (TryDecompileMapUnits(
                createAllUnits.FunctionDeclaration,
                createAllItems.FunctionDeclaration,
                config.FunctionDeclaration,
                initCustomPlayerSlots.FunctionDeclaration,
                formatVersion,
                subVersion,
                useNewFormat,
                out mapUnits,
                out decompilationMetaData))
            {
                createAllUnits.Handled = true;
                createAllItems.Handled = true;
                initCustomPlayerSlots.Handled = true;

                return true;
            }

            mapUnits = null;
            decompilationMetaData = null;
            return false;
        }

        public bool TryDecompileMapUnits(
            JassFunctionDeclarationSyntax createAllUnitsFunction,
            JassFunctionDeclarationSyntax createAllItemsFunction,
            JassFunctionDeclarationSyntax configFunction,
            JassFunctionDeclarationSyntax initCustomPlayerSlotsFunction,
            MapWidgetsFormatVersion formatVersion,
            MapWidgetsSubVersion subVersion,
            bool useNewFormat,
            [NotNullWhen(true)] out MapUnits? mapUnits,
            [NotNullWhen(true)] out Dictionary<UnitData, UnitDataDecompilationMetaData>? decompilationMetaData)
        {
            if (createAllUnitsFunction is null)
            {
                throw new ArgumentNullException(nameof(createAllUnitsFunction));
            }

            if (createAllItemsFunction is null)
            {
                throw new ArgumentNullException(nameof(createAllItemsFunction));
            }

            if (configFunction is null)
            {
                throw new ArgumentNullException(nameof(configFunction));
            }

            if (initCustomPlayerSlotsFunction is null)
            {
                throw new ArgumentNullException(nameof(initCustomPlayerSlotsFunction));
            }

            if (TryDecompileCreateUnitsFunction(createAllUnitsFunction, out var units, out var unitsDecompilationMetaData) &&
                TryDecompileCreateItemsFunction(createAllItemsFunction, out var items, out var itemsDecompilationMetaData) &&
                TryDecompileStartLocationPositionsConfigFunction(configFunction, out var startLocationPositions) &&
                TryDecompileInitCustomPlayerSlotsFunction(initCustomPlayerSlotsFunction, startLocationPositions, out var startLocations, out var slotsDecompilationMetaData))
            {
                mapUnits = new MapUnits(formatVersion, subVersion, useNewFormat);
                decompilationMetaData = unitsDecompilationMetaData.Concat(itemsDecompilationMetaData).Concat(slotsDecompilationMetaData).ToDictionary(x => x.Key, x => x.Value);

                mapUnits.Units.AddRange(units);
                mapUnits.Units.AddRange(items);
                mapUnits.Units.AddRange(startLocations);

                return true;
            }

            mapUnits = null;
            decompilationMetaData = null;
            return false;
        }

        private bool TryDecompileCreateUnitsFunction(JassFunctionDeclarationSyntax createUnitsFunction, [NotNullWhen(true)] out List<UnitData>? units, [NotNullWhen(true)] out Dictionary<UnitData, UnitDataDecompilationMetaData>? decompilationMetaData)
        {
            var localPlayerVariableName = (string?)null;
            var localPlayerVariableValue = (int?)null;

            units = new List<UnitData>();
            decompilationMetaData = new Dictionary<UnitData, UnitDataDecompilationMetaData>();

            foreach (var statement in createUnitsFunction.Body.Statements)
            {
                if (statement is JassCommentSyntax ||
                    statement is JassEmptySyntax)
                {
                    continue;
                }
                else if (statement is JassLocalVariableDeclarationStatementSyntax localVariableDeclarationStatement)
                {
                    var typeName = localVariableDeclarationStatement.Declarator.Type.TypeName.Name;

                    if (string.Equals(typeName, "player", StringComparison.Ordinal))
                    {
                        if (localVariableDeclarationStatement.Declarator is JassVariableDeclaratorSyntax variableDeclarator && variableDeclarator.Type.TypeName.Name == "player")
                        {
                            localPlayerVariableName = variableDeclarator.IdentifierName.Name;
                            if (variableDeclarator.Value is not null && variableDeclarator.Value.Expression is JassInvocationExpressionSyntax playerInvocationExpression &&
                                string.Equals(playerInvocationExpression.IdentifierName.Name, "Player", StringComparison.Ordinal) &&
                                playerInvocationExpression.Arguments.Arguments.Length == 1 &&
                                playerInvocationExpression.Arguments.Arguments[0].TryGetPlayerIdExpressionValue_New(Context.MaxPlayerSlots, out var playerId))
                            {
                                localPlayerVariableValue = playerId;
                            }
                        }
                    }
                    else if (string.Equals(typeName, "unit", StringComparison.Ordinal) ||
                             string.Equals(typeName, "integer", StringComparison.Ordinal) ||
                             string.Equals(typeName, "trigger", StringComparison.Ordinal) ||
                             string.Equals(typeName, "real", StringComparison.Ordinal))
                    {
                        // TODO

                    }
                }
                else if (statement is JassSetStatementSyntax setStatement)
                {
                    if (setStatement.Indexer is null)
                    {
                        if (setStatement.IdentifierName.Name == localPlayerVariableName && setStatement.Value.Expression is JassInvocationExpressionSyntax playerInvocationExpression && string.Equals(playerInvocationExpression.IdentifierName.Name, "Player", StringComparison.Ordinal)
                            && playerInvocationExpression.Arguments.Arguments.Length == 1 && playerInvocationExpression.Arguments.Arguments[0].TryGetPlayerIdExpressionValue_New(Context.MaxPlayerSlots, out var playerId))
                        {
                            localPlayerVariableValue = playerId;
                        }
                        else if (localPlayerVariableValue != null && setStatement.Value.Expression is JassInvocationExpressionSyntax invocationExpression)
                        {
                            if (string.Equals(invocationExpression.IdentifierName.Name, "CreateUnit", StringComparison.Ordinal))
                            {
                                if (invocationExpression.Arguments.Arguments.Length == 5 &&
                                    invocationExpression.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax playerVariableReferenceExpression &&
                                    invocationExpression.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var unitId) &&
                                    invocationExpression.Arguments.Arguments[2].TryGetRealExpressionValue(out var x) &&
                                    invocationExpression.Arguments.Arguments[3].TryGetRealExpressionValue(out var y) &&
                                    invocationExpression.Arguments.Arguments[4].TryGetRealExpressionValue(out var face) &&
                                    string.Equals(playerVariableReferenceExpression.IdentifierName.Name, localPlayerVariableName, StringComparison.Ordinal))
                                {
                                    var unit = new UnitData
                                    {
                                        OwnerId = localPlayerVariableValue.Value,
                                        TypeId = unitId.InvertEndianness(),
                                        Position = new Vector3(x, y, 0f),
                                        Rotation = face * (MathF.PI / 180f),
                                        Scale = Vector3.One,
                                        Flags = 2,
                                        GoldAmount = 12500,
                                        HeroLevel = 1,
                                        CreationNumber = CreationNumber++
                                    };

                                    unit.SkinId = unit.TypeId;

                                    var metaData = decompilationMetaData.GetOrAdd(unit);
                                    metaData.DecompiledFromStatements.Add(statement);
                                    metaData.DecompiledFromVariableName = setStatement.IdentifierName.Name;
                                    units.Add(unit);
                                }
                            }
                            else if (localPlayerVariableValue != null && string.Equals(invocationExpression.IdentifierName.Name, "BlzCreateUnitWithSkin", StringComparison.Ordinal))
                            {
                                if (invocationExpression.Arguments.Arguments.Length == 6 &&
                                    invocationExpression.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax playerVariableReferenceExpression &&
                                    invocationExpression.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var unitId) &&
                                    invocationExpression.Arguments.Arguments[2].TryGetRealExpressionValue(out var x) &&
                                    invocationExpression.Arguments.Arguments[3].TryGetRealExpressionValue(out var y) &&
                                    invocationExpression.Arguments.Arguments[4].TryGetRealExpressionValue(out var face) &&
                                    invocationExpression.Arguments.Arguments[5].TryGetIntegerExpressionValue_New(out var skinId) &&
                                    string.Equals(playerVariableReferenceExpression.IdentifierName.Name, localPlayerVariableName, StringComparison.Ordinal))
                                {
                                    var unit = new UnitData
                                    {
                                        OwnerId = localPlayerVariableValue.Value,
                                        TypeId = unitId.InvertEndianness(),
                                        Position = new Vector3(x, y, 0f),
                                        Rotation = face * (MathF.PI / 180f),
                                        Scale = Vector3.One,
                                        SkinId = skinId.InvertEndianness(),
                                        Flags = 2,
                                        GoldAmount = 12500,
                                        HeroLevel = 1,
                                        CreationNumber = CreationNumber++
                                    };

                                    var metaData = decompilationMetaData.GetOrAdd(unit);
                                    metaData.DecompiledFromStatements.Add(statement);
                                    metaData.DecompiledFromVariableName = setStatement.IdentifierName.Name;
                                    units.Add(unit);
                                }
                            }
                            else if (string.Equals(invocationExpression.IdentifierName.Name, "CreateTrigger", StringComparison.Ordinal))
                            {
                                // TODO
                                continue;
                            }
                            else if (string.Equals(invocationExpression.IdentifierName.Name, "GetUnitState", StringComparison.Ordinal))
                            {
                                // TODO
                                continue;
                            }
                            else if (string.Equals(invocationExpression.IdentifierName.Name, "RandomDistChoose", StringComparison.Ordinal))
                            {
                                // TODO
                                continue;
                            }
                        }
                        else if (setStatement.Value.Expression is JassArrayReferenceExpressionSyntax)
                        {
                            // TODO
                            continue;
                        }
                    }
                }
                else if (units.Any() && statement is JassCallStatementSyntax callStatement)
                {
                    if (string.Equals(callStatement.IdentifierName.Name, "SetResourceAmount", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var amount))
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].GoldAmount = amount;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetUnitColor", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1] is JassInvocationExpressionSyntax convertPlayerColorInvocationExpression &&
                            string.Equals(convertPlayerColorInvocationExpression.IdentifierName.Name, "ConvertPlayerColor", StringComparison.Ordinal) &&
                            convertPlayerColorInvocationExpression.Arguments.Arguments.Length == 1 &&
                            convertPlayerColorInvocationExpression.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out var playerColorId))
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].CustomPlayerColorId = playerColorId;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetUnitAcquireRange", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetRealExpressionValue(out var acquireRange))
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            const float CampAcquireRange = 200f;
                            units[^1].TargetAcquisition = acquireRange == CampAcquireRange ? -2f : acquireRange;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetUnitState", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1] is JassVariableReferenceExpressionSyntax unitStateVariableReferenceExpression)
                        {
                            if (string.Equals(unitStateVariableReferenceExpression.IdentifierName.Name, "UNIT_STATE_LIFE", StringComparison.Ordinal))
                            {
                                if (callStatement.Arguments.Arguments[2] is JassBinaryExpressionSyntax binaryExpression &&
                                    binaryExpression.Left.TryGetRealExpressionValue(out var hp) &&
                                    binaryExpression.Operator == BinaryOperatorType.Multiplication &&
                                    binaryExpression.Right is JassVariableReferenceExpressionSyntax)
                                {
                                    var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                                    metaData.DecompiledFromStatements.Add(statement);

                                    units[^1].HP = (int)(100 * hp);
                                }
                            }
                            else if (string.Equals(unitStateVariableReferenceExpression.IdentifierName.Name, "UNIT_STATE_MANA", StringComparison.Ordinal))
                            {
                                if (callStatement.Arguments.Arguments[2].TryGetIntegerExpressionValue_New(out var mp))
                                {
                                    var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                                    metaData.DecompiledFromStatements.Add(statement);

                                    units[^1].MP = mp;
                                }
                            }
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "UnitAddItemToSlotById", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var itemId) &&
                            callStatement.Arguments.Arguments[2].TryGetIntegerExpressionValue_New(out var slot))
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].InventoryData.Add(new InventoryItemData
                            {
                                ItemId = itemId.InvertEndianness(),
                                Slot = slot,
                            });
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetHeroLevel", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var level) &&
                            callStatement.Arguments.Arguments[2] is JassBooleanLiteralExpressionSyntax)
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].HeroLevel = level;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetHeroStr", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var value) &&
                            callStatement.Arguments.Arguments[2] is JassBooleanLiteralExpressionSyntax)
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].HeroStrength = value;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetHeroAgi", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var value) &&
                            callStatement.Arguments.Arguments[2] is JassBooleanLiteralExpressionSyntax)
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].HeroAgility = value;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetHeroInt", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var value) &&
                            callStatement.Arguments.Arguments[2] is JassBooleanLiteralExpressionSyntax)
                        {
                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                            metaData.DecompiledFromStatements.Add(statement);

                            units[^1].HeroIntelligence = value;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SelectHeroSkill", StringComparison.Ordinal) && callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var heroSkillAbilityId))
                    {
                        var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                        metaData.DecompiledFromStatements.Add(statement);

                        var ability = units[^1].AbilityData.FirstOrDefault(x => x.AbilityId == heroSkillAbilityId.InvertEndianness());
                        if (ability == null)
                        {
                            ability = new ModifiedAbilityData() { AbilityId = heroSkillAbilityId.InvertEndianness(), HeroAbilityLevel = 0, IsAutocastActive = false };
                            units[^1].AbilityData.Add(ability);
                        }

                        ability.HeroAbilityLevel++;
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "IssueImmediateOrder", StringComparison.Ordinal))
                    {
                        var abilityName = (callStatement.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax)?.Value;
                        if (string.IsNullOrWhiteSpace(abilityName))
                        {
                            continue;
                        }

                        var matchingAbilityIdsOn = Context.ObjectData.map.AbilityObjectData?.BaseAbilities.Where(x => x.Modifications.Any(y => (y.Id == "aoro".FromRawcode() || y.Id == "aord".FromRawcode()) && y.Value.ToString() == abilityName)).Select(x => x.OldId).
                               Concat(Context.ObjectData.map.AbilityObjectData?.NewAbilities.Where(x => x.Modifications.Any(y => (y.Id == "aoro".FromRawcode() || y.Id == "aord".FromRawcode()) && y.Value.ToString() == abilityName)).Select(x => x.NewId)).Distinct().ToList();
                        var matchingAbilityIdsOff = Context.ObjectData.map.AbilityObjectData?.BaseAbilities.Where(x => x.Modifications.Any(y => (y.Id == "aorf".FromRawcode() || y.Id == "aoru".FromRawcode()) && y.Value.ToString() == abilityName)).Select(x => x.OldId).
                               Concat(Context.ObjectData.map.AbilityObjectData?.NewAbilities.Where(x => x.Modifications.Any(y => (y.Id == "aorf".FromRawcode() || y.Id == "aoru".FromRawcode()) && y.Value.ToString() == abilityName)).Select(x => x.NewId)).Distinct().ToList();

                        var unit = units[^1];
                        var unitTypes = Context.ObjectData.map.UnitObjectData?.BaseUnits.Where(x => x.OldId == unit.TypeId).Concat(Context.ObjectData.map.UnitObjectData?.NewUnits.Where(x => x.NewId == unit.TypeId)).ToList();

                        foreach (var abilityId in matchingAbilityIdsOn.Concat(matchingAbilityIdsOff))
                        {
                            if (!unitTypes.Any(x => x.Modifications.Any(y => (y.Id == "uabi".FromRawcode() || y.Id == "uabs".FromRawcode()) && y.ValueAsString.Contains(abilityId.ToRawcode()))))
                            {
                                continue;
                            }

                            var metaData = decompilationMetaData.GetOrAdd(unit);
                            metaData.DecompiledFromStatements.Add(statement);

                            var ability = unit.AbilityData.FirstOrDefault(x => x.AbilityId == abilityId);
                            if (ability == null)
                            {
                                ability = new ModifiedAbilityData() { AbilityId = abilityId, HeroAbilityLevel = 0, IsAutocastActive = false };
                                unit.AbilityData.Add(ability);
                            }

                            if (matchingAbilityIdsOn.Contains(abilityId))
                            {
                                ability.IsAutocastActive = true;
                            }
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "TriggerRegisterUnitEvent", StringComparison.Ordinal))
                    {
                        var eventName = (callStatement.Arguments.Arguments[2] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name;
                        if (eventName == "EVENT_UNIT_DEATH" || eventName == "EVENT_UNIT_CHANGE_OWNER")
                        {
                            var unit = units[^1];
                            if (!unit.ItemTableSets.Any() && decompilationMetaData[unit].DecompiledFromVariableName == (callStatement.Arguments.Arguments[1] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name)
                            {
                                var lookaheadStatementIndex = createUnitsFunction.Body.Statements.IndexOf(statement);
                                while (createUnitsFunction.Body.Statements.Length > lookaheadStatementIndex)
                                {
                                    lookaheadStatementIndex++;
                                    var lookaheadStatement = createUnitsFunction.Body.Statements[lookaheadStatementIndex] as JassCallStatementSyntax;
                                    if (lookaheadStatement?.IdentifierName.Name.Contains("Trigger", StringComparison.InvariantCultureIgnoreCase) != true)
                                    {
                                        break;
                                    }

                                    if (string.Equals(lookaheadStatement?.IdentifierName.Name, "TriggerAddAction", StringComparison.Ordinal))
                                    {
                                        var actionFunctionName = (lookaheadStatement.Arguments.Arguments[1] as JassFunctionReferenceExpressionSyntax)?.IdentifierName.Name;
                                        var actionFunction = actionFunctionName != null ? GetFunction(actionFunctionName) : null;
                                        if (actionFunction != null && TryDecompileDropItemsFunction(actionFunction, out var itemTableSets))
                                        {
                                            var metaData = decompilationMetaData.GetOrAdd(units[^1]);
                                            metaData.DecompiledFromStatements.Add(statement);

                                            unit.ItemTableSets.AddRange(itemTableSets);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "WaygateSetDestination", StringComparison.Ordinal))
                    {
                        var argument = callStatement.Arguments?.Arguments.Length >= 1 ? callStatement.Arguments.Arguments[1] as JassInvocationExpressionSyntax : null;
                        if (argument?.Arguments?.Arguments.Length > 0)
                        {
                            var region = argument.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax;
                            if (region != null)
                            {
                                var unit = units[^1];
                                var metaData = decompilationMetaData.GetOrAdd(unit);
                                metaData.DecompiledFromStatements.Add(statement);
                                metaData.WaygateDestinationRegionName = region?.IdentifierName?.Name;
                            }
                        }
                        continue;
                    }
                    /*
                    else if (callStatement.Arguments.Arguments.IsEmpty)
                    {
                        if (Context.FunctionDeclarations.TryGetValue(callStatement.IdentifierName.Name, out var subFunction) &&
                            TryDecompileCreateUnitsFunction(subFunction.FunctionDeclaration, out var subFunctionResult))
                        {
                            result.AddRange(subFunctionResult);
                        }
                    }
                    */
                }
                else if (statement is JassIfStatementSyntax ifStatement)
                {
                    if (ifStatement.Condition.Deparenthesize() is JassBinaryExpressionSyntax binaryExpression &&
                        binaryExpression.Left is JassVariableReferenceExpressionSyntax &&
                        binaryExpression.Operator == BinaryOperatorType.NotEquals &&
                        binaryExpression.Right.TryGetIntegerExpressionValue_New(out var value) &&
                        value == -1)
                    {
                        // TODO
                        continue;
                    }
                }
            }

            foreach (var unit in units)
            {
                var filteredAbilityData = unit.AbilityData.Where(x => x.HeroAbilityLevel != 0 || x.IsAutocastActive).ToList();
                unit.AbilityData.Clear();
                unit.AbilityData.AddRange(filteredAbilityData);
            }

            return true;
        }

        private bool TryDecompileCreateItemsFunction(JassFunctionDeclarationSyntax createItemsFunction, [NotNullWhen(true)] out List<UnitData>? items, [NotNullWhen(true)] out Dictionary<UnitData, UnitDataDecompilationMetaData>? decompilationMetaData)
        {
            items = new List<UnitData>();
            decompilationMetaData = new Dictionary<UnitData, UnitDataDecompilationMetaData>();

            foreach (var statement in createItemsFunction.Body.Statements)
            {
                if (statement is JassCommentSyntax ||
                    statement is JassEmptySyntax)
                {
                    continue;
                }
                else if (statement is JassSetStatementSyntax setStatement)
                {
                    if (setStatement.Value.Expression is JassInvocationExpressionSyntax invocationExpression)
                    {
                        if (string.Equals(invocationExpression.IdentifierName.Name, "CreateItem", StringComparison.Ordinal))
                        {
                            if (invocationExpression.Arguments.Arguments.Length == 3 &&
                                invocationExpression.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out var unitId) &&
                                invocationExpression.Arguments.Arguments[1].TryGetRealExpressionValue(out var x) &&
                                invocationExpression.Arguments.Arguments[2].TryGetRealExpressionValue(out var y))
                            {
                                var unit = new UnitData
                                {
                                    OwnerId = Context.MaxPlayerSlots + 3, // NEUTRAL_PASSIVE
                                    TypeId = unitId.InvertEndianness(),
                                    Position = new Vector3(x, y, 0f),
                                    Rotation = 0,
                                    Scale = Vector3.One,
                                    Flags = 2,
                                    GoldAmount = 12500,
                                    HeroLevel = 0,
                                    CreationNumber = CreationNumber++
                                };

                                unit.SkinId = unit.TypeId;

                                var metaData = decompilationMetaData.GetOrAdd(unit);
                                metaData.DecompiledFromStatements.Add(statement);
                                metaData.DecompiledFromVariableName = setStatement.IdentifierName.Name;
                                items.Add(unit);
                            }
                        }
                        else if (string.Equals(invocationExpression.IdentifierName.Name, "BlzCreateItemWithSkin", StringComparison.Ordinal))
                        {
                            if (invocationExpression.Arguments.Arguments.Length == 4 &&
                                invocationExpression.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out var unitId) &&
                                invocationExpression.Arguments.Arguments[1].TryGetRealExpressionValue(out var x) &&
                                invocationExpression.Arguments.Arguments[2].TryGetRealExpressionValue(out var y) &&
                                invocationExpression.Arguments.Arguments[3].TryGetIntegerExpressionValue_New(out var skinId))
                            {
                                var item = new UnitData
                                {
                                    OwnerId = Context.MaxPlayerSlots + 3, // NEUTRAL_PASSIVE
                                    TypeId = unitId.InvertEndianness(),
                                    Position = new Vector3(x, y, 0f),
                                    Rotation = 0,
                                    Scale = Vector3.One,
                                    SkinId = skinId.InvertEndianness(),
                                    Flags = 2,
                                    GoldAmount = 12500,
                                    HeroLevel = 1,
                                    CreationNumber = CreationNumber++
                                };

                                var metaData = decompilationMetaData.GetOrAdd(item);
                                metaData.DecompiledFromStatements.Add(statement);
                                metaData.DecompiledFromVariableName = setStatement.IdentifierName.Name;
                                items.Add(item);
                            }
                        }
                    }
                }
                else if (statement is JassCallStatementSyntax callStatement)
                {
                    if (string.Equals(callStatement.IdentifierName.Name, "CreateItem", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 3 &&
                            callStatement.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out var itemId) &&
                            callStatement.Arguments.Arguments[1].TryGetRealExpressionValue(out var x) &&
                            callStatement.Arguments.Arguments[2].TryGetRealExpressionValue(out var y))
                        {
                            var item = new UnitData
                            {
                                OwnerId = Context.MaxPlayerSlots + 3, // NEUTRAL_PASSIVE
                                TypeId = itemId.InvertEndianness(),
                                Position = new Vector3(x, y, 0f),
                                Rotation = 0,
                                Scale = Vector3.One,
                                Flags = 2,
                                GoldAmount = 12500,
                                HeroLevel = 0,
                                CreationNumber = CreationNumber++
                            };

                            item.SkinId = item.TypeId;

                            var metaData = decompilationMetaData.GetOrAdd(item);
                            metaData.DecompiledFromStatements.Add(statement);
                            items.Add(item);
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "BlzCreateItemWithSkin", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 4 &&
                            callStatement.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out var itemId) &&
                            callStatement.Arguments.Arguments[1].TryGetRealExpressionValue(out var x) &&
                            callStatement.Arguments.Arguments[2].TryGetRealExpressionValue(out var y) &&
                            callStatement.Arguments.Arguments[3].TryGetIntegerExpressionValue_New(out var skinId))
                        {
                            var item = new UnitData
                            {
                                OwnerId = Context.MaxPlayerSlots + 3, // NEUTRAL_PASSIVE
                                TypeId = itemId.InvertEndianness(),
                                Position = new Vector3(x, y, 0f),
                                Rotation = 0,
                                Scale = Vector3.One,
                                SkinId = skinId.InvertEndianness(),
                                Flags = 2,
                                GoldAmount = 12500,
                                HeroLevel = 1,
                                CreationNumber = CreationNumber++
                            };

                            var metaData = decompilationMetaData.GetOrAdd(item);
                            metaData.DecompiledFromStatements.Add(statement);
                            items.Add(item);
                        }
                    }
                }
            }

            return true;
        }

        private bool TryDecompileStartLocationPositionsConfigFunction(JassFunctionDeclarationSyntax configFunction, [NotNullWhen(true)] out Dictionary<int, Vector2>? startLocationPositions)
        {
            var result = new Dictionary<int, Vector2>();

            foreach (var statement in configFunction.Body.Statements)
            {
                if (statement is JassCallStatementSyntax callStatement &&
                    string.Equals(callStatement.IdentifierName.Name, "DefineStartLocation", StringComparison.Ordinal))
                {
                    if (callStatement.Arguments.Arguments.Length == 3 &&
                        callStatement.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out var index) &&
                        callStatement.Arguments.Arguments[1].TryGetRealExpressionValue(out var x) &&
                        callStatement.Arguments.Arguments[2].TryGetRealExpressionValue(out var y))
                    {
                        result[index] = new Vector2(x, y);
                    }
                }
                else
                {
                    continue;
                }
            }

            startLocationPositions = result;
            return true;
        }

        private bool TryDecompileInitCustomPlayerSlotsFunction(
            JassFunctionDeclarationSyntax initCustomPlayerSlotsFunction,
            Dictionary<int, Vector2> startLocationPositions,
            [NotNullWhen(true)] out List<UnitData>? startLocations,
            [NotNullWhen(true)] out Dictionary<UnitData, UnitDataDecompilationMetaData>? decompilationMetaData)
        {
            startLocations = new List<UnitData>();
            decompilationMetaData = new Dictionary<UnitData, UnitDataDecompilationMetaData>();

            foreach (var statement in initCustomPlayerSlotsFunction.Body.Statements)
            {
                if (statement is JassCommentSyntax ||
                    statement is JassEmptySyntax)
                {
                    continue;
                }
                else if (statement is JassCallStatementSyntax callStatement)
                {
                    if (string.Equals(callStatement.IdentifierName.Name, "SetPlayerStartLocation", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassInvocationExpressionSyntax playerInvocationExpression &&
                            string.Equals(playerInvocationExpression.IdentifierName.Name, "Player", StringComparison.Ordinal) &&
                            playerInvocationExpression.Arguments.Arguments.Length == 1 &&
                            playerInvocationExpression.Arguments.Arguments[0].TryGetPlayerIdExpressionValue_New(Context.MaxPlayerSlots, out var playerId) &&
                            callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var startLocationNumber) &&
                            startLocationPositions.TryGetValue(startLocationNumber, out var startLocationPosition))
                        {
                            var unit = new UnitData
                            {
                                OwnerId = playerId,
                                TypeId = "sloc".FromRawcode(),
                                Position = new Vector3(startLocationPosition, 0f),
                                Rotation = MathF.PI * 1.5f,
                                Scale = Vector3.One,
                                Flags = 2,
                                GoldAmount = 12500,
                                HeroLevel = 0,
                                TargetAcquisition = 0,
                                CreationNumber = CreationNumber++
                            };

                            unit.SkinId = unit.TypeId;

                            var metaData = decompilationMetaData.GetOrAdd(unit);
                            metaData.DecompiledFromStatements.Add(statement);

                            startLocations.Add(unit);
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            if (startLocationPositions.Count != startLocations.Count)
            {
                foreach (var startLocationPosition in startLocationPositions)
                {
                    if (!startLocations.Any(x => x.OwnerId == startLocationPosition.Key))
                    {
                        var unit = new UnitData
                        {
                            OwnerId = startLocationPosition.Key,
                            TypeId = "sloc".FromRawcode(),
                            Position = new Vector3(startLocationPosition.Value, 0f),
                            Rotation = MathF.PI * 1.5f,
                            Scale = Vector3.One,
                            Flags = 2,
                            GoldAmount = 12500,
                            HeroLevel = 0,
                            TargetAcquisition = 0,
                            CreationNumber = CreationNumber++
                        };

                        unit.SkinId = unit.TypeId;

                        startLocations.Add(unit);
                    }
                }
            }

            return true;
        }

        public List<IStatementSyntax> GetAllNestedStatements(JassStatementListSyntax body)
        {
            var result = new List<IStatementSyntax>();
            foreach (var statement in body.Statements)
            {
                result.Add(statement);
                if (statement is JassLoopStatementSyntax loopStatement)
                {
                    result.AddRange(GetAllNestedStatements(loopStatement.Body));
                }
                else if (statement is JassIfStatementSyntax ifStatement)
                {
                    result.AddRange(GetAllNestedStatements(ifStatement.Body));
                    foreach (var elseIfClause in ifStatement.ElseIfClauses)
                    {
                        result.AddRange(GetAllNestedStatements(elseIfClause.Body));
                    }
                    if (ifStatement.ElseClause != null)
                    {
                        result.AddRange(GetAllNestedStatements(ifStatement.ElseClause.Body));
                    }
                }
            }

            return result;
        }

        public bool TryDecompileDropItemsFunction(FunctionDeclarationContext functionDeclarationContext, out List<RandomItemSet> result)
        {
            result = new List<RandomItemSet>();
            result.Add(new RandomItemSet());

            var statements = GetAllNestedStatements(functionDeclarationContext.FunctionDeclaration.Body);
            foreach (var statement in statements)
            {
                if (statement is JassCallStatementSyntax callStatement)
                {
                    if (string.Equals(callStatement.IdentifierName.Name, "RandomDistReset", StringComparison.Ordinal))
                    {
                        result.Add(new RandomItemSet());
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "RandomDistAddItem", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments[0].TryGetIntegerExpressionValue_New(out int itemId) && callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out int chance))
                        {
                            if (itemId != -1)
                            {
                                var itemSet = result[^1];
                                itemSet.Items.Add(new RandomItemSetItem() { ItemId = itemId.InvertEndianness(), Chance = chance });
                            }

                        }
                    }
                }
            }

            result.RemoveAll(x => !x.Items.Any());
            return result.Any();
        }
    }
}