using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.ComputeShaderTool;
using static UnityTools.Rendering.PositionRender;

namespace UnityMPM
{
    public class APICGPU : GPUParticleBase<APICGPU.Particle>
    {
        [StructLayout(LayoutKind.Sequential, Size = 48)]
        public class Cell : IDrawGizmos
        {
            public float mass;
            public float3 mv;
            public float3 vel;
            public float3 force;

            public float2 padding;

            public void OnDrawGizmos()
            {
                Gizmos.DrawSphere(Vector3.zero, this.mass);
                Gizmos.DrawRay(Vector3.zero, this.mv);
            }
        }
        [StructLayout(LayoutKind.Sequential, Size = 68)]
        public class Particle : AlignedGPUData, IPosition
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

        
    }
}