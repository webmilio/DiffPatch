using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiffPatch.Example
{
    internal class Example
    {
        void SomeMethod()
        {
            Console.WriteLine("Bab {0}", ++X);
        }

        public int X { get; set; }
    }
}
