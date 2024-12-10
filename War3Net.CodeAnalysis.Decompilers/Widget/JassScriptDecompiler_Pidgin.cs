// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using Pidgin;
using System;
using War3Net.CodeAnalysis.Jass.Syntax;
using static Pidgin.Parser<War3Net.CodeAnalysis.Jass.Syntax.IJassSyntaxToken>;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_Pidgin
    {
        private DecompilationContext_Pidgin _context;

        private Parser<IJassSyntaxToken, string> GetVariableAssignmentParser()
        {
            return Parser.OneOf(
                Token(x => x is IVariableDeclaratorSyntax syntax).Select(x => ((IVariableDeclaratorSyntax)x).IdentifierName.Name + (x is JassArrayDeclaratorSyntax ? "[]" : "")),
                Token(x => x is JassSetStatementSyntax).Select(x => ((JassSetStatementSyntax)x).IdentifierName.Name)
            );
        }
    }
}