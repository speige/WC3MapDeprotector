// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.Build.Widget;

namespace War3Net.CodeAnalysis.Decompilers
{
    public class DecompilationContext_Pidgin
    {
        public int CreationNumber;
        public readonly Dictionary<string, UnitData> VariableNameToUnitMapping = new();
        public readonly List<UnitData> AllUnits = new();
        public readonly Dictionary<string, Sound> VariableNameToSoundMapping = new();
        public readonly List<Sound> AllSounds = new();
        public readonly Dictionary<string, Camera> VariableNameToCameraMapping = new();
        public readonly List<Camera> AllCameras = new();
        public readonly Dictionary<string, Region> VariableNameToRegionMapping = new();
        public readonly List<Region> AllRegions = new();
    }
}