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
    public class MLSMPMGPUOpt : GPUParticleBase<MPMGPU.Particle>
    {
        public static float GetMU(float E, float nu)
        {
            return E / (2f * (1f + nu));
        }
        public static float GetLambda(float E, float nu)
        {
            return (E * nu) / ((1f + nu) * (1f - 2f * nu));
        }
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
        [StructLayout(LayoutKind.Sequential, Size = 112)]
        public class Particle : IPosition
        {
            public enum Type
            {
                Inactive = 0,
                Elastic,
                Snow,
                Liquid,
            }
            public Type type;
            public float mass;
            public float volume;
            public float3 pos;
            //24
            public float3 vel;
            //36
            public float3x3 C;
            //72
            public float3x3 F;
            //108
            public float Jp;

            public float3 Pos => this.pos;
        }
        [System.Serializable]
        public class MPMGPUParameterContainer : ComputeShaderParameterContainer
        {
            public ComputeShaderParameterBuffer gridBuffer = new ComputeShaderParameterBuffer("_Grid");
            public ComputeShaderParameterBuffer newParticleBuffer = new ComputeShaderParameterBuffer("_CPUNewParticles");
            public ComputeShaderParameterVector start = new ComputeShaderParameterVector("_Start");
            public ComputeShaderParameterInt dimx = new ComputeShaderParameterInt("_DimX", 32);
            public ComputeShaderParameterInt dimy = new ComputeShaderParameterInt("_DimY", 32);
            public ComputeShaderParameterInt dimz = new ComputeShaderParameterInt("_DimZ", 1);
            public ComputeShaderParameterFloat h = new ComputeShaderParameterFloat("_H", 1);
            public ComputeShaderParameterFloat dt = new ComputeShaderParameterFloat("_DT", 0.01f);

            public ComputeShaderParameterInt ptype = new ComputeShaderParameterInt("_ParticleType", 0);

            // Young's modulus
            public ComputeShaderParameterFloat E = new ComputeShaderParameterFloat("_E", 1.4e4f);
            // Poisson's ratio
            public ComputeShaderParameterFloat nu = new ComputeShaderParameterFloat("_nu", 0.2f);
            public ComputeShaderParameterFloat hardening = new ComputeShaderParameterFloat("_hardening", 10);
            public ComputeShaderParameterFloat mu = new ComputeShaderParameterFloat("_mu", 0);
            public ComputeShaderParameterFloat lambda = new ComputeShaderParameterFloat("_lambda", 0);


        }
        [SerializeField] protected Particle.Type type = Particle.Type.Elastic;

        [SerializeField]
        protected MPMGPUParameterContainer mpmParameter = new MPMGPUParameterContainer();
        [SerializeField]
        protected bool Is2D => this.mpmParameter.dimz == 1;

        protected Grid<Cell> grid;


        float3x3 inverse(float3x3 m)
        {
            return 1.0f / math.determinant(m) *
                    new float3x3(
                          m[1][1] * m[2][2] - m[2][1] * m[1][2],
                        -(m[1][0] * m[2][2] - m[2][0] * m[1][2]),
                          m[1][0] * m[2][1] - m[2][0] * m[1][1],
                        -(m[0][1] * m[2][2] - m[2][1] * m[0][2]),
                          m[0][0] * m[2][2] - m[2][0] * m[0][2],
                        -(m[0][0] * m[2][1] - m[2][0] * m[0][1]),
                          m[0][1] * m[1][2] - m[1][1] * m[0][2],
                        -(m[0][0] * m[1][2] - m[1][0] * m[0][2]),
                          m[0][0] * m[1][1] - m[1][0] * m[0][1]
                    );
        }
        protected void Test()
        {
            var mat = new float3x3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value,
            UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value,
            UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);

            var sysInv = math.inverse(mat);
            var newInv = inverse(mat);

            Debug.Log(sysInv - newInv);

        }

        protected override void OnEnable()
        {
            float E = this.mpmParameter.E;
            float nu = this.mpmParameter.nu;
            this.mpmParameter.mu.Value = GetMU(E, nu);
            this.mpmParameter.lambda.Value = GetLambda(E, nu);


            LogTool.AssertNotNull(this.cs);

            this.dispather = new ComputeShaderDispatcher(this.cs);
            //init append buffer
            this.dispather.AddParameter("InitParticle", this.bufferParameter);

            this.dispather.AddParameter("P2G", this.parameter);
            this.dispather.AddParameter("P2G", this.bufferParameter);
            this.dispather.AddParameter("P2G", this.mpmParameter);

            this.dispather.AddParameter("UpdateGrid", this.parameter);
            this.dispather.AddParameter("UpdateGrid", this.bufferParameter);
            this.dispather.AddParameter("UpdateGrid", this.mpmParameter);

            this.dispather.AddParameter("G2P", this.parameter);
            this.dispather.AddParameter("G2P", this.bufferParameter);
            this.dispather.AddParameter("G2P", this.mpmParameter);

            this.dispather.AddParameter("InitGrid", this.mpmParameter);


            this.dispather.AddParameter("AddParticles", this.mpmParameter);
            this.dispather.AddParameter("AddParticles", this.bufferParameter);

            this.dispather.AddParameter("CalGridDensity", this.parameter);
            this.dispather.AddParameter("CalGridDensity", this.bufferParameter);
            this.dispather.AddParameter("CalGridDensity", this.mpmParameter);

            this.dispather.AddParameter("UpdateParticleVolume", this.parameter);
            this.dispather.AddParameter("UpdateParticleVolume", this.bufferParameter);
            this.dispather.AddParameter("UpdateParticleVolume", this.mpmParameter);


            this.ResizeBuffer(this.parameter.numberOfParticles.Value);

            // this.Emit(512);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            this.mpmParameter.ReleaseBuffer();
        }

        protected override void OnResetParticlesData()
        {

            var size = new int3(this.mpmParameter.dimx, this.mpmParameter.dimy, this.mpmParameter.dimz);
            var spacing = new float3(this.mpmParameter.h, this.mpmParameter.h, this.mpmParameter.h);

            this.grid = new Grid<Cell>(size, spacing, 0);
            var gdataLength = this.grid.DataLength;
            this.mpmParameter.ReleaseBuffer();
            this.mpmParameter.gridBuffer.Value = new ComputeBuffer(gdataLength, Marshal.SizeOf<Cell>());

            this.bufferParameter.particlesIndexBufferActive.Value.SetCounterValue(0);
            this.dispather.Dispatch("InitParticle", this.parameter.numberOfParticles.Value);

            if (this.Is2D)
            {
                this.AddBox(new float3(0.4f, 0.8f, 0), new float3(0.9f, 0.5f, 0), this.type);
                this.AddBox(new float3(0.5f, 0.3f, 0), new float3(0.5f, 0.5f, 0), this.type);
                // this.AddBox(new float3(54, 45, 0), new float3(16, 8, 0)*4, this.type, 0.8f);
                // this.AddBox(new float3(30, 40, 0), new float3(10, 14, 0), this.type, 1f);

            }
            else
            {
                // this.mpmParameter.E.Value *= 0.09f;
                this.AddBox(new float3(0.5f, 0.3f, 0.5f), new float3(0.5f, 0.3f, 0.3f), this.type);
                this.AddBox(new float3(0.5f, 0.7f, 0.5f), new float3(0.7f, 0.2f, 0.2f), this.type);
                // this.AddBox(new float3(10, 10, 10), new float3(4, 4, 4)*4, this.type, 0.7f);
                // this.AddBox(new float3(10, 25, 10), new float3(8, 3, 4)*2, this.type, 0.7f);
                // this.AddBox(new float3(20, 20, 10), new float3(12, 8, 4), this.type, 1.5f);
                // this.mpmParameter.dt.Value = 0.01f;
            }

            var gsize = this.grid.DataLength;
            var psize = this.bufferParameter.CurrentBufferLength;
            this.dispather.Dispatch("InitGrid", gsize);
            this.dispather.Dispatch("P2G", gsize);
            this.dispather.Dispatch("CalGridDensity", gsize);
            this.dispather.Dispatch("UpdateParticleVolume", psize);


            this.SetupCamera();
        }

        protected override void Update()
        {
            this.CheckInput();

            var c = 0; while (c++ < 32)
            {
                var gsize = this.grid.DataLength;
                var psize = this.bufferParameter.CurrentBufferLength;
                // this.dispather.Dispatch("InitGrid", gsize);
                this.dispather.Dispatch("P2G", gsize);
                this.dispather.Dispatch("UpdateGrid", gsize);
                this.dispather.Dispatch("G2P", psize);

                // ComputeShaderParameterBuffer.SwapBuffer(this.bufferParameter.particlesDataBufferRead, this.bufferParameter.particlesDataBufferWrite);
            }
        }

        protected void CheckInput()
        {
            if(Input.GetMouseButtonDown(0))
            {
                var pos = Input.mousePosition / new float3(Screen.width, Screen.height,1);
                pos.z = this.Is2D?0:0.5f;
                this.AddBox(pos, new float3(0.5f, 0.5f, this.Is2D?0:0.5f), this.type);
            }

            if(Input.GetKeyDown(KeyCode.C))
            {
                this.parameter.activeNumberOfParticles.Value = 0;
                this.bufferParameter.particlesIndexBufferActive.Value.SetCounterValue(0);
                this.dispather.Dispatch("InitParticle", this.parameter.numberOfParticles.Value);
            }


            if(Input.GetKeyDown(KeyCode.E)) this.type = Particle.Type.Elastic;
            if(Input.GetKeyDown(KeyCode.S)) this.type = Particle.Type.Snow;
            if(Input.GetKeyDown(KeyCode.F)) this.type = Particle.Type.Liquid;

        }

        protected void SetupCamera()
        {
            if(this.Is2D)
            {
                var c = Camera.main;
                c.orthographic = true;
                c.orthographicSize = this.grid.Bounds.extents.x;
                c.aspect = this.grid.Bounds.extents.x / this.grid.Bounds.extents.y;
                c.transform.position = this.grid.Bounds.center - new Vector3(0, 0, 10f);
            }
        }


        private Dictionary<Particle.Type, float> spacingMap = new Dictionary<Particle.Type, float>()
        {
            {Particle.Type.Elastic, 0.5f},
            {Particle.Type.Snow, 0.2f},
            {Particle.Type.Liquid, 1f},
        };

        protected void AddBox(float3 center, float3 size, Particle.Type type)
        {
            this.mpmParameter.newParticleBuffer.Value?.Release();

            var spacing = this.spacingMap[type];

            var dim = this.grid.Dim;
            var ssize = size * spacing;

            var pos = type == Particle.Type.Snow ? Tool.GenerateSphere(this.Is2D?500:3000, !this.Is2D) : Tool.GenerateBox(size * dim);
            for (var i = 0; i < pos.Count; ++i)
            {
                pos[i] = (center - ssize * 0.5f) * dim + pos[i] * dim * ssize;
                // Debug.Log(pos[i]);
            }

            this.mpmParameter.ptype.Value = (int)type;
            this.mpmParameter.newParticleBuffer.Value = new ComputeBuffer(pos.Count, Marshal.SizeOf<float3>());
            this.parameter.activeNumberOfParticles.Value += pos.Count;
            this.mpmParameter.newParticleBuffer.Value.SetData(pos.ToArray());
            this.dispather.Dispatch("AddParticles", pos.Count);

        }
    }
}