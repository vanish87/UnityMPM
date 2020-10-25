using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityTools.Common;

namespace UnityMPM
{
    public class ParticleRender : MonoBehaviour
    {
        public Material mat;
        public DisposableMaterial material;
        public ComputeBuffer buffer;
        public int size;

        public float psize = 0.01f;

        protected void OnEnable()
        {
            this.material = new DisposableMaterial(this.mat);
        }
        protected void OnDisable()
        {
            this.material.Dispose();
        }
        protected void OnRenderObject()
        {
            var inverseViewMatrix = Camera.main.worldToCameraMatrix.inverse;

            var m = this.material.Data;

            m.SetPass(0);
            m.SetMatrix("_InvViewMatrix", inverseViewMatrix);
            m.SetFloat("_ParticleSize", psize);
            m.SetBuffer("_ParticleBuffer", this.buffer);

            Graphics.DrawProceduralNow(MeshTopology.Points, this.size);
        }
    }
}