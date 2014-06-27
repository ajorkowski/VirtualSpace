using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualSpace.Core.Renderer.Screen
{
    public interface IVideoScreen : IScreen
    {
        void Play();
        void Stop();
    }
}
