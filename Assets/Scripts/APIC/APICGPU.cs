using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.ComputeShaderTool;
using UnityTools.Debuging;
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

        [System.Serializable]
        public class APICGPUParameterContainer : ComputeShaderParameterContainer
        {
            public ComputeShaderParameterBuffer gridBuffer = new ComputeShaderParameterBuffer("_Grid");
            public ComputeShaderParameterBuffer newParticleBuffer = new ComputeShaderParameterBuffer("_CPUNewParticles");
            public ComputeShaderParameterVector start = new ComputeShaderParameterVector("_Start");
            public ComputeShaderParameterInt dimx = new ComputeShaderParameterInt("_DimX", 32);
            public ComputeShaderParameterInt dimy = new ComputeShaderParameterInt("_DimY", 32);
            public ComputeShaderParameterInt dimz = new ComputeShaderParameterInt("_DimZ", 1);
            public ComputeShaderParameterFloat h = new ComputeShaderParameterFloat("_H", 1);
            public ComputeShaderParameterFloat dt = new ComputeShaderParameterFloat("_DT", 0.01f);

        }

        [SerializeField]
        protected APICGPUParameterContainer apicParameter = new APICGPUParameterContainer();

        protected Grid<Cell> grid;


        protected override void OnEnable()
        {
            LogTool.AssertNotNull(this.cs);

            this.dispather = new ComputeShaderDispatcher(this.cs);
            //init append buffer
            this.dispather.AddParameter("InitParticle", this.bufferParameter);

            this.dispather.AddParameter("P2G", this.parameter);
            this.dispather.AddParameter("P2G", this.bufferParameter);
            this.dispather.AddParameter("P2G", this.apicParameter);

            this.dispather.AddParameter("UpdateGrid", this.parameter);
            this.dispather.AddParameter("UpdateGrid", this.bufferParameter);
            this.dispather.AddParameter("UpdateGrid", this.apicParameter);

            this.dispather.AddParameter("G2P", this.parameter);
            this.dispather.AddParameter("G2P", this.bufferParameter);
            this.dispather.AddParameter("G2P", this.apicParameter);

            this.dispather.AddParameter("InitGrid", this.apicParameter);


            this.dispather.AddParameter("AddParticles", this.apicParameter);
            this.dispather.AddParameter("AddParticles", this.bufferParameter);


            this.ResizeBuffer(this.parameter.numberOfParticles.Value);

            // this.Emit(512);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            this.apicParameter.ReleaseBuffer();
        }

        protected override void OnResetParticlesData()
        {

            var size = new int3(this.apicParameter.dimx, this.apicParameter.dimy, this.apicParameter.dimz);
            var spacing = new float3(this.apicParameter.h, this.apicParameter.h, this.apicParameter.h);

            this.grid = new Grid<Cell>(size, spacing, 0);
            var gdataLength = this.grid.DataLength;
            this.apicParameter.ReleaseBuffer();
            this.apicParameter.gridBuffer.Value = new ComputeBuffer(gdataLength, Marshal.SizeOf<Cell>());

            this.bufferParameter.particlesIndexBufferActive.Value.SetCounterValue(0);
            this.dispather.Dispatch("InitParticle", this.parameter.numberOfParticles.Value);

            this.AddBox();
        }

        protected override void Update()
        {
            var gsize = this.grid.DataLength;
            var psize = this.bufferParameter.CurrentBufferLength;
            this.dispather.Dispatch("InitGrid", gsize);
            this.dispather.Dispatch("P2G", gsize);
            this.dispather.Dispatch("UpdateGrid", gsize);
            this.dispather.Dispatch("G2P", psize);

                // ComputeShaderParameterBuffer.SwapBuffer(this.bufferParameter.particlesDataBufferRead, this.bufferParameter.particlesDataBufferWrite);
        }

        protected void AddBox()
        {
            var pos = Tool.GenerateBox(new float3(10,10,10), new float3(4,4,4));
            pos.AddRange(Tool.GenerateBox(new float3(20,20,10), new float3(4,4,4)));
            this.apicParameter.newParticleBuffer.Value = new ComputeBuffer(pos.Count, Marshal.SizeOf<float3>());


            this.parameter.activeNumberOfParticles.Value += pos.Count;
            this.apicParameter.newParticleBuffer.Value.SetData(pos.ToArray());
            this.dispather.Dispatch("AddParticles", pos.Count);

        }

    }
}