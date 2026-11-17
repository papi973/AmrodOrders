using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Orders.Domain
{
    public class DomainException( string message, string code) : Exception(message)
    {
        public string Code { get; } = code;
    }
}