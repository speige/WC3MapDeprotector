using War3Net.Build.Info;
using War3Net.CodeAnalysis.Decompilers;
using Jass2Lua;
using System.Collections.Immutable;

namespace WC3MapDeprotector
{
    public class FunctionDeclarationContext_Lua
    {
        public LuaASTNode FunctionDeclaration { get; set; }
        public bool Handled { get; set; }
    }

    public class ObjectManagerDecompilationContext_Lua
    {
        public LuaAST LuaAST { get; }
        public DecompileOptions Options { get; }
        public MapInfo MapInfo { get; }
        public HashSet<LuaASTNode> HandledStatements { get; }

        public ObjectManagerDecompilationContext_Lua(LuaAST luaAST, DecompileOptions options = null, MapInfo mapInfo = null)
        {
            LuaAST = luaAST;
            Options = options;
            MapInfo = mapInfo;
            HandledStatements = new HashSet<LuaASTNode>();

            FunctionDeclarations = luaAST.body.Where(x => x.type == LuaASTType.FunctionDeclaration).ToDictionary(x => x.identifier.name, x => new FunctionDeclarationContext_Lua() { FunctionDeclaration = x }).ToImmutableDictionary();
            MaxPlayerSlots = mapInfo != null && mapInfo.EditorVersion >= EditorVersion.v6060 ? 24 : 12;
        }

        public ImmutableDictionary<string, FunctionDeclarationContext_Lua> FunctionDeclarations { get; }
        public int MaxPlayerSlots { get; }
        protected readonly Dictionary<string, object> _variableNameToValueMapping = new();
        protected readonly List<object> _values = new();
        protected int _lastCreationNumber;

        public int GetNextCreationNumber()
        {
            return _lastCreationNumber++;
        }

        public string GetVariableName(object value)
        {
            return _variableNameToValueMapping.FirstOrDefault(x => x.Value == value).Key;
        }

        public void Add<T>(T value, string variableName = null) where T : class
        {
            if (variableName != null)
            {
                _variableNameToValueMapping[variableName] = value;
            }

            _values.Add(value);
        }

        public void Add_Struct<T>(T value, string variableName = null) where T : struct
        {
            Add(new Nullable_Class<T>(value), variableName);
        }

        public T Get<T>(string variableName) where T : class
        {
            if (variableName == null)
            {
                return default;
            }

            return _variableNameToValueMapping.GetValueOrDefault(variableName) as T;
        }

        public Nullable_Class<T> Get_Struct<T>(string variableName = null) where T : struct
        {
            return Get<Nullable_Class<T>>(variableName);
        }

        public T GetLastCreated<T>(LuaASTType? typeFilter = null) where T : class
        {
            var result = _values.OfType<T>();
            if (typeFilter != null)
            {
                result = result.Where(x => (x as LuaASTNode)?.type == typeFilter);
            }
            return result.LastOrDefault();
        }

        public Nullable_Class<T> GetLastCreated_Struct<T>() where T : struct
        {
            return GetLastCreated<Nullable_Class<T>>();
        }

        public IEnumerable<T> GetAll<T>(LuaASTType? typeFilter = null) where T : class
        {
            var result = _values.OfType<T>();
            if (typeFilter != null)
            {
                result = result.Where(x => (x as LuaASTNode)?.type == typeFilter);
            }
            return result;
        }

        public IEnumerable<Nullable_Class<T>> GetAll_Struct<T>() where T : struct
        {
            return GetAll<Nullable_Class<T>>();
        }

        protected const string PSEUDO_VARIABLE_PREFIX = "##PSEUDO_VARIABLE_PREFIX##";

        internal string CreatePseudoVariableName(string type, string name = "")
        {
            return PSEUDO_VARIABLE_PREFIX + "_" + type.ToString() + "_" + name;
        }
    }
}