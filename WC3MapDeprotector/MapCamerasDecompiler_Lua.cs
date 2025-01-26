using War3Net.Build;
using Region = War3Net.Build.Environment.Region;

namespace WC3MapDeprotector
{
    public partial class ObjectManagerDecompiler_Lua
    {
        [RegisterStatementParser]
        protected void ParseRegionCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            if (variableAssignment == null || !variableAssignment.StartsWith("gg_rct_"))
            {
                return;
            }

            var region = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "Rect")
            .SafeMapFirst(x =>
            {
                return new Region
                {
                    Left = x.arguments[0].GetValueOrDefault<float>(),
                    Bottom = x.arguments[1].GetValueOrDefault<float>(),
                    Right = x.arguments[2].GetValueOrDefault<float>(),
                    Top = x.arguments[3].GetValueOrDefault<float>(),
                    Color = Color.FromArgb(unchecked((int)0xFF8080FF)),
                    CreationNumber = Context.GetNextCreationNumber()
                };
            });

            if (region != null)
            {
                region.Name = variableAssignment["gg_rct_".Length..].Replace('_', ' ');
                Context.Add(region, variableAssignment);
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseAddWeatherEffect(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "AddWeatherEffect")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0].name,
                    WeatherType = (WeatherType)x.arguments[1].GetFourCC(),
                };
            });

            if (match != null)
            {
                var region = Context.Get<Region>(match.VariableName) ?? Context.GetLastCreated<Region>();
                if (region != null)
                {
                    region.WeatherType = match.WeatherType;
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseSetSoundPosition(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "SetSoundPosition")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0].name,
                    x = x.arguments[1].GetValueOrDefault<float>(),
                    y = x.arguments[2].GetValueOrDefault<float>()
                };
            });

            if (match != null)
            {
                var region = Context.Get<Region>(match.VariableName) ?? Context.GetLastCreated<Region>();
                if (region != null)
                {
                    if (region.CenterX == match.x && region.CenterY == match.y)
                    {
                        region.AmbientSound = match.VariableName;
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseRegisterStackedSound(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "RegisterStackedSound")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0].name,
                    Width = x.arguments[2].GetValueOrDefault<float>(),
                    Height = x.arguments[3].GetValueOrDefault<float>()
                };
            });

            if (match != null)
            {
                var region = Context.Get<Region>(match.VariableName) ?? Context.GetLastCreated<Region>();
                if (region != null)
                {
                    if (region.Width == match.Width && region.Height == match.Height)
                    {
                        region.AmbientSound = match.VariableName;
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }
    }
}