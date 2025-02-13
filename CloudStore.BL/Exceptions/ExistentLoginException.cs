﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudStore.BL.Exceptions
{
    public class ExistentLoginException : Exception
    {
        public ExistentLoginException() : base("This Login is exist")
        {
            
        }
    }
}
