Shader "GlobeEffect/Helmholtz Checkerboard"
{
    // Das C#-Skript liefert nur Parameter und eine quadratische Trägerfläche.
    // Dieser Shader berechnet für jeden Bildpunkt das eigentliche Muster:
    // Bildschirmposition -> Blickwinkel -> radiale l-Abbildung -> lineares u/v-Gitter
    // -> Schwarz/Weiß-Farbe -> runde Blende und Fixationskreuz.
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0, 0, 0, 1)
        _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _FixationBackgroundColor ("Fixation Background", Color) = (0.5, 0.5, 0.5, 1)
        _FixationColor ("Fixation Color", Color) = (1, 0, 0, 1)
        _ApparentHalfAngleRad ("Apparent Half Angle [rad]", Float) = 0.785398
        _ApertureEdgeSoftnessRad ("Aperture Edge Softness [rad]", Float) = 0.0174533
        _UseCircularAperture ("Use Circular Aperture", Float) = 1
        _VisualSpaceL ("Visual-space l", Range(0, 1.4)) = 0.5
        _GridLineSpacingUv ("Grid Line Spacing [u/v]", Float) = 0.176327
        _CheckerboardEnabled ("Checkerboard Enabled", Float) = 1
        _NoiseEnabled ("Noise Enabled", Float) = 0
        _NoiseCellSizeUv ("Noise Cell Size [u/v]", Float) = 0.0349
        _NoiseSeed ("Noise Seed", Integer) = 0
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
            #pragma target 3.5
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
            float _UseCircularAperture;
            float _VisualSpaceL;
            float _GridLineSpacingUv;
            float _CheckerboardEnabled;
            float _NoiseEnabled;
            float _NoiseCellSizeUv;
            int _NoiseSeed;
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

                // Aus der Stelle auf dem Träger wird eine Richtung relativ zur
                // aktuellen Kopfpose. Deshalb bleibt das Bild kopffest und verhält
                // sich nicht wie eine nahe Ebene im Unity-Raum.
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

            uint MixNoiseBits(uint value)
            {
                // Mischt die Bits einer ganzen Zahl. Kleine Änderungen am Eingang
                // ergeben dadurch eine andere Verteilung von Schwarz und Weiß.
                // Die Überläufe sind hier Absicht: Es bleiben immer 32 Bits übrig.
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                return value ^ (value >> 16);
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

                float2 displayPosition = input.uv * 2.0 - 1.0;
                float displayRadius = length(displayPosition);

                // displayRadius ist linear auf dem quadratischen Träger. atan
                // macht daraus den tatsächlichen Winkel zur Bildmitte.
                float tangentAtBoundary = tan(_ApparentHalfAngleRad);
                float visualAngle = atan(displayRadius * tangentAtBoundary);

                // Das Gitter wird weiterhin auf einem quadratischen Träger
                // berechnet. Diese unabhängige Kreisblende entscheidet erst
                // danach, welcher Teil davon sichtbar ist. Die Softness gibt
                // an, über wie viele Winkelgrad der Rand nach innen ausblendet.
                float apertureAlpha = 1.0;
                if (_UseCircularAperture > 0.5 &&
                    _ApertureEdgeSoftnessRad <= 1e-6)
                {
                    apertureAlpha = step(
                        visualAngle,
                        _ApparentHalfAngleRad);
                }
                else if (_UseCircularAperture > 0.5)
                {
                    float fadeStart = max(
                        0.0,
                        _ApparentHalfAngleRad - _ApertureEdgeSoftnessRad);
                    apertureAlpha = 1.0 - smoothstep(
                        fadeStart,
                        _ApparentHalfAngleRad,
                        visualAngle);
                }
                // Ohne Kreisblende bleibt apertureAlpha bei 1 und das ganze
                // quadratische Gitter wird sichtbar. Das ist nur zur Kontrolle
                // gedacht; im eigentlichen Versuch bleibt die Öffnung aktiv.
                clip(apertureAlpha - 0.001);

                float sourceRadius;
                if (_VisualSpaceL < 1e-6)
                {
                    // Grenzfall der Visual-Space-Funktion für l gegen null.
                    sourceRadius = visualAngle / _ApparentHalfAngleRad;
                }
                else
                {
                    // Das ist die im Projekt benutzte, am Blendenrand normierte
                    // Visual-Space-Abbildung:
                    // s = tan(l * Winkel) / tan(l * halber FOV-Winkel).
                    // l ist die vorgegebene Bedingung und wird nicht aus Zoom berechnet.
                    sourceRadius = tan(_VisualSpaceL * visualAngle) /
                        tan(_VisualSpaceL * _ApparentHalfAngleRad);
                }

                float2 radialDirection = displayRadius > 1e-6
                    ? displayPosition / displayRadius
                    : float2(1.0, 0.0);
                float2 sourcePosition = radialDirection * sourceRadius;

                // sourcePosition liegt im linearen u/v-Koordinatensystem des
                // unverzerrten Ausgangsgitters. Die feste Gitterweite wird aus
                // dem im Inspector eingestellten Winkelabstand berechnet. l
                // verändert damit nur die radiale Abtastposition und nicht
                // zusätzlich den Aufbau des zugrunde liegenden Schachbretts.
                float2 gridPosition = sourcePosition /
                    max(_GridLineSpacingUv, 1e-6);
                float checkerSignal = sin(UNITY_PI * gridPosition.x)
                    * sin(UNITY_PI * gridPosition.y);
                // Das Vorzeichen wechselt an jeder Gitterlinie. Dadurch entstehen
                // abwechselnd schwarze und weiße Felder, ohne eine Bilddatei zu laden.
                float antialiasWidth = max(fwidth(checkerSignal), 1e-4);
                float checkerMix = smoothstep(
                    -antialiasWidth,
                    antialiasWidth,
                    checkerSignal);
                fixed4 checkerColor = lerp(
                    _DarkColor,
                    _LightColor,
                    checkerMix);

                // Für die kurze Maske wird die angezeigte Fläche in kleine Zellen
                // geteilt. Aus Zellkoordinate und Seed entsteht reproduzierbar ein
                // schwarzes oder weißes Feld. Das ist keine weitere l-Verzerrung.
                int2 noiseCell = (int2)floor(
                    displayPosition / max(_NoiseCellSizeUv, 1e-6));
                // Früher wurde der große Seed direkt in eine Sinusrechnung
                // eingesetzt. Dabei gingen Unterschiede zwischen Zellen verloren;
                // je nach Grafikkarte konnte die Maske fast oder ganz schwarz werden.
                // Jetzt bleiben Seed und Zellnummern ganze Zahlen. Auch negative
                // Zellnummern werden anhand ihrer Bits eindeutig mit einbezogen.
                uint noiseBits = MixNoiseBits(asuint(noiseCell.x) ^ asuint(_NoiseSeed));
                noiseBits = MixNoiseBits(noiseBits ^ asuint(noiseCell.y) ^ 0x9e3779b9u);
                // Das oberste Bit entscheidet: 0 = dunkel, 1 = hell.
                float noiseValue = (float)(noiseBits >> 31);
                fixed4 noiseColor = lerp(
                    _DarkColor,
                    _LightColor,
                    step(0.5, noiseValue));

                fixed4 visiblePattern = lerp(
                    checkerColor,
                    noiseColor,
                    step(0.5, _NoiseEnabled));
                fixed4 color = lerp(
                    _FixationBackgroundColor,
                    visiblePattern,
                    step(0.5, max(_CheckerboardEnabled, _NoiseEnabled)));

                float2 angularPosition = atan(displayPosition * tangentAtBoundary);
                // Das Kreuz wird erst ganz am Ende ergänzt. Es bleibt dadurch in
                // der Mitte und wird nicht zusammen mit dem Schachbrett verzerrt.
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
