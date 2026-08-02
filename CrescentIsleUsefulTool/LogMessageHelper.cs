using System.Text.RegularExpressions;
using System.Text;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace CrescentIsleUsefulTool;

public static class LogMessageHelper
{
    public static string GetLogMessagePattern(uint id)
    {
        // ToString()/ExtractText() removes numeric macro payloads entirely.
        // Preserve the macro form so messages such as Magi Treasuresight keep
        // <num(lnum1)> and <num(lnum2)> available to the regex.
        var macro = Svc.Data.GetExcelSheet<LogMessage>().GetRow(id).Text.ToMacroString();
        var builder = new StringBuilder();
        var offset = 0;
        foreach (Match match in Regex.Matches(macro, @"<num\((\w+)\)>|<[^>]+>"))
        {
            builder.Append(Regex.Escape(macro[offset..match.Index]));
            builder.Append(match.Groups[1].Success
                ? $"(?<{match.Groups[1].Value}>\\d+)"
                : ".*?");
            offset = match.Index + match.Length;
        }

        builder.Append(Regex.Escape(macro[offset..]));
        return builder.ToString();
    }
}
