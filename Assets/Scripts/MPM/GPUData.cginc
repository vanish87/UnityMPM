
//aligned by float4(16 bytes)
struct ParticleData
{
    int type;
    float mass;
    float volume;
    float3 position;
    float3 velocity;
    float3x3 B;
    float3x3 D;
    float3x3 Fe;
    float3x3 Fp;

    
    
    float3 padding;
};


struct CellData
{
    float mass;
    float3 mv;
    float3 vel;
    float3 force;

    float2 padding;
};


