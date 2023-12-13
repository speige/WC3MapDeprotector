// ------------------------------------------------------------------------------
// <copyright file="MapSoundsDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public static class ExpressionSyntaxExtensions_New
    {
        public static bool TryGetIntegerExpressionValue_New(this IExpressionSyntax expression, out int value)
        {
            switch (expression)
            {
                case JassDecimalLiteralExpressionSyntax decimalLiteralExpression:
                    value = decimalLiteralExpression.Value;
                    return true;

                case JassOctalLiteralExpressionSyntax octalLiteralExpression:
                    value = octalLiteralExpression.Value;
                    return true;

                case JassFourCCLiteralExpressionSyntax fourCCLiteralExpression:
                    value = fourCCLiteralExpression.Value;
                    return true;

                case JassUnaryExpressionSyntax unaryExpression:
                    return int.TryParse(unaryExpression.ToString(), out value);

                case JassHexadecimalLiteralExpressionSyntax hexLiteralExpression:
                    value = hexLiteralExpression.Value;
                    return true;

                default:
                    value = default;
                    return false;
            }
        }

        public static bool TryGetPlayerIdExpressionValue_New(this IExpressionSyntax expression, int maxPlayerSlots, out int value)
        {
            if (expression is JassVariableReferenceExpressionSyntax variableReferenceExpression)
            {
                if (string.Equals(variableReferenceExpression.IdentifierName.Name, "PLAYER_NEUTRAL_AGGRESSIVE", StringComparison.Ordinal))
                {
                    value = maxPlayerSlots;
                    return true;
                }
                else if (string.Equals(variableReferenceExpression.IdentifierName.Name, "PLAYER_NEUTRAL_PASSIVE", StringComparison.Ordinal))
                {
                    value = maxPlayerSlots + 3;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }
            else
            {
                return expression.TryGetIntegerExpressionValue_New(out value);
            }
        }
    }
}