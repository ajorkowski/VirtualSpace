using System;
using System.Collections.Generic;

namespace VirtualSpace.Core.Device
{
    public class MenuItem
    {
        public string Name { get; set; }
        public Action Click { get; set; }
        public IEnumerable<MenuItem> Children { get; set; }
    }
}
