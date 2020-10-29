using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Debuging.EditorTool;

namespace UnityMPM
{
    public class MPMGPUMono : MonoBehaviour
    {

        public ComputeShader computeShader;

        public int particleCount;
        public int2 gridSize = new int2(32,32);

        public int boxCount = 3;
        public int2 boxSize = new int2(32, 32);

        protected readonly float2x2 identity = new float2x2(1,0,0,1);

        ComputeBuffer particleBuffer;
        ComputeBuffer gridBuffer;

        public struct Matrix3x3
        {
            public float2 m00; public float2 m01; public float2 m02;
            public float2 m10; public float2 m11; public float2 m12;
            public float2 m20; public float2 m21; public float2 m22;

        }

        public struct Particle
        {
            public float mass;
            public float volume;
            public float4 color;

            public float2 pos;
            public float2 vel;

            public float2x2 B;
            public float2x2 D;

            public float2x2 Fe;
            public float2x2 Fp;

            public float3x3 Weight;
            public Matrix3x3 WeightGrident;
        }

        public struct Cell
        {
            public float mass;
            public float2 mv;
            public float2 vel;
            public float2 force;

        }

        protected void CleanUp()
        {
            this.particleBuffer?.Release();
            this.gridBuffer?.Release();
        }

        protected void OnEnable()
        {

            this.CleanUp();

            this.particleCount = boxSize.x * boxSize.y * boxCount;
            var gridSize = this.gridSize.x * this.gridSize.y;
            this.particleBuffer = new ComputeBuffer(this.particleCount, Marshal.SizeOf<Particle>());
            this.gridBuffer = new ComputeBuffer(gridSize, Marshal.SizeOf<Cell>());

            var render = this.GetComponent<ParticleRender>();
            render.buffer = this.particleBuffer;
            render.size = this.particleCount;

        }
        protected void OnDisable()
        {
            this.CleanUp();
        }

        protected void Start()
        {
            this.Init();
        }

        protected void Update()
        {
            // if(Input.GetKeyDown(KeyCode.Space))
            var c = 0;
            while(c++ < 128)
            {
                this.Step();
            }
        }

        protected void Init()
        {
            var cpuParticle = new Particle[this.particleCount];
            var center = new float2(this.gridSize.x / 3, this.gridSize.y / 2);
            foreach(var bc in Enumerable.Range(0,this.boxCount))
            {
                var pc = center + new float2(0,this.boxSize.y + 0.5f) * bc;
                var spacing = 0.55f;
                foreach(var r in Enumerable.Range(0, this.boxSize.x))
                {
                    foreach(var c in Enumerable.Range(0, this.boxSize.y))
                    {
                        var id = (bc + 1) * (r * this.boxSize.x + c);
                        cpuParticle[id] = new Particle()
                        {
                            mass = 1f,
                            volume = 0.1f,
                            color = new float4(1,0,0,1),
                            pos = pc + new float2(r - this.boxSize.x / 2f, c - this.boxSize.y / 2f) * spacing,
                            vel = 0,
                            B = 0,
                            D = identity,
                            Fe = identity,
                            Fp = identity,
                        };

                        // Debug.Log(id + " " + cpuParticle[id].pos);
                    }
                }
            }

            this.particleBuffer.SetData(cpuParticle);
        }


        protected void Step()
        {
            this.P2G();
            this.UpdateGrid();
            this.G2P();
        }

        protected void P2G()
        {
            var size = new int3(this.particleCount/32,1,1);
            var k = this.computeShader.FindKernel("CalWeights");
            this.computeShader.SetInt("gsx", this.gridSize.x);
            this.computeShader.SetInt("gsy", this.gridSize.y);
            this.computeShader.SetFloat("spacing", 1);
            this.computeShader.SetBuffer(k, "particles", this.particleBuffer);
            this.computeShader.Dispatch(k, size.x, size.y, size.z);



            size = new int3(this.gridSize.x * this.gridSize.y/32, 1, 1);
            k = this.computeShader.FindKernel("P2G");
            this.computeShader.SetInt("particleNum", this.particleCount);
            this.computeShader.SetInt("gsx", this.gridSize.x);
            this.computeShader.SetInt("gsy", this.gridSize.y);
            this.computeShader.SetFloat("spacing", 1);
            this.computeShader.SetBuffer(k, "particles", this.particleBuffer);
            this.computeShader.SetBuffer(k, "grid", this.gridBuffer);
            this.computeShader.Dispatch(k, size.x, size.y, size.z);

            if(debugCell == null)
            {
                debugCell = new Cell[this.gridSize.x * this.gridSize.y];
            }
            // this.gridBuffer.GetData(debugCell);

            if(debugParticle == null)
            {
                debugParticle = new Particle[this.particleCount];
            }
            // this.particleBuffer.GetData(debugParticle);

        }

        protected void UpdateGrid()
        {
            var size = new int3(this.gridSize.x * this.gridSize.y/32, 1, 1);
            var k = this.computeShader.FindKernel("UpdateGrid");
            this.computeShader.SetInt("particleNum", this.particleCount);
            this.computeShader.SetInt("gsx", this.gridSize.x);
            this.computeShader.SetInt("gsy", this.gridSize.y);
            this.computeShader.SetFloat("spacing", 1);
            this.computeShader.SetBuffer(k, "particles", this.particleBuffer);
            this.computeShader.SetBuffer(k, "grid", this.gridBuffer);
            this.computeShader.Dispatch(k, size.x, size.y, size.z);

        }

        protected void G2P()
        {
            var size = new int3(this.particleCount / 32, 1, 1);
            var k = this.computeShader.FindKernel("G2P");
            this.computeShader.SetInt("particleNum", this.particleCount);
            this.computeShader.SetInt("gsx", this.gridSize.x);
            this.computeShader.SetInt("gsy", this.gridSize.y);
            this.computeShader.SetFloat("spacing", 1);
            this.computeShader.SetBuffer(k, "particles", this.particleBuffer);
            this.computeShader.SetBuffer(k, "grid", this.gridBuffer);
            this.computeShader.Dispatch(k, size.x, size.y, size.z);

        }


        Cell[] debugCell;
        Particle[] debugParticle;


        protected void OnDrawGizmos()
        {
            using(new GizmosScope(Color.green, Matrix4x4.identity))
            {
                Gizmos.DrawWireCube(new float3(this.gridSize/2,0), new float3(this.gridSize, 0));
            }

            return;
            if(debugCell != null)
            {
                using (new GizmosScope(Color.red, Matrix4x4.identity))
                {
                    for (var gi = 0; gi < this.gridSize.x; ++gi)
                        for (var gj = 0; gj < this.gridSize.y; ++gj)
                        {
                            var c = this.debugCell[gi*this.gridSize.x + gj];

                            var cc = new float3((new int2(gi, gj)), 0) + 0.5f;
                            if(c.mass> 0)
                                Gizmos.DrawRay(cc, new float3(c.mv / c.mass, 0));
                            Gizmos.DrawSphere(cc, c.mass/50);
                        }
                }
            }

            if(debugParticle != null)
            {
                var p = debugParticle[0];
                Gizmos.DrawSphere(new float3(p.pos,0), 0.1f);

            }
        }


    }

}
