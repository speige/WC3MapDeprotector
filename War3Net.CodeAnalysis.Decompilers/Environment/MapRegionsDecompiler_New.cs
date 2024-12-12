// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using War3Net.Build;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;
using Region = War3Net.Build.Environment.Region;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_New
    {
        [RegisterStatementParser]
        private void ParseRegionCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var region = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "Rect")
            .SafeMapFirst(x =>
            {
                return new Region
                {
                    Left = (float)x.Arguments.Arguments[0].GetDecimalExpressionValueOrDefault(),
                    Bottom = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Right = (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    Top = (float)x.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault(),
                    Color = System.Drawing.Color.FromArgb(unchecked((int)0xFF8080FF)),
                };
            });

            if (region != null)
            {
                region.Name = variableAssignment;
                if (region.Name != null)
                {
                    if (region.Name.StartsWith("gg_rct_"))
                    {
                        region.Name = region.Name["gg_rct_".Length..];
                    }
                    region.Name = region.Name.Replace('_', ' ');
                }

                _context.Add(region, variableAssignment);
                _context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        private void ParseAddWeatherEffect(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "AddWeatherEffect")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    WeatherType = (WeatherType)((JassFourCCLiteralExpressionSyntax)x.Arguments.Arguments[1]).Value.InvertEndianness(),
                };
            });

            if (match != null)
            {
                var region = _context.Get<Region>(match.VariableName) ?? _context.GetLastCreated<Region>();
                if (region != null)
                {
                    region.WeatherType = match.WeatherType;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundPosition(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundPosition")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    x = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    y = (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var region = _context.Get<Region>(match.VariableName) ?? _context.GetLastCreated<Region>();
                if (region != null)
                {
                    if (region.CenterX == match.x && region.CenterY == match.y)
                    {
                        region.AmbientSound = match.VariableName;
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseRegisterStackedSound(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "RegisterStackedSound")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Width = (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    Height = (float)x.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var region = _context.Get<Region>(match.VariableName) ?? _context.GetLastCreated<Region>();
                if (region != null)
                {
                    if (region.Width == match.Width && region.Height == match.Height)
                    {
                        region.AmbientSound = match.VariableName;
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseEnableWeatherEffect(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "EnableWeatherEffect")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Enabled = x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault() == 1,
                };
            });

            if (match != null)
            {
                var region = _context.Get<Region>(match.VariableName) ?? _context.GetLastCreated<Region>();
                if (region != null)
                {
                    //region.WeatherTypeEnabled = match.Enabled;
                }
            }
        }
    }
}