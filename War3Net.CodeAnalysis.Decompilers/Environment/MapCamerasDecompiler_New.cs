// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using War3Net.Build.Environment;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{

    public partial class JassScriptDecompiler_New
    {
        [RegisterStatementParser]
        private void ParseCameraCreation(StatementParserInput input)
        {
            var variableAssignment = GetVariableAssignment(input.StatementChildren);
            var camera = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "CreateCameraSetup")
            .SafeMapFirst(x =>
            {
                return new Camera
                {
                    NearClippingPlane = _context.Options.mapCamerasUseNewFormat ? default : 100f,
                };
            });

            if (camera != null)
            {
                camera.Name = variableAssignment;
                if (camera.Name != null)
                {
                    if (camera.Name.StartsWith("gg_cam_"))
                    {
                        camera.Name = camera.Name["gg_cam_".Length..];
                    }
                    camera.Name = camera.Name.Replace('_', ' ');
                }

                _context.Add(camera, variableAssignment);
                _context.HandledStatements.Add(input.Statement);
            }
        }

        [RegisterStatementParser]
        private void ParseCameraSetupSetField(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "CameraSetupSetField")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    FieldName = ((JassVariableReferenceExpressionSyntax)x.Arguments.Arguments[1]).IdentifierName.Name,
                    Value = x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    Duration = (float)x.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault(),
                };
            });

            if (match != null)
            {
                var camera = _context.Get<Camera>(match.VariableName) ?? _context.GetLastCreated<Camera>();
                if (camera != null)
                {
                    if (match.Duration != 0f)
                    {
                        //TODO
                    }

                    var handled = true;
                    var value = (float)match.Value;
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
                        _context.HandledStatements.Add(input.Statement);
                    }
                }
            }
        }

        [RegisterStatementParser]
        private void ParseCameraSetupSetDestPosition(StatementParserInput input)
        {
            var match = input.StatementChildren.OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "CameraSetupSetDestPosition")
            .SafeMapFirst(x =>
            {
                return new
                {
                    VariableName = (x.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    X = x.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Y = x.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                    Duration = (float)x.Arguments.Arguments[3].GetDecimalExpressionValueOrDefault()
                };
            });

            if (match != null)
            {
                var camera = _context.Get<Camera>(match.VariableName) ?? _context.GetLastCreated<Camera>();
                if (camera != null)
                {
                    if (match.Duration != 0f)
                    {
                        //TODO
                    }

                    camera.TargetPosition = new((float)match.X, (float)match.Y);
                    _context.HandledStatements.Add(input.Statement);
                }
            }
        }
    }
}