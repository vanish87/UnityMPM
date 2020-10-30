using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using static UnityTools.Rendering.PositionRender;

namespace UnityMPM
{
    public class MPMGPU : MonoBehaviour
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
            }
        }
        [StructLayout(LayoutKind.Sequential, Size = 192)]
        public class Particle : IPosition
        {
            public bool active;
            public float mass;
            public float volume;
            public float3 pos;
            //24
            public float3 vel;
            //36
            public float3x3 B;
            //72
            public float3x3 D;
            //108
            public float3x3 Fe;
            //144
            public float3x3 Fp;
            //180
            public float3 padding;

            public float3 Pos => this.pos;
        }
        
    }
}