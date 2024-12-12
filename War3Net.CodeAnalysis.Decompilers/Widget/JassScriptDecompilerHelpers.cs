// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using War3Net.Build.Audio;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_New
    {
        private string GetVariableAssignment(IEnumerable<IJassSyntaxToken> statementChildren)
        {
            return statementChildren.OneOf(x => x.OfType<IVariableDeclaratorSyntax>().SafeMapFirst(x => x.IdentifierName.Name + (x is JassArrayDeclaratorSyntax ? "[]" : "")),
                            x => x.OfType<JassSetStatementSyntax>().SafeMapFirst(x => x.IdentifierName.Name));
        }

        private SoundFlags ParseSoundFlags(ImmutableArray<IExpressionSyntax> arguments, string filePath = default)
        {
            var flags = (SoundFlags)0;
            if (((JassBooleanLiteralExpressionSyntax)arguments[1]).Value)
            {
                flags |= SoundFlags.Looping;
            }
            if (((JassBooleanLiteralExpressionSyntax)arguments[2]).Value)
            {
                flags |= SoundFlags.Is3DSound;
            }
            if (((JassBooleanLiteralExpressionSyntax)arguments[3]).Value)
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