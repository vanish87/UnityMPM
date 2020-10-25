using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Common;
using UnityTools.Debuging.EditorTool;
using UnityTools.Math;

namespace UnityMPM
{

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct ParticleGPU
    {
        public float2 pos;
        public float4 color;
    }
    public class Particle
    {
        public float mass;
        public float V;
        public float2 pos;
        public float2 vel;
        public float2x2 D;
        public float2x2 B;
        public float2x2 C;
        public float2x2 Fe;
        public float2x2 Fp;
        public float J;

        public Matrix<float> weightMatrix;
        public Matrix<float2> weightGradient;

        public float4 color;


        public Particle()
        {
            this.mass = 1;
            this.B = 0;
            this.Fe = new float2x2(1, 0, 0, 1);
            this.Fp = new float2x2(1, 0, 0, 1);

        }
        public void CalculateD(Grid g)
        {
            // var gpos = g.ToGridPos(this.pos);
            // var delta = math.distance(gpos, this.pos);
            var delta = g.h;
            this.D = new float2x2(1,0,0,1) * 0.25f * delta * delta;
        }

        public void CalculateWeightMatrix(Grid g)
        {
            this.weightMatrix = new Matrix<float>(3, 3);
            this.weightGradient = new Matrix<float2>(3, 3);
            var gidx = g.ToGridIndex(this.pos);
            var invh = 1f / g.h;

            for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
            {
                for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                {
                    var id = gidx + new int2(gx, gy);
                    if(g.inGrid(id))
                    {
                        var gpos = g.ToGridPos(id);
                        var w = invh * (this.pos - gpos);
                        var nx = this.N(w.x);
                        var ny = this.N(w.y);
                        var dnx = this.DevN(w.x);
                        var dny = this.DevN(w.y);
                        this.weightMatrix[mx, my] = nx * ny;
                        this.weightGradient[mx, my] = new float2(invh * dnx * ny, nx * invh * dny);
                    }
                }
            }
        }
        protected float N(float x)
        {
            x = math.abs(x);

            if (x < 0.5f) return 0.75f - x * x;
            if (x < 1.5f) return 0.5f * (1.5f - x) * (1.5f - x);
            return 0;
        }

        protected float DevN(float x)
        {
            var absx = math.abs(x);
            if (absx < 0.5f) return -2 * absx;
            if (absx < 1.5f) return (x > 0 ? 1f : -1f) - 1.5f + x;
            return 0;
        }


    }
    public class Grid
    {
        public class Cell
        {
            public float2 force;
            public float2 vel;
            public float mass;
            public float2 mv;

        }

        public int2 size;//dimesion
        public float2 start;//left bottom corner
        public float2 center;
        public float h;//cell size/grid spacing

        public Cell this[int x, int y]
        {
            get => this.data[x, y];
            set => this.data[x, y] = value;
        }

        private Matrix<Cell> data;

        public Grid(int2 size, float2 start, float h)
        {
            this.size = size;
            this.start = start;
            this.h = h;
            this.center = this.start + new float2(this.size) * this.h * 0.5f;

            this.data = new Matrix<Cell>(this.size.x, this.size.y);
            for (var i = 0; i < this.size.x; ++i)
            {
                for (var j = 0; j < this.size.y; ++j)
                {
                    this.data[i, j] = new Cell();
                }
            }
        }
        public int2 ToGridIndex(float2 pos)
        {
            pos -= this.start;
            pos /= this.h;
            return new int2((int)pos.x, (int)pos.y);
        }

        public float2 ToGridPos(float2 pos)
        {
            return this.ToGridPos(this.ToGridIndex(pos));
        }

        public float2 ToGridPos(int2 index)
        {
            return index * new float2(this.h) + new float2(this.h * 0.5f) + this.start;
        }

        public bool inGrid(int2 index)
        {
            return 0 <= index.x && index.x < this.size.x && 0 <= index.y && index.y < this.size.y;
        }
        public void OnDrawGizmos()
        {
            using (new GizmosScope(new Color(0,1,0,0.1f), Matrix4x4.identity))
            {
                var c = new float3(this.center, 0);
                var s = new float3(this.size, 0) * this.h;
                Gizmos.DrawWireCube(c, s);

                for(var gi = 0; gi< this.size.x;++gi)
                for(var gj = 0; gj < this.size.y;++gj)
                {
                    var cc = new float3(this.ToGridPos(new int2(gi,gj)),0);
                    var cs = new float3(this.h,this.h,0);
                    Gizmos.DrawWireCube(cc,cs);
               }
            }
        }

