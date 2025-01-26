using War3Net.Build.Environment;

namespace WC3MapDeprotector
{
    public partial class ObjectManagerDecompiler_Lua
    {
        [RegisterStatementParser]
        protected void ParseCameraCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            if (variableAssignment == null || !variableAssignment.StartsWith("gg_cam_"))
            {
                return;
            }

            var camera = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "CreateCameraSetup")
            .SafeMapFirst(x =>
            {
                return new Camera
                {
                    NearClippingPlane = Context.Options.mapCamerasUseNewFormat ? default : 100f,
                };
            });

            if (camera != null)
            {
                camera.Name = variableAssignment["gg_cam_".Length..].Replace('_', ' ');
                Context.Add(camera, variableAssignment);
                Context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        protected void ParseCameraSetupSetField(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "CameraSetupSetField")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0].name,
                    FieldName = x.arguments[1].name,
                    Value = x.arguments[2].GetValueOrDefault<float>(),
                    Duration = x.arguments[3].GetValueOrDefault<float>(),
                };
            });

            if (match != null)
            {
                var camera = Context.Get<Camera>(match.VariableName) ?? Context.GetLastCreated<Camera>();
                if (camera != null)
                {
                    var handled = true;
                    var value = match.Value;
                    switch (match.FieldName)
                    {
                        case "CAMERA_FIELD_ZOFFSET":
                            camera.ZOffset = value;
                            break;
                        case "CAMERA_FIELD_ROTATION":
                            camera.Rotation = value;
                            break;
                        case "CAMERA_FIELD_ANGLE_OF_ATTACK":
                            camera.AngleOfAttack = value;
                            break;
                        case "CAMERA_FIELD_TARGET_DISTANCE":
                            camera.TargetDistance = value;
                            break;
                        case "CAMERA_FIELD_ROLL":
                            camera.Roll = value;
                            break;
                        case "CAMERA_FIELD_FIELD_OF_VIEW":
                            camera.FieldOfView = value;
                            break;
                        case "CAMERA_FIELD_FARZ":
                            camera.FarClippingPlane = value;
                            break;
                        case "CAMERA_FIELD_NEARZ":
                            camera.NearClippingPlane = value;
                            break;
                        case "CAMERA_FIELD_LOCAL_PITCH":
                            camera.LocalPitch = value;
                            break;
                        case "CAMERA_FIELD_LOCAL_YAW":
                            camera.LocalYaw = value;
                            break;
                        case "CAMERA_FIELD_LOCAL_ROLL":
                            camera.LocalRoll = value;
                            break;
                        default:
                            handled = false;
                            break;
                    }

                    if (handled)
                    {
                        Context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        protected void ParseCameraSetupSetDestPosition(StatementParserInput input)
        {
            var match = input.StatementChildren.Where(x => x.IsInvocation() && x.GetInvocationName() == "CameraSetupSetDestPosition")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = x.arguments[0].name,
                    X = x.arguments[1].GetValueOrDefault<float>(),
                    Y = x.arguments[2].GetValueOrDefault<float>(),
                    Duration = x.arguments[3].GetValueOrDefault<float>()
                };
            });

            if (match != null)
            {
                var camera = Context.Get<Camera>(match.VariableName) ?? Context.GetLastCreated<Camera>();
                if (camera != null)
                {
                    camera.TargetPosition = new(match.X, match.Y);
                    Context.HandledStatements.Add(input.Statement);
                }
            }
        }
    }
}