// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using Pidgin;
using System;
using System.Linq;
using War3Net.CodeAnalysis.Jass.Syntax;
using static Pidgin.Parser<War3Net.CodeAnalysis.Jass.Syntax.IJassSyntaxToken>;

namespace War3Net.CodeAnalysis.Decompilers
{
    public static class JassCompilationUnitSyntaxExtensions
    {
        public static Parser<TToken, T> IgnoreExceptions<TToken, T>(this Parser<TToken, T> innerParser)
        {
            var result = Parser.Lookahead(Parser<TToken>.Token(x =>
            {
                try
                {
                    return innerParser.Parse(new[] { x }).Success;
                }
                catch
                {
                    return false;
                }
            })).Then(innerParser);

            return result;
        }

        public static Parser<IJassSyntaxToken, T> IgnoreExtraTokens<T>(this Parser<IJassSyntaxToken, T> innerParser)
        {
            return Any.SkipUntil(Parser.Lookahead(innerParser)).Then(innerParser);
        }

        public static Parser<IJassSyntaxToken, T> IgnoreExceptionsAndExtraTokens<T>(this Parser<IJassSyntaxToken, T> innerParser)
        {
            return innerParser.IgnoreExceptions().IgnoreExtraTokens();
        }

        public static IJassSyntaxToken GetFunction(this JassCompilationUnitSyntax compilationUnit, string functionName)
        {
            return compilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>().FirstOrDefault(x => x.FunctionDeclarator.IdentifierName.Name == functionName);
        }
    }
}