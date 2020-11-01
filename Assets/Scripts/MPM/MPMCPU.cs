using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Attributes;
using UnityTools.Debuging.EditorTool;
using UnityTools.Math;
using UnityTools.Rendering;

namespace UnityMPM
{
    public class MPMCPU : MonoBehaviour
    {
        [System.Serializable]
        public class MPMGrid : Grid<MPMGPU.Cell>
        {
            public MPMGrid(int3 dimesion, float3 cellSpacing, float3 leftBottom, CenterType centerType = CenterType.Center) : base(dimesion, cellSpacing, leftBottom, centerType)
            {
                this.cellScale = 0.05f;
            }
        }
        [SerializeField] protected int3 dim = new int3(32, 32, 1);
        [SerializeField] protected float3 spacing = new float3(1, 1, 0);
        [SerializeField] protected float dt = 0.05f;

        [SerializeField] protected float E = 1.4e4f;// Young's modulus
        [SerializeField] protected float v = 0.2f;// Poisson's ratio
        [SerializeField] protected float hardening = 10f;

        [SerializeField, DisableEdit] protected float mu;
        [SerializeField, DisableEdit] protected float lambda;


        [SerializeField] protected MPMGrid grid;
        protected List<MPMGPU.Particle> particles = new List<MPMGPU.Particle>();

        protected void Step()
        {
            this.P2G();
            this.ComputeGridVel();
            this.UpdateGridForce();
            this.UpdateGridVel();
            this.G2P();
        }

        protected void ClearGrid()
        {
            foreach(var c in this.grid)
            {
                c.mass = 0;
                c.mv = 0;
                c.force = 0;
                c.vel = 0;
            }
        }
        
