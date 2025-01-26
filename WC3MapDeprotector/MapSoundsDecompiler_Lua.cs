using War3Net.Build.Audio;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public partial class ObjectManagerDecompiler_Lua
    {
        [RegisterStatementParser]
        protected void ParseSoundCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            if (variableAssignment == null || !variableAssignment.StartsWith("gg_snd_"))
            {
                return;
            }

            var sound = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "CreateSound")
            .SafeMapFirst(x =>
            {
                var filePath = Regex.Unescape(x.arguments[0].GetValueOrDefault<string>());

                return new Sound
                {
                    FilePath = filePath,
                    Flags = ParseSoundFlags(x.arguments, filePath),
                    FadeInRate = x.arguments[4].GetValueOrDefault<int>(),
                    FadeOutRate = x.arguments[5].GetValueOrDefault<int>(),
                    EaxSetting = x.arguments[6].GetValueOrDefault<string>(),
                    DialogueTextKey = -1,
                    DialogueSpeakerNameKey = -1
                };
            });

            if (sound != null)
            {
                sound.Name = variableAssignment;
                Context.Add(sound, variableAssignment);
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundParamsFromLabel(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundParamsFromLabel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    SoundName = x.arguments[1]?.GetValueOrDefault<string>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.SoundName = match.SoundName;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundFacialAnimationLabel(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundFacialAnimationLabel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    FacialAnimationLabel = x.arguments[1]?.GetValueOrDefault<string>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.FacialAnimationLabel = match.FacialAnimationLabel;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundFacialAnimationGroupLabel(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundFacialAnimationGroupLabel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    FacialAnimationGroupLabel = x.arguments[1]?.GetValueOrDefault<string>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.FacialAnimationGroupLabel = match.FacialAnimationGroupLabel;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundFacialAnimationSetFilepath(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundFacialAnimationSetFilepath")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    FacialAnimationSetFilepath = x.arguments[1]?.GetValueOrDefault<string>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.FacialAnimationSetFilepath = match.FacialAnimationSetFilepath;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetDialogueSpeakerNameKey(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetDialogueSpeakerNameKey")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    DialogueSpeakerNameKey_TriggerString = x.arguments[1]?.GetValueOrDefault<string>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    if (match.DialogueSpeakerNameKey_TriggerString.StartsWith("TRIGSTR_", StringComparison.Ordinal) && int.TryParse(match.DialogueSpeakerNameKey_TriggerString["TRIGSTR_".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var dialogueSpeakerNameKey))
                    {
                        sound.DialogueSpeakerNameKey = dialogueSpeakerNameKey;
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetDialogueTextKey(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetDialogueTextKey")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    DialogueTextKey_TriggerString = x.arguments[1]?.GetValueOrDefault<string>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    if (match.DialogueTextKey_TriggerString.StartsWith("TRIGSTR_", StringComparison.Ordinal) && int.TryParse(match.DialogueTextKey_TriggerString["TRIGSTR_".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var dialogueTextKey))
                    {
                        sound.DialogueTextKey = dialogueTextKey;
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundDistanceCutoff(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundDistanceCutoff")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    DistanceCutoff = x.arguments[1].GetValueOrDefault<float>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.DistanceCutoff = match.DistanceCutoff;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundChannel(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundChannel")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    Channel = (SoundChannel)x.arguments[1].GetValueOrDefault<int>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.Channel = match.Channel;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundVolume(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundVolume")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    Volume = x.arguments[1].GetValueOrDefault<int>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.Volume = match.Volume;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundPitch(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundPitch")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    Pitch = x.arguments[1].GetValueOrDefault<float>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.Pitch = match.Pitch;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundDistances(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundDistances")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    MinDistance = x.arguments[1].GetValueOrDefault<float>(),
                    MaxDistance = x.arguments[2].GetValueOrDefault<float>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null)
                {
                    sound.MinDistance = match.MinDistance;
                    sound.MaxDistance = match.MaxDistance;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundConeAngles(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundConeAngles")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    ConeAngleInside = x.arguments[1].GetValueOrDefault<float>(),
                    ConeAngleOutside = x.arguments[2].GetValueOrDefault<float>(),
                    ConeOutsideVolume = x.arguments[3].GetValueOrDefault<int>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null && sound.Flags.HasFlag(SoundFlags.Is3DSound))
                {
                    sound.ConeAngleInside = match.ConeAngleInside;
                    sound.ConeAngleOutside = match.ConeAngleOutside;
                    sound.ConeOutsideVolume = match.ConeOutsideVolume;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundConeOrientation(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundConeOrientation")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0]?.IsIdentifier() == true ? x.arguments[0].name : "",
                    x = x.arguments[1].GetValueOrDefault<float>(),
                    y = x.arguments[2].GetValueOrDefault<float>(),
                    z = x.arguments[3].GetValueOrDefault<float>(),
                };
            });

            if (match != null)
            {
                var sound = Context.Get<Sound>(match.VariableName) ?? Context.GetLastCreated<Sound>();
                if (sound != null && sound.Flags.HasFlag(SoundFlags.Is3DSound))
                {
                    sound.ConeOrientation = new(match.x, match.y, match.z);
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }
    }
}