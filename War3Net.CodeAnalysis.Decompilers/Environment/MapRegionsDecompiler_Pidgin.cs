// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using Pidgin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using War3Net.Build;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;
using static Pidgin.Parser<War3Net.CodeAnalysis.Jass.Syntax.IJassSyntaxToken>;
using Region = War3Net.Build.Environment.Region;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_Pidgin
    {
        public bool TryDecompileMapRegions(JassCompilationUnitSyntax compilationUnit, MapRegionsFormatVersion formatVersion, out MapRegions? mapRegions)
        {
            _context = new();

            var functions = new[] { compilationUnit.GetFunction("CreateRegions") };
            var statements = functions.Where(x => x != null).SelectMany(x => x.GetChildren_RecursiveDepthFirst().OfType<IStatementSyntax>()).ToList();
            foreach (var statement in statements)
            {
                var statementChildren = statement.GetChildren_RecursiveDepthFirst().ToList();
                ParseRegionCreation(statementChildren);
                ParseAddWeatherEffect(statementChildren);
                ParseSetSoundPosition(statementChildren);
            }

            mapRegions = _context.AllRegions.Any() ? new MapRegions(formatVersion) { Regions = _context.AllRegions } : null;
            return mapRegions != null;
        }

        private void ParseRegionCreation(List<IJassSyntaxToken> statementChildren)
        {
            var createCameraSetupParser = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "Rect")
            .Select(x =>
            {
                var invocation = (IInvocationSyntax)x;
                return new Region
                {
                    Left = (float)invocation.Arguments.Arguments[0].GetDecimalExpressionValueOrDefault(),
                    Bottom = (float)invocation.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Right = (float)invocation.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    Top = (float)invocation.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault(),
                    Color = System.Drawing.Color.FromArgb(unchecked((int)0xFF8080FF)),
                };
            });

            var pattern = GetVariableAssignmentParser().Optional().SelectMany(x => createCameraSetupParser, (x, y) => (VariableAssignment: x.HasValue ? x.Value : null, Sound: y));
            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var variableName = matchResult.Value.VariableAssignment;
                var region = matchResult.Value.Sound;
                if (region != null)
                {
                    _context.AllRegions.Add(region);

                    if (region.Name != null)
                    {
                        region.Name = variableName;
                        if (region.Name.StartsWith("gg_rct_"))
                        {
                            region.Name = region.Name["gg_rct_".Length..];
                        }
                        region.Name = region.Name.Replace('_', ' ');
                        _context.VariableNameToRegionMapping[region.Name] = region;
                    }
                }
            }
        }

        private void ParseAddWeatherEffect(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "AddWeatherEffect")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    WeatherType = (WeatherType)((JassFourCCLiteralExpressionSyntax)syntax.Arguments.Arguments[1]).Value.InvertEndianness(),
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var region = _context.VariableNameToRegionMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllRegions.LastOrDefault();
                if (region != null)
                {
                    region.WeatherType = matchResult.Value.WeatherType;
                }
            }
        }

        private void ParseSetSoundPosition(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "SetSoundPosition")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    AmbientSound = ((JassVariableReferenceExpressionSyntax)syntax.Arguments.Arguments[0]).IdentifierName.Name,
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var region = _context.VariableNameToRegionMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllRegions.LastOrDefault();
                if (region != null)
                {
                    region.AmbientSound = matchResult.Value.AmbientSound;
                }
            }
        }
    }
}