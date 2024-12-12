// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using War3Net.Build.Common;
using War3Net.Build.Environment;
using War3Net.Build.Info;
using War3Net.Build.Widget;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.Common.Extensions;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_New
    {
        [RegisterStatementParser]
        private void ParseUnitCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var unitData = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "CreateUnit" || x.IdentifierName.Name == "BlzCreateUnitWithSkin").SafeMapFirst(x =>
            {
                var args = x.Arguments.Arguments;
                var result = new UnitData
                {
                    OwnerId = (int)args[0].GetDecimalExpressionValueOrDefault(),
                    TypeId = ((int)args[1].GetDecimalExpressionValueOrDefault()).InvertEndianness(),
                    Position = new Vector3(
                            (float)args[2].GetDecimalExpressionValueOrDefault(),
                            (float)args[3].GetDecimalExpressionValueOrDefault(),
                            0f
                        ),
                    Rotation = (float)args[4].GetDecimalExpressionValueOrDefault() * (MathF.PI / 180f),
                    Scale = Vector3.One,
                    Flags = 2,
                    GoldAmount = 12500,
                    HeroLevel = 1,
                    CreationNumber = _context.GetNextCreationNumber()
                };

                result.SkinId = args.Length > 5 ? (int)args[5].GetDecimalExpressionValueOrDefault() : result.TypeId;
                return result;
            });

            if (unitData != null)
            {
                _context.HandledStatements.Add(input.Statement);
                _context.Add(unitData, variableAssignment);
            }
        }

        [RegisterStatementParser]
        private void ParseResourceAmount(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetResourceAmount").SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    Amount = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.GoldAmount = match.Amount;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetUnitColor(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetUnitColor").SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    CustomPlayerColorId = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.CustomPlayerColorId = match.CustomPlayerColorId;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseAcquireRange(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "SetUnitAcquireRange")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    Range = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });


            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.TargetAcquisition = match.Range;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseUnitState(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "SetUnitState")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    StateType = ((JassVariableReferenceExpressionSyntax)x.Arguments.Arguments[1]).IdentifierName.Name,
                    Value = (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });


            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    if (match.StateType == "UNIT_STATE_LIFE")
                    {
                        unit.HP = (int)match.Value;
                        _context.HandledStatements.Add(input.Statement);
                    }
                    else if (match.StateType == "UNIT_STATE_MANA")
                    {
                        unit.MP = (int)match.Value;
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseUnitInventory(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "UnitAddItemToSlotById")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    ItemId = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Slot = (int)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });


            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.InventoryData.Add(new InventoryItemData
                    {
                        ItemId = match.ItemId,
                        Slot = match.Slot
                    });
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseHeroLevel(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetHeroLevel").SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    Level = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.HeroLevel = match.Level;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseHeroAttributes(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "SetHeroStr" ||
                 x.IdentifierName.Name == "SetHeroAgi" ||
                 x.IdentifierName.Name == "SetHeroInt")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    Attribute = x.IdentifierName.Name,
                    Value = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });


            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    var handled = true;
                    switch (match.Attribute)
                    {
                        case "SetHeroStr":
                            unit.HeroStrength = match.Value;
                            break;
                        case "SetHeroAgi":
                            unit.HeroAgility = match.Value;
                            break;
                        case "SetHeroInt":
                            unit.HeroIntelligence = match.Value;
                            break;
                        default:
                            handled = false;
                            break;
                    }

                    if (handled)
                    {
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseHeroSkills(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "SelectHeroSkill")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    SkillId = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });


            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.VariableName) ?? _context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.AbilityData.Add(new ModifiedAbilityData
                    {
                        AbilityId = match.SkillId,
                        HeroAbilityLevel = 1,
                        IsAutocastActive = false
                    });
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseIssueImmediateOrder(StatementParserInput input)
        {
        }

        [RegisterStatementParser]
        private void ParseDropItemsFunction(StatementParserInput input)
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
                                _context.HandledStatements.Add(input.Statement);
                            }

                        }
                    }
                }
            }

            result.RemoveAll(x => !x.Items.Any());
            return result.Any();
        }


        [RegisterStatementParser]
        private void ParseTriggerRegisterUnitEvent(StatementParserInput input)
        {
        }

        [RegisterStatementParser]
        private void ParseItemCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var unitData = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "CreateItem" || x.IdentifierName.Name == "BlzCreateItemWithSkin").SafeMapFirst(x =>
            {
                var args = x.Arguments.Arguments;
                var result = new UnitData
                {
                    OwnerId = _context.MaxPlayerSlots + 3, // NEUTRAL_PASSIVE
                    TypeId = ((int)args[0].GetDecimalExpressionValueOrDefault()).InvertEndianness(),
                    Position = new Vector3(
                            (float)args[1].GetDecimalExpressionValueOrDefault(),
                            (float)args[2].GetDecimalExpressionValueOrDefault(),
                            0f
                        ),
                    Rotation = 0,
                    Scale = Vector3.One,
                    Flags = 2,
                    GoldAmount = 12500,
                    HeroLevel = 1,
                    CreationNumber = _context.GetNextCreationNumber()
                };

                result.SkinId = args.Length > 3 ? (int)args[3].GetDecimalExpressionValueOrDefault() : result.TypeId;

                return result;
            });

            if (unitData != null)
            {
                _context.Add(unitData, variableAssignment);
                _context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        private void ParseDefineStartLocation(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "DefineStartLocation")
            .SafeMapFirst(x =>
            {
                return new
                {
                    Index = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Location = new Vector2((float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(), (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault())
                };
            });

            if (match != null)
            {
                _context.Add_Struct(match.Location, _context.GetPseudoVariableName(DecompilationContext_New.PseudoVariableType.StartLocationIndex, match.Index.ToString()));
                _context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        private void ParseSetPlayerStartLocation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
               x.IdentifierName.Name == "SetPlayerStartLocation")
           .SafeMapFirst(x =>
           {
               int? playerId = null;
               if (x.Arguments.Arguments[0] is IInvocationSyntax playerInvocationSyntax && playerInvocationSyntax.IdentifierName.Name == "Player")
               {
                   if (playerInvocationSyntax.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax variableReferenceExpression)
                   {
                       if (string.Equals(variableReferenceExpression.IdentifierName.Name, "PLAYER_NEUTRAL_AGGRESSIVE", StringComparison.Ordinal))
                       {
                           playerId = _context.MaxPlayerSlots;
                       }
                       else if (string.Equals(variableReferenceExpression.IdentifierName.Name, "PLAYER_NEUTRAL_PASSIVE", StringComparison.Ordinal))
                       {
                           playerId = _context.MaxPlayerSlots + 3;
                       }
                   }

                   var playerVariableName = ((JassVariableReferenceExpressionSyntax)x.Arguments.Arguments[0]).IdentifierName.Name;
                   var player = _context.Get<PlayerData>(playerVariableName) ?? _context.GetLastCreated<PlayerData>();
                   playerId = player.Id;
               }

               if (playerId == null)
               {
                   return null;
               }

               var startLocationIndex = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault();
               var startLocationPosition = _context.Get_Struct<Vector2>(_context.GetPseudoVariableName(DecompilationContext_New.PseudoVariableType.StartLocationIndex, startLocationIndex.ToString()));

               if (startLocationPosition == null)
               {
                   return null;
               }

               var args = x.Arguments.Arguments;
               var result = new UnitData
               {
                   OwnerId = playerId.Value,
                   TypeId = "sloc".FromRawcode(),
                   Position = new Vector3(startLocationPosition.Value, 0f),
                   Rotation = MathF.PI * 1.5f,
                   Scale = Vector3.One,
                   Flags = 2,
                   GoldAmount = 12500,
                   HeroLevel = 0,
                   TargetAcquisition = 0,
                   CreationNumber = _context.GetNextCreationNumber()
               };

               result.SkinId = result.TypeId;

               return result;
           });


            if (match != null)
            {
                _context.Add(match, variableAssignment);
                _context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        private void ParseWaygateDestination(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x =>
                x.IdentifierName.Name == "WaygateSetDestination")
            .SafeMapFirst(x =>
            {
                var regionVariableName = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "GetRectCenterX" || x.IdentifierName.Name == "GetRectCenterY").SafeMapFirst(x => ((JassVariableReferenceExpressionSyntax)x.Arguments.Arguments[0]).IdentifierName.Name);
                if (regionVariableName == null)
                {
                    var destination = new Vector2((float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(), (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault());
                    //regionVariableName = ; // TODO
                }

                return new
                {
                    UnitVariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax)?.IdentifierName?.Name,
                    RegionVariableName = regionVariableName
                };
            });


            if (match != null)
            {
                var unit = _context.Get<UnitData>(match.UnitVariableName) ?? _context.GetLastCreated<UnitData>();
                var region = _context.Get<Region>(match.RegionVariableName) ?? _context.GetLastCreated<Region>();
                if (unit != null)
                {
                    unit.WaygateDestinationRegionId = region.CreationNumber;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }
    }

}