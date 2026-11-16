// EPIC-012: the game menu is drawn on top of the world (a customer's speech bubble or a stall never hides it).
// Unlit colour (x vertex colour), optionally with the font atlas's alpha for TextMesh text. Single-pass stereo safe.
Shader "Fruktstand/MenuOverlay"
{
    Properties
    {
        _MainTex ("Texture (font atlas for text)", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 1, 1, 1)
        [Toggle] _Text ("Text: alpha from the texture", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Text;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                fixed4 c = i.color;
                if (_Text > 0.5) c.a *= t.a;
                else c *= t;
                return c;
            }
            ENDCG
        }
    }
}
