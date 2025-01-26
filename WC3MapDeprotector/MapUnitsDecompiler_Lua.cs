using System;
using System.Linq;
using System.Numerics;
using War3Net.Build.Common;
using War3Net.Build.Environment;
using War3Net.Build.Widget;
using War3Net.Common.Extensions;
using War3Net.CodeAnalysis.Jass.Extensions;
using Region = War3Net.Build.Environment.Region;

namespace WC3MapDeprotector
{
    public partial class ObjectManagerDecompiler_Lua
    {
        [RegisterStatementParser]
        protected void ParseUnitCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var unitData = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "CreateUnit" || x.GetInvocationName() == "BlzCreateUnitWithSkin").SafeMapFirst(x =>
            {
                var args = x.arguments;

                int? ownerId = GetPlayerIndex(args[0]) ?? GetLastCreatedPlayerIndex();

                if (ownerId == null)
                {
                    return null;
                }

                var typeId = args[1].GetFourCC();
                if (typeId == null)
                {
                    return null;
                }

                var result = new UnitData
                {
                    OwnerId = ownerId.Value,
                    TypeId = typeId.Value,
                    Position = new Vector3(
                            args[2].GetValueOrDefault<float>(),
                            args[3].GetValueOrDefault<float>(),
                            0f
                        ),
                    Rotation = args[4].GetValueOrDefault<float>() * (MathF.PI / 180f),
                    Scale = Vector3.One,
                    Flags = 2,
                    GoldAmount = 12500,
                    HeroLevel = 1,
                    CreationNumber = Context.GetNextCreationNumber()
                };

                result.SkinId = args.Count > 5 ? args[5].GetValueOrDefault<int>() : result.TypeId;
                return result;
            });

