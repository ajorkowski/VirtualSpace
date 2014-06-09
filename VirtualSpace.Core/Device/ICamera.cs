using OpenTK;

namespace VirtualSpace.Core.Device
{
    public interface ICamera
    {
        CameraMatrices M { get; }
        bool HasUpdate { get; set; }

        void MoveRelative(float x, float y, float z);
        void MoveAbsolute(float x, float y, float z);
        void MoveTo(float x, float y, float z);

        void LookAt(float x, float y, float z);
    }
}
