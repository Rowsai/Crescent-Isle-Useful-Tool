using System.Collections.Generic;
using System.Linq;
using CrescentIsleUsefulTool.Modules.Debug;
using ECommons;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.Commands;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Commands;

[OcelotCommand]
public class MainCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciut";
    }

    protected override string Description
    {
        get => @"
メイン画面を開きます。
 - /ciut : メイン画面を開く
 - /ciut config : 設定画面を開く
 - /ciut illegal [on|off|toggle] : 不正モードを操作する
 - /ciut buff : バフ更新を実行する
 - /ciut tp [pot|ce|fate] : 対象に最も近いエーテライトへ移動する
 - /ciut language <en|de|fr|jp|uwu> : 表示言語を変更する
--------------------------------
".Trim();
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/crescent", "/crescentisle"];
    }

    private readonly IReadOnlyList<string> languageCodes =
    [
        "en", "de", "fr", "jp", "uwu",
    ];

    public override void Execute(string command, string arguments)
    {
        if (arguments is "config" or "cfg")
        {
            plugin.Windows.ToggleConfigUI();
            return;
        }

#if DEBUG_BUILD
        if (arguments == "debug")
        {
            plugin.Windows.GetWindow<DebugWindow>().Toggle();
            return;
        }
#endif

        if (arguments == "buff")
        {
            new BuffCommand(plugin).Execute("/ciutbuff", "");
            return;
        }

        if (arguments == "tp" || arguments.StartsWith("tp "))
        {
            new TeleportCommand(plugin).Execute("/ciuttp", arguments.ReplaceFirst("tp", "").Trim());
            return;
        }

        if (arguments == "illegal" || arguments.StartsWith("illegal "))
        {
            new IllegalModeCommand(plugin).Execute("/ciutillegal", arguments.ReplaceFirst("illegal", "").Trim());
            return;
        }

        if (arguments.StartsWith("language"))
        {
            var parts = arguments.Split(' ', 2);
            if (parts.Length == 2)
            {
                var code = parts[1].Trim().ToLowerInvariant();
                if (languageCodes.Contains(code))
                {
                    I18N.SetLanguage(code);
                    Svc.Chat.Print($"表示言語を {code} に変更しました。");
                    return;
                }

                Svc.Chat.PrintError($"対応していない言語コードです: {code}");
                return;
            }

            Svc.Chat.Print("使用方法: /ciut language <en|de|fr|jp|uwu>");
            return;
        }

        plugin.Windows.ToggleMainUI();
    }
}
