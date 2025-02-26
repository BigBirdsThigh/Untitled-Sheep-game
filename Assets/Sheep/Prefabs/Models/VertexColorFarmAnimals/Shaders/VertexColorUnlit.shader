Shader "VertexColorFarmAnimals/VertexColorUnlit" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _RedBoost ("Red Boost", Range(0, 1)) = 0 // Controls how much red is added
    }

    SubShader {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for instancing
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float, _RedBoost) // Instanced property for red effect
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            v2f vert (appdata_t v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float redBoost = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RedBoost);

                // Original vertex color
                fixed4 originalColor = i.color;

                // Boost red by blending towards red (1,0,0)
                fixed4 redTint = fixed4(1, 0, 0, 1);
                i.color = lerp(originalColor, redTint, redBoost); // Interpolate towards red

                return i.color;
            }

            ENDCG
        }
    }
}
