Shader "GlobeEffect/Visual Space Random Dots"
{
    // Der C#-Teil erzeugt nur die Richtungen und Farben der Punkte. Dieser Shader
    // macht daraus das Bild für jedes Auge. Der Ablauf pro Punkt ist:
    // Richtung in Kamerakoordinaten umrechnen -> Schwenk anwenden -> mit der
    // Merlitz-Instrumentenformel aus m und k abbilden -> optionalen Content Zoom
    // anwenden -> Punktgröße ergänzen -> Kreis ausschneiden.
    Properties
    {
        _ApertureHalfAngleRad ("Aperture Half Angle [rad]", Float) = 0.785398
        _ApertureEdgeSoftnessRad ("Aperture Edge Softness [rad]", Float) = 0.0174533
        _InstrumentDistortionK ("Instrument distortion k", Range(0, 1.4)) = 0.5
        _InstrumentMagnificationM ("Instrument magnification m", Range(1, 20)) = 10
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
            float _InstrumentDistortionK;
            float _InstrumentMagnificationM;
            float _ContentZoom;
            float _EyeMode;
            float _DotsEnabled;
            float _DotHalfSizeRad;
            float _SimulatedYawRad;
            float4 _ObserverWorldPosition;
            float4 _ObserverWorldRight;

            // Merlitz' Instrumentenabbildung:
            //
            //     tan(k a) = m tan(k A)
            //
            // A ist der wirkliche Winkel der Punktrichtung zur Instrumentenachse,
            // a der im Fernglas erscheinende Winkel. k = 1 ist die klassische
            // Tangentenbedingung, k = 0,5 die Kreisbedingung. Für k gegen null
            // bleibt die Winkelbedingung a = m A.
            float ApparentAngleFromObject(
                float objectAngle,
                out float validInstrumentDomain)
            {
                if (_InstrumentDistortionK < 1e-6)
                {
                    validInstrumentDomain = 1.0;
                    return _InstrumentMagnificationM * objectAngle;
                }

                // Vor dem Umkehrpunkt des Tangens bleiben Abbildung und
                // Umkehrabbildung eindeutig. Punkte außerhalb dieses Bereichs
                // werden verworfen, statt auf die andere Seite umzuschlagen.
                float scaledObjectAngle =
                    _InstrumentDistortionK * objectAngle;
                validInstrumentDomain = step(
                    scaledObjectAngle,
                    1.560796);
                return atan(
                    _InstrumentMagnificationM *
                    tan(min(scaledObjectAngle, 1.560796))) /
                    _InstrumentDistortionK;
            }

            float CenterInstrumentScale()
            {
                // Im Zentrum ist die Ableitung da/dA für jedes k genau m.
                return _InstrumentMagnificationM;
            }

            float ResolveEyeIndex()
            {
                // Unity liefert den Augenindex je nach XR-Renderverfahren anders.
                // Im Varjo-Multi-Pass-Fall wird er aus der Kameraposition abgeleitet.
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

                // Aus x/z und y/z entsteht die lineare Ausgangsposition. atan
                // wandelt ihren Radius in den zugehörigen Blickwinkel um.
                float2 sourcePosition = viewPosition.xy /
                    max(forwardDistance, 1e-4);
                float sourceRadius = length(sourcePosition);
                float sourceAngle = atan(sourceRadius);
                float validInstrumentDomain = 1.0;
                float displayedAngle = isFixation > 0.5
                    ? 0.0
                    : ApparentAngleFromObject(
                        sourceAngle,
                        validInstrumentDomain);

                float displayedRadius = tan(min(displayedAngle, 1.560796)) *
                    _ContentZoom;
                // Content Zoom bleibt bewusst ein getrennter optionaler Nach-Zoom.
                // Die Fernglasvergrößerung steckt bereits oben in m.
                displayedAngle = atan(displayedRadius);
                float validAngle = step(displayedAngle, 1.560796);
                float radialScale = sourceRadius > 1e-6
                    ? displayedRadius / sourceRadius
                    : CenterInstrumentScale() * _ContentZoom;
                float2 displayedPosition = isFixation > 0.5
                    ? float2(0.0, 0.0)
                    : sourcePosition * radialScale;
                viewPosition.xy = displayedPosition * forwardDistance;

                // Erst wird der Punktmittelpunkt durch das Instrument abgebildet.
                // Danach wird die feste Winkelgröße des sternähnlichen Markers
                // ergänzt; k und m verändern damit seine Bahn, nicht seine Größe.
                float angularHalfSize = _DotHalfSizeRad * input.sizeData.x;
                viewPosition.xy += input.uv * forwardDistance *
                    tan(angularHalfSize);

                output.position = mul(
                    UNITY_MATRIX_P,
                    float4(viewPosition, 1.0));
                output.dotUv = input.uv;
                output.displayedAngle = displayedAngle;
                output.validProjection = validFront *
                    validInstrumentDomain * validAngle;
                output.isFixationTarget = isFixation;
                output.color = input.color;
                return output;
            }

            fixed4 Frag(VertexToFragment input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float eyeIndex = ResolveEyeIndex();
                float visibleForEye = 1.0;
                // 0 = beide Augen, 1 = nur links, 2 = nur rechts.
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
                    // Zwei schmale Rechtecke ergeben zusammen das Fixationskreuz.
                    float vertical = step(abs(input.dotUv.x), 0.18);
                    float horizontal = step(abs(input.dotUv.y), 0.18);
                    clip(saturate(vertical + horizontal) - 0.5);
                    return input.color;
                }

                clip(_DotsEnabled - 0.5);

                // Das Viereck jedes Punktes wird zu einem geglätteten Kreis. Die
                // Ableitung fwidth liefert ungefähr die Breite eines Bildpixels
                // in den lokalen Punktkoordinaten. Dadurch bleibt die Kreiskante
                // auch während des Schwenks stabil, statt hart zwischen sichtbaren
                // und unsichtbaren Pixeln zu springen.
                float radialDistance = length(input.dotUv);
                float antialiasWidth = max(fwidth(radialDistance), 1e-4);
                float dotAlpha = 1.0 - smoothstep(
                    1.0 - antialiasWidth,
                    1.0 + antialiasWidth,
                    radialDistance);
                clip(dotAlpha - 0.001);

                float apertureAlpha;
                if (_ApertureEdgeSoftnessRad <= 1e-6)
                {
                    apertureAlpha = step(
                        input.displayedAngle,
                        _ApertureHalfAngleRad);
                }
                else
                {
                    // Im Softness-Bereich nimmt die Deckkraft bis zum Rand glatt ab.
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
                    input.color.a * apertureAlpha * dotAlpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
