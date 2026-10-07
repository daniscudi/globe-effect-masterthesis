Shader "GlobeEffect/Diagnostic Flow Overlay"
{
    Properties
    {
        _FlowMap ("Flow map", 2D) = "black" {}
        _TanHalfField ("Field radius in image coordinates", Float) = 0.57735
        _HalfFieldRad ("Half field angle", Float) = 0.523599
        _SoftnessRad ("Edge softness", Float) = 0.0174533
        _Arrows ("Draw sharp arrows instead of heatmap", Float) = 0
        _ArrowWidth ("Arrow shaft width", Float) = 0.003
        _HeadHalfWidth ("Arrow head half width", Float) = 0.012
        _ArrowColor ("Arrow color", Color) = (0.05, 0.05, 0.05, 0.95)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
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
                float2 arrowDimensions : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 fieldPosition : TEXCOORD1;
                float2 arrowDimensions : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _FlowMap;
            float _TanHalfField;
            float _HalfFieldRad;
            float _SoftnessRad;
            float _Arrows;
            float _ArrowWidth;
            float _HeadHalfWidth;
            fixed4 _ArrowColor;
            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Festes Diagnoseblatt in Blickrichtungen: wie der Originalhintergrund,
                // keine augenabhängige nahe Leinwand und keine zusätzliche k-Abbildung.
                output.position = mul(UNITY_MATRIX_P,
                    float4(input.vertex.xy * _TanHalfField, -1.0, 1.0));
                output.uv = input.uv;
                output.fieldPosition = input.vertex.xy;
                output.arrowDimensions = input.arrowDimensions;
                return output;
            }

            float ArrowCoverage(float2 localPosition, float2 dimensions)
            {
                float arrowLength = dimensions.x;
                float headLength = dimensions.y;
                float join = arrowLength - headLength;
                // Schaft und Spitze überlappen, damit ihre geglätteten Kanten
                // keine sichtbare Naht an der Verbindungsstelle erzeugen.
                float shaft = max(abs(localPosition.y) - 0.5 * _ArrowWidth,
                    max(-localPosition.x, localPosition.x - (join + 0.5 * headLength)));
                float slope = _HeadHalfWidth / max(headLength, 1e-6);
                float headSide = (abs(localPosition.y) - slope * (arrowLength - localPosition.x))
                    / sqrt(1.0 + slope * slope);
                float head = max(headSide, max(join - localPosition.x, localPosition.x - arrowLength));
                float distance = min(shaft, head);
                // Bildschirmabhängige Kantenglättung: unabhängig von der 192px-Heatmap.
                float pixel = max(fwidth(distance), 1e-6);
                return 1.0 - smoothstep(-0.5 * pixel, 0.5 * pixel, distance);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float angle = atan(length(input.fieldPosition * _TanHalfField));
                clip(_HalfFieldRad - angle);
                fixed4 color;
                if (_Arrows > 0.5)
                {
                    color = _ArrowColor;
                    color.a *= ArrowCoverage(input.uv, input.arrowDimensions);
                }
                else color = tex2D(_FlowMap, input.uv);
                if (_SoftnessRad > 1e-6)
                    color.a *= 1.0 - smoothstep(
                        max(0.0, _HalfFieldRad - _SoftnessRad), _HalfFieldRad, angle);
                return color;
            }
            ENDCG
        }
    }
}
