using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace UnityMPM
{
    public static class Tool
    {
        public static readonly float3x3 identity = new float3x3(1,0,0,0,1,0,0,0,1);
        public static List<float3> GenerateBox(float3 center, float3 size, float spacing = 1)
        {
            var ret = new List<float3>();
            var count = new int3(size / spacing) + 1;
            foreach (var x in Enumerable.Range(0, count.x))
            {
                foreach (var y in Enumerable.Range(0, count.y))
                {
                    foreach (var z in Enumerable.Range(0, count.z))
                    {
                        var local = new float3(x, y, z) * spacing;
                        ret.Add(center - size * 0.5f + local);
                    }
                }
            }
            return ret;
        }

        public static List<float3> GenerateBox(float3 size)
        {
            var ret = new List<float3>();
            var count = new int3(size) + 1;
            
            var scout = size;
            scout.z = scout.z == 0?1f:scout.z;
            foreach (var x in Enumerable.Range(0, count.x))
            {
                foreach (var y in Enumerable.Range(0, count.y))
                {
                    foreach (var z in Enumerable.Range(0, count.z))
                    {
                        var local = new float3(x, y, z) / scout;
                        ret.Add(local);
                    }
                }
            }

            return ret;
        }
    }
}