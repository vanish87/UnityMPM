using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.ComputeShaderTool;
using static UnityTools.Rendering.PositionRender;

namespace UnityMPM
{
    public class APIC : GPUParticleBase<APIC.ParticleAPIC>
    {
        [StructLayout(LayoutKind.Sequential, Size = 48)]
        public class Cell : IDrawGizmos
        {
            public float mass;
            public float3 mv;
            public float3 vel;
            public float3 force;

            public float2 padding;

            public void OnDrawGizmos(Matrix4x4 parent)
            {
                Gizmos.DrawSphere(Vector3.zero, this.mass);
            }
        }
        [StructLayout(LayoutKind.Sequential, Size = 68)]
        public class ParticleAPIC : AlignedGPUData, IPosition
        {
            public bool active;
            public float mass;
            public float3 pos;
            public float3 vel;
            //32

            public float3x3 B;

            public float3 Pos => this.pos;
        }
        
        protected Grid<Cell> grid;

        [StructLayout(LayoutKind.Sequential, Size = 256)]
        public class ParticleMPM
        {
            public bool active;
            public float mass;
            public float volume;
            public float3 pos;
            //24
            public float3 vel;
            //36
            public float2x2 B;
            //52
            public float2x2 D;
            //68
            public float2x2 Fe;
            //84
            public float2x2 Fp;
            //100
            public float3x3 weight;
            //136
            public float3x3 weightGradientX;
            public float3x3 weightGradientY;
            public float3x3 weightGradientZ;
            //136+108 = 244
            public float3 padding;
        }
    }
}