        protected void P2G()
        {
            this.ClearGrid();

            var Dinv = math.inverse(this.grid.GetD());

            foreach(var p in this.particles)
            {
                var gidx = this.grid.ToIndex(p.pos);
                var apic = math.mul(p.B, Dinv);

                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        for(int gz = -1, mz = 0; gz <= 1; ++gz, ++mz)
                        {
                            var delta = new int3(gx, gy, gz);
                            var idx = gidx + delta;
                            if (!this.grid.InGrid(idx)) continue;

                            var w = this.grid.GetWeight(p.pos, delta);
                            var gpos = this.grid.IndexToCellPos(idx);


                            this.grid[idx].mass += w * p.mass;
                            this.grid[idx].mv += w * p.mass * (p.vel + math.mul(apic, (gpos-p.pos)));
                        }
                    }
                }

            }

        }

        protected void ComputeGridVel()
        {                
            foreach(var x in Enumerable.Range(0, this.grid.Dim.x))
            foreach(var y in Enumerable.Range(0, this.grid.Dim.y))
            foreach(var z in Enumerable.Range(0, this.grid.Dim.z))
            {
                var c = this.grid[x,y,z];
                if (c.mass <= 0)
                {
                    c.force = 0;
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

        protected void UpdateGridForce()
        {
            foreach(var p in this.particles)
            {
                var gidx = this.grid.ToIndex(p.pos);
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        for (int gz = -1, mz = 0; gz <= 1; ++gz, ++mz)
                        {
                            var delta = new int3(gx, gy, gz);
                            var idx = gidx + delta;
                            if (!this.grid.InGrid(idx)) continue;

                            var w = this.grid.GetWeight(p.pos, delta);
                            var wd = this.grid.GetWeightGradient(p.pos, delta);
                            var F = new float2x2(p.Fe[0][0],p.Fe[1][0],p.Fe[0][1],p.Fe[1][1]);
                            // var F = p.Fe;
                            var j = math.determinant(F);
                            var volume = p.volume;

                            var R = new float2x2();
                            var S = new float2x2();
                            SVD.GetPolarDecomposition2D(F, out R, out S);
                            // var R = Tool.identity;
                            // var S = Tool.identity;
                            // var U = new float3x3();
                            // var d = new float3();
                            // var V = new float3x3();

                            //Note: 3D is not tested on CPU and is very slow for real time 
                            // SVD.GetSVD3D(F, out U, out d, out V);
                            // var D = new float3x3(d[0], 0, 0, 0, d[1], 0, 0, 0, d[2]);
                            // R = math.mul(U, math.transpose(V));
                            // S = math.mul(math.mul(V, D), math.transpose(V));
                                
                            var Jp = math.determinant(p.Fp);
                            Jp = math.clamp(Jp, 0.6f, 20f);
                            var e = p.type == MPMGPU.Particle.Type.Snow ? math.exp(hardening * (1 - Jp)) : 1f;
                            var mup = mu * e;
                            var lambdap = lambda * e;

                            if(p.type == MPMGPU.Particle.Type.Liquid)
                            {
                                mup = 0;
                                lambdap = 5000;
                                p.Fe = new float3x3(j, 0, 0, 0, 1, 0, 0, 0, 1);
                            }

                            var FinvT = math.transpose(math.inverse(F));
                            var P = (2f * mup * (F - R)) + lambdap * (j - 1f) * j * FinvT;

                            var stress = 1f / j * math.mul(P, math.transpose(F));
                            this.grid[idx].force += new float3(-volume * math.mul(stress, wd.xy),0);
                            // this.grid[idx].force += -volume * math.mul(stress, wd);

                        }
                    }
                }
            }
        }
        protected void UpdateGridVel()
        {                
            foreach(var x in Enumerable.Range(0, this.grid.Dim.x))
            foreach(var y in Enumerable.Range(0, this.grid.Dim.y))
            foreach(var z in Enumerable.Range(0, this.grid.Dim.z))
            {
                var c = this.grid[x,y,z];
                if(c.mass > 0)
                {
                    var g = new float3(0f, -9.8f, 0) * 10;
                    c.vel += dt * (c.force / c.mass + g);
                    if (x < 2 || x > this.grid.Dim.x - 3) c.vel.x = 0;
                    if (y < 2 || y > this.grid.Dim.y - 3) c.vel.y = 0;
                }
            }

        }


        protected void G2P()
        {
            foreach(var p in this.particles)
            {
                p.vel = 0;
                p.B = 0;

                var sum = new float3x3();

                var gidx = this.grid.ToIndex(p.pos);
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        for (int gz = -1, mz = 0; gz <= 1; ++gz, ++mz)
                        {
                            var delta = new int3(gx, gy, gz);
                            var idx = gidx + delta;
                            if (!this.grid.InGrid(idx)) continue;

                            var w = this.grid.GetWeight(p.pos, delta);
                            var wd = this.grid.GetWeightGradient(p.pos, delta);
                            var gpos = this.grid.IndexToCellPos(idx);
                            var vel = this.grid[idx].vel;

                            p.vel += w * vel;
                            p.B += w * Outer(vel, gpos - p.pos);

                            sum += Outer(vel, wd);
                        }
                    }
                }
                p.pos += dt * p.vel;
                var b = this.grid.Bounds;
                p.pos = math.clamp(p.pos, b.min, b.max);


                var F = p.Fe;
                F = math.mul(Tool.identity + dt * sum, F);

                if(p.type == MPMGPU.Particle.Type.Snow)
                {
                    var F2d = new float2x2(F[0][0],F[1][0],F[0][1],F[1][1]);
                    var Fp2d = new float2x2(p.Fp[0][0],p.Fp[1][0],p.Fp[0][1],p.Fp[1][1]);
                    var U = new float2x2();
                    var d = new float2();
                    var V = new float2x2();
                    SVD.GetSVD2D(F2d, out U, out d, out V);

                    d = math.clamp(d, new float2(1f - 2.5e-2f), new float2(1f + 7.5e-3f));
                    var D = new float2x2(d[0], 0, 0, d[1]);

                    var Fpn1 = math.mul(F2d, Fp2d);
                    var Fen1 = math.mul(math.mul(U, D),math.transpose(V));
                    Fpn1 = math.mul(math.mul(math.mul(V, math.inverse(D)), math.transpose(U)), Fpn1);

                    p.Fe = Tool.identity;
                    p.Fe[0][0] = Fen1[0][0];
                    p.Fe[1][0] = Fen1[1][0];
                    p.Fe[0][1] = Fen1[0][1];
                    p.Fe[1][1] = Fen1[1][1];

                    p.Fp = Tool.identity;
                    p.Fp[0][0] = Fpn1[0][0];
                    p.Fp[1][0] = Fpn1[1][0];
                    p.Fp[0][1] = Fpn1[0][1];
                    p.Fp[1][1] = Fpn1[1][1];
                }
                else
                {
                    p.Fe = F;
                }
            }


        }

        protected float3x3 Outer(float3 u, float3 v)
        {
            return new float3x3(u[0]*v[0], u[0]*v[1],u[0]*v[2],
                                u[1]*v[0], u[1]*v[1],u[1]*v[2],
                                u[2]*v[0], u[2]*v[1],u[2]*v[2]);


        }

        protected void CalculateVolume()
        {
            this.ClearGrid();
            this.P2G();

            foreach(var c in this.grid)
            {
                c.mass /= this.grid.CellVolume;
            }

            foreach(var p in this.particles)
            {
                var gidx = this.grid.ToIndex(p.pos);
                var density = 0f;
                for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
                {
                    for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                    {
                        for (int gz = -1, mz = 0; gz <= 1; ++gz, ++mz)
                        {
                            var delta = new int3(gx, gy, gz);
                            var idx = gidx + delta;
                            if (!this.grid.InGrid(idx)) continue;

                            var w = this.grid.GetWeight(p.pos, delta);
                            density += this.grid[idx].mass * w;
                        }
                    }
                }

                p.volume = p.mass / density;
            }

        }

        protected void AddPos(List<float3> pos, MPMGPU.Particle.Type type)
        {
            foreach(var p in pos)
            {
                this.particles.Add(new MPMGPU.Particle() 
                { 
                    type = type,
                    pos = p, 
                    mass = 1,
                    vel = 0,
                    D = 0,
                    B = 0,
                    Fe = Tool.identity,
                    Fp = Tool.identity
                });
            }
        }

        protected void Start()
        {
            this.mu = E / (2f * (1f + v));
            this.lambda = (E * v) / ((1f + v) * (1f - 2f * v));

            var pos = Tool.GenerateBox(new float3(13, 18, 0), new float3(5, 5, 0), 0.3f);
            this.AddPos(pos, MPMGPU.Particle.Type.Snow);

            pos = Tool.GenerateBox(new float3(19, 22, 0), new float3(3, 8, 0), 0.5f);
            this.AddPos(pos, MPMGPU.Particle.Type.Elastic);

            pos = Tool.GenerateBox(new float3(16, 10, 0), new float3(25, 10, 0), 0.7f);
            this.AddPos(pos, MPMGPU.Particle.Type.Liquid);

            this.CalculateVolume();

            var render = this.GetComponent<PositionRender>();
            render.Init(this.particles.Cast<PositionRender.IPosition>().ToList());
        }

        protected void Update()
        {
            // if(Input.GetKey(KeyCode.Space))
            // var c = 0;while(c++ < 5)
            this.Step();
        }

        protected void OnEnable()
        {
            this.grid = new MPMGrid(this.dim, this.spacing, float3.zero);
        }

        protected void OnDrawGizmos()
        {
            using(new GizmosScope(new Color(0,1,0,0.1f), Matrix4x4.identity))
            {
                this.grid?.OnDrawGizmos();
            }
        }

    }
}