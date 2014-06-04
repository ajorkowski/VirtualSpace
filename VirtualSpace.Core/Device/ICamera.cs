namespace VirtualSpace.Core.Device
{
    public interface ICamera
    {
        void MoveRelative(float x, float y, float z);
        void MoveAbsolute(float x, float y, float z);
        void MoveTo(float x, float y, float z);

        void LookAt(float x, float y, float z);
    }
}
