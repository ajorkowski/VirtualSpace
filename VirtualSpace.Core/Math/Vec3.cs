namespace VirtualSpace.Core.Math
{
    public struct Vec3
    {
        private float _x;
        private float _y;
        private float _z;

        public Vec3(float x, float y, float z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        public float X { get { return _x; } set { _x = value; } }
        public float Y { get { return _y; } set { _y = value; } }
        public float Z { get { return _z; } set { _z = value; } }
    }
}
