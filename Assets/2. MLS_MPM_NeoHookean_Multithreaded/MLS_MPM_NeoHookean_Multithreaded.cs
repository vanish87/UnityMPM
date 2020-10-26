using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Jobs;
using System.Collections.Generic;

using UnityEngine.Profiling;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using System.Linq;
using UnityTools.Common;
using UnityMPM;

public class MLS_MPM_NeoHookean_Multithreaded : MonoBehaviour
{
    class Particle
    {
        public float2 pos; // position
        public float2 v; // velocity
        public float mass;
        public float volume_0; // initial volume

        public float2x2 B;
        public float2x2 D;
        public float2x2 Fe;
        public float2x2 Fp;

        public Matrix<float> weight;
        public Matrix<float2> weightGradient;

        public bool inGrid(int2 index)
        {
            return 0 <= index.x && index.x < 32 && 0 <= index.y && index.y < 32;
        }

        public void CalculateWeightMatrix()
        {
            this.weight = new Matrix<float>(3, 3);
            this.weightGradient = new Matrix<float2>(3, 3);
            var gidx = new int2((int)pos.x, (int)pos.y);

            for (int gx = -1, mx = 0; gx <= 1; ++gx, ++mx)
            {
                for (int gy = -1, my = 0; gy <= 1; ++gy, ++my)
                {
                    var id = gidx + new int2(gx, gy);
                    if(inGrid(id))
                    {
                        var gpos = ToGridPos(id);
                        var w = (pos - gpos);
                        var nx = this.N(w.x);
                        var ny = this.N(w.y);
                        var dnx = this.DevN(w.x);
                        var dny = this.DevN(w.y);
                        this.weight[mx, my] = nx * ny;
                        this.weightGradient[mx, my] = new float2(dnx * ny, nx * dny);
                    }
                }
            }
        }
        public void CalculateD()
        {
            // var gpos = g.ToGridPos(this.pos);
            // var delta = math.distance(gpos, this.pos);
            var delta = 1;
            this.D = new float2x2(1,0,0,1) * 0.25f * delta * delta;
        }
        private float2 ToGridPos(int2 id)
        {
            return id + new float2(0.5f);
        }
        private float N(float x)
        {
            x = math.abs(x);

            if (x < 0.5f) return 0.75f - x * x;
            if (x < 1.5f) return 0.5f * (1.5f - x) * (1.5f - x);
            return 0;
        }

        private float DevN(float x)
        {
            var absx = math.abs(x);
            if (absx < 0.5f) return -2 * absx;
            if (absx < 1.5f) return (x > 0 ? 1f : -1f) - 1.5f + x;
            return 0;
        }
        
    }

    class Cell
    {
        public float2 v; // velocity
        public float mass;
        public float padding; // unused
    }

    protected ParticleGPU[] gpuData;
    protected ComputeBuffer gpuBuffer;

    const int grid_res = 32;
    const int num_cells = grid_res * grid_res;

    // batch size for the job system. just determined experimentally
    const int division = 16;

    // simulation parameters
    const float dt = 0.1f; // timestep
    const float iterations = (int)(1.0f / dt);
    const float gravity = -0.3f;

    // Lamé parameters for stress-strain relationship
    const float elastic_lambda = 10.0f;
    const float elastic_mu = 20.0f;

    Particle[] ps; // particles
    Matrix<Cell> grid;



    // deformation gradient. stored as a separate array to use same rendering code for all demos, but feel free to store this field in the particle struct instead

    int num_particles;
    List<float2> temp_positions;

    SimRenderer sim_renderer;

    // interaction
    const float mouse_radius = 10;
    bool mouse_down = false;
    float2 mouse_pos;

    void spawn_box(int x, int y, int box_x = 8, int box_y = 8)
    {
        const float spacing = 0.75f;
        for (float i = -box_x / 2; i < box_x / 2; i += spacing)
        {
            for (float j = -box_y / 2; j < box_y / 2; j += spacing)
            {
                var pos = math.float2(x + i, y + j);

                temp_positions.Add(pos);
            }
        }
    }

    void InitGPU(int num)
    {
        this.gpuData = new ParticleGPU[num];
        this.gpuBuffer = new ComputeBuffer(num, Marshal.SizeOf<ParticleGPU>());

        var render = this.GetComponent<ParticleRender>();
        render.buffer = this.gpuBuffer;
        render.size = num;
    }

