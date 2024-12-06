// ------------------------------------------------------------------------------
// <copyright file="MapUnitsDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public class ObjectManagerDecompilationMetaData
    {
        public HashSet<IStatementSyntax> DecompiledFromStatements { get; init; } = new HashSet<IStatementSyntax>();
    }
}