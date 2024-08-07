using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
using HaselCommon.Services;
using HaselCommon.Utils;
using HaselDebug.Abstracts;
using HaselDebug.Utils;
using ImGuiNET;

namespace HaselDebug.Tabs;

#pragma warning disable SeStringRenderer
public unsafe class RaptureTextModuleTab(IPluginLog PluginLog, IChatGui ChatGui, ExcelService ExcelService) : DebugTab
{
    public override unsafe void Draw()
    {
        using var tabs = ImRaii.TabBar("RaptureTextModuleTab_TabBar");
        if (!tabs) return;

        DrawGlobalParameters();
        DrawDefinitions();
        DrawStringMaker();
    }

    private void DrawGlobalParameters()
    {
        using var tab = ImRaii.TabItem("GlobalParameters");
        if (!tab) return;

        using var table = ImRaii.Table("GlobalParametersTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY);
        if (!table) return;

        ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("ValuePtr", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(5, 1);
        ImGui.TableHeadersRow();

        var deque = RaptureTextModule.Instance()->GlobalParameters;
        for (var i = 0u; i < deque.MySize; i++)
        {
            var item = deque[i];

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // Id
            ImGui.TextUnformatted(i.ToString());

            ImGui.TableNextColumn(); // Type
            ImGui.TextUnformatted(item.Type.ToString());

            ImGui.TableNextColumn(); // ValuePtr
            DebugUtils.DrawAddress(item.ValuePtr);

            ImGui.TableNextColumn(); // Value
            switch (item.Type)
            {
                case TextParameterType.Integer:
                    DebugUtils.DrawCopyableText($"0x{item.IntValue:X}");
                    ImGui.SameLine();
                    DebugUtils.DrawCopyableText(item.IntValue.ToString());
                    break;

                case TextParameterType.ReferencedUtf8String:
                    if (item.ReferencedUtf8StringValue != null)
                        DebugUtils.DrawSeString(item.ReferencedUtf8StringValue->Utf8String.StringPtr, new NodeOptions { AddressPath = new AddressPath([(nint)i, (nint)item.ReferencedUtf8StringValue]), Indent = false });
                    else
                        ImGui.TextUnformatted("null");
                    break;

                case TextParameterType.String:
                    DebugUtils.DrawSeString(item.StringValue, new NodeOptions { Indent = false });
                    break;
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(i switch
            {
                0 => "Player Name",
                1 => "Log Message Name 1",
                2 => "Log Message Name 2",
                3 => "Player Sex",
                10 => "Eorzea Time Hours",
                11 => "Eorzea Time Minutes",
                12 => "Log Text Colors - Chat 1 - Say",
                13 => "Log Text Colors - Chat 1 - Shout",
                14 => "Log Text Colors - Chat 1 - Tell",
                15 => "Log Text Colors - Chat 1 - Party",
                16 => "Log Text Colors - Chat 1 - Alliance",
                17 => "Log Text Colors - Chat 2 - LS1",
                18 => "Log Text Colors - Chat 2 - LS2",
                19 => "Log Text Colors - Chat 2 - LS3",
                20 => "Log Text Colors - Chat 2 - LS4",
                21 => "Log Text Colors - Chat 2 - LS5",
                22 => "Log Text Colors - Chat 2 - LS6",
                23 => "Log Text Colors - Chat 2 - LS7",
                24 => "Log Text Colors - Chat 2 - LS8",
                25 => "Log Text Colors - Chat 2 - Free Company",
                26 => "Log Text Colors - Chat 2 - PvP Team",
                29 or 30 => "Log Text Colors - Chat 1 - Emotes",
                31 => "Log Text Colors - Chat 1 - Yell",
                34 => "Log Text Colors - Chat 2 - CWLS1",
                27 => "Log Text Colors - General - PvP Team Announcements",
                28 => "Log Text Colors - Chat 2 - Novice Network",
                32 => "Log Text Colors - General - Free Company Announcements",
                33 => "Log Text Colors - General - Novice Network Announcements",
                35 => "Log Text Colors - Battle - Damage Dealt",
                36 => "Log Text Colors - Battle - Missed Attacks",
                37 => "Log Text Colors - Battle - Actions",
                38 => "Log Text Colors - Battle - Items",
                39 => "Log Text Colors - Battle - Healing",
                40 => "Log Text Colors - Battle - Enchanting Effects",
                41 => "Log Text Colors - Battle - Enfeebing Effects",
                42 => "Log Text Colors - General - Echo",
                43 => "Log Text Colors - General - System Messages",
                54 => "Companion Name",
                56 => "Log Text Colors - General - Battle System Messages",
                57 => "Log Text Colors - General - Gathering System Messages",
                58 => "Log Text Colors - General - Error Messages",
                59 => "Log Text Colors - General - NPC Dialogue",
                60 => "Log Text Colors - General - Item Drops",
                61 => "Log Text Colors - General - Level Up",
                62 => "Log Text Colors - General - Loot",
                63 => "Log Text Colors - General - Synthesis",
                64 => "Log Text Colors - General - Gathering",
                67 => "Player ClassJobId",
                68 => "Player Level",
                70 => "Player Race",
                71 => "Player Sycned Level",
                77 => "Client/Plattform?",
                82 => "Datacenter Region (see WorldDCGroupType sheet)",
                92 => "TerritoryType Id",
                95 => "Log Role Color - Tank (LogColorRoleTank)",
                97 => "Log Role Color - Healer (LogColorRoleHealer)",
                99 => "Log Role Color - DPS (LogColorRoleDPS)",
                101 => "Log Role Color - Other (LogColorOtherClass)",
                _ => "",
            });
        }
    }

    private void DrawDefinitions()
    {
        using var tab = ImRaii.TabItem("Definitions");
        if (!tab) return;

        using var table = ImRaii.Table("DefinitionsTable", 13, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY);
        if (!table) return;

        ImGui.TableSetupColumn("Code", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("TotalParamCount", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("ParamCount", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("IsTerminated", ImGuiTableColumnFlags.WidthFixed, 60);
        for (var i = 0; i < 7; i++)
            ImGui.TableSetupColumn($"{i}", ImGuiTableColumnFlags.WidthFixed, 20);

        ImGui.TableSetupScrollFreeze(13, 1);
        ImGui.TableHeadersRow();

        var raptureTextModule = RaptureTextModule.Instance();

        foreach (var item in raptureTextModule->TextModule.MacroEncoder.MacroCodeMap)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // Code
            ImGui.TextUnformatted(item.Item1.ToString());

            ImGui.TableNextColumn(); // Id
            ImGui.TextUnformatted($"0x{item.Item2.Id:X}");

            ImGui.TableNextColumn(); // TotalParamCount
            ImGui.TextUnformatted(item.Item2.TotalParamCount.ToString());

            ImGui.TableNextColumn(); // ParamCount
            ImGui.TextUnformatted(item.Item2.ParamCount.ToString());

            ImGui.TableNextColumn(); // IsTerminated
            ImGui.TextUnformatted(item.Item2.IsTerminated.ToString());

            ImGui.TableNextColumn();
            for (var i = 0; i < 7; i++)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(((char)item.Item2.ParamTypes[i]).ToString());
            }
        }

    }

    private void DrawStringMaker()
    {
        using var tab = ImRaii.TabItem("StringMaker");
        if (!tab) return;

        var raptureTextModule = RaptureTextModule.Instance();

        var ArrowUpButtonSize = ImGuiUtils.GetIconButtonSize(FontAwesomeIcon.ArrowUp);
        var ArrowDownButtonSize = ImGuiUtils.GetIconButtonSize(FontAwesomeIcon.ArrowDown);
        var TrashButtonSize = ImGuiUtils.GetIconButtonSize(FontAwesomeIcon.Trash);
        var TerminalButtonSize = ImGuiUtils.GetIconButtonSize(FontAwesomeIcon.Terminal);

        var entryToRemove = -1;
        var entryToMoveUp = -1;
        var entryToMoveDown = -1;

        for (var i = 0; i < entries.Count; i++)
        {
            var key = $"##Entry{i}";
            using var child = ImRaii.Child(key + "_Child", new(-1, 120), true);
            var entry = entries[i];
            entry.Draw(i);

            ImGui.Separator();

            if (i > 0)
            {
                if (ImGuiUtils.IconButton(key + "_Up", FontAwesomeIcon.ArrowUp, "Move up"))
                {
                    entryToMoveUp = i;
                }
            }
            else
            {
                ImGui.Dummy(ArrowUpButtonSize);
            }

            ImGui.SameLine();

            if (i < entries.Count - 1)
            {
                if (ImGuiUtils.IconButton(key + "_Down", FontAwesomeIcon.ArrowDown, "Move down"))
                {
                    entryToMoveDown = i;
                }
            }
            else
            {
                ImGui.Dummy(ArrowDownButtonSize);
            }

            ImGui.SameLine();

            if (ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift))
            {
                if (ImGuiUtils.IconButton(key + "_Delete", FontAwesomeIcon.Trash, "Delete"))
                {
                    entryToRemove = i;
                }
            }
            else
            {
                ImGuiUtils.IconButton(
                    key + "_Delete",
                    FontAwesomeIcon.Trash,
                    "Delete with shift",
                    disabled: true);
            }
        }

        if (entryToMoveUp != -1)
        {
            var removedItem = entries[entryToMoveUp];
            entries.RemoveAt(entryToMoveUp);
            entries.Insert(entryToMoveUp - 1, removedItem);
        }

        if (entryToMoveDown != -1)
        {
            var removedItem = entries[entryToMoveDown];
            entries.RemoveAt(entryToMoveDown);
            entries.Insert(entryToMoveDown + 1, removedItem);
        }

        if (entryToRemove != -1)
        {
            entries.RemoveAt(entryToRemove);
        }

        if (ImGui.Button("Add entry"))
        {
            entries.Add(new(ExcelService));
        }

        ImGui.SameLine();
        if (ImGui.Button("PrintString"))
        {
            using var temp = new Utf8String();
            var tmpptr = &temp;
            tmpptr->BufSize = 0;
            foreach (var entry in entries)
            {
                var output = entry.Run();
                temp.Append(output);
            }
            RaptureLogModule.Instance()->PrintString(temp.StringPtr);
        }

        ImGui.SameLine();
        if (ImGui.Button("PrintMessage"))
        {
            var temp = Utf8String.CreateEmpty();
            foreach (var entry in entries)
            {
                var output = entry.Run();
                temp->Append(output);
            }
            using var sender = new Utf8String("HaselDebug");
            PluginLog.Debug($"new message id: {RaptureLogModule.Instance()->PrintMessage(16, &sender, temp, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false)} -> {temp->ToString()}");
            temp->Dtor(true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Print via Dalamud"))
        {
            using var temp = new Utf8String();
            var tmpptr = &temp;
            tmpptr->BufSize = 0;
            foreach (var entry in entries)
            {
                var output = entry.Run();
                temp.Append(output);
            }
            using var sender = new Utf8String("HaselDebug");
            ChatGui.Print(new Dalamud.Game.Text.XivChatEntry()
            {
                Type = Dalamud.Game.Text.XivChatType.Party,
                Message = SeString.Parse(temp.AsSpan()),
                Name = SeString.Parse(sender.AsSpan()),
                Timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Silent = false
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Print via Dalamud (Silent)"))
        {
            using var temp = new Utf8String();
            var tmpptr = &temp;
            tmpptr->BufSize = 0;
            foreach (var entry in entries)
            {
                var output = entry.Run();
                temp.Append(output);
            }
            using var sender = new Utf8String("HaselDebug");
            ChatGui.Print(new Dalamud.Game.Text.XivChatEntry()
            {
                Type = Dalamud.Game.Text.XivChatType.Party,
                Message = SeString.Parse(temp.AsSpan()),
                Name = SeString.Parse(sender.AsSpan()),
                Timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Silent = true
            });
        }

        ImGui.TextUnformatted(raptureTextModule->TextModule.MacroEncoder.EncoderError.ToString());
    }

    private enum TextEntryType
    {
        String,
        Macro,
        Fixed
    }

    private class TextEntry : IDisposable
    {
        private readonly Utf8String* _str;
        private readonly ExcelService ExcelService;

        //private string _message = "<sheet(ENpcResident,1046073,0)>";
        //private string _message = "<denoun(ENpcResident,1,1046099,2,3)>";
        private string _message = "<denoun(ENpcResident,1,1046099,2,1)>"; // "<denoun(Item,1,36664,1,1)>"; // <fixed(200,4,39246,1,0,0,Phoenix Riser Suit)>
        private int _type = 2;

        public TextEntry(ExcelService excelService)
        {
            ExcelService = excelService;
            _str = Utf8String.FromString("<denoun(ENpcResident,1,1046099,2,1)>");
        }

        public TextEntry(ExcelService excelService, TextEntryType type, string text)
        {
            ExcelService = excelService;

            _type = (int)type;
            _str = Utf8String.CreateEmpty();
            _message = text;
        }

        public Utf8String* Run()
        {
            _str->Clear();

            switch (_type)
            {
                case 0:
                    _str->ConcatCStr(_message);
                    break;
                case 1:
                    AppendMacro(_message);
                    break;
                case 2:
                    //AppendFixed(_message);
                    break;
            }

            return _str;
        }

        private void AppendMacro(string macro)
        {
            using var output = new Utf8String();
            RaptureTextModule.Instance()->MacroEncoder.EncodeString(&output, macro);
            _str->Append(&output);
        }

        public void Dispose()
        {
            _str->Dtor(true);
        }

        public void Draw(int index)
        {
            ImGui.RadioButton($"IsString##{index}_isString", ref _type, 0);
            ImGui.SameLine();
            ImGui.RadioButton($"IsMacro##{index}_isMacro", ref _type, 1);
            ImGui.SameLine();
            ImGui.RadioButton($"IsFixed##{index}_isFixed", ref _type, 2);

            ImGui.InputText($"Message##{index}_Message", ref _message, 255);

            Run();

            using var output = new Utf8String();
            var ptr = &output;
            RaptureTextModule.Instance()->TextModule.FormatString(_str->StringPtr, null, ptr);
            DebugUtils.DrawUtf8String((nint)ptr, new NodeOptions() { AddressPath = new AddressPath((nint)ptr) });
        }
    }

    private readonly List<TextEntry> entries = [
        new TextEntry(ExcelService, TextEntryType.String, "Test1 "),
        new TextEntry(ExcelService, TextEntryType.Macro, "<color(0xFF9000)>"),
        new TextEntry(ExcelService, TextEntryType.String, "Test2 "),
        new TextEntry(ExcelService, TextEntryType.Macro, "<color(0)>"),
        new TextEntry(ExcelService, TextEntryType.String, "Test3 "),
        new TextEntry(ExcelService, TextEntryType.Macro, "<color(stackcolor)>"),
        new TextEntry(ExcelService, TextEntryType.String, "Test 4 "),
        new TextEntry(ExcelService, TextEntryType.Macro, "<color(stackcolor)>"),
        new TextEntry(ExcelService, TextEntryType.String, "Test 5"),
    ];

}