        public void Clear()
        {
            foreach(var c in this.data)
            {
                c.mass = 0;
                c.mv = 0;
                c.vel = 0;
                c.force = 0;
            }
        }

    }
    public class UnityMPM : MonoBehaviour
    {
        public int numOfParticles = 512;
        public int2 gridSize = new int2(32, 32);
        public float h = 1f;
        public float dt = 0.001f;
        public const float E = 64f;// Young's modulus
        public const float v = 0.4f;// Poisson's ratio

        public float mu = E / (2f * (1f + v));
        public float lambda = (E * v) / ((1f + v) * (1f - 2f * v));

        public ParticleRender render;

        protected Particle[] particles;
        protected ParticleGPU[] gpuData;
        protected ComputeBuffer gpuBuffer;

        protected Grid g;

        protected void OnEnable()
        {
            this.Init();
        }

        protected void OnDisable()
        {
            this.gpuBuffer.Release();
        }

        protected void Update()
        {
            if(Input.GetKey(KeyCode.Space))
            {
                this.Step();
                this.ToGPUBuffer();
            }
        }

        protected void Init()
        {
            this.g = new Grid(this.gridSize, new float2(0, 0), this.h);

            if (this.gpuBuffer != null) this.gpuBuffer.Release();

            this.particles = new Particle[this.numOfParticles];

            this.gpuData = new ParticleGPU[this.numOfParticles];
            this.gpuBuffer = new ComputeBuffer(this.numOfParticles, Marshal.SizeOf<ParticleGPU>());

            this.render.buffer = this.gpuBuffer;
            this.render.size = this.numOfParticles;

            for (var i = 0; i < this.particles.Length; ++i)
            {
                var p = new Particle();
                var pos = this.g.start + new float2(UnityEngine.Random.value, UnityEngine.Random.value) * this.g.size;
                while (math.distance(pos, this.g.center) > 5)
                {
                    pos = this.g.start + new float2(UnityEngine.Random.value, UnityEngine.Random.value) * this.g.size;
                }
                p.pos = pos;
                p.color = new float4(1, 0, 0, 1);
                this.particles[i] = p;
            }

            var massSum = new Matrix<float>(this.g.size.x, this.g.size.y);
            foreach(var p in this.particles)
            {
                p.CalculateWeightMatrix(this.g);
                var gidx = this.g.ToGridIndex(p.pos);
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        var idx = gidx + new int2(gx, gy);
                        if (this.g.inGrid(idx))
                        {
                            massSum[idx.x, idx.y] += p.mass * p.weightMatrix[mx,my];
                        }
                    }
                }
            }

            for(var gx = 0; gx < this.g.size.x; ++gx)
            {
                for (var gy = 0; gy < this.g.size.y; ++gy)
                {
                    massSum[gx, gy] /= this.g.h * this.g.h;
                }
            }
 
