﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chat
{
    public class ACSEvent
    {
        public string Id { get; set; }

        public string ChatSessionThreadId { get; set; }

        public Dictionary<string, ACSRoom> Rooms { get; set; }
    }

    public class ACSRoom
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string ChatSessionThreadId { get; set; }

        public string CallingSessionId { get; set; }
    }
}