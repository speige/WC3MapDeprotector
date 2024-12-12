// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using War3Net.Build.Audio;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_New
    {
        [RegisterStatementParser]
        private void ParseSoundCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var sound = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "CreateSound")
            .SafeMapFirst(x =>
            {
                var filePath = Regex.Unescape(((JassStringLiteralExpressionSyntax)x.Arguments.Arguments[0]).Value);
                //_context.ImportedFileNames.Add(filePath);

                return new Sound
                {
                    FilePath = filePath,
                    Flags = ParseSoundFlags(x.Arguments.Arguments, filePath),
                    FadeInRate = (int)x.Arguments.Arguments[4].GetDecimalExpressionValueOrDefault(),
                    FadeOutRate = (int)x.Arguments.Arguments[5].GetDecimalExpressionValueOrDefault(),
                    EaxSetting = ((JassStringLiteralExpressionSyntax)x.Arguments.Arguments[6]).Value,
                    DialogueTextKey = -1,
                    DialogueSpeakerNameKey = -1
                };
            });

            if (sound != null)
            {
                sound.Name = variableAssignment;
                _context.Add(sound, variableAssignment);
                _context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundParamsFromLabel(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundParamsFromLabel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    ParamsLabel = (x.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax)?.Value,
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    //sound.ParamsLabel = match.ParamsLabel;
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundFacialAnimationLabel(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundFacialAnimationLabel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    FacialAnimationLabel = (x.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax)?.Value,
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.FacialAnimationLabel = match.FacialAnimationLabel;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundFacialAnimationGroupLabel(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundFacialAnimationGroupLabel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    FacialAnimationGroupLabel = (x.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax)?.Value,
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.FacialAnimationGroupLabel = match.FacialAnimationGroupLabel;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundFacialAnimationSetFilepath(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundFacialAnimationSetFilepath")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    FacialAnimationSetFilepath = (x.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax)?.Value,
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.FacialAnimationSetFilepath = match.FacialAnimationSetFilepath;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetDialogueSpeakerNameKey(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetDialogueSpeakerNameKey")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    DialogueSpeakerNameKey_TriggerString = (x.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax).Value,
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    if (match.DialogueSpeakerNameKey_TriggerString.StartsWith("TRIGSTR_", StringComparison.Ordinal) && int.TryParse(match.DialogueSpeakerNameKey_TriggerString["TRIGSTR_".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var dialogueSpeakerNameKey))
                    {
                        sound.DialogueSpeakerNameKey = dialogueSpeakerNameKey;
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetDialogueTextKey(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetDialogueTextKey")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    DialogueTextKey_TriggerString = (x.Arguments.Arguments[1] as JassStringLiteralExpressionSyntax).Value,
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    if (match.DialogueTextKey_TriggerString.StartsWith("TRIGSTR_", StringComparison.Ordinal) && int.TryParse(match.DialogueTextKey_TriggerString["TRIGSTR_".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var dialogueTextKey))
                    {
                        sound.DialogueTextKey = dialogueTextKey;
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundDuration(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundDuration")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Duration = (int)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    //sound.Duration = match.Duration;
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundDistanceCutoff(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundDistanceCutoff")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    DistanceCutoff = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.DistanceCutoff = match.DistanceCutoff;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundChannel(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundChannel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Channel = (SoundChannel)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.Channel = match.Channel;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundVolume(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundVolume")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Volume = x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.Volume = (int)match.Volume;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundPitch(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundPitch")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Pitch = x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                };
            });
            
            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.Pitch = (float)match.Pitch;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundDistances(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundDistances")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    MinDistance = x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    MaxDistance = x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.MinDistance = (float)match.MinDistance;
                    sound.MaxDistance = (float)match.MaxDistance;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundConeAngles(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundConeAngles")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    ConeAngleInside = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    ConeAngleOutside = (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    ConeOutsideVolume = (int)x.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null && sound.Flags.HasFlag(SoundFlags.Is3DSound))
                {
                    sound.ConeAngleInside = match.ConeAngleInside;
                    sound.ConeAngleOutside = match.ConeAngleOutside;
                    sound.ConeOutsideVolume = match.ConeOutsideVolume;
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        private void ParseSetSoundConeOrientation(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "SetSoundConeOrientation")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    x = (float)x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    y = (float)x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    z = (float)x.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault(),
                };
            });

            if (match != null)
            {
                var sound = _context.Get<Sound>(match.VariableName) ?? _context.GetLastCreated<Sound>();
                if (sound != null && sound.Flags.HasFlag(SoundFlags.Is3DSound))
                {
                    sound.ConeOrientation = new(match.x, match.y, match.z);
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }
    }
}