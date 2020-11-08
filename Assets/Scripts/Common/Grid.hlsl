#ifndef GRID_INCLUDED
#define GRID_INCLUDED

#include "Assets/Scripts/Common/Constant.hlsl"

cbuffer grid
{
	float4 _Start;
	uint _DimX;
	uint _DimY;
	uint _DimZ;
	float _H;
};

inline bool Is2D()
{
	return _DimZ == 1;
}
void Set3DZero(inout float3x3 mat)
{
	mat[2] = mat[0][2] = mat[1][2] = 0;
}
inline float N(float x)
{
    x = abs(x);

    if (x < 0.5f) return 0.75f - x * x;
    if (x < 1.5f) return 0.5f * (1.5f - x) * (1.5f - x);
    return 0;
}

inline float DevN(float x)
{
    float absx = abs(x);
    if (absx < 0.5f) return -2 * x;
    if (absx < 1.5f) return x > 0 ? absx - 1.5f : -(absx - 1.5f);
    return 0;
}

inline float3x3 InvD()
{
	return 4.0f * Identity3x3 * _H * _H;
}
inline uint3 PPosToCIndex(float3 pos)
{
	return uint3(pos-_Start.xyz);
}

inline uint CIndexToCDIndex(uint3 idx)
{
	return idx.x + idx.y * _DimX + idx.z * _DimX * _DimY;
}

inline uint3 CDIndexToCIndex(uint idx)
{
	uint z = idx/(_DimX * _DimY);
	uint xy = idx%(_DimX * _DimY);

	return uint3(xy%_DimX, xy/_DimX, z);
}

inline float3 CIndexToCPos(uint3 idx)
{
	return _Start.xyz + (idx + 0.5f) * _H;
}

inline bool InGrid(uint3 idx)
{
	uint cdid = CIndexToCDIndex(idx);
	return 0<= cdid && cdid < _DimX * _DimY *_DimZ;
}
inline float GetWeightWithCell(float3 pos, int3 cellIndex)
{
	if (!InGrid(cellIndex)) return 0;

	float3 gpos = CIndexToCPos(cellIndex);
	float3 dis = pos - gpos;
	float3 invH = 1.0f / _H;
	dis *= invH;

	return  N(dis.x) * N(dis.y) *(Is2D()?1: N(dis.z));
}
inline float GetWeight(float3 pos, int3 delta)
{
	int3 gindex = PPosToCIndex(pos) + delta;
	if (!InGrid(gindex)) return 0;

	float3 gpos = CIndexToCPos(gindex);
	float3 dis = pos - gpos;
	float3 invH = 1.0f / _H;
	dis *= invH;

	return  N(dis.x) * N(dis.y) *(Is2D()?1: N(dis.z));
}
inline float3 GetWeightGradient(float3 pos, int3 delta)
{
	int3 gindex = PPosToCIndex(pos) + delta;
	if (!InGrid(gindex)) return 0;

	float3 gpos = CIndexToCPos(gindex);
	float3 dis = pos - gpos;
	float3 invH = 1.0f / _H;
	dis *= invH;

	float wx = N(dis.x);
	float wy = N(dis.y);
	float wz = Is2D()?1:N(dis.z);

	float wdx = DevN(dis.x);
	float wdy = DevN(dis.y);
	float wdz = Is2D()?0:DevN(dis.z);

	return invH * float3(wdx * wy * wz, wx * wdy * wz, wx * wy * wdz);
}
#endif