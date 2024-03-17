using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class StringBuilderCodeExtensions
{
    public static StringBuilder Indent(this StringBuilder sb, int count = 1)
    {
        for (int i = 0; i < count; i++)
            sb.Append("    ");
        return sb;
    }

    public static StringBuilderCSAttributeBuilder WithAttribute(this StringBuilder sb, string attributeName)
        => new(sb, attributeName);
}
public record struct StringBuilderCSAttributeBuilder(StringBuilder SB, string AttributeName)
{
    readonly List<object> arguments = new(0);
    readonly List<(string, object)> properties = new(0);

    public StringBuilderCSAttributeBuilder Argument(object value)
    {
        arguments.Add(value);
        return this;
    }

    public StringBuilderCSAttributeBuilder Property(string name, object value)
    {
        properties.Add((name, value));
        return this;
    }

    public StringBuilder Into()
    {
        SB.Append('[').Append(AttributeName);
        if (arguments.Count > 0)
        {
            SB.Append('(');
            for (int i = 0; i < arguments.Count; i++)
            {
                if (i > 0)
                    SB.Append(", ");
                SB.Append(arguments[i]);
            }

            for (int i = 0; i < properties.Count; i++)
            {
                if (i > 0 || arguments.Count > 0)
                    SB.Append(", ");
                var (name, value) = properties[i];
                SB.Append(name).Append(" = ").Append(value);
            }

            SB.Append(')');
        }
        SB.Append(']');
        return SB;
    }
}
