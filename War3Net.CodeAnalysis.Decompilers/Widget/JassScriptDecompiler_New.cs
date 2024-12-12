// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using War3Net.Build.Environment;
using War3Net.Build;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.Build.Audio;
using War3Net.Build.Widget;
using System;
using System.Reflection;
using War3Net.Build.Info;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_New
    {
        [AttributeUsage(AttributeTargets.Method)]
        private class RegisterStatementParserAttribute : Attribute
        {
        }

        private class StatementParserInput
        {
            public IStatementSyntax Statement;
            public List<IJassSyntaxToken> StatementChildren;
        }

        private static List<MethodInfo> _statementParsers;
        static JassScriptDecompiler_New()
        {
            _statementParsers = typeof(JassScriptDecompiler_New)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(RegisterStatementParserAttribute), false).Any()).ToList();
        }

        private DecompilationContext_New _context;

        private JassScriptDecompiler_New(JassCompilationUnitSyntax compilationUnit, DecompileOptions options = default, MapInfo mapInfo = null)
        {
            _context = new DecompilationContext_New(mapInfo) { CompilationUnit = compilationUnit, Options = options ?? new() };
        }

        public static Map DecompileMap(JassCompilationUnitSyntax compilationUnit, DecompileOptions options = default, MapInfo mapInfo = null)
        {
            //todo: add other decompilers (triggers/etc & add them to "options")

            var decompiler = new JassScriptDecompiler_New(compilationUnit, options);

            var functions = new[] { decompiler._context.FunctionDeclarations.GetValueOrDefault("config")?.FunctionDeclaration, decompiler._context.FunctionDeclarations.GetValueOrDefault("main")?.FunctionDeclaration };
            var statements = functions.Where(x => x != null).SelectMany(x => x.GetChildren_RecursiveDepthFirst().OfType<IStatementSyntax>()).ToList(); //todo: move ExtractStatements_IncludingEnteringFirstExecutionOfEachFunctionCall to War3Net & use "Handled" from context to only allow 1x each (move "handled' from FunctionDeclarationContext to a local HashSet<JassFunctionDeclarationSyntax>)
            foreach (var statement in statements)
            {
                var input = new StatementParserInput() { Statement = statement, StatementChildren = statement.GetChildren_RecursiveDepthFirst().ToList() };
                foreach (var parser in _statementParsers)
                {
                    try
                    {
                        parser.Invoke(decompiler, new[] { input });
                        if (decompiler._context.HandledStatements.Contains(statement))
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

            var map = new Map() { Info = mapInfo };
            map.Cameras = new MapCameras(decompiler._context.Options.mapCamerasFormatVersion, decompiler._context.Options.mapCamerasUseNewFormat) { Cameras = decompiler._context.GetAll<Camera>().ToList() };
            map.Regions = new MapRegions(decompiler._context.Options.mapRegionsFormatVersion) { Regions = decompiler._context.GetAll<Region>().ToList() };
            map.Sounds = new MapSounds(decompiler._context.Options.mapSoundsFormatVersion) { Sounds = decompiler._context.GetAll<Sound>().ToList() };
            map.Units = new MapUnits(decompiler._context.Options.mapWidgetsFormatVersion, decompiler._context.Options.mapWidgetsSubVersion, decompiler._context.Options.mapWidgetsUseNewFormat) { Units = decompiler._context.GetAll<UnitData>().ToList() };
            return map;
        }
    }
}