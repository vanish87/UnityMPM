
//aligned by float4(16 bytes)
struct ParticleData
{
    int type;
    float mass;
    float volume;
    float3 position;
    float3 velocity;
    float3x3 C;
    float3x3 Fe;
    float Jp;
};


struct CellData
{
    float mass;
    float3 mv;
    float3 vel;
    float3 force;

    float2 padding;
};