            if (unitData != null)
            {
                Context.HandledStatements.Add(input.Statement);
                Context.Add(unitData, variableAssignment);
            }
        }

        [RegisterStatementParser]
        protected void ParsePlayerIndex(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "Player").SafeMapFirst(x =>
            {
                var playerId = GetPlayerIndex(x);
                if (!playerId.HasValue)
                {
                    return null;
                }

                return new
                {
                    PlayerIndex = playerId.Value
                };
            });

            if (match != null && variableAssignment != null)
            {
                Context.Add_Struct(match.PlayerIndex, Context.CreatePseudoVariableName(nameof(ParsePlayerIndex), variableAssignment));
                Context.Add_Struct(match.PlayerIndex, Context.CreatePseudoVariableName(nameof(ParsePlayerIndex)));
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseResourceAmount(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetResourceAmount").SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    Amount = x.arguments[1].GetValueOrDefault<int>()
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.GoldAmount = match.Amount;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetUnitColor(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetUnitColor").SafeMapFirst(x =>
            {
                int? playerColor = null;
                var playerColorString = (x.arguments[1].GetValueOrDefault<string>() ?? "").Trim();

                if (playerColorString.StartsWith("PLAYER_COLOR_"))
                {
                    playerColorString = playerColorString.Replace("PLAYER_COLOR_", "");
                    if (Enum.TryParse<KnownPlayerColor>(playerColorString, out var color))
                    {
                        playerColor = (int)color;
                    }
                }
                else
                {
                    playerColorString = playerColorString.Replace("ConvertPlayerColor", "").Replace("(", "").Replace(")", "");
                    if (int.TryParse(playerColorString, out var intValue))
                    {
                        playerColor = intValue;
                    }
                }

                if (playerColor == null)
                {
                    return null;
                }

                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    CustomPlayerColorId = playerColor.Value
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.CustomPlayerColorId = match.CustomPlayerColorId;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseAcquireRange(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetUnitAcquireRange")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    Range = x.arguments[1].GetValueOrDefault<float>()
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.TargetAcquisition = match.Range;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseUnitState(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetUnitState")
            .SafeMapFirst(x =>
            {
                var stateType = x.arguments[1].name;
                var value = x.arguments[2].GetValueOrDefault<int>();
                
                if (stateType == "UNIT_STATE_LIFE")
                {
                    var stringValue = (x.arguments[2].GetValueOrDefault<string>() ?? "").Trim().Replace(" ", "").Replace("*life", "").Replace("(", "").Replace(")", "");
                    if (float.TryParse(stringValue, out var floatValue))
                    {
                        value = (int)(floatValue * 100);
                    }
                }

                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    StateType = stateType,
                    Value = value
                };
            });


            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    if (match.StateType == "UNIT_STATE_LIFE")
                    {
                        unit.HP = match.Value;
                        Context.HandledStatements.Add(input.Statement);
                    }
                    else if (match.StateType == "UNIT_STATE_MANA")
                    {
                        unit.MP = match.Value;
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseUnitInventory(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "UnitAddItemToSlotById")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    ItemId = x.arguments[1].GetFourCC().Value,
                    Slot = x.arguments[2].GetValueOrDefault<int>()
                };
            });


            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.InventoryData.Add(new InventoryItemData
                    {
                        ItemId = match.ItemId,
                        Slot = match.Slot
                    });
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseHeroLevel(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetHeroLevel").SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    Level = x.arguments[1].GetValueOrDefault<int>()
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.HeroLevel = match.Level;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseHeroAttributes(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetHeroStr" || x.GetInvocationName() == "SetHeroAgi" || x.GetInvocationName() == "SetHeroInt")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    MethodName = x.GetInvocationName(),
                    Value = x.arguments[1].GetValueOrDefault<int>()
                };
            });


            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    var handled = true;
                    switch (match.MethodName)
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
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseHeroSkills(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SelectHeroSkill")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    SkillId = x.arguments[1].GetFourCC().Value
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.AbilityData.Add(new ModifiedAbilityData
                    {
                        AbilityId = match.SkillId,
                        HeroAbilityLevel = 1,
                        IsAutocastActive = false
                    });
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseIssueImmediateOrder(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "IssueImmediateOrder")
            .SafeMapFirst(x =>
            {
                var abilityName = x.arguments[1].GetValueOrDefault<string>();
                bool? isAutoCastActive = null;
                if (abilityName?.EndsWith("on", StringComparison.OrdinalIgnoreCase) == true)
                {
                    isAutoCastActive = true;
                }
                else if (abilityName?.EndsWith("off", StringComparison.OrdinalIgnoreCase) == true)
                {
                    isAutoCastActive = false;
                }
                else
                {
                    return null;
                }

                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    //AbilityName = abilityName,
                    IsAutocastActive = isAutoCastActive.Value
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.VariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    //todo: lookup abilityId by searching for name in _context.ObjectData.map.AbilityObjectData
                    //todo: lookup on/off via SLK Metadata instead of abilityName suffix [example: Modifications with FourCC aoro/aord are names for "on", aorf/aoru are names for "off"]
                    var ability = unit.AbilityData.LastOrDefault();
                    if (ability != null)
                    {
                        ability.IsAutocastActive = match.IsAutocastActive;
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseDropItemsOnDeath_TriggerRegister(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "TriggerRegisterUnitEvent")
            .SafeMapFirst(x =>
            {
                var eventName = x.arguments[2]?.IsIdentifier() == true ? x.arguments[2].name : "";
                if (eventName != "EVENT_UNIT_DEATH" && eventName != "EVENT_UNIT_CHANGE_OWNER")
                {
                    return null;
                }

                return new
                {
                    TriggerVariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    UnitVariableName = x.arguments[1]?.IsIdentifier() == true ? x.arguments[1].name : ""
                };
            });

            if (match != null)
            {
                Context.Add(match.UnitVariableName, Context.CreatePseudoVariableName(nameof(ParseDropItemsOnDeath_TriggerRegister), match.TriggerVariableName));
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseDropItemsOnDeath_TriggerAction(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "TriggerAddAction")
            .SafeMapFirst(x =>
            {
                return new
                {
                    TriggerVariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    FunctionName = x.arguments[1]?.IsIdentifier() == true ? x.arguments[1].name : ""
                };
            });

            if (match != null)
            {
                var unitVariableName = Context.Get<string>(Context.CreatePseudoVariableName(nameof(ParseDropItemsOnDeath_TriggerRegister), match.TriggerVariableName));

                if (match.FunctionName != null && Context.FunctionDeclarations.TryGetValue(match.FunctionName, out var function))
                {
                    var statements = function.FunctionDeclaration.GetChildren_RecursiveDepthFirst().Where(x => x.IsStatement()).ToList();
                    foreach (var statement in statements)
                    {
                        ProcessStatementParsers(statement, new Action<StatementParserInput>[] {
                            input => ParseRandomDistReset(input, unitVariableName),
                            input => ParseRandomDistAddItem(input, unitVariableName),
                        });
                    }

                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        protected void ParseRandomDistReset(StatementParserInput input, string unitVariableName)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "RandomDistReset")
            .SafeMapFirst(x =>
            {
                return new RandomItemSet();
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(unitVariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    unit.ItemTableSets.Add(match);
                }
                Context.Add(match);
                Context.HandledStatements.Add(input.Statement);
            }
        }

        protected void ParseRandomDistAddItem(StatementParserInput input, string unitVariableName)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "RandomDistAddItem")
            .SafeMapFirst(x =>
            {
                return new RandomItemSetItem()
                {
                    ItemId = x.arguments[0].GetFourCC() ?? 0,
                    Chance = x.arguments[1].GetValueOrDefault<int>(),
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(unitVariableName) ?? Context.GetLastCreated<UnitData>();
                if (unit != null)
                {
                    var itemSet = unit.ItemTableSets.LastOrDefault();
                    if (itemSet != null)
                    {
                        itemSet.Items.Add(match);
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseItemCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var unitData = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "CreateItem" || x.GetInvocationName() == "BlzCreateItemWithSkin").SafeMapFirst(x =>
            {
                var args = x.arguments;

                var typeId = args[0].GetFourCC();
                if (typeId == null)
                {
                    return null;
                }

                var result = new UnitData
                {
                    OwnerId = Context.MaxPlayerSlots + 3, // NEUTRAL_PASSIVE
                    TypeId = typeId.Value,
                    Position = new Vector3(
                            args[1].GetValueOrDefault<float>(),
                            args[2].GetValueOrDefault<float>(),
                            0f
                        ),
                    Rotation = 0,
                    Scale = Vector3.One,
                    Flags = 2,
                    GoldAmount = 12500,
                    HeroLevel = 1,
                    CreationNumber = Context.GetNextCreationNumber()
                };

                result.SkinId = args.Count > 3 ? args[3].GetValueOrDefault<int>() : result.TypeId;

                return result;
            });

            if (unitData != null)
            {
                Context.Add(unitData, variableAssignment);
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseDefineStartLocation(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "DefineStartLocation")
            .SafeMapFirst(x =>
            {
                return new
                {
                    Index = x.arguments[0].GetValueOrDefault<int>(),
                    Location = new Vector2(x.arguments[1].GetValueOrDefault<float>(), x.arguments[2].GetValueOrDefault<float>())
                };
            });

            if (match != null)
            {
                Context.Add_Struct(match.Location, Context.CreatePseudoVariableName(nameof(ParseDefineStartLocation), match.Index.ToString()));
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseSetPlayerStartLocation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetPlayerStartLocation")
           .SafeMapFirst(x =>
           {
               int? playerId = GetPlayerIndex(x.arguments[0]) ?? GetLastCreatedPlayerIndex();

               if (playerId == null)
               {
                   return null;
               }

               var startLocationIndex = x.arguments[1].GetValueOrDefault<int>();
               var startLocationPosition = Context.Get_Struct<Vector2>(Context.CreatePseudoVariableName(nameof(ParseDefineStartLocation), startLocationIndex.ToString()));

               if (startLocationPosition == null)
               {
                   return null;
               }

               var args = x.arguments;
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
                   CreationNumber = Context.GetNextCreationNumber()
               };

               result.SkinId = result.TypeId;

               return result;
           });

            if (match != null)
            {
                Context.Add(match, variableAssignment);
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseWaygateDestination(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "WaygateSetDestination")
            .SafeMapFirst(x =>
            {
                var regionVariableName = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "GetRectCenterX" || x.GetInvocationName() == "GetRectCenterY").SafeMapFirst(x => x.arguments[0].name);
                if (regionVariableName == null)
                {
                    var destination = new Vector2(x.arguments[1].GetValueOrDefault<float>(), x.arguments[2].GetValueOrDefault<float>());
                    var region = Context.GetAll<Region>().LastOrDefault(x => x.CenterX == destination.X && x.CenterY == destination.Y);
                    regionVariableName = Context.GetVariableName(region);
                }

                return new
                {
                    UnitVariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    RegionVariableName = regionVariableName
                };
            });

            if (match != null)
            {
                var unit = Context.Get<UnitData>(match.UnitVariableName) ?? Context.GetLastCreated<UnitData>();
                var region = Context.Get<Region>(match.RegionVariableName) ?? Context.GetLastCreated<Region>();
                if (unit != null && region != null)
                {
                    unit.WaygateDestinationRegionId = region.CreationNumber;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }
    }
}