            foreach (var p in this.particles)
            {
                var gidx = this.g.ToGridIndex(p.pos);
                var density = 0f;
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        var idx = gidx + new int2(gx, gy);
                        if (this.g.inGrid(idx))
                        {
                            density += massSum[idx.x, idx.y] * p.weightMatrix[mx, my];
                        }
                    }
                }

                p.V = p.mass / density;
                
            }
            this.ToGPUBuffer();
        }

        protected void ToGPUBuffer()
        {
            for (var i = 0; i < this.particles.Length; ++i)
            {
                this.gpuData[i].pos = this.particles[i].pos;
                this.gpuData[i].color = this.particles[i].color;
            }

            this.gpuBuffer.SetData(this.gpuData);
        }
        protected void Step()
        {
            this.P2G();
            this.UpdateGrid();
            this.G2P();
        }

        protected void P2G()
        {
            this.g.Clear();

            foreach (var p in this.particles)
            {
                p.CalculateWeightMatrix(this.g);
                p.CalculateD(this.g);

                var gidx = this.g.ToGridIndex(p.pos);
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        var idx = gidx + new int2(gx, gy);
                        if (this.g.inGrid(idx))
                        {
                            var w = p.weightMatrix[mx, my];
                            var gpos = this.g.ToGridPos(idx);
                            var apic = math.mul(math.mul(p.B, math.inverse(p.D)), (gpos - p.pos));
                            this.g[idx.x, idx.y].mass += p.mass * w;
                            this.g[idx.x, idx.y].mv += p.mass * w * (p.vel + apic);
                        }
                    }
                }
            }

        }

        protected void UpdateGrid()
        {
            for(var gx = 0; gx < this.g.size.x; ++gx)
            {
                for (var gy = 0; gy < this.g.size.y; ++gy)
                {
                    var c = this.g[gx, gy];
                    if (c.mass <= 0)
                    {
                        c.mass = 0;
                        c.mv = 0;
                    }
                    else
                    {
                        c.vel = c.mv / c.mass;
                    }
                }
            }

            foreach (var p in particles)
            {
                var gidx = this.g.ToGridIndex(p.pos);
                var Fe = p.Fe;
                var j = math.determinant(Fe);
                var invtF = math.transpose(math.inverse(Fe));
                var stress = mu * (Fe - invtF) + lambda * math.log(j) * invtF;
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        var idx = gidx + new int2(gx, gy);
                        if (!this.g.inGrid(idx)) continue;
                        if (g[idx.x, idx.y].mass == 0) continue;

                        var dw = p.weightGradient[mx, my];
                        g[idx.x, idx.y].force += -p.V * math.mul(math.mul(stress, math.transpose(Fe)), dw);
                    }
                }
            }

            for (var gx = 0; gx < this.g.size.x; ++gx)
            {
                for (var gy = 0; gy < this.g.size.y; ++gy)
                {
                    var c = this.g[gx, gy];
                    c.vel += dt * (c.force / c.mass);
                    c.vel += dt * new float2(0, -9.8f);
                    if (gx < 2 || gx > this.g.size.x - 2) c.vel.x = 0;
                    if (gy < 2 || gy > this.g.size.y - 2) c.vel.y = 0;
                }
            }


            foreach(var p in this.particles)
            {
                var gidx = this.g.ToGridIndex(p.pos);
                var sum = new float2x2();
                var I = new float2x2(1, 0, 0, 1);

                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        var idx = gidx + new int2(gx, gy);
                        if (!this.g.inGrid(idx)) continue;
                        if (g[idx.x, idx.y].mass == 0) continue;

                        var dw = p.weightGradient[mx, my];
                        var dwT = new float2x2(dw.x, 0, dw.y, 0);
                        var vel = g[idx.x, idx.y].vel;
                        var velMat = new float2x2(vel.x, vel.y, 0, 0);

                        sum += math.mul(velMat, dwT);
                    }
                }
                //first Fn+1 is assumed all Fe
                var Fn1 = (I + dt * sum) * p.Fe;
                var Fen1 = math.mul(Fn1, math.inverse(p.Fp));
                var U = new float2x2();
                var d = new float2();
                var V = new float2x2();

                SVD.GetSVD2D(Fen1, out U, out d, out V);

                var thetaC = 0.25f;
                var thetaS = 0.075f;
                d[0] = math.clamp(d[0], 1f - thetaC, 1f + thetaS);
                d[1] = math.clamp(d[1], 1f - thetaC, 1f + thetaS);

                var D = new float2x2(d[0],0,0,d[1]);

                Fen1 = math.mul(math.mul(U, D), math.transpose(V));
                var Fpn1 = math.mul(math.inverse(Fen1), Fn1); 

                p.Fe = Fen1;
                p.Fp = Fpn1;
                
            }
        }
        protected void G2P()
        {
            foreach(var p in this.particles)
            {
                p.vel = 0;
                p.B = 0;

                var gidx = this.g.ToGridIndex(p.pos);
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        var idx = gidx + new int2(gx, gy);
                        if (!this.g.inGrid(idx)) continue;
                        if (g[idx.x, idx.y].mass == 0) continue;

                        var gpos = this.g.ToGridPos(idx);

                        var w = p.weightMatrix[mx, my];
                        var delta = new float2x2(gpos.x - p.pos.x, gpos.y - p.pos.y, 0, 0);
                        var vel = g[idx.x, idx.y].vel;
                        var velMat = new float2x2(vel.x, vel.y, 0, 0);

                        p.vel += w * vel;
                        p.B += math.mul(w * velMat, math.transpose(delta));
                    }
                }

                p.pos += dt * p.vel;
                p.pos = math.clamp(p.pos, this.g.start, this.g.start + new float2(this.g.size) * this.g.h);
            }

        }


        protected void OnDrawGizmos()
        {
            this.g?.OnDrawGizmos();
        }

    }
}