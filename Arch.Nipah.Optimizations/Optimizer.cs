using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arch.Nipah.Optimizations;

public static class Optimizer
{
    public static T OutOfScope<T>(Func<T> func) => func();

    public class Break : Exception { }
}
