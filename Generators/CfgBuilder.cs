using System.Text;

namespace NetConfigPro.Generators;

/// <summary>按厂商注释风格输出配置命令块，带层级缩进。</summary>
public sealed class CfgBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly string _comment;
    private int _level;

    public CfgBuilder(string commentToken) => _comment = commentToken;

    /// <summary>输出带分隔线的章节标题。</summary>
    public CfgBuilder Section(string title)
    {
        Blank();
        var bar = _comment == "!" ? new string('!', 62) : new string('#', 62);
        Line(bar);
        Line($"{_comment} {title}");
        Line(bar);
        return this;
    }

    /// <summary>输出普通注释。</summary>
    public CfgBuilder Note(string text) => Line($"{_comment} {text}");

    public CfgBuilder Line(string text = "")
    {
        if (text.Length > 0 && _level > 0) _sb.Append(' ', _level);
        _sb.AppendLine(text);
        return this;
    }

    public CfgBuilder Blank()
    {
        if (_sb.Length > 0) _sb.AppendLine();
        return this;
    }

    public IndentScope Indent(int spaces = 1)
    {
        _level += spaces;
        return new IndentScope(this, spaces);
    }

    private void Outdent(int spaces) => _level = Math.Max(0, _level - spaces);

    public sealed class IndentScope : IDisposable
    {
        private readonly CfgBuilder _owner;
        private readonly int _spaces;
        public IndentScope(CfgBuilder owner, int spaces) { _owner = owner; _spaces = spaces; }
        public void Dispose() => _owner.Outdent(_spaces);
    }

    public bool IsEmpty => _sb.Length == 0;

    public override string ToString() => _sb.ToString();
}
