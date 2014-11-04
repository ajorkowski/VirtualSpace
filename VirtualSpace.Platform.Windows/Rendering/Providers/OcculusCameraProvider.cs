using SharpDX;
using SharpDX.Toolkit;
using SharpOVR;

namespace VirtualSpace.Platform.Windows.Rendering.Providers
{
    internal sealed class OcculusCameraProvider : CameraProvider
    {
        private Matrix _view;
        private Matrix _projection;
        private bool _useBaseMatrix;

        public OcculusCameraProvider(Game game)
            : base(game)
        {
            _useBaseMatrix = true;
        }

        public void UseBaseMatrix()
        {
            _useBaseMatrix = true;
        }

        public void UseOcculusEye(ref EyeRenderDesc desc, ref PoseF pose)
        {
            var rot = Matrix.RotationQuaternion(pose.Orientation);
            rot.Transpose();
            _view = base.View * rot * Matrix.Translation(desc.ViewAdjust);

            // Calculate projection matrix
            _projection = OVR.MatrixProjection(desc.Fov, 0.01f, 200.0f, true);
            _projection.Transpose();

            _useBaseMatrix = false;
        }

        public override Matrix View
        {
            get
            {
                return _useBaseMatrix ? base.View : _view;
            }
        }

        public override Matrix Projection
        {
            get
            {
                return _useBaseMatrix ? base.Projection : _projection;
            }
        }
    }
}
