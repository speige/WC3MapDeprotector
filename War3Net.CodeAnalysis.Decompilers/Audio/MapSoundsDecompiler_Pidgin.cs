// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using Pidgin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using War3Net.Build.Audio;
using War3Net.CodeAnalysis.Jass.Syntax;
using static Pidgin.Parser<War3Net.CodeAnalysis.Jass.Syntax.IJassSyntaxToken>;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_Pidgin
    {
        public bool TryDecompileMapSounds(JassCompilationUnitSyntax compilationUnit, MapSoundsFormatVersion formatVersion, out MapSounds? mapSounds)
        {
            _context = new();

            var functions = compilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>()
                .Where(f => f.FunctionDeclarator.IdentifierName.Name == "InitSounds");

            foreach (var function in functions)
            {
                var statements = function.Body.Statements;

                foreach (var statement in statements)
                {
                    var statementChildren = statement.GetChildren_RecursiveDepthFirst().ToList();
                    ParseSoundCreation(statementChildren);
                    ParseSoundProperties(statementChildren);
                }
            }

            mapSounds = _context.AllSounds.Any() ? new MapSounds(formatVersion) { Sounds = _context.AllSounds } : null;
            return mapSounds != null;
        }

        private void ParseSoundCreation(List<IJassSyntaxToken> statementChildren)
        {
            var soundCreationPattern = Token(x =>
                x is JassSetStatementSyntax setStatement &&
                setStatement.Value.Expression is JassInvocationExpressionSyntax invocation &&
                invocation.IdentifierName.Name == "CreateSound")
            .Select(x =>
            {
                var setStatement = (JassSetStatementSyntax)x;
                var invocation = (JassInvocationExpressionSyntax)setStatement.Value.Expression;
                return new Sound
                {
                    Name = setStatement.IdentifierName.Name,
                    FilePath = Regex.Unescape(((JassStringLiteralExpressionSyntax)invocation.Arguments.Arguments[0]).Value),
                    Flags = ParseSoundFlags(invocation.Arguments.Arguments),
                    FadeInRate = (int)invocation.Arguments.Arguments[4].GetDecimalExpressionValueOrDefault(),
                    FadeOutRate = (int)invocation.Arguments.Arguments[5].GetDecimalExpressionValueOrDefault(),
                    EaxSetting = ((JassStringLiteralExpressionSyntax)invocation.Arguments.Arguments[6]).Value,
                };
            }).IgnoreExceptionsAndExtraTokens();

            var matchResult = soundCreationPattern.Parse(statementChildren);
            if (matchResult.Success)
            {
                var sound = matchResult.Value;
                _context.AllSounds.Add(sound);
                _context.VariableNameToSoundMapping[sound.Name] = sound;
            }
        }

        private void ParseSoundProperties(List<IJassSyntaxToken> statementChildren)
        {
            var soundPropertyPattern = Token(x =>
            {
                return x is JassCallStatementSyntax callStatement && _context.VariableNameToSoundMapping.ContainsKey(((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name);
            })
            .Select(x =>
            {
                var callStatement = (JassCallStatementSyntax)x;
                var sound = _context.VariableNameToSoundMapping[((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name];

                switch (callStatement.IdentifierName.Name)
                {
                    case "SetSoundVolume":
                        sound.Volume = (int)callStatement.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault();
                        break;
                    case "SetSoundPitch":
                        sound.Pitch = (float)callStatement.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault();
                        break;
                    case "SetSoundDistances":
                        sound.MinDistance = (float)callStatement.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault();
                        sound.MaxDistance = (float)callStatement.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault();
                        break;
                }

                return sound;
            }).IgnoreExceptionsAndExtraTokens();

            soundPropertyPattern.Parse(statementChildren);
        }

        private SoundFlags ParseSoundFlags(ImmutableArray<IExpressionSyntax> arguments)
        {
            var flags = (SoundFlags)0;
            if (((JassBooleanLiteralExpressionSyntax)arguments[1]).Value)
                flags |= SoundFlags.Looping;
            if (((JassBooleanLiteralExpressionSyntax)arguments[2]).Value)
                flags |= SoundFlags.Is3DSound;
            if (((JassBooleanLiteralExpressionSyntax)arguments[3]).Value)
                flags |= SoundFlags.StopWhenOutOfRange;
            return flags;
        }
    }

}

