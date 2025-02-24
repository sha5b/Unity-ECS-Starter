Shader "Custom/TerrainShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PlainsColor ("Plains Color", Color) = (0.4, 0.8, 0.4, 1)
        _ForestColor ("Forest Color", Color) = (0.2, 0.6, 0.2, 1)
        _MountainColor ("Mountain Color", Color) = (0.6, 0.6, 0.6, 1)
        _DesertColor ("Desert Color", Color) = (0.9, 0.8, 0.5, 1)
        _BlendDistance ("Blend Distance", Float) = 1.0
        _HeightBlend ("Height Blend", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _PlainsColor;
            float4 _ForestColor;
            float4 _MountainColor;
            float4 _DesertColor;
            float _BlendDistance;
            float _HeightBlend;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 BlendBiomeColors(float4 baseColor, float height)
            {
                // Get biome weights from vertex color
                float plains = baseColor.r;
                float forest = baseColor.g;
                float mountain = baseColor.b;

                // Apply height-based blending
                float heightFactor = saturate((height + _HeightBlend) / (1 + _HeightBlend));
                
                // Blend colors based on height and biome weights
                float4 finalColor = float4(0,0,0,1);
                finalColor += _PlainsColor * plains * (1 - heightFactor);
                finalColor += _ForestColor * forest;
                finalColor += _MountainColor * mountain * heightFactor;

                // Normalize
                finalColor.rgb /= (plains + forest + mountain);
                
                return finalColor;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Sample base texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Get world height
                float height = i.worldPos.y;

                // Blend biome colors
                float4 biomeColor = BlendBiomeColors(i.color, height);

                // Apply normal-based shading
                float3 normal = normalize(i.normal);
                float ndotl = saturate(dot(normal, _WorldSpaceLightPos0.xyz));
                float lighting = ndotl * 0.5 + 0.5; // Soften the lighting

                // Apply lighting to final color
                return biomeColor * lighting;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
