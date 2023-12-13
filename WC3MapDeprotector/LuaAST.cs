using ICSharpCode.Decompiler.TypeSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.Configuration;
using System.Runtime.Serialization;

namespace WC3MapDeprotector
{
    public class LuaAST
    {
        public string Type { get; set; }
        public LuaASTNode[] Body { get; set; }
        public LuaASTNode[] Comments { get; set; }
    }

    public class LuaASTNode
    {
        public bool IsLocal { get; set; }
        public LuaASTType Type { get; set; }

        public LuaASTNode Argument { get; set; }
        public LuaASTNode Base { get; set; }
        public LuaASTNode[] Body { get; set; }
        public LuaASTNode Condition { get; set; }
        public LuaASTNode End { get; set; }
        public LuaASTNode Expression { get; set; }
        public LuaASTNode Identifier { get; set; }
        public LuaASTNode Index { get; set; }
        public LuaASTNode Key { get; set; }
        public LuaASTNode Left { get; set; }
        public LuaASTNode Label { get; set; }
        public LuaASTNode Start { get; set; }
        public LuaASTNode Step { get; set; }
        public LuaASTNode Right { get; set; }
        public LuaASTNode Variable { get; set; }

        public LuaASTNode[] Clauses { get; set; }
        public LuaASTNode[] Fields { get; set; }
        public LuaASTNode[] Init { get; set; }
        public LuaASTNode[] Iterators { get; set; }
        public LuaASTNode[] Parameters { get; set; }
        public LuaASTNode[] Variables { get; set; }

        public string Indexer { get; set; }
        public string Name { get; set; }
        public string Operator { get; set; }
        public string Raw { get; set; }

        //ambiguous columns, used differently in JSON depending on parent Type
        [JsonIgnore]
        public LuaASTNode[] Arguments { get; set; }
        [JsonIgnore]
        public string Value { get; set; }
        [JsonIgnore]
        public LuaASTNode TableValue { get; set; }

        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (Type == LuaASTType.TableCallExpression)
            {
                if (_additionalData.TryGetValue("arguments", out var jsonToken))
                {
                    Argument = jsonToken.ToObject<LuaASTNode>();
                }
            }
            else
            {
                if (_additionalData.TryGetValue("arguments", out var jsonToken))
                {
                    Arguments = jsonToken.ToObject<LuaASTNode[]>();
                }
            }

            if (Type == LuaASTType.TableValue || Type == LuaASTType.TableKey || Type == LuaASTType.TableKeyString)
            {
                if (_additionalData.TryGetValue("value", out var jsonToken))
                {
                    TableValue = jsonToken.ToObject<LuaASTNode>();
                }
            }
            else
            {
                if (_additionalData.TryGetValue("value", out var jsonToken))
                {
                    Value = jsonToken.ToObject<string>();
                }
            }

            foreach (var child in AllNodes)
            {
                child.ParentNode = this;
            }
        }

        public LuaASTNode ParentNode { get; set; }

        public IEnumerable<LuaASTNode> AllNodes
        {
            get
            {
                //note: could write much shorter code since this is a repeating pattern, but performance will be better this way. Important due to frequent use by recursion & filtering algorithms.
                if (Argument != null)
                {
                    yield return Argument;
                }

                if (Base != null)
                {
                    yield return Base;
                }

                if (Condition != null)
                {
                    yield return Condition;
                }

                if (End != null)
                {
                    yield return End;
                }

                if (Expression != null)
                {
                    yield return Expression;
                }

                if (Identifier != null)
                {
                    yield return Identifier;
                }

                if (Index != null)
                {
                    yield return Index;
                }

                if (Key != null)
                {
                    yield return Key;
                }

                if (Left != null)
                {
                    yield return Left;
                }

                if (Label != null)
                {
                    yield return Label;
                }

                if (Start != null)
                {
                    yield return Start;
                }

                if (Step != null)
                {
                    yield return Step;
                }

                if (Right != null)
                {
                    yield return Right;
                }

                if (Variable != null)
                {
                    yield return Variable;
                }

                if (TableValue != null)
                {
                    yield return TableValue;
                }

                if (Body != null)
                {
                    foreach (var child in Body)
                    {
                        yield return child;
                    }
                }

                if (Arguments != null)
                {
                    foreach (var child in Arguments)
                    {
                        yield return child;
                    }
                }

                if (Clauses != null)
                {
                    foreach (var child in Clauses)
                    {
                        yield return child;
                    }
                }

                if (Fields != null)
                {
                    foreach (var child in Fields)
                    {
                        yield return child;
                    }
                }

                if (Init != null)
                {
                    foreach (var child in Init)
                    {
                        yield return child;
                    }
                }

                if (Iterators != null)
                {
                    foreach (var child in Iterators)
                    {
                        yield return child;
                    }
                }

                if (Parameters != null)
                {
                    foreach (var child in Parameters)
                    {
                        yield return child;
                    }
                }

                if (Variables != null)
                {
                    foreach (var child in Variables)
                    {
                        yield return child;
                    }
                }
            }
        }
    }

    public enum LuaASTType { AssignmentStatement, BinaryExpression, BooleanLiteral, BreakStatement, CallExpression, CallStatement, Comment, DoStatement, ElseClause, ElseifClause, ForGenericStatement, ForNumericStatement, FunctionDeclaration, GotoStatement, Identifier, IfClause, IfStatement, IndexExpression, LabelStatement, LocalStatement, LogicalExpression, MemberExpression, NilLiteral, NumericLiteral, RepeatStatement, ReturnStatement, StringCallExpression, StringLiteral, TableCallExpression, TableConstructorExpression, TableKey, TableKeyString, TableValue, UnaryExpression, VarargLiteral, WhileStatement };
}