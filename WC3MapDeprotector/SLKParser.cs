using War3Net.Build;
using War3Net.IO.Slk;

namespace WC3MapDeprotector
{
    public enum SLKType
    {
        Ability,
        Buff,
        Destructable,
        Doodad,
        Item,
        Unit,
        Upgrade
    }

    public class SLKObject
    {
        public Dictionary<string, object> Data { get; protected set; } = new Dictionary<string, object>();

        public SLKType SLKType
        {
            get
            {
                var slkFilesFoundIn = Data.Keys.Where(x => x.EndsWith(".slk", StringComparison.InvariantCultureIgnoreCase)).ToList();
                //NOTE: order is important to avoid misclassifying AbilityBuffData as AbilityData
                if (slkFilesFoundIn.Any(x => x.Contains("doodad", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Doodad;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("destructable", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Destructable;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("item", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Item;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("upgrade", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Upgrade;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("buff", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Buff;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("unit", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Unit;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("ability", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Ability;
                }

                throw new NotImplementedException();
            }
        }
    }

    public class SLKParser
    {
        public Dictionary<string, SLKObject> ParseSLKObjectsFromFiles(List<string> fileNames)
        {
            var result = new Dictionary<string, SLKObject>();
            foreach (var fileName in fileNames)
            {
                try
                {
                    var slkTable = new SylkParser().Parse(File.OpenRead(fileName));
                    var columnNames = new string[slkTable.Width];
                    for (var columnIdx = 0; columnIdx < slkTable.Width; columnIdx++)
                    {
                        columnNames[columnIdx] = slkTable[columnIdx, 0]?.ToString() ?? $"{Path.GetFileName(fileName)}_{columnIdx}";
                    }

                    var objectIdColumnName = columnNames[0];
                    for (var row = 1; row < slkTable.Rows; row++)
                    {
                        try
                        {
                            var objectId = slkTable[0, row]?.ToString();
                            if (!result.TryGetValue(objectId, out var slkObject))
                            {
                                slkObject = new SLKObject();
                                result[objectId] = slkObject;
                            }

                            slkObject.Data[Path.GetFileName(fileName)] = true;

                            for (var columnIdx = 0; columnIdx < slkTable.Width; columnIdx++)
                            {
                                try
                                {
                                    var value = slkTable[columnIdx, row];
                                    if (value != null)
                                    {
                                        slkObject.Data[columnNames[columnIdx]] = value;
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return result;
        }

        protected Dictionary<SLKType, Dictionary<string, string>> PropertyIdRawCodePerSLKColumn = new Dictionary<SLKType, Dictionary<string, string>>()
        {
            {
                SLKType.Ability, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Animnames", "aani" },
                    { "Area1", "aare" },
                    { "Areaeffectart", "aaea" },
                    { "Art", "aart" },
                    { "BuffID1", "abuf" },
                    { "Buttonpos", "abpx" },
                    { "Buttonpos", "abpy" },
                    { "Cast1", "acas" },
                    { "CasterArt", "acat" },
                    { "Casterattach", "acap" },
                    { "Casterattach1", "aca1" },
                    { "Casterattachcount", "acac" },
                    { "Cool1", "acdn" },
                    { "Cost1", "amcs" },
                    { "DataA1", "mls1" },
                    { "Dur1", "adur" },
                    { "EditorSuffix", "ansf" },
                    { "EfctID1", "aeff" },
                    { "EffectArt", "aeat" },
                    { "Effectsound", "aefs" },
                    { "Effectsoundlooped", "aefl" },
                    { "HeroDur1", "ahdu" },
                    { "Hotkey", "ahky" },
                    { "LightningEffect", "alig" },
                    { "MissileHoming", "amho" },
                    { "Missilearc", "amac" },
                    { "Missileart", "amat" },
                    { "Missilespeed", "amsp" },
                    { "Name", "anam" },
                    { "Order", "aord" },
                    { "Orderoff", "aorf" },
                    { "Orderon", "aoro" },
                    { "Requires", "areq" },
                    { "Requiresamount", "arqa" },
                    { "Rng1", "aran" },
                    { "SpecialArt", "asat" },
                    { "Specialattach", "aspt" },
                    { "TargetArt", "atat" },
                    { "Targetattach", "ata0" },
                    { "Targetattach1", "ata1" },
                    { "Targetattach2", "ata2" },
                    { "Targetattach3", "ata3" },
                    { "Targetattach4", "ata4" },
                    { "Targetattach5", "ata5" },
                    { "Targetattachcount", "atac" },
                    { "Tip", "atp1" },
                    { "Ubertip", "aub1" },
                    { "UnButtonpos", "aubx" },
                    { "UnButtonpos", "auby" },
                    { "Unart", "auar" },
                    { "Unhotkey", "auhk" },
                    { "UnitSkinID", "ausk" },
                    { "Unorder", "aoru" },
                    { "Untip", "aut1" },
                    { "Unubertip", "auu1" },
                    { "checkDep", "achd" },
                    { "hero", "aher" },
                    { "item", "aite" },
                    { "levels", "alev" },
                    { "priority", "apri" },
                    { "race", "arac" },
                    { "targs1", "atar" }
                }
            },
            {
                SLKType.Buff, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Buffart", "fart" },
                    { "Bufftip", "ftip" },
                    { "Buffubertip", "fube" },
                    { "EditorName", "fnam" },
                    { "EditorSuffix", "fnsf" },
                    { "EffectArt", "feat" },
                    { "Effectattach", "feft" },
                    { "Effectsound", "fefs" },
                    { "Effectsoundlooped", "fefl" },
                    { "LightningEffect", "flig" },
                    { "MissileHoming", "fmho" },
                    { "Missilearc", "fmac" },
                    { "Missileart", "fmat" },
                    { "Missilespeed", "fmsp" },
                    { "SpecialArt", "fsat" },
                    { "Specialattach", "fspt" },
                    { "Spelldetail", "fspd" },
                    { "TargetArt", "ftat" },
                    { "Targetattach", "fta0" },
                    { "Targetattach1", "fta1" },
                    { "Targetattach2", "fta2" },
                    { "Targetattach3", "fta3" },
                    { "Targetattach4", "fta4" },
                    { "Targetattach5", "fta5" },
                    { "Targetattachcount", "ftac" },
                    { "isEffect", "feff" },
                    { "race", "frac" }
                }
            },
            {
                SLKType.Destructable, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "EditorSuffix", "bsuf" },
                    { "HP", "bhps" },
                    { "MMBlue", "bmmb" },
                    { "MMGreen", "bmmg" },
                    { "MMRed", "bmmr" },
                    { "Name", "bnam" },
                    { "UserList", "busr" },
                    { "armor", "barm" },
                    { "buildTime", "bbut" },
                    { "canPlaceDead", "bcpd" },
                    { "canPlaceRandScale", "bcpr" },
                    { "category", "bcat" },
                    { "cliffHeight", "bclh" },
                    { "colorB", "bvcb" },
                    { "colorG", "bvcg" },
                    { "colorR", "bvcr" },
                    { "deathSnd", "bdsn" },
                    { "fatLOS", "bflo" },
                    { "file", "bfil" },
                    { "fixedRot", "bfxr" },
                    { "flyH", "bflh" },
                    { "fogRadius", "bfra" },
                    { "fogVis", "bfvi" },
                    { "goldRep", "breg" },
                    { "lightweight", "blit" },
                    { "loopSound", "bsnd" },
                    { "lumberRep", "brel" },
                    { "maxPitch", "bmap" },
                    { "maxRoll", "bmar" },
                    { "maxScale", "bmas" },
                    { "minScale", "bmis" },
                    { "numVar", "bvar" },
                    { "occH", "boch" },
                    { "onCliffs", "bonc" },
                    { "onWater", "bonw" },
                    { "pathTex", "bptx" },
                    { "pathTexDeath", "bptd" },
                    { "portraitmodel", "bgpm" },
                    { "radius", "brad" },
                    { "repairTime", "bret" },
                    { "selSize", "bsel" },
                    { "selcircsize", "bgsc" },
                    { "selectable", "bgse" },
                    { "shadow", "bshd" },
                    { "showInMM", "bsmm" },
                    { "targType", "btar" },
                    { "texFile", "btxf" },
                    { "texID", "btxi" },
                    { "tilesetSpecific", "btsp" },
                    { "tilesets", "btil" },
                    { "useClickHelper", "buch" },
                    { "useMMColor", "bumm" },
                    { "walkable", "bwal" }
                }
            },
            {
                SLKType.Doodad, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "MMBlue", "dmmb" },
                    { "MMGreen", "dmmg" },
                    { "MMRed", "dmmr" },
                    { "Name", "dnam" },
                    { "UserList", "dusr" },
                    { "animInFog", "danf" },
                    { "canPlaceRandScale", "dcpr" },
                    { "category", "dcat" },
                    { "defScale", "ddes" },
                    { "file", "dfil" },
                    { "fixedRot", "dfxr" },
                    { "floats", "dflt" },
                    { "ignoreModelClick", "dimc" },
                    { "maxPitch", "dmap" },
                    { "maxRoll", "dmar" },
                    { "maxScale", "dmas" },
                    { "minScale", "dmis" },
                    { "numVar", "dvar" },
                    { "onCliffs", "donc" },
                    { "onWater", "donw" },
                    { "pathTex", "dptx" },
                    { "selSize", "dsel" },
                    { "shadow", "dshd" },
                    { "showInFog", "dshf" },
                    { "showInMM", "dsmm" },
                    { "soundLoop", "dsnd" },
                    { "tilesetSpecific", "dtsp" },
                    { "tilesets", "dtil" },
                    { "useClickHelper", "duch" },
                    { "useMMColor", "dumc" },
                    { "vertB", "dvb1" },
                    { "vertG", "dvg1" },
                    { "vertR", "dvr1" },
                    { "visRadius", "dvis" },
                    { "walkable", "dwlk" }
                }
            },
            {
                SLKType.Item, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Art", "iico" },
                    { "Buttonpos", "ubpx" },
                    { "Buttonpos", "ubpy" },
                    { "Description", "ides" },
                    { "HP", "ihtp" },
                    { "Hotkey", "uhot" },
                    { "Level", "ilev" },
                    { "Name", "unam" },
                    { "Requires", "ureq" },
                    { "Requiresamount", "urqa" },
                    { "Tip", "utip" },
                    { "Ubertip", "utub" },
                    { "abilList", "iabi" },
                    { "armor", "iarm" },
                    { "class", "icla" },
                    { "colorB", "iclb" },
                    { "colorG", "iclg" },
                    { "colorR", "iclr" },
                    { "cooldownID", "icid" },
                    { "drop", "idrp" },
                    { "droppable", "idro" },
                    { "file", "ifil" },
                    { "goldcost", "igol" },
                    { "ignoreCD", "iicd" },
                    { "lumbercost", "ilum" },
                    { "morph", "imor" },
                    { "oldLevel", "ilvo" },
                    { "pawnable", "ipaw" },
                    { "perishable", "iper" },
                    { "pickRandom", "iprn" },
                    { "powerup", "ipow" },
                    { "prio", "ipri" },
                    { "scale", "isca" },
                    { "selSize", "issc" },
                    { "sellable", "isel" },
                    { "stackMax", "ista" },
                    { "stockInitial", "isit" },
                    { "stockMax", "isto" },
                    { "stockRegen", "istr" },
                    { "stockStart", "isst" },
                    { "usable", "iusa" },
                    { "uses", "iuse" }
                }
            },
            {
                SLKType.Unit, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Art", "uico" },
                    { "Attachmentanimprops", "uaap" },
                    { "Attachmentlinkprops", "ualp" },
                    { "Boneprops", "ubpr" },
                    { "Builds", "ubui" },
                    { "Buttonpos", "ubpx" },
                    { "Buttonpos", "ubpy" },
                    { "Casterupgradeart", "ucua" },
                    { "Casterupgradename", "ucun" },
                    { "Casterupgradetip", "ucut" },
                    { "DependencyOr", "udep" },
                    { "Description", "ides" },
                    { "EditorSuffix", "unsf" },
                    { "Farea1", "ua1f" },
                    { "Farea2", "ua2f" },
                    { "HP", "uhpm" },
                    { "Harea1", "ua1h" },
                    { "Harea2", "ua2h" },
                    { "Hfact1", "uhd1" },
                    { "Hfact2", "uhd2" },
                    { "Hotkey", "uhot" },
                    { "LoopingSoundFadeIn", "ulfi" },
                    { "LoopingSoundFadeOut", "ulfo" },
                    { "MissileHoming", "umh1" },
                    { "MissileHoming", "umh2" },
                    { "Missilearc", "uma1" },
                    { "Missilearc", "uma2" },
                    { "Missileart", "ua1m" },
                    { "Missileart", "ua2m" },
                    { "Missilespeed", "ua1z" },
                    { "Missilespeed", "ua2z" },
                    { "MovementSoundLabel", "umsl" },
                    { "Name", "unam" },
                    { "Qarea1", "ua1q" },
                    { "Qarea2", "ua2q" },
                    { "Qfact1", "uqd1" },
                    { "Qfact2", "uqd2" },
                    { "RandomSoundLabel", "ursl" },
                    { "Requires", "ureq" },
                    { "Requiresamount", "urqa" },
                    { "RngBuff1", "urb1" },
                    { "RngBuff2", "urb2" },
                    { "ScoreScreenIcon", "ussi" },
                    { "Sellitems", "usei" },
                    { "Sellunits", "useu" },
                    { "Specialart", "uspa" },
                    { "Targetart", "utaa" },
                    { "Tip", "utip" },
                    { "Ubertip", "utub" },
                    { "abilList", "uabi" },
                    { "abilSkinList", "uabs" },
                    { "acquire", "uacq" },
                    { "animProps", "uani" },
                    { "armor", "uarm" },
                    { "atkType1", "ua1t" },
                    { "atkType2", "ua2t" },
                    { "auto", "udaa" },
                    { "backSw1", "ubs1" },
                    { "backSw2", "ubs2" },
                    { "bldtm", "ubld" },
                    { "blend", "uble" },
                    { "blue", "uclb" },
                    { "bountydice", "ubdi" },
                    { "bountyplus", "ubba" },
                    { "bountysides", "ubsi" },
                    { "buffRadius", "uabr" },
                    { "buffType", "uabt" },
                    { "buildingShadow", "ushb" },
                    { "campaign", "ucam" },
                    { "canFlee", "ufle" },
                    { "canSleep", "usle" },
                    { "cargoSize", "ucar" },
                    { "castbsw", "ucbs" },
                    { "castpt", "ucpt" },
                    { "collision", "ucol" },
                    { "cool1", "ua1c" },
                    { "cool2", "ua2c" },
                    { "customTeamColor", "utcc" },
                    { "damageLoss1", "udl1" },
                    { "damageLoss2", "udl2" },
                    { "death", "udtm" },
                    { "deathType", "udea" },
                    { "def", "udef" },
                    { "defType", "udty" },
                    { "defUp", "udup" },
                    { "dice1", "ua1d" },
                    { "dice2", "ua2d" },
                    { "dmgUp1", "udu1" },
                    { "dmgUp2", "udu2" },
                    { "dmgplus1", "ua1b" },
                    { "dmgplus2", "ua2b" },
                    { "dmgpt1", "udp1" },
                    { "dmgpt2", "udp2" },
                    { "dropItems", "udro" },
                    { "elevPts", "uept" },
                    { "elevRad", "uerd" },
                    { "fatLOS", "ulos" },
                    { "file", "umdl" },
                    { "fileVerFlags", "uver" },
                    { "fmade", "ufma" },
                    { "fogRad", "ufrd" },
                    { "formation", "ufor" },
                    { "fused", "ufoo" },
                    { "goldRep", "ugor" },
                    { "goldcost", "ugol" },
                    { "green", "uclg" },
                    { "heroAbilSkinList", "uhas" },
                    { "hideOnMinimap", "uhom" },
                    { "hostilePal", "uhos" },
                    { "impactSwimZ", "uisz" },
                    { "impactZ", "uimz" },
                    { "inEditor", "uine" },
                    { "isbldg", "ubdg" },
                    { "launchSwimZ", "ulsz" },
                    { "launchX", "ulpx" },
                    { "launchY", "ulpy" },
                    { "launchZ", "ulpz" },
                    { "level", "ulev" },
                    { "lumberRep", "ulur" },
                    { "lumberbountydice", "ulbd" },
                    { "lumberbountyplus", "ulba" },
                    { "lumberbountysides", "ulbs" },
                    { "lumbercost", "ulum" },
                    { "mana0", "umpi" },
                    { "manaN", "umpm" },
                    { "maxPitch", "umxp" },
                    { "maxRoll", "umxr" },
                    { "maxSpd", "umas" },
                    { "minRange", "uamn" },
                    { "minSpd", "umis" },
                    { "modelScale", "usca" },
                    { "moveFloor", "umvf" },
                    { "moveHeight", "umvh" },
                    { "movetp", "umvt" },
                    { "nsight", "usin" },
                    { "occH", "uocc" },
                    { "orientInterp", "uori" },
                    { "points", "upoi" },
                    { "prio", "upri" },
                    { "propWin", "uprw" },
                    { "race", "urac" },
                    { "rangeN1", "ua1r" },
                    { "rangeN2", "ua2r" },
                    { "red", "uclr" },
                    { "regenHP", "uhpr" },
                    { "regenMana", "umpr" },
                    { "regenType", "uhrt" },
                    { "reptm", "urtm" },
                    { "repulse", "urpo" },
                    { "repulseGroup", "urpg" },
                    { "repulseParam", "urpp" },
                    { "repulsePrio", "urpr" },
                    { "run", "urun" },
                    { "scale", "ussc" },
                    { "scaleBull", "uscb" },
                    { "selCircOnWater", "usew" },
                    { "selZ", "uslz" },
                    { "shadowH", "ushh" },
                    { "shadowOnWater", "ushr" },
                    { "shadowW", "ushw" },
                    { "shadowX", "ushx" },
                    { "shadowY", "ushy" },
                    { "showUI1", "uwu1" },
                    { "showUI4", "uwu2" },
                    { "sides1", "ua1s" },
                    { "sides2", "ua2s" },
                    { "sight", "usid" },
                    { "spd", "umvs" },
                    { "special", "uspe" },
                    { "spillDist1", "usd1" },
                    { "spillDist2", "usd2" },
                    { "spillRadius1", "usr1" },
                    { "spillRadius2", "usr2" },
                    { "splashTargs1", "ua1p" },
                    { "splashTargs2", "ua2p" },
                    { "stockInitial", "usit" },
                    { "stockMax", "usma" },
                    { "stockRegen", "usrg" },
                    { "stockStart", "usst" },
                    { "targCount1", "utc1" },
                    { "targCount2", "utc2" },
                    { "targType", "utar" },
                    { "targs1", "ua1g" },
                    { "targs2", "ua2g" },
                    { "teamColor", "utco" },
                    { "tilesetSpecific", "utss" },
                    { "tilesets", "util" },
                    { "turnRate", "umvr" },
                    { "type", "utyp" },
                    { "unitShadow", "ushu" },
                    { "unitSound", "usnd" },
                    { "upgrades", "upgr" },
                    { "useClickHelper", "uuch" },
                    { "walk", "uwal" },
                    { "weapTp1", "ua1w" },
                    { "weapTp2", "ua2w" },
                    { "weapType1", "ucs1" },
                    { "weapType2", "ucs2" },
                    { "weapsOn", "uaen" }
                }
            },
            {
                SLKType.Upgrade, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Art", "gar1" },
                    { "Buttonpos", "gbpx" },
                    { "Buttonpos", "gbpy" },
                    { "EditorSuffix", "gnsf" },
                    { "Hotkey", "ghk1" },
                    { "Name", "gnam" },
                    { "Requires", "greq" },
                    { "Requiresamount", "grqc" },
                    { "Tip", "gtp1" },
                    { "Ubertip", "gub1" },
                    { "base1", "gba1" },
                    { "class", "gcls" },
                    { "effect1", "gef1" },
                    { "effect2", "gef2" },
                    { "effect3", "gef3" },
                    { "effect4", "gef4" },
                    { "global", "glob" },
                    { "goldbase", "gglb" },
                    { "goldmod", "gglm" },
                    { "inherit", "ginh" },
                    { "lumberbase", "glmb" },
                    { "lumbermod", "glmm" },
                    { "maxlevel", "glvl" },
                    { "mod1", "gmo1" },
                    { "race", "grac" },
                    { "timebase", "gtib" },
                    { "timemod", "gtim" }
                }
            }
        };

        public string ConvertToPropertyIdRawCode(SLKType slkType, string columnName)
        {
            if (PropertyIdRawCodePerSLKColumn.TryGetValue(slkType, out var rawCodePerSLKColumn) && rawCodePerSLKColumn.TryGetValue((columnName ?? "").Trim(), out var result))
            {
                return result;
            }

            throw new NotImplementedException();
        }
    }
}