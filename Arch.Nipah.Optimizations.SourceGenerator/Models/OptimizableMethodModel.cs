using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator.Models;

public class OptimizableMethodModel
{
    public MethodHeader Header { get; }
    public List<QueryModel> Queries { get; } = new(32);

    public OptimizableMethodModel(MethodHeader header)
    {
        Header = header;
    }
}
