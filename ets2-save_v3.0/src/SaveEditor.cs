using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ets2Save
{
    public sealed class TruckInfo
    {
        public SiiBlock Block;
        public string Ident;
        public string Plate;
        public long Odometer;
        public float Fuel;
        public float EngineWear;
        public float TransmissionWear;
        public float CabinWear;
        public float ChassisWear;
        public float[] WheelsWear;

        public float MaxWear
        {
            get
            {
                float m = Math.Max(Math.Max(EngineWear, TransmissionWear), Math.Max(CabinWear, ChassisWear));
                if (WheelsWear != null)
                    for (int i = 0; i < WheelsWear.Length; i++) m = Math.Max(m, WheelsWear[i]);
                return m;
            }
        }

        public string Title
        {
            get
            {
                string plate = CleanPlate(Plate);
                if (string.IsNullOrEmpty(plate)) plate = Ident;
                return string.Format("{0}  ·  {1:N0} km", plate, Odometer);
            }
        }

        public static string CleanPlate(string plate)
        {
            if (string.IsNullOrEmpty(plate)) return plate;
            int cut = plate.IndexOf('<');
            if (cut >= 0) plate = plate.Substring(0, cut);
            cut = plate.IndexOf('|');
            if (cut >= 0) plate = plate.Substring(0, cut);
            return plate.Trim().TrimEnd('.');
        }
    }

    /// <summary>High level access to the editable values of one save slot.</summary>
    public sealed class SaveEditor
    {
        public string SourcePath;
        public SiiFile File;

        public SiiBlock Bank;
        public SiiBlock Economy;
        public SiiBlock Player;

        public List<TruckInfo> Trucks = new List<TruckInfo>();

        // ------------------------------------------------------------------
        public static SaveEditor Load(string gameSiiPath)
        {
            string container;
            byte[] payload = Scsc.LoadPayload(gameSiiPath, out container);
            if (Scsc.DetectFormat(payload) != "bsii")
                throw new SiiException("该存档是文本格式（SiiNunit）。请先进入游戏载入这个存档并存档一次，\n游戏会把它写回二进制格式，然后再用本工具修改。");

            SaveEditor editor = new SaveEditor();
            editor.SourcePath = gameSiiPath;
            editor.File = SiiParser.Parse(payload);
            editor.Bind();
            return editor;
        }

        void Bind()
        {
            Bank = File.FindBlock("bank");
            Economy = File.FindBlock("economy");
            Player = File.FindBlock("player");
            if (Bank == null || Economy == null || Player == null)
                throw new SiiException("存档结构不完整（缺少 bank/economy/player）");

            Dictionary<string, SiiBlock> byId = new Dictionary<string, SiiBlock>(StringComparer.Ordinal);
            for (int i = 0; i < File.Blocks.Count; i++)
                byId[File.Blocks[i].Ident] = File.Blocks[i];

            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            object refs = Player.Get("my_vehicles");
            string[] vehicles = refs as string[];
            if (vehicles != null)
            {
                for (int i = 0; i < vehicles.Length; i++)
                {
                    SiiBlock pv;
                    if (!byId.TryGetValue(vehicles[i], out pv)) continue;
                    object vehRef = pv.Get("vehicle");
                    string vehId = vehRef as string;
                    if (vehId == null) continue;
                    SiiBlock vehicle;
                    if (!byId.TryGetValue(vehId, out vehicle)) continue;
                    if (!used.Add(vehId)) continue;
                    Trucks.Add(MakeTruck(vehicle));
                }
            }

            if (Trucks.Count == 0)
            {
                // Fall back to every vehicle unit in the file.
                List<SiiBlock> all = File.FindBlocks("vehicle");
                for (int i = 0; i < all.Count && i < 64; i++)
                    Trucks.Add(MakeTruck(all[i]));
            }
        }

        static TruckInfo MakeTruck(SiiBlock block)
        {
            TruckInfo t = new TruckInfo();
            t.Block = block;
            t.Ident = block.Ident;
            t.Plate = block.Get("license_plate") as string;
            object od = block.Get("odometer");
            t.Odometer = od == null ? 0 : Convert.ToInt64(od, CultureInfo.InvariantCulture);
            t.Fuel = ToFloat(block.Get("fuel_relative"));
            t.EngineWear = ToFloat(block.Get("engine_wear"));
            t.TransmissionWear = ToFloat(block.Get("transmission_wear"));
            t.CabinWear = ToFloat(block.Get("cabin_wear"));
            t.ChassisWear = ToFloat(block.Get("chassis_wear"));
            t.WheelsWear = block.Get("wheels_wear") as float[];
            return t;
        }

        static float ToFloat(object value)
        {
            if (value == null) return 0f;
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------
        public long Money
        {
            get { return Convert.ToInt64(Bank.Get("money_account"), CultureInfo.InvariantCulture); }
            set { File.SetInt(Bank, "money_account", value); }
        }

        public long Experience
        {
            get { return Convert.ToInt64(Economy.Get("experience_points"), CultureInfo.InvariantCulture); }
            set { File.SetInt(Economy, "experience_points", value); }
        }

        public long DrivingTime
        {
            get { return Convert.ToInt64(Player.Get("driving_time"), CultureInfo.InvariantCulture); }
        }

        public void ClearFatigue()
        {
            File.SetInt(Player, "driving_time", 0);
            SiiValue v = Player.GetValue("time_since_mandatory_break");
            if (v != null) File.SetInt(Player, "time_since_mandatory_break", 0);
            v = Player.GetValue("on_break_time");
            if (v != null) File.SetInt(Player, "on_break_time", 0);
            v = Player.GetValue("last_sleep_time");
            if (v != null) File.SetInt(Player, "last_sleep_time", 0);
        }

        public void ClearDebt()
        {
            SiiValue v = Bank.GetValue("overdraft");
            if (v != null && v.Type == SiiType.Bool) File.SetBool(Bank, "overdraft", false);
            v = Player.GetValue("gas_pump_money_debt");
            if (v != null && v.Type == SiiType.Single) File.SetFloat(Player, "gas_pump_money_debt", 0f);
        }

        public void FillFuel()
        {
            for (int i = 0; i < Trucks.Count; i++)
            {
                TruckInfo t = Trucks[i];
                SiiValue v = t.Block.GetValue("fuel_relative");
                if (v != null && v.Type == SiiType.Single)
                {
                    File.SetFloat(t.Block, "fuel_relative", 1f);
                    t.Fuel = 1f;
                }
            }
        }

        public void RepairAll()
        {
            for (int i = 0; i < Trucks.Count; i++) Repair(Trucks[i]);
        }

        public void Repair(TruckInfo t)
        {
            SetWear(t, "engine_wear", 0f);
            SetWear(t, "transmission_wear", 0f);
            SetWear(t, "cabin_wear", 0f);
            SetWear(t, "chassis_wear", 0f);
            SiiValue wheels = t.Block.GetValue("wheels_wear");
            if (wheels != null && wheels.Type == SiiType.SingleArray)
            {
                float[] arr = (float[])wheels.Data;
                for (int i = 0; i < wheels.Count; i++)
                {
                    File.SetFloatArrayItem(t.Block, "wheels_wear", i, 0f);
                    arr[i] = 0f;
                }
            }
            SiiValue engineU = t.Block.GetValue("engine_wear_unfixable");
            if (engineU != null) SetWear(t, "engine_wear_unfixable", 0f);
            SiiValue transU = t.Block.GetValue("transmission_wear_unfixable");
            if (transU != null) SetWear(t, "transmission_wear_unfixable", 0f);
            SiiValue cabinU = t.Block.GetValue("cabin_wear_unfixable");
            if (cabinU != null) SetWear(t, "cabin_wear_unfixable", 0f);
            SiiValue chassisU = t.Block.GetValue("chassis_wear_unfixable");
            if (chassisU != null) SetWear(t, "chassis_wear_unfixable", 0f);
            t.EngineWear = 0f;
            t.TransmissionWear = 0f;
            t.CabinWear = 0f;
            t.ChassisWear = 0f;
            if (t.WheelsWear != null)
                for (int i = 0; i < t.WheelsWear.Length; i++) t.WheelsWear[i] = 0f;
        }

        void SetWear(TruckInfo t, string field, float value)
        {
            SiiValue v = t.Block.GetValue(field);
            if (v == null || v.Type != SiiType.Single) return;
            File.SetFloat(t.Block, field, value);
        }

        // ------------------------------------------------------------------
        public byte[] BuildOutput(bool asText)
        {
            if (asText) return new UTF8Encoding(false).GetBytes(File.ToText());
            return File.Rebuild();
        }

        public string Save(bool asText)
        {
            string path = SourcePath;
            byte[] output = BuildOutput(asText);
            VerifyOutput(output, asText);

            string backup = Backup(path);
            string temp = path + ".tmp";
            System.IO.File.WriteAllBytes(temp, output);
            System.IO.File.Replace(temp, path, null);
            return backup;
        }

        /// <summary>Refuses to write anything that cannot be parsed back.</summary>
        static void VerifyOutput(byte[] output, bool asText)
        {
            if (output == null || output.Length < 32) throw new SiiException("生成的存档为空，已取消写入");
            if (asText)
            {
                string head = Encoding.UTF8.GetString(output, 0, Math.Min(16, output.Length));
                if (!head.StartsWith("SiiNunit", StringComparison.Ordinal))
                    throw new SiiException("生成的文本存档格式不正确，已取消写入");
            }
            else
            {
                SiiFile check = SiiParser.Parse(output);
                if (check.Blocks.Count == 0) throw new SiiException("生成的二进制存档为空，已取消写入");
            }
        }

        public static string Backup(string path)
        {
            string dir = Path.Combine(Path.GetDirectoryName(path), "editor_backups");
            Directory.CreateDirectory(dir);
            string name = string.Format("{0}_{1:yyyyMMdd_HHmmss}.sii", Path.GetFileNameWithoutExtension(path), DateTime.Now);
            string target = Path.Combine(dir, name);
            System.IO.File.Copy(path, target, true);
            return target;
        }
    }
}
