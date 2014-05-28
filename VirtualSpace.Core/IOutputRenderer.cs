using System;
namespace VirtualSpace.Core
{
    public interface IOutputRenderer : IDisposable
    {
        void Run();
    }
}
