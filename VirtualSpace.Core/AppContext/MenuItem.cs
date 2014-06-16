using System;
using System.Collections.Generic;

namespace VirtualSpace.Core.AppContext
{
    public class MenuItem
    {
        public string Name { get; set; }
        public Action Click { get; set; }
        public IEnumerable<MenuItem> Children { get; set; }
    }
}
