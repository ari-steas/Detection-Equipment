using System;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    public static class MathUtils
    {
        public static Random Random = new Random();

        public static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        public static double ClampAbs(double value, double absMax)
        {
            if (value < -absMax)
                return -absMax;
            if (value > absMax)
                return absMax;
            return value;
        }

        public static double ClampAbs(double value, double absMin, double absMax)
        {
            if (value < 0)
            {
                if (value > -absMin)
                    return -absMin;
                if (value < -absMax)
                    return -absMax;
            }
            else
            {
                if (value < absMin)
                    return absMin;
                if (value > absMax)
                    return absMax;
            }
            return value;
        }

        public static double MinAbs(double value1, double value2)
        {
            if (Math.Abs(value1) < Math.Abs(value2))
                return value1;
            return value2;
        }

        public static double MaxAbs(double value1, double value2)
        {
            if (Math.Abs(value1) > Math.Abs(value2))
                return value1;
            return value2;
        }

        public static double LimitRotationSpeed(double currentAngle, double targetAngle, double maxRotationSpeed)
        {
            // https://yal.cc/angular-rotations-explained/
            // It should NOT HAVE BEEN THAT HARD
            // I (aristeas) AM REALLY STUPID

            var diff = NormalizeAngle(targetAngle - currentAngle);

            // clamp rotations by speed:
            if (diff < -maxRotationSpeed) return NormalizeAngle(currentAngle - maxRotationSpeed);
            if (diff > maxRotationSpeed) return NormalizeAngle(currentAngle + maxRotationSpeed);
            // if difference within speed, rotation's done:
            return targetAngle;
        }

        public static double NormalizeAngle(double angleRads, double limit = Math.PI)
        {
            if (angleRads > limit)
                return angleRads % limit - limit;
            if (angleRads < -limit)
                return angleRads % limit + limit;
            return angleRads;
        }

        public static bool LineIntersect(this BoundingSphereD sphere, LineD line)
        {
            Vector3D v = line.From - sphere.Center;
            double num1 = line.Direction.Dot(line.Direction);
            double num2 = 2.0 * v.Dot(line.Direction);
            double num3 = v.Dot(v) - sphere.Radius * sphere.Radius;
            double d = num2 * num2 - 4.0 * num1 * num3;
            if (d < 0.0)
                return false;
            double tmin = (-num2 - Math.Sqrt(d)) / (2.0 * num1);
            double tmax = (-num2 + Math.Sqrt(d)) / (2.0 * num1);
            if (tmin > tmax)
            {
                //double num4 = tmin;
                tmin = tmax;
                //tmax = num4;
            }
            return tmin <= line.Length;
        }

        public static bool RayIntersect(this BoundingSphereD sphere, LineD line)
        {
            Vector3D v = line.From - sphere.Center;
            double a = line.Direction.Dot(line.Direction);
            double b = 2 * v.Dot(line.Direction);
            double c = v.Dot(v) - sphere.Radius * sphere.Radius;
            double discriminant = b * b - 4 * a * c;
            return discriminant > 0;
        }

        public static Vector3D RandomCone(Vector3D centerDirection, double radius)
        {
            // Optimized random vector cone. Assume normalized center direction.
            //double azi = Math.Atan2(centerDirection.X, centerDirection.Z);
            //if (double.IsNaN(azi))
            //    azi = Math.PI;
            //
            //double elev = Math.Asin(-centerDirection.Y);
            //if (double.IsNaN(elev))
            //    elev = Math.PI;
            //
            //azi += radius * (1 - 2 * Random.NextDouble());
            //elev += radius * (1 - 2 * Random.NextDouble());
            //
            //var cosElev = Math.Cos(elev);
            //
            //return new Vector3D(
            //    Math.Sin(azi) * cosElev,
            //    Math.Cos(azi) * cosElev,
            //    Math.Sin(elev)
            //);

            Vector3D axis = Vector3D.CalculatePerpendicularVector(centerDirection).Rotate(centerDirection, Math.PI * 2 * Random.NextDouble());
            return centerDirection.Rotate(axis, radius * Random.NextDouble());
        }

        public static Vector3D RandomSphere(Vector3D position, double radius)
        {
            return RandomCone(Vector3D.Forward, Math.PI) * radius * Random.NextDouble() + position;
        }

        public static double ToDecibels(double power, double reference = 1)
        {
            return 10 * Math.Log10(power / reference);
        }

        public static double FromDecibels(double dB, double reference = 1)
        {
            return Math.Pow(10, dB / 10) * reference;
        }

        public static Vector2D GetAngleTo(MatrixD matrix, Vector3D? targetPos)
        {
            if (targetPos == null)
                return Vector2D.Zero;
        
            Vector3D vecFromTarget = matrix.Translation - targetPos.Value;
        
            vecFromTarget = Vector3D.Rotate(vecFromTarget.Normalized(), MatrixD.Invert(matrix));
        
            double desiredAzimuth = Math.Atan2(vecFromTarget.X, vecFromTarget.Z);
            if (double.IsNaN(desiredAzimuth))
                desiredAzimuth = Math.PI;
        
            double desiredElevation = Math.Asin(-vecFromTarget.Y);
            if (double.IsNaN(desiredElevation))
                desiredElevation = Math.PI;
        
            return new Vector2D(desiredAzimuth, desiredElevation);
        }

        public static Vector2 Abs(this Vector2 vec)
        {
            return new Vector2(vec.X < 0 ? -vec.X : vec.X, vec.Y < 0 ? -vec.Y : vec.Y);
        }

        public static float Angle(this Vector2 vec)
        {
            return (float) Math.Atan2(vec.Y, vec.X);
        }

        public static Vector2 FromPolar(float angle, float length)
        {
            return new Vector2(length * (float) Math.Cos(angle), length * (float) Math.Sin(angle));
        }

        public static bool IntersectBoxD(Vector3D position, Vector3D direction, Vector3D boxMin, Vector3D boxMax, out double tMin, out double tMax)
        {
            double num1 = 1.0 / direction.X;
            double num2 = 1.0 / direction.Y;
            double num3 = 1.0 / direction.Z;
            double val1_1 = (boxMin.X - position.X) * num1;
            double val2_1 = (boxMax.X - position.X) * num1;
            double val1_2 = (boxMin.Y - position.Y) * num2;
            double val2_2 = (boxMax.Y - position.Y) * num2;
            double val1_3 = (boxMin.Z - position.Z) * num3;
            double val2_3 = (boxMax.Z - position.Z) * num3;
            tMin = Math.Max(Math.Max(Math.Min(val1_1, val2_1), Math.Min(val1_2, val2_2)), Math.Min(val1_3, val2_3));
            tMax = Math.Min(Math.Min(Math.Max(val1_1, val2_1), Math.Max(val1_2, val2_2)), Math.Max(val1_3, val2_3));
            return tMax >= 0.0 && tMin <= tMax;
        }

        public static void LargestComponent(ref Vector3D vec)
        {
            var absX = Math.Abs(vec.X);
            var absY = Math.Abs(vec.Y);
            var absZ = Math.Abs(vec.Z);

            if (absX >= absY && absX >= absZ)
            {
                vec.Y = 0;
                vec.Z = 0;
            }
            else if (absY >= absX && absY >= absZ)
            {
                vec.X = 0;
                vec.Z = 0;
            }
            else if (absZ >= absX && absZ >= absY)
            {
                vec.X = 0;
                vec.Y = 0;
            }
        }
    }
}
