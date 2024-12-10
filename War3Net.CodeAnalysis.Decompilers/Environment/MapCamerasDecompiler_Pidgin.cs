// ------------------------------------------------------------------------------
// 
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// ------------------------------------------------------------------------------

using Pidgin;
using System;
using System.Collections.Generic;
using System.Linq;
using War3Net.Build.Environment;
using War3Net.CodeAnalysis.Jass.Extensions;
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
                    ParseCameraProperties(statementChildren);
            }

            mapCameras = _context.AllCameras.Any() ? new MapCameras(formatVersion, useNewFormat) { Cameras = _context.AllCameras } : null;
            return mapCameras != null;
        }

        private void ParseCameraCreation(List<IJassSyntaxToken> statementChildren, bool useNewFormat)
        {
            var cameraCreationPattern = Token(x =>
                x is JassSetStatementSyntax setStatement &&
                setStatement.Value.Expression is JassInvocationExpressionSyntax invocation &&
                invocation.IdentifierName.Name == "CreateCameraSetup")
            .Select(x =>
            {
                var setStatement = (JassSetStatementSyntax)x;
                return new Camera
                {
                    Name = setStatement.IdentifierName.Name["gg_cam_".Length..].Replace('_', ' '),
                    NearClippingPlane = useNewFormat ? default : 100f,
                };
            }).IgnoreExceptionsAndExtraTokens();

            var matchResult = cameraCreationPattern.Parse(statementChildren);
            if (matchResult.Success)
            {
                var camera = matchResult.Value;
                _context.AllCameras.Add(camera);
                _context.VariableNameToCameraMapping[camera.Name] = camera;
            }
        }

        private void ParseCameraProperties(List<IJassSyntaxToken> statementChildren)
        {
            var cameraPropertyPattern = Token(x =>
                x is JassCallStatementSyntax callStatement &&
                _context.VariableNameToCameraMapping.ContainsKey(((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name))
            .Select(x =>
            {
                var callStatement = (JassCallStatementSyntax)x;
                var camera = _context.VariableNameToCameraMapping[((JassVariableReferenceExpressionSyntax)callStatement.Arguments.Arguments[0]).IdentifierName.Name];

                switch (callStatement.IdentifierName.Name)
                {
                    case "CameraSetupSetField":
                        ParseCameraField(callStatement, camera);
                        break;
                    case "CameraSetupSetDestPosition":
                        camera.TargetPosition = new(
                            (float)callStatement.Arguments.Arguments[1].GetDecimalExpressionValueOrDefault(),
                            (float)callStatement.Arguments.Arguments[2].GetDecimalExpressionValueOrDefault());
                        break;
                }

                return camera;
            }).IgnoreExceptionsAndExtraTokens();

            cameraPropertyPattern.Parse(statementChildren);
        }

        private void ParseCameraField(JassCallStatementSyntax callStatement, Camera camera)
        {
            if (callStatement.Arguments.Arguments[1] is JassVariableReferenceExpressionSyntax fieldReference &&
                callStatement.Arguments.Arguments[2].TryGetRealExpressionValue(out var value))
            {
                switch (fieldReference.IdentifierName.Name)
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

}

