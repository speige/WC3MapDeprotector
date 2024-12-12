// ------------------------------------------------------------------------------
// <copyright file="MapSoundsDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public static class ExpressionSyntaxExtensions_New
    {
        /*
        public static T GetValueOrDefault<T>(this IExpressionSyntax expression, T defaultValue = default(T))
        {

        }
        */

        public static decimal GetDecimalExpressionValueOrDefault(this IExpressionSyntax expression, decimal defaultValue = default(decimal))
        {
            if (expression.TryGetDecimalExpressionValue(out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public static bool TryGetDecimalExpressionValue(this IExpressionSyntax expression, out decimal value)
        {
            switch (expression)
            {
                case JassBooleanLiteralExpressionSyntax booleanLiteralExpression:
                    value = booleanLiteralExpression.Value ? 1 : 0;
                    return true;

                case JassDecimalLiteralExpressionSyntax decimalLiteralExpression:
                    value = decimalLiteralExpression.Value;
                    return true;

                case JassRealLiteralExpressionSyntax realLiteralExpression:
                    if (decimal.TryParse(realLiteralExpression.IntPart + "." + realLiteralExpression.FracPart, out value))
                    {
                        return true;
                    }
                    break;

                case JassOctalLiteralExpressionSyntax octalLiteralExpression:
                    value = octalLiteralExpression.Value;
                    return true;

                case JassFourCCLiteralExpressionSyntax fourCCLiteralExpression:
                    value = fourCCLiteralExpression.Value;
                    return true;

                case JassUnaryExpressionSyntax unaryExpression:
                    return decimal.TryParse(unaryExpression.ToString(), out value);

                case JassHexadecimalLiteralExpressionSyntax hexLiteralExpression:
                    value = hexLiteralExpression.Value;
                    return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetIntegerExpressionValue_New(this IExpressionSyntax expression, out int value)
        {
            switch (expression)
            {
                case JassDecimalLiteralExpressionSyntax decimalLiteralExpression:
                    value = decimalLiteralExpression.Value;
                    return true;

                case JassRealLiteralExpressionSyntax realLiteralExpression:
                    if (int.TryParse(realLiteralExpression.IntPart, out var intValue))
                    {
                        value = intValue;
                        return true;
                    }
                    break;

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
            }

            value = default;
            return false;
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