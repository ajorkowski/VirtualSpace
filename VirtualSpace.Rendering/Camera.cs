using OpenTK;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Rendering
{
    public sealed class Camera : ICamera
    {
        public Camera()
        {
            M = new CameraMatrices
            {
                View = Matrix4.Identity,
                Projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 800f / 600f, 1.0f, 200f)
            };
        }

        public CameraMatrices M { get; private set; }
        public bool HasUpdate { get; set; }

        public void MoveRelative(float x, float y, float z)
        {
            var trans = Matrix4.CreateTranslation(x, y, z);
            Matrix4.Mult(ref M.View, ref trans, out M.View);
            HasUpdate = true;
        }

        public void MoveAbsolute(float x, float y, float z)
        {
            var trans = Matrix4.CreateTranslation(x, y, z);
            Matrix4.Mult(ref trans, ref M.View, out M.View);
            HasUpdate = true;
        }

        public void MoveTo(float x, float y, float z)
        {
            var trans = Matrix4.CreateTranslation(x, y, z);
            var noTrans = M.View.ClearTranslation();
            Matrix4.Mult(ref trans, ref noTrans, out M.View);
            HasUpdate = true;
        }

        public void LookAt(float x, float y, float z)
        {
            M.View = Matrix4.LookAt(M.View.ExtractTranslation(), new Vector3(x, y, z), new Vector3(M.View.M21, M.View.M22, M.View.M23));
            HasUpdate = true;
        }
    }
}
