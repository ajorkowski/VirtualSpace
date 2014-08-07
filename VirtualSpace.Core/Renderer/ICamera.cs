using VirtualSpace.Core.Math;

namespace VirtualSpace.Core.Renderer
{
    public interface ICamera
    {
        void MoveRelative(Vec3 vec);
        void RotateRelative(Vec3 rot);
        void MoveAbsolute(Vec3 move);
        void MoveTo(Vec3 pos);

        void LookAt(Vec3 pos);

        Vec3 FindPointInWorldSpace(Vec3 pos);
    }
}
