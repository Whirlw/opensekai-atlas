Shader "Sekai/Live/Note/Slide"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _NextRushPalette ("NextRUSH Palette", Color) = (0,0,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
        _NoteShowRate ("NoteShowRate", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "NextRushPalette.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                // x: u * q, y: v, z: q (see vert). Dividing x by z per fragment keeps the texture straight.
                float3 uvq : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _NoteShowRate;

            // Lane geometry (LiveConfig): notes travel from SpawnPosition (0, 5.46) to the judge line at
            // y -2.96 and are scaled by their progress, so every lane edge is a ray from (0, 5.46). A note
            // centred at depth d = progress * 8.42 below that point should have each of its points on the
            // ray through it: x' = x * (5.46 - y) / d. With y = centre + progress * ly (ly in the sprite's own
            // units) the progress cancels: x' = x * (1 - ly / 8.42).
            static const float LaneDepth = 8.42;
            // Note sprites (notes_0..6) are 186 px tall, centred in seven 188 px cells of the atlas at
            // 108 px per unit; the vertical uv therefore gives the vertex's height above the note centre.
            static const float AtlasCells = 7.0;
            static const float CellPixels = 188.0;
            static const float PixelsPerUnit = 108.0;

            v2f vert(appdata v)
            {
                // Sprites may be batched into world space; work in world space either way.
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                float ly = (frac(v.uv.y * AtlasCells) - 0.5) * CellPixels / PixelsPerUnit;
                float q = max(1.0 - ly / LaneDepth, 1e-4);
                world.x *= q;

                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(world.xyz, 1.0));
                o.color = fixed4(_Color.rgb, v.color.a * _Color.a);
                // The squeeze is bilinear (it grows with both x and height), which two affine triangles per
                // quad cannot map: the texture bent along each quad's diagonal and the note's middle drifted
                // sideways. u * q and q are both linear on screen, so interpolating them and dividing is exact.
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvq = float3(uv.x * q, uv.y, q);
                o.screenPosition = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenUv = i.screenPosition.xy / i.screenPosition.w;
                clip(_NoteShowRate - screenUv.y);

                fixed4 tex = tex2D(_MainTex, float2(i.uvq.x / i.uvq.z, i.uvq.y));
                tex.rgb = nextRushColor(tex.rgb);
                fixed alpha = tex.a * i.color.a;
                return fixed4(tex.rgb * alpha, tex.a);
            }
            ENDCG
        }
    }
}
