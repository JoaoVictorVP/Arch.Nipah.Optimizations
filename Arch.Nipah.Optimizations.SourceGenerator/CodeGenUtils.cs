using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class CodeGenUtils
{
    public static IEnumerable<string> NamespaceAndSubNamespacesFrom(string nms)
    {
        var parts = nms.Split('.');
        for (int i = 0; i < parts.Length; i++)
            yield return string.Join(".", parts.Take(i + 1));
    }
}
