// ------------------------------------------------------------------------------
// <copyright file="MapCamerasDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using War3Net.Build.Environment;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.CodeAnalysis.Decompilers
{
    public partial class JassScriptDecompiler
    {
        public bool TryDecompileMapCameras(MapCamerasFormatVersion formatVersion, bool useNewFormat, [NotNullWhen(true)] out MapCameras? mapCameras, [NotNullWhen(true)] out Dictionary<Camera, ObjectManagerDecompilationMetaData>? decompilationMetaData)
        {
            foreach (var candidateFunction in GetCandidateFunctions("CreateCameras"))
            {
                if (TryDecompileMapCameras(candidateFunction.FunctionDeclaration, formatVersion, useNewFormat, out mapCameras, out decompilationMetaData))
                {
                    candidateFunction.Handled = true;

                    return true;
                }
            }

            mapCameras = null;
            decompilationMetaData = null;
            return false;
        }

        public bool TryDecompileMapCameras(JassFunctionDeclarationSyntax functionDeclaration, MapCamerasFormatVersion formatVersion, bool useNewFormat, [NotNullWhen(true)] out MapCameras? mapCameras, [NotNullWhen(true)] out Dictionary<Camera, ObjectManagerDecompilationMetaData>? decompilationMetaData)
        {
            if (functionDeclaration is null)
            {
                throw new ArgumentNullException(nameof(functionDeclaration));
            }

            decompilationMetaData = new Dictionary<Camera, ObjectManagerDecompilationMetaData>();

            var cameras = new Dictionary<string, Camera>(StringComparer.Ordinal);

            foreach (var statement in functionDeclaration.Body.Statements)
            {
                if (statement is JassCommentSyntax ||
                    statement is JassEmptySyntax)
                {
                    continue;
                }
                else if (statement is JassSetStatementSyntax setStatement)
                {
                    if (setStatement.Indexer is null &&
                        setStatement.IdentifierName.Name.StartsWith("gg_cam_", StringComparison.Ordinal) &&
                        setStatement.Value.Expression is JassInvocationExpressionSyntax invocationExpression &&
                        string.Equals(invocationExpression.IdentifierName.Name, "CreateCameraSetup", StringComparison.Ordinal))
                    {
                        if (invocationExpression.Arguments.Arguments.IsEmpty)
                        {
                            var camera = new Camera
                            {
                                Name = setStatement.IdentifierName.Name["gg_cam_".Length..].Replace('_', ' '),
                                NearClippingPlane = useNewFormat ? default : 100f,
                            };

                            var metaData = decompilationMetaData.GetOrAdd(camera);
                            metaData.DecompiledFromStatements.Add(statement);

                            cameras[setStatement.IdentifierName.Name] = camera;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                else if (statement is JassCallStatementSyntax callStatement)
                {
                    if (string.Equals(callStatement.IdentifierName.Name, "CameraSetupSetField", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 4 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax cameraVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1] is JassVariableReferenceExpressionSyntax cameraFieldVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[2].TryGetRealExpressionValue(out var value) &&
                            callStatement.Arguments.Arguments[3].TryGetRealExpressionValue(out var duration) &&
                            duration == 0f &&
                            cameras.TryGetValue(cameraVariableReferenceExpression.IdentifierName.Name, out var camera))
                        {
                            var setProperty = true;
                            switch (cameraFieldVariableReferenceExpression.IdentifierName.Name)
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
                                    setProperty = false;
                                    break;
                            }

                            if (setProperty)
                            {
                                var metaData = decompilationMetaData.GetOrAdd(camera);
                                metaData.DecompiledFromStatements.Add(statement);
                            }
                        }
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "CameraSetupSetDestPosition", StringComparison.Ordinal))
                    {
                        if (callStatement.Arguments.Arguments.Length == 4 &&
                            callStatement.Arguments.Arguments[0] is JassVariableReferenceExpressionSyntax cameraVariableReferenceExpression &&
                            callStatement.Arguments.Arguments[1].TryGetRealExpressionValue(out var x) &&
                            callStatement.Arguments.Arguments[2].TryGetRealExpressionValue(out var y) &&
                            callStatement.Arguments.Arguments[3].TryGetRealExpressionValue(out var duration) &&
                            duration == 0f &&
                            cameras.TryGetValue(cameraVariableReferenceExpression.IdentifierName.Name, out var camera))
                        {
                            var metaData = decompilationMetaData.GetOrAdd(camera);
                            metaData.DecompiledFromStatements.Add(statement);

                            camera.TargetPosition = new(x, y);
                        }
                    }
                }
            }

            if (cameras.Any())
            {
                mapCameras = new MapCameras(formatVersion, useNewFormat);
                mapCameras.Cameras.AddRange(cameras.Values);
                return true;
            }

            mapCameras = null;
            decompilationMetaData = null;
            return false;
        }
    }
}