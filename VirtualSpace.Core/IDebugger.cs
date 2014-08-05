namespace VirtualSpace.Core
{
    public interface IDebugger
    {
        void WriteLine(string format, params object[] items);

        string LastWrite { get; }
    }
}
