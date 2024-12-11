// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using Pidgin;
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

            var functions = new[] { compilationUnit.GetFunction("InitSounds") };
            var statements = functions.Where(x => x != null).SelectMany(x => x.GetChildren_RecursiveDepthFirst().OfType<IStatementSyntax>()).ToList();
            foreach (var statement in statements)
            {
                var statementChildren = statement.GetChildren_RecursiveDepthFirst().ToList();
                ParseSoundCreation(statementChildren);
                ParseSetSoundVolume(statementChildren);
                ParseSetSoundPitch(statementChildren);
                ParseSetSoundDistances(statementChildren);
            }

            mapSounds = _context.AllSounds.Any() ? new MapSounds(formatVersion) { Sounds = _context.AllSounds } : null;
            return mapSounds != null;
        }

        private void ParseSoundCreation(List<IJassSyntaxToken> statementChildren)
        {
            var createCameraSetupParser = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "CreateSound")
            .Select(x =>
            {
                var invocation = (IInvocationSyntax)x;
                return new Sound
                {
                    FilePath = Regex.Unescape(((JassStringLiteralExpressionSyntax)invocation.Arguments.Arguments[0]).Value),
                    Flags = ParseSoundFlags(invocation.Arguments.Arguments),
                    FadeInRate = (int)invocation.Arguments.Arguments[4].GetDecimalExpressionValueOrDefault(),
                    FadeOutRate = (int)invocation.Arguments.Arguments[5].GetDecimalExpressionValueOrDefault(),
                    EaxSetting = ((JassStringLiteralExpressionSyntax)invocation.Arguments.Arguments[6]).Value
                };
            });

            var pattern = GetVariableAssignmentParser().Optional().SelectMany(x => createCameraSetupParser, (x, y) => (VariableAssignment: x.HasValue ? x.Value : null, Sound: y));
            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var variableName = matchResult.Value.VariableAssignment;
                var sound = matchResult.Value.Sound;
                if (sound != null)
                {
                    _context.AllSounds.Add(sound);

                    if (!string.IsNullOrWhiteSpace(variableName))
                    {
                        sound.Name = variableName;
                        _context.VariableNameToSoundMapping[sound.Name] = sound;
                    }
                }
            }
        }

        private void ParseSetSoundVolume(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "SetSoundVolume")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Volume = syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var sound = _context.VariableNameToSoundMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllSounds.LastOrDefault();
                if (sound != null)
                {
                    sound.Volume = (int)matchResult.Value.Volume;
                }
            }
        }

        private void ParseSetSoundPitch(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "SetSoundPitch")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Pitch = syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var sound = _context.VariableNameToSoundMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllSounds.LastOrDefault();
                if (sound != null)
                {
                    sound.Pitch = (float)matchResult.Value.Pitch;
                }
            }
        }

        private void ParseSetSoundDistances(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "SetSoundDistances")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    MinDistance = syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    MaxDistance = syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var sound = _context.VariableNameToSoundMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllSounds.LastOrDefault();
                if (sound != null)
                {
                    sound.MinDistance = (float)matchResult.Value.MinDistance;
                    sound.MaxDistance = (float)matchResult.Value.MaxDistance;
                }
            }
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

