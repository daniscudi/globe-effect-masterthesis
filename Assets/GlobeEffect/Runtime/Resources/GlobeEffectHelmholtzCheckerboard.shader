Shader "GlobeEffect/Helmholtz Checkerboard"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0, 0, 0, 1)
        _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _FixationBackgroundColor ("Fixation Background", Color) = (0.5, 0.5, 0.5, 1)
        _FixationColor ("Fixation Color", Color) = (1, 0, 0, 1)
        _ApparentHalfAngleRad ("Apparent Half Angle [rad]", Float) = 0.785398
        _ApertureEdgeSoftnessRad ("Aperture Edge Softness [rad]", Float) = 0.0174533
        _VisualSpaceL ("Visual-space l", Range(0, 1.4)) = 0.5
        _ChecksAcrossDiameter ("Checks Across Diameter", Float) = 16
        _CheckerboardEnabled ("Checkerboard Enabled", Float) = 1
        _EyeMode ("Eye Mode", Float) = 0
        _FixationEnabled ("Fixation Enabled", Float) = 1
        _FixationHalfSizeRad ("Fixation Half Size [rad]", Float) = 0.0043633
        [HideInInspector] _ObserverWorldPosition ("Observer World Position", Vector) = (0, 0, 0, 1)
        [HideInInspector] _ObserverWorldRight ("Observer World Right", Vector) = (1, 0, 0, 0)
        [HideInInspector] _ObserverWorldUp ("Observer World Up", Vector) = (0, 1, 0, 0)
        [HideInInspector] _ObserverWorldForward ("Observer World Forward", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

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
            fixed4 _FixationBackgroundColor;
            fixed4 _FixationColor;
            float _ApparentHalfAngleRad;
            float _ApertureEdgeSoftnessRad;
            float _VisualSpaceL;
            float _ChecksAcrossDiameter;
            float _CheckerboardEnabled;
            float _EyeMode;
            float _FixationEnabled;
            float _FixationHalfSizeRad;
            float4 _ObserverWorldPosition;
            float4 _ObserverWorldRight;
            float4 _ObserverWorldUp;
            float4 _ObserverWorldForward;

            VertexToFragment Vert(AppData input)
            {
                VertexToFragment output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(VertexToFragment, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 displayPosition = input.vertex.xy * 2.0;
                float tangentAtBoundary = tan(_ApparentHalfAngleRad);
                float3 worldDirection = normalize(
                    _ObserverWorldForward.xyz +
                    _ObserverWorldRight.xyz * displayPosition.x * tangentAtBoundary +
                    _ObserverWorldUp.xyz * displayPosition.y * tangentAtBoundary);

                // Bei der Umrechnung in Kamerakoordinaten entfernt w = 0 die
                // Kameratranslation. Linkes und rechtes Auge bekommen dadurch
                // weiterhin dieselbe Richtung und keine Nahdisparität.
                float3 viewDirection = mul(
                    UNITY_MATRIX_V,
                    float4(worldDirection, 0.0)).xyz;

                // Für die eigentliche Projektion verwenden wir anschließend
                // w = 1. Die x/y-Position bleibt dieselbe Blickrichtung, aber
                // der Punkt erhält eine gültige Tiefe. Mit w = 0 konnte der
                // normale Mono-Game-View das ganze Viereck an der Fern-Ebene
                // abschneiden, obwohl der Varjo-Multi-Pass-Pfad es noch zeigte.
                output.position = mul(
                    UNITY_MATRIX_P,
                    float4(viewDirection, 1.0));
                output.uv = input.uv;
                return output;
            }

            float ResolveEyeIndex()
            {
                #if defined(UNITY_SINGLE_PASS_STEREO) || \
                    defined(UNITY_STEREO_INSTANCING_ENABLED) || \
                    defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    return (float)unity_StereoEyeIndex;
                #else
                    // Im Varjo-Multi-Pass-Modus kann der Stereoindex fehlen.
                    // Dann lässt sich das Auge an seiner seitlichen Kameraposition
                    // relativ zur Center-Eye-Pose erkennen.
                    float lateralEyeOffset = dot(
                        _WorldSpaceCameraPos.xyz - _ObserverWorldPosition.xyz,
                        _ObserverWorldRight.xyz);
                    return lateralEyeOffset > 0.0001 ? 1.0 : 0.0;
                #endif
            }

            fixed4 Frag(VertexToFragment input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float eyeIndex = ResolveEyeIndex();
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

                float tangentAtBoundary = tan(_ApparentHalfAngleRad);
                float visualAngle = atan(displayRadius * tangentAtBoundary);

                // Das Gitter wird weiterhin auf einem quadratischen Träger
                // berechnet. Diese unabhängige Kreisblende entscheidet erst
                // danach, welcher Teil davon sichtbar ist. Die Softness gibt
                // an, über wie viele Winkelgrad der Rand nach innen ausblendet.
                float apertureAlpha;
                if (_ApertureEdgeSoftnessRad <= 1e-6)
                {
                    apertureAlpha = step(
                        visualAngle,
                        _ApparentHalfAngleRad);
                }
                else
                {
                    float fadeStart = max(
                        0.0,
                        _ApparentHalfAngleRad - _ApertureEdgeSoftnessRad);
                    apertureAlpha = 1.0 - smoothstep(
                        fadeStart,
                        _ApparentHalfAngleRad,
                        visualAngle);
                }
                clip(apertureAlpha - 0.001);

                float sourceRadius;
                if (_VisualSpaceL < 1e-6)
                {
                    // Grenzfall der Visual-Space-Funktion für l gegen null.
                    sourceRadius = visualAngle / _ApparentHalfAngleRad;
                }
                else
                {
                    sourceRadius = tan(_VisualSpaceL * visualAngle) /
                        tan(_VisualSpaceL * _ApparentHalfAngleRad);
                }

                float2 radialDirection = displayRadius > 1e-6
                    ? displayPosition / displayRadius
                    : float2(1.0, 0.0);
                float2 sourcePosition = radialDirection * sourceRadius;

                float2 gridPosition = (sourcePosition + 1.0) * 0.5
                    * _ChecksAcrossDiameter;
                float checkerSignal = sin(UNITY_PI * gridPosition.x)
                    * sin(UNITY_PI * gridPosition.y);
                float antialiasWidth = max(fwidth(checkerSignal), 1e-4);
                float checkerMix = smoothstep(
                    -antialiasWidth,
                    antialiasWidth,
                    checkerSignal);
                fixed4 checkerColor = lerp(
                    _DarkColor,
                    _LightColor,
                    checkerMix);
                fixed4 color = lerp(
                    _FixationBackgroundColor,
                    checkerColor,
                    step(0.5, _CheckerboardEnabled));

                float2 angularPosition = atan(displayPosition * tangentAtBoundary);
                float crossThickness = max(_FixationHalfSizeRad * 0.18, 1e-5);
                float verticalBar = step(abs(angularPosition.x), crossThickness)
                    * step(abs(angularPosition.y), _FixationHalfSizeRad);
                float horizontalBar = step(abs(angularPosition.y), crossThickness)
                    * step(abs(angularPosition.x), _FixationHalfSizeRad);
                float fixationMask = saturate(verticalBar + horizontalBar)
                    * step(0.5, _FixationEnabled);

                fixed4 finalColor = lerp(color, _FixationColor, fixationMask);
                finalColor.a *= apertureAlpha;
                return finalColor;
            }
            ENDCG
        }
    }

    Fallback Off
}
