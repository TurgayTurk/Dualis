using System.Text;

namespace Dualis.SourceGen;

/// <summary>
/// Minimal helper for emitting source with indentation.
/// Wraps a StringBuilder and provides basic indentation-aware write APIs.
/// </summary>
internal sealed class CodeWriter(int capacity = 0, string indentUnit = "    ")
{
    private readonly StringBuilder _sb = capacity > 0 ? new StringBuilder(capacity) : new StringBuilder();
    private int _indent;

    public void Indent() => _indent++;

    public void Unindent()
    {
        if (_indent > 0)
        {
            _indent--;
        }
    }

    public void OpenBlock()
    {
        WriteLine("{");
        Indent();
    }

    public void CloseBlock(string? suffix = null)
    {
        Unindent();
        if (suffix is null)
        {
            WriteLine("}");
        }
        else
        {
            WriteLine("}" + suffix);
        }
    }

    // Back-compat overload for older call sites that were passing an extra unused parameter.
    public void CloseBlock(string suffix, int _) => CloseBlock(suffix);

    public void Write(string text) => _sb.Append(text);

    public void WriteRaw(string text) => _sb.Append(text);

    public void WriteLine() => _sb.AppendLine();

    public void WriteLine(string text)
    {
        for (int i = 0; i < _indent; i++)
        {
            _sb.Append(indentUnit);
        }
        _sb.AppendLine(text);
    }

    public void WriteLineRaw(string text) => _sb.AppendLine(text);

    public override string ToString() => _sb.ToString();
}
