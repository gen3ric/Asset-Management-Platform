﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asset_Management_Platform.Messages
{
    public class DatabaseMessage
    {
        public string Message;
        public bool Success;

        public DatabaseMessage()
        {

        }

        public DatabaseMessage(string message, bool success)
        {
            Message = message;
            Success = success;
        }
    }
}
