using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class HashingUtils
{
    public static int GetDeterministicHashCode(this string str)
    {
        int hash = 0;
        for (int i = 0; i < str.Length; i++)
            hash = (hash << 5) - hash + str[i];
        return hash;
    }
}
