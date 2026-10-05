using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Win32;
using SimDeck.Core;

namespace SimDeck.App;

public static class SnowRunnerProfile
{
    public static readonly Dictionary<string,string> GameActions = new()
    {
        ["snowEngine"]="Truck.Engine",["snowParkingBrake"]="Truck.Handbrake",["snowAwd"]="Truck.Awd",
        ["snowDifferential"]="Truck.DiffLock",["snowGearbox"]="Truck.ClutchMode",["snowHorn"]="Truck.Honk",
        ["snowHeadlights"]="Truck.Headlights",["snowCamera"]="Truck.ToggleCamera",["snowFunctions"]="Truck.Functions",
        ["snowQuickWinch"]="Truck.QuickWinch",["snowWinch"]="Truck.PullWinch",["snowReleaseWinch"]="Truck.ReleaseWinch",
        ["snowPackCargo"]="Exploration.StartCargoManagement",["snowMap"]="Exploration.ShowMinimap",
        ["snowTasks"]="Exploration.PlayerProfile",["snowGearLow"]="Exploration.SetGearLow2",
        ["snowGearLowMinus"]="Exploration.SetGearLow1",["snowGearLowPlus"]="Exploration.SetGearLow3",
        ["snowGearAuto"]="Exploration.SetGearAuto",["snowGearHigh"]="Exploration.SetGearHigh",
        ["snowGearReverse"]="Exploration.SetGearReverse",["snowGearNeutral"]="Exploration.SetGearNeutral"
    };
    public static GameProfile Default() => new("snowrunner","SnowRunner","SnowRunner",
    [
        A("snowEngine","Вождение","ДВИГАТЕЛЬ","B","Запуск / остановка двигателя"),
        A("snowParkingBrake","Вождение","РУЧНИК","Space","Ручной тормоз"),
        A("snowAwd","Вождение","ПОЛНЫЙ ПРИВОД","E","Требует установленный полный привод"),
        A("snowDifferential","Вождение","БЛОКИРОВКА","Q","Для переключаемой блокировки выберите L"),
        A("snowHorn","Вождение","СИГНАЛ","G","Удерживайте для сигнала","hold"),
        A("snowHeadlights","Вождение","ФАРЫ","L","Включить / выключить свет"),
        A("snowCamera","Вождение","КАМЕРА","D1","Переключить внешний вид / кабину"),
        A("snowPause","Вождение","ПАУЗА / НАЗАД","Escape","Закрыть меню или открыть паузу"),
        A("snowGearbox","Коробка","СЕЛЕКТОР","LeftShift","Удерживайте и выбирайте передачу в игре","hold"),
        A("snowGearLowMinus","Коробка","L −","NumPad1","Пониженная: медленная"),
        A("snowGearLow","Коробка","L","NumPad4","Пониженная"),
        A("snowGearLowPlus","Коробка","L +","NumPad7","Пониженная: быстрая"),
        A("snowGearAuto","Коробка","АВТОМАТ","NumPad5","Автоматическая коробка"),
        A("snowGearHigh","Коробка","H","NumPad8","Высокая передача; требует подходящую коробку"),
        A("snowGearReverse","Коробка","R","NumPad2","Задняя передача"),
        A("snowGearNeutral","Коробка","N","NumPad6","Нейтральная передача; назначьте NumPad6 в игре"),
        A("snowFunctions","Функции","ФУНКЦИИ","V","Открыть / закрыть меню техники"),
        A("snowQuickWinch","Функции","БЫСТРАЯ ЛЕБЁДКА","F","Подсоединить лебёдку к ближайшей точке",group:"Лебёдка"),
        A("snowWinch","Функции","ТЯНУТЬ ЛЕБЁДКУ","F","Удерживайте: тянуть установленную лебёдку","hold","Лебёдка"),
        A("snowReleaseWinch","Функции","ОТПУСТИТЬ ЛЕБЁДКУ","R","Отсоединить лебёдку",group:"Лебёдка"),
        A("snowPackCargo","Функции","УПРАВЛЕНИЕ ГРУЗОМ","C","Открыть управление грузом; упаковка выбирается в меню",group:"Груз"),
        A("snowMap","Навигация","КАРТА","M","Открыть карту игры"),
        A("snowTasks","Навигация","ПРОФИЛЬ / ЗАДАЧИ","F4","Открыть профиль игрока"),
        A("snowBack","Навигация","НАЗАД В ИГРЕ","Escape","Закрыть карту или текущее меню")
    ],2);
    static DeckAction A(string id,string page,string label,string key,string description,string gesture="press",string group="") =>
        new(id,page,label,description,key,gesture,group);

