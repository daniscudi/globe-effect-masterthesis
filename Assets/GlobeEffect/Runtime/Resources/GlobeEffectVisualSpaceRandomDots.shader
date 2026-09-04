Shader "GlobeEffect/Visual Space Random Dots"
{
    Properties
    {
        _ApertureHalfAngleRad ("Aperture Half Angle [rad]", Float) = 0.785398
        _ApertureEdgeSoftnessRad ("Aperture Edge Softness [rad]", Float) = 0.0174533
        _VisualSpaceL ("Visual-space l", Range(0, 1.4)) = 0.5
        _ContentZoom ("Content Zoom", Float) = 1
        _EyeMode ("Eye Mode", Float) = 0
        _DotsEnabled ("Dots Enabled", Float) = 1
        _DotHalfSizeRad ("Dot Half Size [rad]", Float) = 0.00191986
        [HideInInspector] _SimulatedYawRad ("Simulated Yaw [rad]", Float) = 0
        [HideInInspector] _ObserverWorldPosition ("Observer World Position", Vector) = (0, 0, 0, 1)
        [HideInInspector] _ObserverWorldRight ("Observer World Right", Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 sizeData : TEXCOORD1;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexToFragment
            {
                float4 position : SV_POSITION;
                float2 dotUv : TEXCOORD0;
                float displayedAngle : TEXCOORD1;
                float validProjection : TEXCOORD2;
                float isFixationTarget : TEXCOORD3;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _ApertureHalfAngleRad;
            float _ApertureEdgeSoftnessRad;
            float _VisualSpaceL;
            float _ContentZoom;
            float _EyeMode;
            float _DotsEnabled;
            float _DotHalfSizeRad;
            float _SimulatedYawRad;
            float4 _ObserverWorldPosition;
            float4 _ObserverWorldRight;

            // Dieselbe normierte l-Abbildung wie beim Checkerboard, diesmal
            // vorwärts gelöst: Aus der unverzerrten Punktposition wird die
            // Position berechnet, an der der Punkt im HMD erscheinen soll.
            float DisplayedAngleFromSource(float sourceAngle)
            {
                float tangentAtBoundary = tan(_ApertureHalfAngleRad);
                float sourceRadius = tan(sourceAngle) /
                    max(tangentAtBoundary, 1e-6);

                if (_VisualSpaceL < 1e-6)
                {
                    return sourceRadius * _ApertureHalfAngleRad;
                }

                return atan(
                    sourceRadius * tan(_VisualSpaceL * _ApertureHalfAngleRad)) /
                    _VisualSpaceL;
            }

            float CenterRadialScale()
            {
                float tangentAtBoundary = tan(_ApertureHalfAngleRad);
                if (_VisualSpaceL < 1e-6)
                {
                    return _ApertureHalfAngleRad /
                        max(tangentAtBoundary, 1e-6);
                }

                return tan(_VisualSpaceL * _ApertureHalfAngleRad) /
                    max(_VisualSpaceL * tangentAtBoundary, 1e-6);
            }

            float ResolveEyeIndex()
            {
                #if defined(UNITY_SINGLE_PASS_STEREO) || \
                    defined(UNITY_STEREO_INSTANCING_ENABLED) || \
                    defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    return (float)unity_StereoEyeIndex;
                #else
                    float lateralEyeOffset = dot(
                        _WorldSpaceCameraPos.xyz - _ObserverWorldPosition.xyz,
                        _ObserverWorldRight.xyz);
                    return lateralEyeOffset > 0.0001 ? 1.0 : 0.0;
                #endif
            }

            VertexToFragment Vert(AppData input)
            {
                VertexToFragment output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(VertexToFragment, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float isFixation = step(0.5, input.sizeData.y);

                // Die Punkte werden als Richtungen dargestellt. w = 0 entfernt
                // die Verschiebung zwischen den beiden Augenkameras und damit
                // eine künstliche Nahdisparität.
                float3 worldDirection = normalize(mul(
                    (float3x3)unity_ObjectToWorld,
                    input.vertex.xyz));
                float3 viewPosition = mul(
                    UNITY_MATRIX_V,
                    float4(worldDirection, 0.0)).xyz;

                // Das Kreuz bleibt in der Mitte. Nur das Punktfeld führt den
                // kontrollierten Links-Rechts-Schwenk aus.
                if (isFixation > 0.5)
                {
                    viewPosition = float3(0.0, 0.0, -1.0);
                }
                else
                {
                    float cosine = cos(_SimulatedYawRad);
                    float sine = sin(_SimulatedYawRad);
                    viewPosition.xz = float2(
                        cosine * viewPosition.x + sine * viewPosition.z,
                        -sine * viewPosition.x + cosine * viewPosition.z);
                }

                float forwardDistance = -viewPosition.z;
                float validFront = step(1e-4, forwardDistance);
                float2 sourcePosition = viewPosition.xy /
                    max(forwardDistance, 1e-4);
                float sourceRadius = length(sourcePosition);
                float sourceAngle = atan(sourceRadius);
                float displayedAngle = isFixation > 0.5
                    ? 0.0
                    : DisplayedAngleFromSource(sourceAngle);
                float validAngle = step(displayedAngle, 1.560796);

                float displayedRadius = tan(min(displayedAngle, 1.560796)) *
                    _ContentZoom;
                displayedAngle = atan(displayedRadius);
                float radialScale = sourceRadius > 1e-6
                    ? displayedRadius / sourceRadius
                    : CenterRadialScale() * _ContentZoom;
                float2 displayedPosition = isFixation > 0.5
                    ? float2(0.0, 0.0)
                    : sourcePosition * radialScale;
                viewPosition.xy = displayedPosition * forwardDistance;

                // Erst wird der Punktmittelpunkt verzerrt. Danach wird die
                // feste Winkelgröße ergänzt, damit l nur die Bahn verändert.
                float angularHalfSize = _DotHalfSizeRad * input.sizeData.x;
                viewPosition.xy += input.uv * forwardDistance *
                    tan(angularHalfSize);

                output.position = mul(
                    UNITY_MATRIX_P,
                    float4(viewPosition, 1.0));
                output.dotUv = input.uv;
                output.displayedAngle = displayedAngle;
                output.validProjection = validFront * validAngle;
                output.isFixationTarget = isFixation;
                output.color = input.color;
                return output;
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
                clip(input.validProjection - 0.5);

                if (input.isFixationTarget > 0.5)
                {
                    float vertical = step(abs(input.dotUv.x), 0.18);
                    float horizontal = step(abs(input.dotUv.y), 0.18);
                    clip(saturate(vertical + horizontal) - 0.5);
                    return input.color;
                }

                clip(_DotsEnabled - 0.5);
                clip(1.0 - length(input.dotUv));

                float apertureAlpha;
                if (_ApertureEdgeSoftnessRad <= 1e-6)
                {
                    apertureAlpha = step(
                        input.displayedAngle,
                        _ApertureHalfAngleRad);
                }
                else
                {
                    float fadeStart = max(
                        0.0,
                        _ApertureHalfAngleRad - _ApertureEdgeSoftnessRad);
                    apertureAlpha = 1.0 - smoothstep(
                        fadeStart,
                        _ApertureHalfAngleRad,
                        input.displayedAngle);
                }

                clip(apertureAlpha - 0.001);
                return fixed4(
                    input.color.rgb,
                    input.color.a * apertureAlpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
