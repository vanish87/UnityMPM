#ifndef MATH_INCLUDED
#define MATH_INCLUDED

float3x3 Math_OuterProduct(float3 lhs, float3 rhs)
{
	return float3x3(lhs[0] * rhs[0], lhs[0] * rhs[1], lhs[0] * rhs[2],
					lhs[1] * rhs[0], lhs[1] * rhs[1], lhs[1] * rhs[2],
					lhs[2] * rhs[0], lhs[2] * rhs[1], lhs[2] * rhs[2]);
}
float3x3 Math_OuterProduct2D(float3 lhs, float3 rhs)
{
	return float3x3(lhs[0] * rhs[0], lhs[0] * rhs[1], 0,
					lhs[1] * rhs[0], lhs[1] * rhs[1], 0,
					0,0,0);
}
float2x2 inverse(float2x2 m)
{
    return 1.0f / determinant(m) *
					float2x2(
						 m[0][0], -m[0][1], 
						-m[1][0],  m[0][0]
					);
}

float3x3 inverse(float3x3 m)
{
	float det = determinant(m);
	if(det == 0) return 0;
	return 1.0f / det *
                    float3x3(
                          m[1][1] * m[2][2] - m[1][2] * m[2][1],
                        -(m[0][1] * m[2][2] - m[0][2] * m[2][1]),
                          m[0][1] * m[1][2] - m[0][2] * m[1][1],
						  
                        -(m[1][0] * m[2][2] - m[1][2] * m[2][0]),
                          m[0][0] * m[2][2] - m[0][2] * m[2][0],
                        -(m[0][0] * m[1][2] - m[0][2] * m[1][0]),
						
                          m[1][0] * m[2][1] - m[1][1] * m[2][0],
                        -(m[0][0] * m[2][1] - m[0][1] * m[2][0]),
                          m[0][0] * m[1][1] - m[0][1] * m[1][0]
                    );
}

float3x3 ToIdentity(float3 diagnal)
{
	return float3x3(diagnal[0],0,0,0,diagnal[1],0,0,0,diagnal[2]);
}
float3x3 ToIdentity(float diagnal)
{
	return float3x3(diagnal,0,0,0,diagnal,0,0,0,diagnal);
}

float2x2 ToIdentity(float2 diagnal)
{
	return float2x2(diagnal[0],0,0,diagnal[1]);
}

inline float3x3 To3D(float2x2 m)
{
	return float3x3(m[0][0], m[0][1], 0,
					m[1][0], m[1][1], 0,
					0,		 0,		  1);
}

inline float2x2 To2D(float3x3 m)
{
	return float2x2(m[0][0],m[0][1],m[1][0],m[1][1]);
}
#endif