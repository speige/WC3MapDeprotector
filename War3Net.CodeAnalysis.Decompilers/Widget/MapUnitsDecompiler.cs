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

using War3Net.Build.Widget;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.Common.Extensions;

namespace War3Net.CodeAnalysis.Decompilers
{
    public class UnitDataDecompilationMetaData
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

            if (TryDecompileCreateUnitsFunction(createAllUnitsFunction, out var units, out var unitsDecompiledFromVariableName) &&
                TryDecompileCreateItemsFunction(createAllItemsFunction, out var items, out var itemsDecompiledFromVariableName) &&
                TryDecompileStartLocationPositionsConfigFunction(configFunction, out var startLocationPositions) &&
                TryDecompileInitCustomPlayerSlotsFunction(initCustomPlayerSlotsFunction, startLocationPositions, out var startLocations))
            {
                mapUnits = new MapUnits(formatVersion, subVersion, useNewFormat);
                decompilationMetaData = unitsDecompiledFromVariableName.Concat(itemsDecompiledFromVariableName).ToDictionary(x => x.Key, x => x.Value);

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

                                    if (!decompilationMetaData.ContainsKey(unit))
                                    {
                                        decompilationMetaData[unit] = new UnitDataDecompilationMetaData();
                                    }
                                    decompilationMetaData[unit].DecompiledFromVariableName = setStatement.IdentifierName.Name;
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

                                    if (!decompilationMetaData.ContainsKey(unit))
                                    {
                                        decompilationMetaData[unit] = new UnitDataDecompilationMetaData();
                                    }
                                    decompilationMetaData[unit].DecompiledFromVariableName = setStatement.IdentifierName.Name;
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
                            units[^1].CustomPlayerColorId = playerColorId;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SetUnitAcquireRange", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 2 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax unitVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetRealExpressionValue(out var acquireRange))
                        {
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
                                    units[^1].HP = (int)(100 * hp);
                                }
                            }
                            else if (string.Equals(unitStateVariableReferenceExpression.IdentifierName.Name, "UNIT_STATE_MANA", StringComparison.Ordinal))
                            {
                                if (callStatement.Arguments.Arguments[2].TryGetIntegerExpressionValue_New(out var mp))
                                {
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
                            units[^1].HeroIntelligence = value;
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "SelectHeroSkill", StringComparison.Ordinal) && callStatement.Arguments.Arguments[1].TryGetIntegerExpressionValue_New(out var heroSkillAbilityId))
                    {
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
                        var matchingAbilityIds = Context.ObjectData.map.AbilityObjectData?.BaseAbilities.Where(x => x.Modifications.Any(y => (y.Id == "aoro".FromRawcode() || y.Id == "aorf".FromRawcode() || y.Id == "aoru".FromRawcode() || y.Id == "aord".FromRawcode()) && y.Value.ToString() == abilityName)).Select(x => x.OldId).
                               Concat(Context.ObjectData.map.AbilityObjectData?.NewAbilities.Where(x => x.Modifications.Any(y => (y.Id == "aoro".FromRawcode() || y.Id == "aorf".FromRawcode() || y.Id == "aoru".FromRawcode() || y.Id == "aord".FromRawcode()) && y.Value.ToString() == abilityName)).Select(x => x.NewId)).Distinct().ToList();
                        if (string.IsNullOrWhiteSpace(abilityName))
                        {
                            continue;
                        }

                        var unit = units[^1];
                        foreach (var abilityId in matchingAbilityIds)
                        {
                            var ability = unit.AbilityData.FirstOrDefault(x => x.AbilityId == abilityId);
                            if (ability == null)
                            {
                                ability = new ModifiedAbilityData() { AbilityId = abilityId, HeroAbilityLevel = 0, IsAutocastActive = false };
                                unit.AbilityData.Add(ability);
                            }

                            ability.IsAutocastActive = true;
                        }

                        continue;
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "RandomDistReset", StringComparison.Ordinal))
                    {
                        // TODO
                        continue;
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "RandomDistAddItem", StringComparison.Ordinal))
                    {
                        // TODO
                        continue;
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "TriggerRegisterUnitEvent", StringComparison.Ordinal))
                    {
                        // TODO
                        continue;
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "TriggerAddAction", StringComparison.Ordinal))
                    {
                        // TODO
                        continue;
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
                                if (!decompilationMetaData.ContainsKey(unit))
                                {
                                    decompilationMetaData[unit] = new UnitDataDecompilationMetaData();
                                }
                                decompilationMetaData[unit].WaygateDestinationRegionName = region?.IdentifierName?.Name;
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

                                if (!decompilationMetaData.ContainsKey(unit))
                                {
                                    decompilationMetaData[unit] = new UnitDataDecompilationMetaData();
                                }
                                decompilationMetaData[unit].DecompiledFromVariableName = setStatement.IdentifierName.Name;
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
                                var unit = new UnitData
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

                                if (!decompilationMetaData.ContainsKey(unit))
                                {
                                    decompilationMetaData[unit] = new UnitDataDecompilationMetaData();
                                }
                                decompilationMetaData[unit].DecompiledFromVariableName = setStatement.IdentifierName.Name;
                                items.Add(unit);
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
                        result.Add(index, new Vector2(x, y));
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
            [NotNullWhen(true)] out List<UnitData>? startLocations)
        {
            startLocations = new List<UnitData>();

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
    }
}