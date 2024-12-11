// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using Pidgin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using War3Net.Build.Widget;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;
using static Pidgin.Parser<War3Net.CodeAnalysis.Jass.Syntax.IJassSyntaxToken>;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_Pidgin
    {
        public bool TryDecompileMapUnits(JassCompilationUnitSyntax compilationUnit, MapWidgetsFormatVersion mapWidgetsFormatVersion, MapWidgetsSubVersion mapWidgetsSubVersion, bool useNewFormat, out MapUnits units)
        {
            _context = new();

            var functions = new[] { compilationUnit.GetFunction("CreateAllUnits"), compilationUnit.GetFunction("CreateAllItems"), compilationUnit.GetFunction("InitCustomPlayerSlots") };
            var statements = functions.Where(x => x != null).SelectMany(x => x.GetChildren_RecursiveDepthFirst().OfType<IStatementSyntax>()).ToList();
            foreach (var statement in statements)
            {
                var statementChildren = statement.GetChildren_RecursiveDepthFirst().ToList();
                ParseUnitCreation(statementChildren);
                ParseHeroLevel(statementChildren);
                ParseResourceAmount(statementChildren);
                ParseUnitColor(statementChildren);
                ParseAcquireRange(statementChildren);
                ParseUnitState(statementChildren);
                ParseHeroAttributes(statementChildren);
                ParseHeroSkills(statementChildren);
                ParseItemCreation(statementChildren);
                ParseUnitInventory(statementChildren);
                ParseTriggerEvent(statementChildren);
                ParseWaygateDestination(statementChildren);
                ParseRandomDistribution(statementChildren);
            }

            units = new MapUnits(mapWidgetsFormatVersion, mapWidgetsSubVersion, useNewFormat) { Units = _context.AllUnits };
            return true;
        }

        private void ParseUnitCreation(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = GetVariableAssignmentParser().Optional().SelectMany(x => GetCreateUnitParser(), (x, y) => (VariableAssignment: x.HasValue ? x.Value : null, UnitData: y));
            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);

            if (matchResult.Success)
            {
                var unitData = matchResult.Value.UnitData;
                var variableName = matchResult.Value.VariableAssignment;
                _context.AllUnits.Add(unitData);

                if (variableName != null)
                {
                    _context.VariableNameToUnitMapping[variableName] = unitData;
                }
            }
        }

        private void ParseHeroLevel(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "SetHeroLevel")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    Level = (int)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    unit.HeroLevel = matchResult.Value.Level;
                }
            }
        }

        private void ParseResourceAmount(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "SetResourceAmount")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    Amount = (int)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    unit.GoldAmount = matchResult.Value.Amount;
                }
            }
        }

        private void ParseUnitColor(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "SetUnitColor")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    ColorId = (int)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    unit.CustomPlayerColorId = matchResult.Value.ColorId;
                }
            }
        }

        private void ParseAcquireRange(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "SetUnitAcquireRange")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    Range = (float)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    unit.TargetAcquisition = matchResult.Value.Range;
                }
            }
        }

        private void ParseUnitState(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "SetUnitState")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    StateType = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[1]).IdentifierName.Name,
                    Value = (float)syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    if (matchResult.Value.StateType == "UNIT_STATE_LIFE")
                    {
                        unit.HP = (int)matchResult.Value.Value;
                    }
                    else if (matchResult.Value.StateType == "UNIT_STATE_MANA")
                    {
                        unit.MP = (int)matchResult.Value.Value;
                    }
                }
            }
        }

        private void ParseHeroAttributes(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                (syntax.IdentifierName.Name == "SetHeroStr" ||
                 syntax.IdentifierName.Name == "SetHeroAgi" ||
                 syntax.IdentifierName.Name == "SetHeroInt"))
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    Attribute = syntax.IdentifierName.Name,
                    Value = (int)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    switch (matchResult.Value.Attribute)
                    {
                        case "SetHeroStr":
                            unit.HeroStrength = matchResult.Value.Value;
                            break;
                        case "SetHeroAgi":
                            unit.HeroAgility = matchResult.Value.Value;
                            break;
                        case "SetHeroInt":
                            unit.HeroIntelligence = matchResult.Value.Value;
                            break;
                    }
                }
            }
        }

        private void ParseHeroSkills(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "SelectHeroSkill")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    SkillId = (int)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    unit.AbilityData.Add(new ModifiedAbilityData
                    {
                        AbilityId = matchResult.Value.SkillId,
                        HeroAbilityLevel = 1,
                        IsAutocastActive = false
                    });
                }
            }
        }

        private void ParseItemCreation(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                (syntax.IdentifierName.Name == "CreateItem" || syntax.IdentifierName.Name == "BlzCreateItemWithSkin"))
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                var isWithSkin = syntax.IdentifierName.Name == "BlzCreateItemWithSkin";
                return new
                {
                    ItemId = (int)syntax.Arguments.Arguments[0].GetDecimalExpressionValueOrDefault(),
                    Position = new Vector3(
                        (float)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                        (float)syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                        0f),
                    SkinId = isWithSkin ? (int)syntax.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault() : (int)syntax.Arguments.Arguments[0].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                // Handle item creation as needed.
            }
        }

        private void ParseUnitInventory(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "UnitAddItemToSlotById")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    ItemId = (int)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Slot = (int)syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    unit.InventoryData.Add(new InventoryItemData
                    {
                        ItemId = matchResult.Value.ItemId,
                        Slot = matchResult.Value.Slot
                    });
                }
            }
        }

        private void ParseTriggerEvent(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "TriggerRegisterUnitEvent")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    EventName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[1]).IdentifierName.Name
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    // Handle trigger registration as needed.
                }
            }
        }

        private void ParseWaygateDestination(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "WaygateSetDestination")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                    Destination = new Vector2(
                        (float)syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                        (float)syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault())
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var unit = _context.VariableNameToUnitMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllUnits.LastOrDefault();
                if (unit != null)
                {
                    //unit.WaygateDestination = matchResult.Value.Destination;
                }
            }
        }

        private void ParseRandomDistribution(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x =>
                x is IInvocationSyntax syntax &&
                syntax.IdentifierName.Name == "RandomDistReset")
            .Select(x => true);

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                // Reset random distribution logic as needed.
            }
        }

        private Parser<IJassSyntaxToken, UnitData> GetCreateUnitParser()
        {
            return Token(x =>
                x is IInvocationSyntax syntax && (syntax.IdentifierName.Name == "CreateUnit" || syntax.IdentifierName.Name == "BlzCreateUnitWithSkin"))
                .Select(x =>
                {
                    var args = ((IInvocationSyntax)x).Arguments.Arguments;
                    return new UnitData
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
                        SkinId = args.Length > 5 ? (int)args[5].GetDecimalExpressionValueOrDefault() : (int)args[1].GetDecimalExpressionValueOrDefault(),
                        CreationNumber = _context.CreationNumber++
                    };
                });
        }
    }    
}