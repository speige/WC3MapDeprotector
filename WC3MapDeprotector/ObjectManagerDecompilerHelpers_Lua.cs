using War3Net.Build.Audio;
using System.Collections.Immutable;
using Jass2Lua;

namespace WC3MapDeprotector
{
    public partial class ObjectManagerDecompiler_Lua
    {
        protected int? GetLastCreatedPlayerIndex()
        {
            return Context.Get_Struct<int>(Context.CreatePseudoVariableName(nameof(ParsePlayerIndex)));
        }

        protected int? GetPlayerIndex_FromIdentifier(LuaASTNode identifier)
        {
            if (string.Equals(identifier.name, "PLAYER_NEUTRAL_AGGRESSIVE", StringComparison.Ordinal))
            {
                return Context.MaxPlayerSlots;
            }
            else if (string.Equals(identifier.name, "PLAYER_NEUTRAL_PASSIVE", StringComparison.Ordinal))
            {
                return Context.MaxPlayerSlots + 3;
            }
            else
            {
                return Context.Get_Struct<int>(Context.CreatePseudoVariableName(nameof(ParsePlayerIndex), identifier.name));
            }
        }

        protected int? GetPlayerIndex_FromInvocation(LuaASTNode invocation)
        {
            if (invocation.GetInvocationName() != "Player")
            {
                return null;
            }

            if (invocation.arguments[0].IsIdentifier())
            {
                return GetPlayerIndex_FromIdentifier(invocation.arguments[0]);
            }
            else if (invocation.arguments[0].TryGetValue<int>(out var playerId))
            {
                return playerId;
            }

            return null;
        }

        protected int? GetPlayerIndex(LuaASTNode node)
        {
            if (node.IsInvocation())
            {
                return GetPlayerIndex_FromInvocation(node);
            }
            else if (node.IsIdentifier())
            {
                return GetPlayerIndex_FromIdentifier(node);
            }

            return null;
        }

        protected string GetVariableAssignment(IEnumerable<LuaASTNode> statementChildren)
        {
            return statementChildren.Where(x => x.IsVariableAssignment()).SafeMapFirst(x => x.variables[0].name);
        }

        protected SoundFlags ParseSoundFlags(List<LuaASTNode> arguments, string filePath = default)
        {
            var flags = (SoundFlags)0;
            if (arguments[1].TryGetValue<bool>(out var looping) && looping)
            {
                flags |= SoundFlags.Looping;
            }
            if (arguments[2].TryGetValue<bool>(out var is3DSound) && is3DSound)
            {
                flags |= SoundFlags.Is3DSound;
            }
            if (arguments[3].TryGetValue<bool>(out var stopWhenOutOfRange) && stopWhenOutOfRange)
            {
                flags |= SoundFlags.StopWhenOutOfRange;
            }
            if ((flags & SoundFlags.Is3DSound) == SoundFlags.Is3DSound && !IsInternalSound(filePath ?? ""))
            {
                flags |= SoundFlags.UNK16;
            }

            return flags;
        }

        [Obsolete]
        private static bool IsInternalSound(string filePath)
        {
            return filePath.StartsWith(@"Sound\", StringComparison.OrdinalIgnoreCase)
                || filePath.StartsWith(@"Sound/", StringComparison.OrdinalIgnoreCase)
                || filePath.StartsWith(@"UI\", StringComparison.OrdinalIgnoreCase)
                || filePath.StartsWith(@"UI/", StringComparison.OrdinalIgnoreCase)
                || filePath.StartsWith(@"Units\", StringComparison.OrdinalIgnoreCase)
                || filePath.StartsWith(@"Units/", StringComparison.OrdinalIgnoreCase);
        }
    }
}