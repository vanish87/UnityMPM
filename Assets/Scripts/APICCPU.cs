using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Debuging.EditorTool;
using UnityTools.Rendering;

namespace UnityMPM
{
    public class APICCPU : MonoBehaviour
    {
        [SerializeField] protected int3 dim = new int3(32, 32, 1);
        [SerializeField] protected float3 spacing = new float3(1, 1, 0);
        [SerializeField] protected float cellScale = 1;

        [SerializeField] protected float dt = 0.05f;

        protected Grid<APIC.Cell> grid;
        protected List<APIC.ParticleAPIC> particles = new List<APIC.ParticleAPIC>();

        public static List<float3> GenerateBox(float3 center, float3 size, float spacing = 1)
        {
            var ret = new List<float3>();
            var count = new int3(size / spacing) + 1;
            foreach(var x in Enumerable.Range(0,count.x))
            {
                foreach(var y in Enumerable.Range(0, count.y))
                {
                    foreach(var z in Enumerable.Range(0, count.z))
                    {
                        var local = new float3(x, y, z) * spacing;
                        ret.Add(center - size * 0.5f + local);
                    }
                }
            }
            return ret;
        }

        protected void Step()
        {
            this.P2G();
            this.UpdateGrid();
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
                            var gpos = this.grid.IndexToCenter(idx);


                            this.grid[idx].mass += w * p.mass;
                            this.grid[idx].mv += w * p.mass * (p.vel + math.mul(apic, (gpos-p.pos)));
                        }
                    }
                }

            }

        }

        protected void UpdateGrid()
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
                    var g = new float3(0f, -9.8f, 0);
                    c.vel += dt * (c.force / c.mass + g);
                    if (x < 2 || x > this.grid.Dim.x - 2) c.vel.x = 0;
                    if (y < 2 || y > this.grid.Dim.y - 2) c.vel.y = 0;
                }
            }

        }

        protected void G2P()
        {
            foreach(var p in this.particles)
            {
                p.vel = 0;
                p.B = 0;

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
                            var gpos = this.grid.IndexToCenter(idx);
                            var vel = this.grid[idx].vel;


                            p.vel += w * vel;
                            p.B += w * Outer(vel, gpos - p.pos);
                        }
                    }
                }
                p.pos += dt * p.vel;
                var b = this.grid.Bounds;
                p.pos = math.clamp(p.pos, b.min, b.max);
            }


        }

        protected float3x3 Outer(float3 u, float3 v)
        {
            return new float3x3(u[0]*v[0], u[0]*v[1],u[0]*v[2],
                                u[1]*v[0], u[1]*v[1],u[1]*v[2],
                                u[2]*v[0], u[2]*v[1],u[2]*v[2]);


        }

        protected void Start()
        {
            var pos = GenerateBox(new float3(5,10,0), new float3(8,8,0), 0.5f);
            foreach(var p in pos)
            {
                this.particles.Add(new APIC.ParticleAPIC() { pos = p, mass = 1 });
            }

            var render = this.GetComponent<PositionRender>();
            render.Init(this.particles.Cast<PositionRender.IPosition>().ToList());
        }

        protected void Update()
        {
            if(Input.GetKey(KeyCode.Space))
            this.Step();
        }

        protected void OnEnable()
        {
            this.grid = new Grid<APIC.Cell>(this.dim, this.spacing, float3.zero);
        }

        protected void OnDrawGizmos()
        {
            using(new GizmosScope(new Color(0,1,0,0.1f), Matrix4x4.identity))
            {
                this.grid?.OnDrawGizmos(Matrix4x4.Scale(new Vector3(this.cellScale, this.cellScale, this.cellScale)));
            }
        }
    }
}