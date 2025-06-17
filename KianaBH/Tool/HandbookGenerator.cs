﻿using KianaBH.Data;
using KianaBH.GameServer.Command;
using KianaBH.Internationalization;
using KianaBH.Util;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace KianaBH.KianaBH.Tool;

public static class HandbookGenerator
{
    public static void GenerateAll()
    {
        var directory = new DirectoryInfo(ConfigManager.Config.Path.ResourcePath + "/TextMap");
        var handbook = new DirectoryInfo(ConfigManager.Config.Path.HandbookPath);
        if (!handbook.Exists) handbook.Create();
        if (!directory.Exists) return;

        foreach (var langFile in directory.GetFiles())
        {
            if (langFile.Extension != ".json") continue;
            var lang = langFile.Name.Replace("TextMap", "").Replace(".json", "");

            // Check if handbook needs to regenerate
            var handbookPath = $"{ConfigManager.Config.Path.HandbookPath}/Handbook{lang}.txt";
            if (File.Exists(handbookPath))
            {
                var handbookInfo = new FileInfo(handbookPath);
                if (handbookInfo.LastWriteTime >= langFile.LastWriteTime)
                    continue; // Skip if handbook is newer than language file
            }

            Generate(lang);
        }

        Logger.GetByClassName()
            .Info(I18NManager.Translate("Server.ServerInfo.GeneratedItem", I18NManager.Translate("Word.Handbook")));
    }

    public static void Generate(string lang)
    {
        var textMapPath = ConfigManager.Config.Path.ResourcePath + "/TextMap/TextMap" + lang + ".json";
        
        if (!File.Exists(textMapPath))
        {
            Logger.GetByClassName().Error(I18NManager.Translate("Server.ServerInfo.FailedToReadItem", textMapPath,
                I18NManager.Translate("Word.NotFound")));
            return;
        }

        List<TextMapEntry> textMapList = JsonConvert.DeserializeObject<List<TextMapEntry>>(File.ReadAllText(textMapPath))!;

        if (textMapList == null)
        {
            Logger.GetByClassName().Error(I18NManager.Translate("Server.ServerInfo.FailedToReadItem", textMapPath,
                I18NManager.Translate("Word.Error")));
            return;
        }

        Dictionary<long, string> textMap = [];

        foreach (var map in textMapList) textMap.Add(map.Value!.Hash, map.Text!);

        var builder = new StringBuilder();
        builder.AppendLine("#Handbook generated in " + DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
        builder.AppendLine();
        builder.AppendLine("#Command");
        builder.AppendLine();
        GenerateCmd(builder, lang);

        builder.AppendLine();
        builder.AppendLine("#Valkyrie");
        builder.AppendLine();
        GenerateValk(builder, textMap);

        builder.AppendLine();
        builder.AppendLine("#Elf");
        builder.AppendLine();
        GenerateElf(builder, textMap);

        builder.AppendLine();
        builder.AppendLine("#Stigmata");
        builder.AppendLine();
        GenerateStigmata(builder, textMap);

        builder.AppendLine();
        builder.AppendLine("#Weapon");
        builder.AppendLine();
        GenerateWeapon(builder, textMap);

        builder.AppendLine();
        WriteToFile(lang, builder.ToString());
    }

    public static void GenerateCmd(StringBuilder builder, string lang)
    {
        foreach (var cmd in CommandManager.CommandInfo)
        {
            builder.Append("\t" + cmd.Key);
            var desc = I18NManager.TranslateAsCertainLang(lang, cmd.Value.Description).Replace("\n", "\n\t\t");
            builder.AppendLine(": " + desc);
        }
    }

    public static void WriteToFile(string lang, string content)
    {
        File.WriteAllText($"{ConfigManager.Config.Path.HandbookPath}/Handbook{lang}.txt", content);
    }

    public static void GenerateValk(StringBuilder builder, Dictionary<long, string> map)
    {
        foreach (var avatar in GameData.AvatarData.Values)
        {
            var name = map.TryGetValue(avatar.FullName.hash, out var value) ? value : $"[{avatar.FullName.hash}]";
            builder.AppendLine(avatar.AvatarID + ": " + name);
        }
    }

    public static void GenerateElf(StringBuilder builder, Dictionary<long, string> map)
    {
        foreach (var elf in GameData.ElfAstraMateData.Values)
        {
            var name = map.TryGetValue(elf.FullName.hash, out var value) ? value : $"[{elf.FullName.hash}]";
            builder.AppendLine(elf.ElfID + ": " + name);
        }
    }

    public static void GenerateStigmata(StringBuilder builder, Dictionary<long, string> map)
    {
        foreach (var stigmata in GameData.StigmataData.Values)
        {
            var name = map.TryGetValue(stigmata.DisplayTitle.hash, out var value) ? value : $"[{stigmata.DisplayTitle.hash}]";
            builder.AppendLine(stigmata.ID + ": " + name);
        }
    }

    public static void GenerateWeapon(StringBuilder builder, Dictionary<long, string> map)
    {
        foreach (var weapon in GameData.WeaponData.Values)
        {
            var name = map.TryGetValue(weapon.DisplayTitle.hash, out var value) ? value : $"[{weapon.DisplayTitle.hash}]";
            builder.AppendLine(weapon.ID + ": " + name);
        }
    }
}

public class TextMapEntry
{
    [JsonPropertyName("value")] public ValueEntry? Value { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public class ValueEntry
{
    [JsonPropertyName("hash")] public int Hash { get; set; }
}