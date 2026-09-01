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
        [HideInInspector] _ObserverWorldPosition ("Observer World Position", Vector) = (0, 0, 0, 1)
        [HideInInspector] _ObserverWorldRight ("Observer World Right", Vector) = (1, 0, 0, 0)
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
            float4 _ObserverWorldPosition;
            float4 _ObserverWorldRight;

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
                // Diese Funktion löst die Merlitz-Gleichung rückwärts. Aus dem
                // sichtbaren Winkel wird also wieder der ursprüngliche Winkel.
                // Für k gegen null gilt direkt A = a / m. Dieser eigene Fall
                // verhindert außerdem eine Division durch einen sehr kleinen Wert.
                if (_MerlitzK < 1e-5)
                {
                    return apparentAngle / _Magnification;
                }

                return atan(tan(_MerlitzK * apparentAngle) / _Magnification)
                    / _MerlitzK;
            }

            float ResolveEyeIndex()
            {
                // In den normalen Stereo-Modi teilt Unity direkt mit, für welches
                // Auge gerade gerendert wird. Bei Varjo Multi Pass war dieser Wert
                // nicht in jedem einzelnen Focus-Pass zuverlässig. In diesem Fall
                // wird links oder rechts aus der Position der Renderkamera relativ
                // zur Center-Eye-Pose bestimmt.
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

            fixed4 Frag(VertexToFragment input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 0 = links, 1 = rechts. Im normalen Game View verhält sich
                // die Vorschau wie das linke Auge.
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

                // displayPosition liegt im fertigen Kreis. Die Mitte ist (0,0),
                // der Kreisrand liegt bei einem Radius von 1.
                //
                // Jetzt wird rückwärts berechnet, welche Stelle des ursprünglich
                // geraden Schachbretts zu diesem sichtbaren Pixel gehört:
                // 1. sichtbaren Radius in einen sichtbaren Winkel umrechnen,
                // 2. Merlitz-Gleichung rückwärts anwenden,
                // 3. Ergebnis wieder auf den Bereich von Mitte bis Rand skalieren.
                float tangentAtBoundary = tan(_ApparentHalfAngleRad);
                float apparentAngle = atan(displayRadius * tangentAtBoundary);
                float objectAngle = ObjectAngleFromApparent(apparentAngle);
                float maximumObjectAngle = ObjectAngleFromApparent(
                    _ApparentHalfAngleRad);
                float sourceRadius = tan(objectAngle) / tan(maximumObjectAngle);

                float2 radialDirection = displayRadius > 1e-6
                    ? displayPosition / displayRadius
                    : float2(1.0, 0.0);
                // Die Richtung von der Mitte zum Pixel bleibt gleich. Nur der
                // Abstand zur Mitte wird durch die Merlitz-Abbildung verändert.
                float2 sourcePosition = radialDirection * sourceRadius;

                // Das Sinusprodukt wechselt an jeder ganzzahligen Gitterlinie
                // sein Vorzeichen. Dadurch entstehen abwechselnd schwarze und
                // weiße Felder, ohne dass eine Bildtextur benötigt wird. fwidth
                // glättet nur die Pixelkante und verändert nicht die eigentliche
                // Form des Musters.
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

                // Das Fixationskreuz wird über seinen Winkel definiert. Deshalb
                // erscheint es bei einem anderen Abstand oder FOV weiterhin gleich
                // groß und wächst nicht einfach mit der physischen Fläche mit.
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
