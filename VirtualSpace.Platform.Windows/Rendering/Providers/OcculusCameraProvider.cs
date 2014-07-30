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
            // Calculate view matrix
            _view = Matrix.Translation(desc.ViewAdjust) * Matrix.RotationQuaternion(pose.Orientation) * Matrix.Translation(pose.Position * -Vector3.UnitZ) * base.View;

            //var rollPitchYaw = Matrix.RotationY(eyeYaw);
            //var finalRollPitchYaw = rollPitchYaw * Matrix.RotationQuaternion(pose.Orientation);
            //var finalUp = Vector3.TransformNormal(new Vector3(0, 1, 0), finalRollPitchYaw);
            //var finalForward = Vector3.TransformNormal(new Vector3(0, 0, 1), finalRollPitchYaw);
            //var shiftedEyePos = eyePos + Vector3.TransformNormal(pose.Position * -Vector3.UnitZ, rollPitchYaw);
            //_view = Matrix.Translation(desc.ViewAdjust) * Matrix.LookAtRH(shiftedEyePos, shiftedEyePos + finalForward, finalUp);

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
