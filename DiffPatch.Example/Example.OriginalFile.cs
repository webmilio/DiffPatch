using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiffPatch.Example
{
    class Example
    {
        public int x;

        void SomeMethod()
        {
            x = x + 1;

            Console.WriteLine("Bab {0}", x);
        }
    }
}
