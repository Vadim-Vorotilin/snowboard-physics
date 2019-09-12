using SnowboardPhysics.Core;
using UnityEditor;
using UnityEngine;

namespace SnowboardPhysics.Editor {
    [CustomPropertyDrawer (typeof (ModelParameters))]
    public class ModelParametersDrawer : PropertyDrawer {

        private const float PropertyHeight = 16f;
        private const float SpaceHeight = 5f;
        private const float SpaceBetween = 1f;

        private const float FromToSliderLabelSize = 0.3f;

        private static readonly GUIStyle RightTextStyle = new GUIStyle {
            alignment = TextAnchor.MiddleRight
        };
        
        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
            const int spacesCount = 12;
            int propsCount = 24;

            if (!prop.FindPropertyRelative("RotateInAir").boolValue)
                propsCount--;

            if (!prop.FindPropertyRelative("FallOnUnsafeLandingAngle").boolValue)
                propsCount--;

            if (!prop.FindPropertyRelative("FallOnUnsafeLandingSpeed").boolValue)
                propsCount--;

            return base.GetPropertyHeight(prop, label) * propsCount + SpaceBetween * (propsCount - 1) + SpaceHeight * spacesCount;
        }

        private static Rect GetPropRect(ref float yPos, Rect pos, int spaceAbove = 0, int spaceBelow = 0) {
            float y = pos.y + yPos + SpaceHeight * spaceAbove;

            yPos += PropertyHeight + SpaceBetween + SpaceHeight * (spaceAbove + spaceBelow);
            
            return new Rect(pos.x,
                            y,
                            pos.width,
                            PropertyHeight);
        }

