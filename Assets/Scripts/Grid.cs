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
        void OnDrawGizmos(Matrix4x4 parent);
    }
    public class Grid<T> : IDrawGizmos, IEnumerable<T> where T : IDrawGizmos, new()
    {
        public enum CenterType
        {
            Center,
            LeftBottom,
            LeftMiddle,
            //TODO, add all possible for cell center type
        }

        public int DataLength => dimesion.x * dimesion.y * dimesion.z;
        public T this[int x, int y, int z = 0]
        {
            get => this.data[this.ToIndex(new int3(x, y, z))];
            set => this.data[this.ToIndex(new int3(x, y, z))] = value;
        }

        public T this[int3 idx]
        {
            get => this.data[this.ToIndex(idx)];
            set => this.data[this.ToIndex(idx)] = value;
        }

        public int3 Dim => this.dimesion;

        public Bounds Bounds => new Bounds(this.start + this.dimesion * this.spacing / 2, this.dimesion * this.spacing);

        protected T[] data;
        protected int3 dimesion;
        protected float3 spacing;
        protected float3 start;
        protected CenterType centerType;

        public Grid(int3 dimesion, float3 cellSpacing, float3 leftBottom, CenterType centerType = CenterType.Center)
        {
            this.dimesion = dimesion;
            this.spacing = cellSpacing;
            this.start = leftBottom;
            this.centerType = centerType;

            this.InitData();
        }
        public float3 IndexToCenter(int3 index)
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
            return new int3((pos - this.start) / this.spacing);
        }

        public int ToIndex(int3 index)
        {
            //note index.xyz is not row/colum
            //they are accessed as coordinate
            //same as thread group in compute shader
            //and start from 0
            return index.x + index.y * this.dimesion.x + index.z * this.dimesion.x * this.dimesion.y;
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

            var gpos = this.IndexToCenter(gindex);
            var dis = pos - gpos;

            var w = this.N(dis.x) * this.N(dis.y) * this.N(dis.z);
            return w;
        }
        public float3x3 GetD()
        {
            var v = 0.25f * this.spacing * this.spacing;
            return
            new float3x3(v.x>0?v.x:1, 0, 0,
                         0, v.y>0?v.y:1, 0,
                         0, 0, v.z>0?v.z:1);
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
            LogTool.AssertIsTrue(this.dimesion.x > 0);
            LogTool.AssertIsTrue(this.dimesion.y > 0);
            LogTool.AssertIsTrue(this.dimesion.z > 0);
            this.data = new T[this.DataLength];

            foreach (var d in Enumerable.Range(0, this.data.Length))
            {
                this.data[d] = new T();
            }
        }
        public virtual void OnDrawGizmos(Matrix4x4 parent)
        {
            // using (new GizmosScope(Gizmos.color, parent))
            {
                foreach (var x in Enumerable.Range(0, this.dimesion.x))
                    foreach (var y in Enumerable.Range(0, this.dimesion.y))
                        foreach (var z in Enumerable.Range(0, this.dimesion.z))
                        {
                            var center = this.IndexToCenter(new int3(x, y, z));
                            Gizmos.DrawWireCube(center, this.spacing);

                            using (new GizmosScope(Color.red, Matrix4x4.Translate(center) * parent))
                            {
                                this[x, y, z].OnDrawGizmos(Matrix4x4.identity);
                            }
                        }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.data.Cast<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
