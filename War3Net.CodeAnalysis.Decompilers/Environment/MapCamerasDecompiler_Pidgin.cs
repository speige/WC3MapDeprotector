// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using Pidgin;
using System.Collections.Generic;
using System.Linq;
using War3Net.Build.Environment;
using War3Net.CodeAnalysis.Jass.Syntax;
using static Pidgin.Parser<War3Net.CodeAnalysis.Jass.Syntax.IJassSyntaxToken>;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler_Pidgin
    {
        public bool TryDecompileMapCameras(JassCompilationUnitSyntax compilationUnit, MapCamerasFormatVersion formatVersion, bool useNewFormat, out MapCameras? mapCameras)
        {
            _context = new();

            var functions = new[] { compilationUnit.GetFunction("CreateCameras") };
            var statements = functions.Where(x => x != null).SelectMany(x => x.GetChildren_RecursiveDepthFirst().OfType<IStatementSyntax>()).ToList();
            foreach (var statement in statements)
            {
                var statementChildren = statement.GetChildren_RecursiveDepthFirst().ToList();
                ParseCameraCreation(statementChildren, useNewFormat);
                ParseCameraSetupSetField(statementChildren);
                ParseCameraSetupSetDestPosition(statementChildren);
            }

            mapCameras = _context.AllCameras.Any() ? new MapCameras(formatVersion, useNewFormat) { Cameras = _context.AllCameras } : null;
            return mapCameras != null;
        }

        private void ParseCameraCreation(List<IJassSyntaxToken> statementChildren, bool useNewFormat)
        {
            var createCameraSetupParser = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "CreateCameraSetup")
            .Select(x =>
            {
                return new Camera
                {
                    NearClippingPlane = useNewFormat ? default : 100f,
                };
            });

            var pattern = GetVariableAssignmentParser().Optional().SelectMany(x => createCameraSetupParser, (x, y) => (VariableAssignment: x.HasValue ? x.Value : null, Camera: y));
            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var variableName = matchResult.Value.VariableAssignment;
                var camera = matchResult.Value.Camera;
                if (camera != null)
                {
                    _context.AllCameras.Add(camera);

                    if (variableName != null)
                    {
                        camera.Name = variableName;
                        if (camera.Name.StartsWith("gg_cam_"))
                        {
                            camera.Name = camera.Name["gg_cam_".Length..];
                        }
                        camera.Name = camera.Name.Replace('_', ' ');
                        _context.VariableNameToCameraMapping[camera.Name] = camera;
                    }
                }
            }
        }

        private void ParseCameraSetupSetField(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "CameraSetupSetField")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    FieldName = (syntax.Arguments.Arguments[1] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    Value = syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault(),
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var camera = _context.VariableNameToCameraMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllCameras.LastOrDefault();
                if (camera != null)
                {
                    var value = (float)matchResult.Value.Value;
                    switch (matchResult.Value.FieldName)
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
                    }
                }
            }
        }

        private void ParseCameraSetupSetDestPosition(List<IJassSyntaxToken> statementChildren)
        {
            var pattern = Token(x => x is IInvocationSyntax syntax && syntax.IdentifierName.Name == "CameraSetupSetDestPosition")
            .Select(x =>
            {
                var syntax = (IInvocationSyntax)x;
                return new
                {
                    VariableName = (syntax.Arguments.Arguments[0] as JassVariableReferenceExpressionSyntax).IdentifierName.Name,
                    X = syntax.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                    Y = syntax.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault()
                };
            });

            var matchResult = pattern.IgnoreExceptionsAndExtraTokens().Parse(statementChildren);
            if (matchResult.Success)
            {
                var camera = _context.VariableNameToCameraMapping.GetValueOrDefault(matchResult.Value.VariableName) ?? _context.AllCameras.LastOrDefault();
                if (camera != null)
                {
                    camera.TargetPosition = new((float)matchResult.Value.X, (float)matchResult.Value.Y);
                }
            }
        }
    }
}

