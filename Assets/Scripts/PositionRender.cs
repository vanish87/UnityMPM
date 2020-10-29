using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Common;

namespace UnityTools.Rendering
{
    public class PositionRender : MonoBehaviour
    {
        public interface IPosition
        {
            float3 Pos { get; }
        }
        public Material mat;
        public float psize = 0.01f;

        protected DisposableMaterial material;
        protected IPosition[] cpuData;
        protected float3[] positionData;
        protected ComputeBuffer buffer;


        public void Init(List<IPosition> pos)
        {
            this.CleanUp();
            this.cpuData = pos.ToArray();
            this.positionData = new float3[this.cpuData.Length];
            this.buffer = new ComputeBuffer(this.cpuData.Length, Marshal.SizeOf<float3>());
        }

        protected void ToGPUData()
        {
            foreach(var i in Enumerable.Range(0, this.cpuData.Length))
            {
                this.positionData[i] = this.cpuData[i].Pos;
            }
            this.buffer.SetData(this.positionData);
        }

        protected void CleanUp()
        {
            this.buffer?.Release();
        }

        protected void OnEnable()
        {
            this.material = new DisposableMaterial(this.mat);
        }
        protected void OnDisable()
        {
            this.CleanUp();
            this.material.Dispose();
        }
        protected void OnRenderObject()
        {
            this.ToGPUData();

            var inverseViewMatrix = Camera.main.worldToCameraMatrix.inverse;

            var m = this.material.Data;

            m.SetPass(0);
            m.SetMatrix("_InvViewMatrix", inverseViewMatrix);
            m.SetFloat("_ParticleSize", psize);
            m.SetBuffer("_ParticleBuffer", this.buffer);

            Graphics.DrawProceduralNow(MeshTopology.Points, this.cpuData.Length);
        }
    }
}