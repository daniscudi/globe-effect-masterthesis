Shader "GlobeEffect/Merlitz Checkerboard"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0, 0, 0, 1)
        _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _FixationColor ("Fixation Color", Color) = (1, 0, 0, 1)
        _ApparentHalfAngleRad ("Apparent Half Angle [rad]", Float) = 0.610865
        _MerlitzK ("Merlitz k", Range(0, 1)) = 0.7
        _Magnification ("Magnification", Float) = 10
        _ChecksAcrossDiameter ("Checks Across Diameter", Float) = 16
        _EyeMode ("Eye Mode", Float) = 0
        _FixationEnabled ("Fixation Enabled", Float) = 1
        _FixationHalfSizeRad ("Fixation Half Size [rad]", Float) = 0.0043633
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexToFragment
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _DarkColor;
            fixed4 _LightColor;
            fixed4 _FixationColor;
            float _ApparentHalfAngleRad;
            float _MerlitzK;
            float _Magnification;
            float _ChecksAcrossDiameter;
            float _EyeMode;
            float _FixationEnabled;
            float _FixationHalfSizeRad;

            VertexToFragment Vert(AppData input)
            {
                VertexToFragment output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(VertexToFragment, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float ObjectAngleFromApparent(float apparentAngle)
            {
                // Exakter Grenzfall der Winkelbedingung fuer k -> 0.
                if (_MerlitzK < 1e-5)
                {
                    return apparentAngle / _Magnification;
                }

                return atan(tan(_MerlitzK * apparentAngle) / _Magnification)
                    / _MerlitzK;
            }

            fixed4 Frag(VertexToFragment input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // unity_StereoEyeIndex: 0 = links, 1 = rechts. Im nicht-XR
                // Game-View verhaelt sich die Vorschau wie das linke Auge.
                float eyeIndex = (float)unity_StereoEyeIndex;
                float visibleForEye = 1.0;
                if (_EyeMode > 0.5 && _EyeMode < 1.5)
                {
                    visibleForEye = 1.0 - eyeIndex;
                }
                else if (_EyeMode >= 1.5)
                {
                    visibleForEye = eyeIndex;
                }
                clip(visibleForEye - 0.5);

                float2 displayPosition = input.uv * 2.0 - 1.0;
                float displayRadius = length(displayPosition);

                // Inverse radiale Abbildung: angezeigter Radius -> regulaeres
                // Ausgangsgitter auf der gedachten Wand.
                float tangentAtBoundary = tan(_ApparentHalfAngleRad);
                float apparentAngle = atan(displayRadius * tangentAtBoundary);
                float objectAngle = ObjectAngleFromApparent(apparentAngle);
                float maximumObjectAngle = ObjectAngleFromApparent(
                    _ApparentHalfAngleRad);
                float sourceRadius = tan(objectAngle) / tan(maximumObjectAngle);

                float2 radialDirection = displayRadius > 1e-6
                    ? displayPosition / displayRadius
                    : float2(1.0, 0.0);
                float2 sourcePosition = radialDirection * sourceRadius;

                // Das Sinusprodukt wechselt an jeder ganzzahligen Gitterlinie
                // sein Vorzeichen. fwidth glaettet nur die Pixelkante, nicht
                // die mathematische Verzeichnung.
                float2 gridPosition = (sourcePosition + 1.0) * 0.5
                    * _ChecksAcrossDiameter;
                float checkerSignal = sin(UNITY_PI * gridPosition.x)
                    * sin(UNITY_PI * gridPosition.y);
                float antialiasWidth = max(fwidth(checkerSignal), 1e-4);
                float checkerMix = smoothstep(
                    -antialiasWidth,
                    antialiasWidth,
                    checkerSignal);
                fixed4 color = lerp(_DarkColor, _LightColor, checkerMix);

                // Fixationskreuz in Winkelkoordinaten, damit seine Groesse bei
                // einer Distanz- oder FOV-Aenderung konstant bleibt.
                float2 angularPosition = atan(
                    displayPosition * tangentAtBoundary);
                float crossThickness = max(_FixationHalfSizeRad * 0.18, 1e-5);
                float verticalBar = step(abs(angularPosition.x), crossThickness)
                    * step(abs(angularPosition.y), _FixationHalfSizeRad);
                float horizontalBar = step(abs(angularPosition.y), crossThickness)
                    * step(abs(angularPosition.x), _FixationHalfSizeRad);
                float fixationMask = saturate(verticalBar + horizontalBar)
                    * step(0.5, _FixationEnabled);

                return lerp(color, _FixationColor, fixationMask);
            }
            ENDCG
        }
    }

    Fallback Off
}
