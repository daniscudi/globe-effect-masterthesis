Shader "GlobeEffect/Natural Panorama"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
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
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 plane : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float _HalfAngle, _Softness, _K, _M, _Zoom, _Sweep, _Axis, _Yaw;
            float _FixationSize, _FixationEnabled;
            float4 _FixationColor;
            float4 _HeadForward, _HeadRight, _HeadUp;

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Eine Leinwand für Richtungen: keine Verschiebung zwischen den Augen.
                output.plane = input.uv * tan(_HalfAngle);
                output.position = mul(UNITY_MATRIX_P, float4(output.plane, -1, 1));
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius = length(input.plane);
                float displayAngle = atan(radius);
                clip(_HalfAngle - displayAngle);
                float alpha = _Softness <= 0.000001 ? 1 :
                    1 - smoothstep(max(0, _HalfAngle - _Softness), _HalfAngle, displayAngle);

                // Inverse der Random-Dot-Abbildung: tan(k a) = m tan(k A).
                // a: angezeigter Winkel ohne Content Zoom; A: ursprünglicher Winkel.
                float a = atan(radius / _Zoom);
                clip(1.560796 - _K * a);
                float objectAngle = _K < 0.000001 ? a / _M : atan(tan(_K * a) / _M) / _K;
                float scale = radius > 0.000001 ? tan(objectAngle) / radius : 1 / (_M * _Zoom);
                float3 ray = float3(input.plane * scale, 1);

                // Rückwärts schwenken, um den passenden Ausschnitt im festen Panorama zu finden.
                float c = cos(_Sweep), s = sin(_Sweep);
                if (_Axis < 0.5) ray.xz = float2(c * ray.x + s * ray.z, -s * ray.x + c * ray.z);
                else ray.yz = float2(c * ray.y + s * ray.z, -s * ray.y + c * ray.z);
                ray = normalize(_HeadRight.xyz * ray.x + _HeadUp.xyz * ray.y + _HeadForward.xyz * ray.z);
                c = cos(_Yaw); s = sin(_Yaw);
                ray.xz = float2(c * ray.x + s * ray.z, -s * ray.x + c * ray.z);
                float2 uv = float2(atan2(ray.x, ray.z) / (2 * UNITY_PI) + 0.5,
                    asin(clamp(ray.y, -1, 1)) / UNITY_PI + 0.5);
                fixed3 color = tex2D(_MainTex, uv).rgb;
                float2 crossPosition = abs(input.plane);
                float size = tan(_FixationSize * 0.5);
                float crossMask = max(
                    step(crossPosition.x, size) * step(crossPosition.y, size * 0.15),
                    step(crossPosition.y, size) * step(crossPosition.x, size * 0.15));
                color = lerp(color, _FixationColor.rgb, crossMask * _FixationEnabled);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
