using System.Reflection;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.Build.Info;
using War3Net.Build.Widget;
using War3Net.Build;
using War3Net.CodeAnalysis.Decompilers;
using Jass2Lua;

namespace WC3MapDeprotector
{
    public partial class ObjectManagerDecompiler_Lua
    {
        [AttributeUsage(AttributeTargets.Method)]
        protected class RegisterStatementParserAttribute : Attribute
        {
        }

        protected class StatementParserInput
        {
            public LuaASTNode Statement;
            public List<LuaASTNode> StatementChildren;
        }

        private static List<MethodInfo> _statementParsers;
        static ObjectManagerDecompiler_Lua()
        {
            _statementParsers = typeof(ObjectManagerDecompiler_Lua)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(RegisterStatementParserAttribute), false).Any()).ToList();
        }

        public ObjectManagerDecompilationContext_Lua Context { get; }

        public ObjectManagerDecompiler_Lua(LuaAST luaAST, DecompileOptions options = null, MapInfo mapInfo = null)
        {
            Context = new ObjectManagerDecompilationContext_Lua(luaAST, options, mapInfo);
        }

        protected void ProcessStatementParsers(LuaASTNode statement, IEnumerable<Action<StatementParserInput>> statementParsers)
        {
            var input = new StatementParserInput() { Statement = statement, StatementChildren = statement.GetChildren_RecursiveDepthFirst().ToList() };
            foreach (var parser in statementParsers)
            {
                try
                {
                    parser(input);
                    if (Context.HandledStatements.Contains(statement))
                    {
                        break;
                    }
                }
                catch
                {
                    //swallow exceptions
                }
            }
        }

        protected FunctionDeclarationContext_Lua? GetFunction(string functionName)
        {
            if (Context.FunctionDeclarations.TryGetValue(functionName, out var functionDeclaration))
            {
                return functionDeclaration;
            }

            return null;
        }

        public List<LuaASTNode> GetFunctionStatements_EnteringCalls(string startingFunctionName)
        {
            var functionDeclaration = Context.FunctionDeclarations.GetValueOrDefault(startingFunctionName);
            if (functionDeclaration == null || functionDeclaration.Handled)
            {
                return new List<LuaASTNode>();
            }

            functionDeclaration.Handled = true;
            var result = new List<LuaASTNode>();
            foreach (var child in functionDeclaration.FunctionDeclaration.GetChildren_RecursiveDepthFirst())
            {
                if (child.IsStatement())
                {
                    result.Add(child);
                }

                if (child.IsInvocation())
                {
                    if (string.Equals(child.GetInvocationName(), "ExecuteFunc", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.AddRange(GetFunctionStatements_EnteringCalls(child.arguments[0].GetValueOrDefault<string>()));
                    }
                    else
                    {
                        result.AddRange(GetFunctionStatements_EnteringCalls(child.GetInvocationName()));
                    }
                }
            }

            return result.ToList();
        }

        public Map DecompileObjectManagerData()
        {
            if (Context.MapInfo?.ScriptLanguage == ScriptLanguage.Jass)
            {
                throw new Exception("Transpile Jass to Lua first");
            }            

            var actions = _statementParsers.Select(parser => (Action<StatementParserInput>)((StatementParserInput input) => parser.Invoke(this, new[] { input }))).ToList();

            foreach (var function in Context.FunctionDeclarations)
            {
                function.Value.Handled = false;
            }
            var statements = GetFunctionStatements_EnteringCalls("config").Concat(GetFunctionStatements_EnteringCalls("main")).ToList();
            foreach (var statement in statements)
            {
                ProcessStatementParsers(statement, actions);
            }

            var map = new Map() { Info = Context.MapInfo };
            map.Cameras = new MapCameras(Context.Options.mapCamerasFormatVersion, Context.Options.mapCamerasUseNewFormat) { Cameras = Context.GetAll<Camera>().ToList() };
            map.Regions = new MapRegions(Context.Options.mapRegionsFormatVersion) { Regions = Context.GetAll<War3Net.Build.Environment.Region>().ToList() };
            map.Sounds = new MapSounds(Context.Options.mapSoundsFormatVersion) { Sounds = Context.GetAll<Sound>().ToList() };
            map.Units = new MapUnits(Context.Options.mapWidgetsFormatVersion, Context.Options.mapWidgetsSubVersion, Context.Options.mapWidgetsUseNewFormat) { Units = Context.GetAll<UnitData>().ToList() };
            map.Doodads = new MapDoodads(Context.Options.mapWidgetsFormatVersion, Context.Options.mapWidgetsSubVersion, Context.Options.mapWidgetsUseNewFormat) { Doodads = Context.GetAll<DoodadData>().ToList(), SpecialDoodads = Context.GetAll<SpecialDoodadData>().ToList(), SpecialDoodadVersion = Context.Options.specialDoodadVersion };
            return map;
        }
    }
}