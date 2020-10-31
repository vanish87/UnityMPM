
//aligned by float4(16 bytes)
struct ParticleData
{
    bool active;
    float mass;
    float3 position;
    float3 velocity;
    //32

    float3x3 B;
};


struct CellData
{
    float mass;
    float3 mv;
    float3 vel;
    float3 force;

    float2 padding;
};


