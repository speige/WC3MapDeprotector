using Jass2Lua.Ast;
using System.Globalization;
using War3Net.CodeAnalysis.Decompilers;
using War3Net.CodeAnalysis.Jass.Extensions;

namespace WC3MapDeprotector
{
    public static class LuaASTNodeExtensions
    {
        private static LuaASTType[] _statementTypes = Enum.GetValues<LuaASTType>().Where(x => Enum.GetName(x).EndsWith("Statement")).ToArray();
        public static bool IsStatement(this LuaASTNode node)
        {
            return _statementTypes.Contains(node.type);
        }

        private static LuaASTType[] _invocationTypes = Enum.GetValues<LuaASTType>().Where(x => Enum.GetName(x).Contains("CallExpression")).ToArray();
        public static bool IsInvocation(this LuaASTNode node)
        {
            return _invocationTypes.Contains(node.type);
        }

        public static string GetInvocationName(this LuaASTNode node)
        {
            return node.IsInvocation() ? node.@base?.name ?? node.expression.@base.name : null;
        }

        /*
        private static LuaASTType[] _expressionTypes = Enum.GetValues<LuaASTType>().Where(x => Enum.GetName(x).Contains("Expression")).ToArray();
        public static bool IsExpression(this LuaASTNode node)
        {
            return _expressionTypes.Contains(node.type);
        }
        */

        private static LuaASTType[] _literalTypes = Enum.GetValues<LuaASTType>().Where(x => Enum.GetName(x).Contains("Literal") && x != LuaASTType.VarargLiteral).ToArray();
        public static bool IsLiteral(this LuaASTNode node)
        {
            return _literalTypes.Contains(node.type);
        }

        public static bool IsVariableAssignment(this LuaASTNode node)
        {
            return node.type == LuaASTType.AssignmentStatement || node.type == LuaASTType.LocalStatement;
        }

        public static bool IsIdentifier(this LuaASTNode node)
        {
            return node.type == LuaASTType.Identifier;
        }

        public static IEnumerable<LuaASTNode> GetChildren_RecursiveDepthFirst(this LuaASTNode node)
        {
            var stack = new Stack<LuaASTNode>();
            stack.Push(node);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                var children = current.AllNodes.ToArray();
                if (children != null)
                {
                    foreach (var child in children.Reverse())
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        public static int? GetFourCC(this LuaASTNode node)
        {
            var fourCC = node.GetChildren_RecursiveDepthFirst().FirstOrDefault(x => x.IsInvocation() && x.GetInvocationName() == "FourCC");
            if (fourCC == null)
            {
                return fourCC.GetValueOrDefault<int>().InvertEndianness();
            }

            return new FourCC(fourCC.arguments[0].GetValueOrDefault<string>().Substring(1, 4)).ToObjectID();
        }

        public static T GetValueOrDefault<T>(this LuaASTNode node, T defaultValue = default)
        {
            if (node.TryGetValue<T>(out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public static bool TryGetValue<T>(this LuaASTNode node, out T value)
        {
            value = default;
            if (node == null)
            {
                return false;
            }

            var stringValue = GetStringValue(node);

            if (typeof(T) == typeof(string))
            {
                value = (T)(object)stringValue;
                return true;
            }

            var numericString = stringValue.Replace("(", "").Replace(")", "").Trim();
            if (decimal.TryParse(numericString, out var decimalValue))
            {
                value = SafeConvertDecimalTo<T>(decimalValue);
                return true;
            }
            
            if (numericString.StartsWith("0x") && long.TryParse(stringValue.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var longValue))
            {
                value = SafeConvertDecimalTo<T>(longValue);
                return true;
            }

            return false;
        }

        private static string GetStringValue(this LuaASTNode node)
        {
            if (node == null)
            {
                return null;
            }

            string result;
            if (node.type == LuaASTType.StringLiteral)
            {
                result = node.raw;
                if (result.StartsWith('"') && result.EndsWith('"') && result.Length >= 2)
                {
                    result = result.Substring(1, result.Length - 2);
                }
            }
            else
            {
                result = LuaRenderer.Render(node);
            }
            
            if (node.type == LuaASTType.BooleanLiteral)
            {
                if (result.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                {
                    result = "1";
                }
                else if (result.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                {
                    result = "0";
                }
            }

            return result;
        }

        private static T SafeConvertDecimalTo<T>(decimal value)
        {
            if (Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                var nonNullableType = Nullable.GetUnderlyingType(typeof(T));
                var method = typeof(LuaASTNodeExtensions).GetMethod(nameof(SafeConvertDecimalTo),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var genericMethod = method.MakeGenericMethod(nonNullableType);
                var result = genericMethod.Invoke(null, new object[] { value });
                return (T)result;
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)(int)Math.Clamp(value, int.MinValue, int.MaxValue);
            }
            else if (typeof(T) == typeof(uint))
            {
                return (T)(object)(uint)Math.Clamp(value, uint.MinValue, uint.MaxValue);
            }
            else if (typeof(T) == typeof(byte))
            {
                return (T)(object)(byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
            }
            else if (typeof(T) == typeof(sbyte))
            {
                return (T)(object)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
            }
            else if (typeof(T) == typeof(short))
            {
                return (T)(object)(short)Math.Clamp(value, short.MinValue, short.MaxValue);
            }
            else if (typeof(T) == typeof(ushort))
            {
                return (T)(object)(ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
            }
            else if (typeof(T) == typeof(long))
            {
                return (T)(object)(long)Math.Clamp(value, long.MinValue, long.MaxValue);
            }
            else if (typeof(T) == typeof(ulong))
            {
                return (T)(object)(ulong)Math.Clamp(value, ulong.MinValue, ulong.MaxValue);
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)(value != 0);
            }
            else if (typeof(T) == typeof(decimal))
            {
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)(float)value;
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)(double)value;
            }

            return default;
        }

        public static T2 OneOf<T1, T2>(this IEnumerable<T1> tokens, params Func<IEnumerable<T1>, T2>[] mapping) where T2 : class
        {
            foreach (var map in mapping)
            {
                try
                {
                    var result = map(tokens);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch
                {
                    //swallow exceptions
                }
            }

            return default;
        }

        public static Nullable_Class<T2> OneOf_Struct<T1, T2>(this IEnumerable<T1> tokens, params Func<IEnumerable<T1>, Nullable_Class<T2>>[] mapping) where T2 : struct
        {
            return OneOf(tokens, mapping);
        }

        public static T2 SafeMapFirst<T1, T2>(this IEnumerable<T1> tokens, Func<T1, T2> mapping) where T2 : class
        {
            return tokens.Select(x => {
                try
                {
                    return mapping(x);
                }
                catch
                {
                    //swallow exceptions
                }

                return default;
            }).Where(x => x != null).FirstOrDefault();
        }

        public static Nullable_Class<T2> SafeMapFirst_Struct<T1, T2>(this IEnumerable<T1> tokens, Func<T1, Nullable_Class<T2>> mapping) where T2 : struct
        {
            return SafeMapFirst(tokens, mapping);
        }
    }
}