    public static GameProfile Upgrade(GameProfile old)
    {
        var current=Default();var before=AdditionalProfiles.LegacySnowRunner();
        var defaults=before.Actions.ToDictionary(a=>a.Id,a=>a.Key);var ids=current.Actions.Select(a=>a.Id).ToHashSet();
        var existing=old.Actions.ToDictionary(a=>a.Id);
        return current with { Actions=[..current.Actions.Select(a=>existing.TryGetValue(a.Id,out var saved)&&
            (!defaults.TryGetValue(a.Id,out var original)||saved.Key!=original)?a with{Key=saved.Key}:a),
            ..old.Actions.Where(a=>!ids.Contains(a.Id)&&(!defaults.TryGetValue(a.Id,out var original)||a.Key!=original))],
            Revision=Math.Max(current.Revision,old.Revision+1) };
    }
    public static Dictionary<string,string> ReadBindings(string text)
    {
        using var json=JsonDocument.Parse(text.TrimEnd('\0'));
        var result=new Dictionary<string,string>();
        if(!json.RootElement.TryGetProperty("UserSettings",out var settings)||!settings.TryGetProperty("bindings",out var bindings)||bindings.ValueKind!=JsonValueKind.Array)return result;
        foreach(var binding in bindings.EnumerateArray())
        {
            if(!binding.TryGetProperty("device",out var device)||device.GetString()!="keyboard"||
                !binding.TryGetProperty("combIndex",out var comb)||!comb.TryGetInt32(out var index)||index!=0||
                !binding.TryGetProperty("keyCode",out var code)||!code.TryGetInt32(out var scan)||scan is <=0 or >255||
                !binding.TryGetProperty("toolLink",out var link))continue;
            var native=scan<128?(uint)scan:(uint)((scan&127)|0xe000);
            var vk=MapVirtualKey(native,3);var key=scan switch { 0x47=>Key.NumPad7,0x48=>Key.NumPad8,0x49=>Key.NumPad9,0x4b=>Key.NumPad4,0x4c=>Key.NumPad5,0x4d=>Key.NumPad6,0x4f=>Key.NumPad1,0x50=>Key.NumPad2,0x51=>Key.NumPad3,0x52=>Key.NumPad0,0x53=>Key.Decimal, _=>KeyInterop.KeyFromVirtualKey((int)vk) };
            if(key==Key.None||vk==0)continue;
            foreach(var item in GameActions.Where(p=>p.Value==link.GetString())) result[item.Key]=key.ToString();
        }
        return result;
    }
    public static string? FindBindings()
    {
        var candidates=new List<string>();var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) is string steam)roots.Add(steam);
        foreach(var process in Process.GetProcessesByName("SnowRunner")) using(process)
        {
            try { if(process.MainModule is { } m) { var game=Directory.GetParent(m.FileName)?.Parent?.Parent; if(game?.Parent?.Parent?.Parent is { } root)roots.Add(root.FullName); } }
            catch(System.ComponentModel.Win32Exception) { }
        }
        foreach(var root in roots)
        {
            var userdata=Path.Combine(root,"userdata");if(!Directory.Exists(userdata))continue;
            foreach(var user in Directory.EnumerateDirectories(userdata))
            {var file=Path.Combine(user,"1465360","remote","user_settings.cfg");if(File.Exists(file))candidates.Add(file);}
        }
        var storage=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"My Games","SnowRunner","base","storage");
        if(Directory.Exists(storage))foreach(var dir in Directory.EnumerateDirectories(storage))
            foreach(var name in new[]{"user_settings.cfg","user_settings.dat"}) {var file=Path.Combine(dir,name);if(File.Exists(file))candidates.Add(file);}
        return candidates.Distinct().OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint type);
}