    void ToGPUBuffer()
    {
        foreach(var i in Enumerable.Range(0, num_particles))
        {
            this.gpuData[i].pos = this.ps[i].pos;
            this.gpuData[i].color = new float4(1,0,0,1);
        }
        this.gpuBuffer.SetData(this.gpuData);
    }
    void Start()
    {
        // populate our array of particles
        temp_positions = new List<float2>();
        spawn_box(grid_res / 2, grid_res / 2, 16, 16);
        num_particles = temp_positions.Count;

        ps = new Particle[num_particles];

        // initialise particles
        for (int i = 0; i < num_particles; ++i)
        {
            Particle p = new Particle();
            p.pos = temp_positions[i];
            p.v = 0;
            p.mass = 1.0f;
            p.weight = new Matrix<float>(3,3);

            // deformation gradient initialised to the identity
            p.Fe = new float2x2(1,0,0,1);
            ps[i] = p;
        }

        grid = new Matrix<Cell>(grid_res, grid_res);

        for(var i = 0; i< grid_res; ++i)
        {
            for(var j = 0; j < grid_res;++j)
            {
                var c = new Cell();
                c.v = 0;
                grid[i][j] = c;
                
            }
        }

        // ---- begin precomputation of particle volumes
        // MPM course, equation 152 

        // launch a P2G job to scatter particle mass to the grid
        this.P2G();

        for (int i = 0; i < num_particles; ++i)
        {
            var p = ps[i];
            p.CalculateWeightMatrix();

            // quadratic interpolation weights
            float2 cell_idx = math.floor(p.pos);

            float density = 0.0f;
            // iterate over neighbouring 3x3 cells
            for (int gx = 0; gx < 3; ++gx)
            {
                for (int gy = 0; gy < 3; ++gy)
                {
                    float weight = p.weight[gx][gy];

                    // map 2D to 1D index in grid
                    var cell_index = new int2((int)cell_idx.x + (gx - 1) , (int)cell_idx.y + gy - 1);
                    density += grid[cell_index.x,cell_index.y].mass * weight;
                }
            }

            // per-particle volume estimate has now been computed
            float volume = p.mass / density;
            p.volume_0 = volume;

            ps[i] = p;
        }

        // ---- end precomputation of particle volumes

        // boilerplate rendering code handled elsewhere
        this.InitGPU(num_particles);
    }

    private void Update()
    {
        HandleMouseInteraction();

        for (int i = 0; i < iterations; ++i)
        {
            Simulate();
        }

        this.ToGPUBuffer();

    }

    void HandleMouseInteraction()
    {
        mouse_down = false;
        if (Input.GetMouseButton(0))
        {
            mouse_down = true;
            var mp = Camera.main.ScreenToViewportPoint(Input.mousePosition);
            mouse_pos = math.float2(mp.x * grid_res, mp.y * grid_res);
        }
    }

    void Simulate()
    {
        this.ClearGrid();
        this.P2G();
        this.UpdateGrid();
        this.G2P();
    }

    #region Jobs

    public void ClearGrid()
    {
        foreach (var cell in grid)
        {
            // reset grid scratch-pad entirely
            cell.mass = 0;
            cell.v = 0;
        }
    }


    public void P2G()
    {
        var weights = new float2[3];

        for (int i = 0; i < num_particles; ++i)
        {
            var p = ps[i];
            p.CalculateWeightMatrix();
            p.CalculateD();

            float2x2 stress = 0;


            // deformation gradient
            var F = p.Fe;

            var J = math.determinant(F);

            // MPM course, page 46
            var volume = p.volume_0 * J;

            // useful matrices for Neo-Hookean model
            var F_T = math.transpose(F);
            var F_inv_T = math.inverse(F_T);
            var F_minus_F_inv_T = F - F_inv_T;

            // MPM course equation 48
            var P_term_0 = elastic_mu * (F_minus_F_inv_T);
            var P_term_1 = elastic_lambda * math.log(J) * F_inv_T;
            var P = P_term_0 + P_term_1;

            // cauchy_stress = (1 / det(F)) * P * F_T
            // equation 38, MPM course
            stress = (1.0f / J) * math.mul(P, F_T);

            // (M_p)^-1 = 4, see APIC paper and MPM course page 42
            // this term is used in MLS-MPM paper eq. 16. with quadratic weights, Mp = (1/4) * (delta_x)^2.
            // in this simulation, delta_x = 1, because i scale the rendering of the domain rather than the domain itself.
            // we multiply by dt as part of the process of fusing the momentum and force update for MLS-MPM
            var eq_16_term_0 = -volume * 4 * stress * dt;

            // quadratic interpolation weights
            int2 cell_idx = (int2)p.pos;

            // for all surrounding 9 cells
            for (int gx = 0; gx < 3; ++gx)
            {
                for (int gy = 0; gy < 3; ++gy)
                {
                    var weight = p.weight[gx,gy];

                    int2 cell_x = new int2(cell_idx.x + gx - 1, cell_idx.y + gy - 1);
                    float2 cell_dist = (cell_x - p.pos) + 0.5f;

                    // scatter mass and momentum to the grid
                    var cell_index = new int2((int)cell_x.x , (int)cell_x.y);
                    Cell cell = grid[cell_index.x, cell_index.y];

                    // MPM course, equation 172
                    // float weighted_mass = weight * p.mass;
                    // cell.mass += weighted_mass;

                    // APIC P2G momentum contribution
                    // cell.v += weighted_mass * (p.v + Q);

                    // fused force/momentum update from MLS-MPM
                    // see MLS-MPM paper, equation listed after eqn. 28
                    float2 momentum = math.mul(eq_16_term_0 * weight, cell_dist);
                    cell.v += momentum;

                    var apic = math.mul(math.mul(p.B, math.inverse(p.D)), cell_dist);
                    cell.mass += p.mass * weight;
                    cell.v += p.mass * weight * (p.v + apic);

                    // total update on cell.v is now:
                    // weight * (dt * M^-1 * p.volume * p.stress + p.mass * p.C)
                    // this is the fused momentum + force from MLS-MPM. however, instead of our stress being derived from the energy density,
                    // i use the weak form with cauchy stress. converted:
                    // p.volume_0 * (dΨ/dF)(Fp)*(Fp_transposed)
                    // is equal to p.volume * σ

                    // note: currently "cell.v" refers to MOMENTUM, not velocity!
                    // this gets converted in the UpdateGrid step below.
                }
            }
        }
    }


