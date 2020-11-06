using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityTools.Debuging;
using UnityTools.Debuging.EditorTool;

namespace UnityMPM
{
    public interface IDrawGizmos
    {
        void OnDrawGizmos();
    }
    [System.Serializable]
    public class Grid<Cell> : IDrawGizmos, IEnumerable<Cell> where Cell : IDrawGizmos, new()
    {
        public enum CenterType
        {
            Center,
            LeftBottom,
            LeftMiddle,
            //TODO, add all possible for cell center type
        }

        public int DataLength => size.x * size.y * size.z;
        public Cell this[int x, int y, int z = 0]
        {
            get => this.data[this.ToIndex(new int3(x, y, z))];
            set => this.data[this.ToIndex(new int3(x, y, z))] = value;
        }

        public Cell this[int3 idx]
        {
            get => this.data[this.ToIndex(idx)];
            set => this.data[this.ToIndex(idx)] = value;
        }

        public int3 Dim => this.size;
        public float3 H => new float3(this.spacing.x > 0 ? this.spacing.x : 1,
                                      this.spacing.y > 0 ? this.spacing.y : 1,
                                      this.spacing.z > 0 ? this.spacing.z : 1);
        public float CellVolume => this.H.x * this.H.y * this.H.z;

        public Bounds Bounds => new Bounds(this.start + this.size * this.spacing / 2, this.size * this.spacing);

        protected Cell[] data;
        protected int3 size;
        protected float3 spacing;
        protected float3 start;
        protected CenterType centerType;

        [SerializeField] protected bool visualize = false;
        [SerializeField] protected float cellScale = 1f;

        public Grid(int3 size, float3 cellSpacing, float3 leftBottom, CenterType centerType = CenterType.Center)
        {
            this.size = size;
            this.spacing = cellSpacing;
            this.start = leftBottom;
            this.centerType = centerType;

            this.InitData();
        }
        public float3 IndexToCellPos(int3 index)
        {
            switch (this.centerType)
            {
                case CenterType.Center: return this.start + (new float3(index) + 0.5f) * this.spacing;
                case CenterType.LeftMiddle: return this.start + (new float3(index) + new float3(0.5f, 0, 0)) * this.spacing;
                case CenterType.LeftBottom:
                default: return this.start + index * this.spacing;
            }
        }
        public int3 ToIndex(float3 pos)
        {
            return new int3((pos - this.start) / this.H);
        }

        public int ToIndex(int3 index)
        {
            //note index.xyz is not row/colum
            //they are accessed as coordinate
            //same as thread group in compute shader
            //and start from 0
            return index.x + index.y * this.size.x + index.z * this.size.x * this.size.y;
        }

        public bool InGrid(int3 index)
        {
            var didx = this.ToIndex(index);
            return 0 <= didx && didx < this.data.Length;
        }


        public float GetWeight(float3 pos, int3 delta)
        {
            var gindex = this.ToIndex(pos) + delta;
            if (!this.InGrid(gindex)) return 0;

            var gpos = this.IndexToCellPos(gindex);
            var dis = pos - gpos;
            var invH = 1f / this.H;
            dis *= invH;

            var w = this.N(dis.x) * this.N(dis.y) * (this.spacing.z > 0 ? this.N(dis.z) : 1);
            return w;
        }

        public float3 GetWeightGradient(float3 pos, int3 delta)
        {
            var gindex = this.ToIndex(pos) + delta;
            if (!this.InGrid(gindex)) return 0;

            var gpos = this.IndexToCellPos(gindex);
            var dis = pos - gpos;
            var invH = 1f / this.H;
            dis *= invH;

            var wx = this.N(dis.x);
            var wy = this.N(dis.y);
            var wz = this.spacing.z > 0 ? this.N(dis.z) : 1;

            var wdx = this.DevN(dis.x);
            var wdy = this.DevN(dis.y);
            var wdz = this.spacing.z > 0 ? this.DevN(dis.z) : 1;

            return invH * new float3(wdx * wy * wz, wx * wdy * wz, wx * wy * wdz);
        }
        public float3x3 GetD()
        {
            var v = 0.25f * this.H * this.H;
            return
            new float3x3(v.x, 0, 0,
                         0, v.y, 0,
                         0, 0, v.z);
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
            if (absx < 0.5f) return -2 * x;
            if (absx < 1.5f) return x > 0 ? absx - 1.5f : -(absx - 1.5f);
            return 0;
        }



        protected virtual void InitData()
        {
            LogTool.AssertIsTrue(this.size.x > 0);
            LogTool.AssertIsTrue(this.size.y > 0);
            LogTool.AssertIsTrue(this.size.z > 0);
            this.data = new Cell[this.DataLength];

            foreach (var d in Enumerable.Range(0, this.data.Length))
            {
                this.data[d] = new Cell();
            }
        }
        public virtual void OnDrawGizmos()
        {
            if(!this.visualize) return;

            foreach (var x in Enumerable.Range(0, this.size.x))
                foreach (var y in Enumerable.Range(0, this.size.y))
                    foreach (var z in Enumerable.Range(0, this.size.z))
                    {
                        var center = this.IndexToCellPos(new int3(x, y, z));
                        Gizmos.DrawWireCube(center, this.spacing);

                        using (new GizmosScope(Color.red, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * this.cellScale)))
                        {
                            this[x, y, z].OnDrawGizmos();
                        }
                    }
        }

        public IEnumerator<Cell> GetEnumerator()
        {
            return this.data.Cast<Cell>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
