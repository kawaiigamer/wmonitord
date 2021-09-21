using System;
using System.Collections.Generic;

namespace wmonitord
{
    internal class WindowActivityInformation
    {
        public long msElapsed = 0;
        public long lastTimeUpdated = 0;
        public readonly HashSet<string> titles = new HashSet<string>();
    }
}
