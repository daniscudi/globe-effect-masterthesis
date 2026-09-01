Shader "GlobeEffect/Merlitz Random Dots"
{
    Properties
    {
        _ApertureHalfAngleRad ("Aperture Half Angle [rad]", Float) = 0.610865
        _MerlitzK ("Merlitz k", Range(0, 1)) = 0.7
        _Magnification ("Magnification", Float) = 10
        _EyeMode ("Eye Mode", Float) = 0
        _DotHalfSizeRad ("Dot Half Size [rad]", Float) = 0.00191986
        [HideInInspector] _SimulatedYawRad ("Simulated Yaw [rad]", Float) = 0
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
                float apparentAngle : TEXCOORD1;
                float validProjection : TEXCOORD2;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _ApertureHalfAngleRad;
            float _MerlitzK;
            float _Magnification;
            float _EyeMode;
            float _DotHalfSizeRad;
            float _SimulatedYawRad;
            float4 _ObserverWorldPosition;
            float4 _ObserverWorldRight;

            float ApparentAngleFromObject(float objectAngle)
            {
                // Hier wird die Merlitz-Gleichung vorwärts verwendet: Aus dem
                // ursprünglichen Winkel A wird der sichtbare Winkel a. Für k gegen
                // null gilt direkt a = m A, damit nicht durch k geteilt werden muss.
                if (_MerlitzK < 1e-5)
                {
                    return _Magnification * objectAngle;
                }

                return atan(_Magnification * tan(_MerlitzK * objectAngle))
                    / _MerlitzK;
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

                float4 worldPosition = mul(unity_ObjectToWorld, input.vertex);
                float3 viewPosition = mul(UNITY_MATRIX_V, worldPosition).xyz;

                // Mit SimulatedYaw kann die Bewegung ohne Headset in Unity geprüft
                // werden. Im echten Versuch ist dieser Winkel null; dann kommt die
                // gesamte Bewegung ausschließlich von der getrackten HMD-Pose.
                float cosine = cos(_SimulatedYawRad);
                float sine = sin(_SimulatedYawRad);
                viewPosition.xz = float2(
                    cosine * viewPosition.x + sine * viewPosition.z,
                    -sine * viewPosition.x + cosine * viewPosition.z);

                float forwardDistance = -viewPosition.z;
                float validFront = step(1e-4, forwardDistance);
                float2 objectPosition = viewPosition.xy / max(forwardDistance, 1e-4);
                float objectRadius = length(objectPosition);
                float objectAngle = atan(objectRadius);
                float apparentAngle = ApparentAngleFromObject(objectAngle);
                float validAngle = step(apparentAngle, 1.560796);

                float apparentRadius = tan(min(apparentAngle, 1.560796));
                float radialScale = objectRadius > 1e-6
                    ? apparentRadius / objectRadius
                    : _Magnification;
                float2 displayedPosition = objectPosition * radialScale;
                viewPosition.xy = displayedPosition * forwardDistance;

                // Zuerst wird die Position des Punktes mit Merlitz verschoben. Erst
                // danach wird um diese Position die kleine runde Punktfläche gebaut.
                // Dadurch bleibt die sichtbare Punktgröße bei allen k-Werten gleich;
                // nur die Position beziehungsweise die Punktbahn ändert sich.
                float angularHalfSize = _DotHalfSizeRad * input.sizeData.x;
                viewPosition.xy += input.uv * forwardDistance * tan(angularHalfSize);

                output.position = mul(UNITY_MATRIX_P, float4(viewPosition, 1.0));
                output.dotUv = input.uv;
                output.apparentAngle = apparentAngle;
                output.validProjection = validFront * validAngle;
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
                clip(_ApertureHalfAngleRad - input.apparentAngle);
                clip(1.0 - length(input.dotUv));
                return input.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
