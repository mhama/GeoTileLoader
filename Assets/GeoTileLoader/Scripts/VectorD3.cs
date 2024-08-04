
using System;

namespace GeoTile
{
    [Serializable]
    public struct VectorD3
    {
        public double x;
        public double y;
        public double z;

        public VectorD3(double[] src)
        {
            x = src[0];
            y = src[1];
            z = src[2];
        }

        public VectorD3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static VectorD3 operator+ (VectorD3 lhs, VectorD3 rhs)
        {
            return new VectorD3(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
        }

        public static VectorD3 operator- (VectorD3 lhs, VectorD3 rhs)
        {
            return new VectorD3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
        }

        public static VectorD3 operator- (VectorD3 lhs)
        {
            return new VectorD3(-lhs.x, -lhs.y, -lhs.z);
        }

        public static VectorD3 operator* (VectorD3 lhs, double scalar)
        {
            return new VectorD3(lhs.x * scalar, lhs.y * scalar, lhs.z * scalar);
        }


        public double magnitude => Math.Sqrt(x * x + y * y + z * z);
    }
}