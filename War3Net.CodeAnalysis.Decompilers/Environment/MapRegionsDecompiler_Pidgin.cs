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
using War3Net.Build;
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
                ParseRegionProperties(statementChildren);
            }

            mapRegions = _context.AllRegions.Any() ? new MapRegions(formatVersion) { Regions = _context.AllRegions } : null;
            return mapRegions != null;
        }

        private void ParseRegionCreation(List<IJassSyntaxToken> statementChildren)
        {
            var regionCreationPattern = Token(x =>
                x is JassSetStatementSyntax setStatement &&
                setStatement.Value.Expression is JassInvocationExpressionSyntax invocation &&
                invocation.IdentifierName.Name == "Rect")
            .Select(x =>
            {
                var setStatement = (JassSetStatementSyntax)x;
                var invocation = (JassInvocationExpressionSyntax)setStatement.Value.Expression;
                return new Region
                {
                    Name = setStatement.IdentifierName.Name["gg_rct_".Length..].Replace('_', ' '),
                    Left = (float)invocation.Arguments.Arguments[0].GetDecimalExpressionValueOrDefault(),
                    Bottom = (float)invocation.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Right = (float)invocation.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    Top = (float)invocation.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault(),
                    Color = System.Drawing.Color.FromArgb(unchecked((int)0xFF8080FF)),
                };
            }).IgnoreExceptionsAndExtraTokens();

            var matchResult = regionCreationPattern.Parse(statementChildren);
            if (matchResult.Success)
            {
                var region = matchResult.Value;
                _context.AllRegions.Add(region);
                _context.VariableNameToRegionMapping[region.Name] = region;
            }
        }

        private void ParseRegionProperties(List<IJassSyntaxToken> statementChildren)
        {
            var regionPropertyPattern = Token(x =>
                x is JassCallStatementSyntax callStatement &&
                _context.VariableNameToRegionMapping.ContainsKey(((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name))
            .Select(x =>
            {
                var callStatement = (JassCallStatementSyntax)x;
                var region = _context.VariableNameToRegionMapping[((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name];

                switch (callStatement.IdentifierName.Name)
                {
                    case "AddWeatherEffect":
                        region.WeatherType = (WeatherType)((JassFourCCLiteralExpressionSyntax)callStatement.Arguments.Arguments[1]).Value.InvertEndianness();
                        break;
                    case "SetSoundPosition":
                        region.AmbientSound = ((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name;
                        break;
                }

                return region;
            }).IgnoreExceptionsAndExtraTokens();

            regionPropertyPattern.Parse(statementChildren);
        }
    }

}