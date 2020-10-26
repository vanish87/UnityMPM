using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Common;
using UnityTools.Debuging;
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
        public float2x2 stress;
        public float J;

        public Matrix<float> weightMatrix;
        public Matrix<float2> weightGradient;

        public float4 color;

        private readonly float2x2 identity = new float2x2(1, 0, 0, 1);

        public Particle()
        {
            this.mass = 1;
            // this.vel = new float2(UnityEngine.Random.value, UnityEngine.Random.value);
            this.B = 0;
            this.D = 0;
            this.stress = identity;
            this.Fe = identity;
            this.Fp = identity;

        }
        public void CalculateD(Grid g)
        {
            // var gpos = g.ToGridPos(this.pos);
            // var delta = math.distance(gpos, this.pos);
            var delta = g.h;
            this.D = new float2x2(1, 0, 0, 1) * 0.25f * delta * delta;
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
                    if (g.inGrid(id))
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
            var vel = false;
            var grid = false;
            if (grid)
                using (new GizmosScope(new Color(0, 1, 0, 0.05f), Matrix4x4.identity))
                {
                    var c = new float3(this.center, 0);
                    var s = new float3(this.size, 0) * this.h;
                    Gizmos.DrawWireCube(c, s);

                    for (var gi = 0; gi < this.size.x; ++gi)
                        for (var gj = 0; gj < this.size.y; ++gj)
                        {
                            var cc = new float3(this.ToGridPos(new int2(gi, gj)), 0);
                            var cs = new float3(this.h, this.h, 0);
                            Gizmos.DrawWireCube(cc, cs);
                        }
                }

            if (vel)
                using (new GizmosScope(Color.red, Matrix4x4.identity))
                {
                    for (var gi = 0; gi < this.size.x; ++gi)
                        for (var gj = 0; gj < this.size.y; ++gj)
                        {
                            var c = this.data[gi, gj];

                            var cc = new float3(this.ToGridPos(new int2(gi, gj)), 0);
                            Gizmos.DrawRay(cc, new float3(c.mv, 0));
                            Gizmos.DrawSphere(cc, c.mass / 50f);
                        }
                }
        }

        public void Clear()
        {
            foreach (var c in this.data)
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
        public const float E = 1000;// Young's modulus
        public const float v = 0.4f;// Poisson's ratio

        public const float mu = E / (2f * (1f + v));
        public const float lambda = (E * v) / ((1f + v) * (1f - 2f * v));

        public ParticleRender render;

        protected Particle[] particles;
        protected ParticleGPU[] gpuData;
        protected ComputeBuffer gpuBuffer;

        protected Grid g;

        protected void OnEnable()
        {
            this.Init();
            LogTool.Log("mu " + mu, LogLevel.Info);
            LogTool.Log("lambda " + lambda, LogLevel.Info);
        }

        protected void OnDisable()
        {
            this.gpuBuffer.Release();
        }

        protected void Update()
        {
            if (Input.GetKey(KeyCode.Space))
            {
                this.Step();
                this.ToGPUBuffer();
            }
        }
        List<float2> spawn_box(float x, float y, int box_x = 8, int box_y = 8)
        {
            const float spacing = 0.25f;
            var ret = new List<float2>();
            for (float i = -box_x / 2; i < box_x / 2; i += spacing)
            {
                for (float j = -box_y / 2; j < box_y / 2; j += spacing)
                {
                    var pos = math.float2(x + i, y + j);

                    ret.Add(pos);
                }
            }
            return ret;
        }
        protected void Init()
        {
            this.g = new Grid(this.gridSize, new float2(0, 0), this.h);

            if (this.gpuBuffer != null) this.gpuBuffer.Release();
            
            var pos = this.spawn_box(this.g.center.x, this.g.center.y);
            this.numOfParticles = pos.Count;

            this.particles = new Particle[this.numOfParticles];

            this.gpuData = new ParticleGPU[this.numOfParticles];
            this.gpuBuffer = new ComputeBuffer(this.numOfParticles, Marshal.SizeOf<ParticleGPU>());

            this.render.buffer = this.gpuBuffer;
            this.render.size = this.numOfParticles;

            for (var i = 0; i < this.particles.Length; ++i)
            {
                var p = new Particle();
                p.pos = pos[i];
                p.color = new float4(1, 0, 0, 1);
                this.particles[i] = p;
            }

            var massSum = new Matrix<float>(this.g.size.x, this.g.size.y);
            foreach (var p in this.particles)
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
                            massSum[idx.x, idx.y] += p.mass * p.weightMatrix[mx, my];
                        }
                    }
                }
            }

            for (var gx = 0; gx < this.g.size.x; ++gx)
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

                var F = p.stress;
                var j = math.determinant(F);
                var volume = p.V * j;


                // var mu = 2000f;
                // var lambda = 0.4f;

                var invtF = math.transpose(math.inverse(F));
                var P = mu * (F - invtF) + lambda * math.log(j) * invtF;
                var stress = (1f / j) * math.mul(P, math.transpose(F));
                var term = -volume * 4 * stress * dt;


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
                            this.g[idx.x, idx.y].mv += math.mul(term * w, (gpos - p.pos));
                        }
                    }
                }
            }

        }

        protected void UpdateGrid()
        {
            for (var gx = 0; gx < this.g.size.x; ++gx)
            {
                for (var gy = 0; gy < this.g.size.y; ++gy)
                {
                    var c = this.g[gx, gy];
                    if (c.mass <= 0)
                    {
                        c.mass = 0;
                        c.mv = 0;
                        c.vel = 0;
                    }
                    else
                    {
                        c.vel = c.mv / c.mass;
                    }
                }
            }


            for (var gx = 0; gx < this.g.size.x; ++gx)
            {
                for (var gy = 0; gy < this.g.size.y; ++gy)
                {
                    var c = this.g[gx, gy];
                    if(c.mass > 0)
                    {
                        var g = new float2(0, -2.8f);
                        c.vel += dt * (c.force / c.mass);
                        c.vel += g * dt;
                        if (gx < 2 || gx > this.g.size.x - 2) c.vel.x = 0;
                        if (gy < 2 || gy > this.g.size.y - 2) c.vel.y = 0;
                    }
                }
            }

            
        }
        protected void G2P()
        {
            foreach (var p in this.particles)
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
                        var vel = g[idx.x, idx.y].vel;

                        p.vel += w * vel;
                        p.B += Outer(w * vel, gpos - p.pos);
                    }
                }

                p.pos += dt * p.vel;
                p.pos = math.clamp(p.pos, this.g.start, this.g.start + new float2(this.g.size) * this.g.h);


                var F = new float2x2(1,0,0,1);
                F+= dt * p.B * 4;
                p.stress = math.mul(F, p.stress);
            }

        }
        protected float2x2 Outer(float2 u, float2 v)
        {
            return new float2x2(u[0] * v[0], u[0] * v[1], u[1] * v[0], u[1] * v[1]);
        }

        protected void OnDrawGizmos()
        {
            this.g?.OnDrawGizmos();
            return;

            var p = this.particles[0];
            var gidx = this.g.ToGridIndex(p.pos);
            p.CalculateWeightMatrix(this.g);
            for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
            {
                for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                {
                    var idx = gidx + new int2(gx, gy);
                    if (this.g.inGrid(idx))
                    {
                        using (new GizmosScope(Color.green, Matrix4x4.identity))
                        {
                            var gpos = new float3(this.g.ToGridPos(idx), 0);
                            Gizmos.DrawWireSphere(gpos, p.weightMatrix[mx, my]);
                        }

                    }
                }
            }
        }

    }
}