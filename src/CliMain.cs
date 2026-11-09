using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ets2Save
{
    public static class CliMain
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return 1;
                }
                string command = args[0].ToLowerInvariant();
                if (command == "list") return ListCommand();
                if (command == "dump") return DumpCommand(args);
                if (command == "info") return InfoCommand(args);
                if (command == "set") return SetCommand(args);
                if (command == "trucks") return TrucksCommand(args);
                if (command == "backup") return BackupCommand(args);
                if (command == "selftest") return SelfTest();
                if (command == "freight") return FreightCommand(args);
                PrintUsage();
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("错误: " + ex.Message);
                return 2;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("ETS2 存档修改器 - 命令行");
            Console.WriteLine("  list                                   列出所有存档");
            Console.WriteLine("  info <game.sii>                        显示存档内容");
            Console.WriteLine("  dump <game.sii> [out.sii]              导出可读文本");
            Console.WriteLine("  trucks <game.sii>                      显示卡车状态");
            Console.WriteLine("  set <game.sii> --money N --xp N [--out path] [--text] [--fuel] [--repair]");
            Console.WriteLine("  backup [dir] [keep]                    备份全部存档");
            Console.WriteLine("  freight <game.sii> [--source 公司 --cargo 货物 --target 目的地 --distance km --urgency 0-2 --minutes 分 --out path]");
            Console.WriteLine("  freight <game.sii> --edit N --cargo 货物 --target 目的地 --distance km [--out path]");
            Console.WriteLine("  selftest                               校验解析/重建是否逐字节一致");
        }

        static int ListCommand()
        {
            List<SaveSlot> slots = SaveLibrary.Discover();
            Console.WriteLine("找到 {0} 个存档 (目录: {1})", slots.Count, SaveLibrary.DocumentsRoot);
            foreach (SaveSlot s in slots)
            {
                Console.WriteLine("{0,-14} {1,-20} 金钱={2,12:N0}  经验={3,9:N0}  {4:yyyy-MM-dd HH:mm}  {5}",
                    s.Slot, s.Name, s.Money, s.Experience, s.Modified, s.ProfileName);
            }
            return 0;
        }

        static int DumpCommand(string[] args)
        {
            if (args.Length < 2) { PrintUsage(); return 1; }
            SaveEditor editor = SaveEditor.Load(args[1]);
            string text = editor.File.ToText();
            string outPath = args.Length > 2 ? args[2] : Path.ChangeExtension(args[1], ".txt.sii");
            File.WriteAllText(outPath, text, new UTF8Encoding(false));
            Console.WriteLine("已导出文本: " + outPath + " (" + text.Length + " 字符)");
            return 0;
        }

        static int InfoCommand(string[] args)
        {
            if (args.Length < 2) { PrintUsage(); return 1; }
            SaveEditor editor = SaveEditor.Load(args[1]);
            Console.WriteLine("文件: " + editor.SourcePath);
            Console.WriteLine("版本: {0}, 结构: {1}, 数据块: {2}", editor.File.Version, editor.File.Structures.Count, editor.File.Blocks.Count);
            Console.WriteLine("金钱: {0:N0}", editor.Money);
            Console.WriteLine("经验: {0:N0}", editor.Experience);
            Console.WriteLine("卡车数量: " + editor.Trucks.Count);
            return 0;
        }

        static int TrucksCommand(string[] args)
        {
            if (args.Length < 2) { PrintUsage(); return 1; }
            SaveEditor editor = SaveEditor.Load(args[1]);
            foreach (TruckInfo t in editor.Trucks)
            {
                Console.WriteLine("{0}", t.Title);
                Console.WriteLine("   id={0}  油量={1:P1}  最大磨损={2:P1}", t.Ident, t.Fuel, t.MaxWear);
                Console.WriteLine("   引擎={0:P1} 变速箱={1:P1} 驾驶室={2:P1} 底盘={3:P1}", t.EngineWear, t.TransmissionWear, t.CabinWear, t.ChassisWear);
            }
            return 0;
        }

        static int SetCommand(string[] args)
        {
            if (args.Length < 2) { PrintUsage(); return 1; }
            string path = args[1];
            long? money = null, xp = null;
            string outPath = null;
            bool asText = false, fuel = false, repair = false;

            for (int i = 2; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--money" && i + 1 < args.Length) money = ParseLong(args[++i]);
                else if (a == "--xp" && i + 1 < args.Length) xp = ParseLong(args[++i]);
                else if (a == "--out" && i + 1 < args.Length) outPath = args[++i];
                else if (a == "--text") asText = true;
                else if (a == "--fuel") fuel = true;
                else if (a == "--repair") repair = true;
                else throw new ArgumentException("未知参数: " + a);
            }

            SaveEditor editor = SaveEditor.Load(path);
            if (money.HasValue) editor.Money = money.Value;
            if (xp.HasValue) editor.Experience = xp.Value;
            if (fuel) editor.FillFuel();
            if (repair) editor.RepairAll();

            if (outPath == null)
            {
                editor.Save(asText);
                Console.WriteLine("已保存: " + path);
            }
            else
            {
                File.WriteAllBytes(outPath, editor.BuildOutput(asText));
                Console.WriteLine("已写入: " + outPath + "  (" + (asText ? "文本" : "二进制") + ")");
            }
            Console.WriteLine("金钱={0:N0} 经验={1:N0}", editor.Money, editor.Experience);
            return 0;
        }

        static int FreightCommand(string[] args)
        {
            if (args.Length < 2) { PrintUsage(); return 1; }
            SaveEditor editor = SaveEditor.Load(args[1]);
            FreightMarket market = FreightMarket.Load(editor.File);
            string source = null, cargo = null, target = null, outPath = null;
            int distance = 500, urgency = 0, minutes = 2880, units = 0, editIndex = -1;
            float fill = 1f;
            for (int i = 2; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--source" && i + 1 < args.Length) source = args[++i];
                else if (a == "--cargo" && i + 1 < args.Length) cargo = args[++i];
                else if (a == "--target" && i + 1 < args.Length) target = args[++i];
                else if (a == "--distance" && i + 1 < args.Length) distance = (int)ParseLong(args[++i]);
                else if (a == "--urgency" && i + 1 < args.Length) urgency = (int)ParseLong(args[++i]);
                else if (a == "--minutes" && i + 1 < args.Length) minutes = (int)ParseLong(args[++i]);
                else if (a == "--units" && i + 1 < args.Length) units = (int)ParseLong(args[++i]);
                else if (a == "--out" && i + 1 < args.Length) outPath = args[++i];
                else if (a == "--edit" && i + 1 < args.Length) editIndex = (int)ParseLong(args[++i]);
            }

            if (editIndex >= 0)
            {
                FreightOffer existing = market.Offers[editIndex];
                market.Apply(existing, cargo, target, distance, urgency, minutes, units > 0 ? units : existing.Units, fill);
                Console.WriteLine("已修改报价 #{0}: {1} -> {2} {3} {4}km", editIndex, existing.Source, existing.Target, existing.Cargo, existing.Distance);
                string outFile = outPath ?? args[1];
                File.WriteAllBytes(outFile, editor.File.Rebuild());
                Console.WriteLine("已写入: " + outFile);
                return 0;
            }

            if (source == null)
            {
                Console.WriteLine("报价总数: {0}, 来源公司: {1}, 货物种类: {2}, 目的地: {3}",
                    market.Offers.Count, market.SourceCompanies.Count, market.Cargos.Count, market.Targets.Count);
                Console.WriteLine("游戏内时间(分钟): {0}", market.GameTime);
                for (int i = 0; i < market.Offers.Count && i < 15; i++)
                {
                    FreightOffer o = market.Offers[i];
                    Console.WriteLine("  {0} -> {1}  {2}  {3}km  urg={4}  到期={5}",
                        o.SourceLabel, o.TargetLabel, o.Cargo, o.Distance, o.Urgency, o.Expiry);
                }
                Console.WriteLine("货物示例: " + string.Join(", ", market.Cargos.GetRange(0, Math.Min(12, market.Cargos.Count)).ToArray()));
                return 0;
            }

            FreightOffer added = market.Add(source, cargo, target, distance, urgency, minutes, units, fill);
            Console.WriteLine("已添加: {0} -> {1} {2} {3}km", added.Source, added.Target, added.Cargo, added.Distance);
            byte[] output = editor.File.Rebuild();
            string path = outPath ?? args[1];
            File.WriteAllBytes(path, output);
            Console.WriteLine("已写入: " + path + " (" + output.Length + " 字节)");
            return 0;
        }

        static int SelfTest()
        {
            List<SaveSlot> slots = SaveLibrary.Discover();
            int failed = 0;
            foreach (SaveSlot slot in slots)
            {
                try
                {
                    string container;
                    byte[] payload = Scsc.LoadPayload(slot.GamePath, out container);
                    SiiFile file = SiiParser.Parse(payload);
                    byte[] rebuilt = file.Rebuild();
                    bool same = rebuilt.Length == payload.Length;
                    int diff = 0;
                    if (same)
                    {
                        for (int i = 0; i < payload.Length; i++)
                            if (payload[i] != rebuilt[i]) { diff++; if (diff > 5) break; }
                        same = diff == 0;
                    }
                    Console.WriteLine("{0,-18} {1,-26} blocks={2,-6} {3}", slot.Slot, slot.Name,
                        file.Blocks.Count, same ? "OK" : ("MISMATCH len " + rebuilt.Length + "/" + payload.Length + " diff " + diff));
                    if (!same) failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("{0,-18} {1,-26} ERROR {2}", slot.Slot, slot.Name, ex.Message);
                }
            }
            Console.WriteLine(failed == 0 ? "全部通过" : ("失败 " + failed + " 个"));
            return failed == 0 ? 0 : 3;
        }

        static int BackupCommand(string[] args)
        {
            string root = args.Length > 1 ? args[1]
                : Path.Combine(SaveLibrary.DocumentsRoot, "存档备份");
            int keep = 30;
            if (args.Length > 2) keep = (int)ParseLong(args[2]);
            List<string> written = SaveLibrary.BackupAll(root, keep);
            Console.WriteLine("已备份 {0} 个存档到 {1}", written.Count, root);
            foreach (string path in written) Console.WriteLine("  " + path);
            return 0;
        }

        static long ParseLong(string text)
        {
            long value;
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
            double d;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return (long)d;
            throw new ArgumentException("无效的数字: " + text);
        }
    }
}