    public void UpdateGrid()
    {
        for(var i = 0; i < grid_res;++i)
        {
            for (var j = 0; j < grid_res; ++j)
            {
                var cell = grid[i, j];

                if (cell.mass > 0)
                {
                    // convert momentum to velocity, apply gravity
                    cell.v /= cell.mass;
                    cell.v += dt * math.float2(0, gravity);

                    // 'slip' boundary conditions
                    int x = i;
                    int y = j;
                    if (x < 2 || x > grid_res - 3) { cell.v.x = 0; }
                    if (y < 2 || y > grid_res - 3) { cell.v.y = 0; }

                }
            }
        }
    }


    protected float2x2 Outer(float2 u, float2 v)
    {
        return new float2x2(u[0]*v[0], u[0]*v[1] , u[1]*v[0], u[1]*v[1] );

    }

    public void G2P()
    {
        foreach (var i in Enumerable.Range(0, num_particles))
        {
            var p = ps[i];
            // p.CalculateWeightMatrix();

            // reset particle velocity. we calculate it from scratch each step using the grid
            p.v = 0;

            // quadratic interpolation weights
            int2 cell_idx = (int2)p.pos;

            // constructing affine per-particle momentum matrix from APIC / MLS-MPM.
            // see APIC paper (https://web.archive.org/web/20190427165435/https://www.math.ucla.edu/~jteran/papers/JSSTS15.pdf), page 6
            // below equation 11 for clarification. this is calculating C = B * (D^-1) for APIC equation 8,
            // where B is calculated in the inner loop at (D^-1) = 4 is a constant when using quadratic interpolation functions
            float2x2 B = 0;
            p.B = 0;
            for (int gx = 0; gx < 3; ++gx)
            {
                for (int gy = 0; gy < 3; ++gy)
                {
                    var weight = p.weight[gx, gy];

                    int2 cell_x = math.int2(cell_idx.x + gx - 1, cell_idx.y + gy - 1);

                    float2 dist = (cell_x - p.pos) + 0.5f;
                    float2 weighted_velocity = grid[cell_x.x, cell_x.y].v * weight;

                    // APIC paper equation 10, constructing inner term for B
                    var term = math.float2x2(weighted_velocity * dist.x, weighted_velocity * dist.y);

                    p.v += weighted_velocity;
                    p.B += Outer(weighted_velocity, dist);

                }
            }

            // advect particles
            p.pos += p.v * dt;

            // safety clamp to ensure particles don't exit simulation domain
            p.pos = math.clamp(p.pos, 1, grid_res - 2);

            // mouse interaction
            if (mouse_down)
            {
                var dist = p.pos - mouse_pos;
                if (math.dot(dist, dist) < mouse_radius * mouse_radius)
                {
                    float norm_factor = (math.length(dist) / mouse_radius);
                    norm_factor = math.pow(math.sqrt(norm_factor), 8);
                    var force = math.normalize(dist) * norm_factor * 0.5f;
                    p.v += force;
                }
            }

            // deformation gradient update - MPM course, equation 181
            // Fp' = (I + dt * p.C) * Fp
            var Fp_new = math.float2x2(
                1, 0,
                0, 1
            );
            Fp_new += dt * p.B * 4;
            p.Fe = math.mul(Fp_new, p.Fe);

            ps[i] = p;
        }
    }

    #endregion

    private void OnDestroy()
    {
        gpuBuffer.Release();
    }
}