        public override void OnGUI (Rect pos, SerializedProperty prop, GUIContent label) {

            var terrainLayerProp = prop.FindPropertyRelative("TerrainLayer");
            var rotateInAirProp = prop.FindPropertyRelative("RotateInAir");
            var inAirRotationAngularVelocityProp = prop.FindPropertyRelative("InAirRotationAngularVelocity");
            var invertInputBackwardsProp = prop.FindPropertyRelative("InvertInputBackwards");
            var fallOnUnsafeLandingAngleProp = prop.FindPropertyRelative("FallOnUnsafeLandingAngle");
            var maxSafeLandingAngleProp = prop.FindPropertyRelative("MaxSafeLandingAngle");
            var fallOnUnsafeLandingSpeedProp = prop.FindPropertyRelative("FallOnUnsafeLandingSpeed");
            var maxSafeLandingSpeedProp = prop.FindPropertyRelative("MaxSafeLandingSpeed");
            
            var turnAbruptnessProp = prop.FindPropertyRelative("TurnAbruptness");
            var turnToSlopeRateProp = prop.FindPropertyRelative("TurnToSlopeRate");
            var slowingDownRateProp = prop.FindPropertyRelative("SlowingDownRate");
            var frictionProp = prop.FindPropertyRelative("Friction");
            var slippingProp = prop.FindPropertyRelative("Slipping");
            var airResistanceProp = prop.FindPropertyRelative("AirResistance");
            var contactOffsetProp = prop.FindPropertyRelative("ContactOffset");
            
            var boardLengthProp = prop.FindPropertyRelative("BoardLength");
            var boardWidthProp = prop.FindPropertyRelative("BoardWidth");

            EditorGUIUtility.labelWidth = 150f;

            float yPos = 0;
            
            //TODO: tooltips, pixtures?            
            EditorGUI.PropertyField(GetPropRect(ref yPos, pos),
                                    terrainLayerProp,
                                    new GUIContent("Terrain layer"));
            
            EditorGUI.PropertyField(GetPropRect(ref yPos, pos, 1),
                                    rotateInAirProp,
                                    new GUIContent("Rotate in air"));
            
            if (rotateInAirProp.boolValue) {
                EditorGUI.indentLevel++;
                
                EditorGUI.PropertyField(GetPropRect(ref yPos, pos),
                                        inAirRotationAngularVelocityProp,
                                        new GUIContent("Angular velocity (°/s)"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUI.PropertyField(GetPropRect(ref yPos, pos, 1),
                                    invertInputBackwardsProp,
                                    new GUIContent("Invert input backwards")); //TODO: backwards?

            EditorGUI.PropertyField(GetPropRect(ref yPos, pos, 1),
                                    fallOnUnsafeLandingAngleProp,
                                    new GUIContent("Fall on disorientation", "Fall on unsafe landing angle")); //TODO: disorientation?

            if (fallOnUnsafeLandingAngleProp.boolValue) {
                EditorGUI.indentLevel++;
                
                EditorGUI.PropertyField(GetPropRect(ref yPos, pos),
                                        maxSafeLandingAngleProp,
                                        new GUIContent("Safe angle (°)"));
                
                EditorGUI.indentLevel--;
            }

            EditorGUI.PropertyField(GetPropRect(ref yPos, pos, 1),
                                    fallOnUnsafeLandingSpeedProp,
                                    new GUIContent("Fall on overload", "Fall on unsafe landing speed")); //TODO: overload?

            if (fallOnUnsafeLandingSpeedProp.boolValue) {
                EditorGUI.indentLevel++;
                
                EditorGUI.PropertyField(GetPropRect(ref yPos, pos),
                                        maxSafeLandingSpeedProp,
                                        new GUIContent("Safe speed (m/s)"));
                
                EditorGUI.indentLevel--;
            }
            
            DrawLabelAndFromToSlider(ref yPos, pos, turnAbruptnessProp, "Turn abruptness", 0, 1, "Smooth", "Abrupt");
            DrawLabelAndFromToSlider(ref yPos, pos, turnToSlopeRateProp, "Turn to slope rate", 0, 1, "No turn", "Quickly"); //TODO: turn to slope?
            DrawLabelAndFromToSlider(ref yPos, pos, slowingDownRateProp, "Slowing down rate", 0, 1, "Smooth", "Abrupt");
            DrawLabelAndFromToSlider(ref yPos, pos, frictionProp, "Snow friction", 0, 1, "No friction", "Wet snow");
            DrawLabelAndFromToSlider(ref yPos, pos, slippingProp, "Should the board slip", 0, 1, "Slipping", "No slipping");
            DrawLabelAndFromToSlider(ref yPos, pos, airResistanceProp, "Air resistance", 0, 1, "No resistance", "Heavy");
            DrawLabelAndFromToSlider(ref yPos, pos, contactOffsetProp, "How easily should the board lift off", 0, 1, "Easy", "Hard");
            
            EditorGUI.PropertyField(GetPropRect(ref yPos, pos, 1),
                                    boardLengthProp,
                                    new GUIContent("Board length"));
            
            EditorGUI.PropertyField(GetPropRect(ref yPos, pos),
                                    boardWidthProp,
                                    new GUIContent("Board width"));
        }

        private static void DrawLabelAndFromToSlider(ref float yPos,
                                                     Rect pos,
                                                     SerializedProperty prop,
                                                     string label,
                                                     float fromValue,
                                                     float toValue,
                                                     string fromLabel,
                                                     string toLabel) {
            GUI.Label(GetPropRect(ref yPos, pos, 1), label);

            EditorGUI.indentLevel++;

            DrawFromToSlider(GetPropRect(ref yPos, pos),
                             prop,
                             fromValue, toValue,
                             fromLabel, toLabel);
            
            EditorGUI.indentLevel--;
        }

        private static void DrawFromToSlider(Rect rect,
                                             SerializedProperty prop,
                                             float fromValue,
                                             float toValue,
                                             string fromLabel,
                                             string toLabel) {
            rect.xMin += 15f * EditorGUI.indentLevel;
            
            var fromLabelRect = new Rect(rect.x, rect.y, rect.width * FromToSliderLabelSize, rect.height);
            var sliderRect = new Rect(rect.x + fromLabelRect.width, rect.y, rect.width * (1 - 2 * FromToSliderLabelSize), rect.height);
            var toLabelRect = new Rect(sliderRect.x + sliderRect.width, rect.y, rect.width * FromToSliderLabelSize, rect.height);
            
            GUI.Label(fromLabelRect, fromLabel);

            prop.floatValue = GUI.HorizontalSlider(sliderRect, prop.floatValue, fromValue, toValue);
            
            GUI.Label(toLabelRect, toLabel, RightTextStyle);
        }
    }
}