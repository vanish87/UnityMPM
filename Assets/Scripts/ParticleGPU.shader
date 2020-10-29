Shader "ParticleRenderGPU"
{
	CGINCLUDE

	struct v2g
	{
		float3 position : TEXCOORD0;
		float4 color    : COLOR;
	};
	struct g2f
	{
		float4 position : POSITION;
		float2 texcoord : TEXCOORD0;
		float4 color    : COLOR;
	};
    struct ParticleData
    {
        float mass;
        float volume;
        float4 color;

        float2 pos;
        float2 vel;

        float2x2 B;
        float2x2 D;

        float2x2 Fe;
        float2x2 Fp;

        float3x3 Weight;

        float2 dw00; float2 dw01; float2 dw02;
        float2 dw10; float2 dw11; float2 dw12;
        float2 dw20; float2 dw21; float2 dw22;

    };

	StructuredBuffer<ParticleData> _ParticleBuffer;
	sampler2D _MainTex;
	float4    _MainTex_ST;
	float     _ParticleSize;
	float4x4  _InvViewMatrix;
	static const float3 g_positions[4] =
	{
		float3(-1, 1, 0),
		float3(1, 1, 0),
		float3(-1,-1, 0),
		float3(1,-1, 0),
	};
	static const float2 g_texcoords[4] =
	{
		float2(0, 0),
		float2(1, 0),
		float2(0, 1),
		float2(1, 1),
	};

	// --------------------------------------------------------------------
	// Vertex Shader
	// --------------------------------------------------------------------
	v2g vert(uint id : SV_VertexID) // SV_VertexID:
	{
		v2g o = (v2g)0;
		o.position = float3(_ParticleBuffer[id].pos.xy,0);
		o.color = _ParticleBuffer[id].color;// float4(0.5 + 0.5 * normalize(_ParticleBuffer[id].velocity), 1.0);
		return o;
	}

	// --------------------------------------------------------------------
	// Geometry Shader
	// --------------------------------------------------------------------
	[maxvertexcount(4)]
	void geom(point v2g In[1], inout TriangleStream<g2f> SpriteStream)
	{
		g2f o = (g2f)0;
		[unroll]
		for (int i = 0; i < 4; i++)
		{
			float3 position = g_positions[i] * _ParticleSize;
			position = mul(_InvViewMatrix, position) + In[0].position;
			o.position = UnityObjectToClipPos(float4(position, 1.0));

			o.color = In[0].color;
			o.texcoord = g_texcoords[i];

			SpriteStream.Append(o);
		}

		SpriteStream.RestartStrip();
	}

	// --------------------------------------------------------------------
	// Fragment Shader
	// --------------------------------------------------------------------
	fixed4 frag(g2f i) : SV_Target
	{
		return tex2D(_MainTex, i.texcoord.xy) * i.color;
	}
	ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		LOD 100

		ZWrite Off
		Blend One One

		Pass
		{
			CGPROGRAM
			#pragma target   5.0
			#pragma vertex   vert
			#pragma geometry geom
			#pragma fragment frag
			ENDCG
		}
	}
}


