using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ets2Save
{
    public sealed class FreightOffer
    {
        public SiiBlock Block;
        public SiiBlock Company;
        public string Source;
        public string Target;
        public string Cargo;
        public int Distance;
        public int Urgency;
        public long Expiry;
        public int Units;
        public float Fill;

        public string SourceLabel
        {
            get { return ShortCompany(Source); }
        }

        public string TargetLabel
        {
            get { return ShortCompany(Target); }
        }

        public static string ShortCompany(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id.StartsWith("company.volatile.", StringComparison.Ordinal))
                return id.Substring("company.volatile.".Length);
            return id;
        }
    }

    /// <summary>Reads and edits the freight market job offers stored in game.sii.</summary>
    public sealed class FreightMarket
    {
        public SiiFile File;
        public List<FreightOffer> Offers = new List<FreightOffer>();
        public List<string> Cargos = new List<string>();
        public List<string> Targets = new List<string>();
        public List<string> SourceCompanies = new List<string>();
        public long GameTime;

        Dictionary<string, SiiBlock> _byIdent = new Dictionary<string, SiiBlock>(StringComparer.Ordinal);
        HashSet<string> _usedIds = new HashSet<string>(StringComparer.Ordinal);
        int _idCounter;

        public static FreightMarket Load(SiiFile file)
        {
            FreightMarket market = new FreightMarket();
            market.File = file;
            market.Build(file);
            return market;
        }

        void Build(SiiFile file)
        {
            SiiBlock economy = file.FindBlock("economy");
            if (economy != null)
            {
                object gt = economy.Get("game_time");
                if (gt != null) GameTime = Convert.ToInt64(gt, CultureInfo.InvariantCulture);
            }

            for (int i = 0; i < file.Blocks.Count; i++)
            {
                SiiBlock block = file.Blocks[i];
                _byIdent[block.Ident] = block;
                _usedIds.Add(block.Ident);
            }

            List<SiiBlock> companies = file.FindBlocks("company");
            HashSet<string> cargos = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < companies.Count; i++)
            {
                SiiBlock company = companies[i];
                SiiValue offers = company.GetValue("job_offer");
                if (offers == null) continue;
                string[] ids = offers.Data as string[];
                if (ids == null || ids.Length == 0) continue;
                sources.Add(company.Ident);

                for (int k = 0; k < ids.Length; k++)
                {
                    SiiBlock offerBlock;
                    if (!_byIdent.TryGetValue(ids[k], out offerBlock)) continue;
                    if (offerBlock.Structure.Name != "job_offer_data") continue;

                    FreightOffer offer = new FreightOffer();
                    offer.Block = offerBlock;
                    offer.Company = company;
                    offer.Source = company.Ident;
                    offer.Target = offerBlock.Get("target") as string;
                    offer.Cargo = offerBlock.Get("cargo") as string;
                    offer.Distance = ToInt(offerBlock.GetValue("shortest_distance_km"));
                    offer.Urgency = ToInt(offerBlock.GetValue("urgency"));
                    offer.Expiry = ToLong(offerBlock.GetValue("expiration_time"));
                    offer.Units = ToInt(offerBlock.GetValue("units_count"));
                    object fill = offerBlock.Get("fill_ratio");
                    offer.Fill = fill == null ? 1f : Convert.ToSingle(fill, CultureInfo.InvariantCulture);
                    Offers.Add(offer);

                    if (!string.IsNullOrEmpty(offer.Cargo)) cargos.Add(offer.Cargo);
                    if (!string.IsNullOrEmpty(offer.Target)) targets.Add(offer.Target);
                }
            }

            Cargos.AddRange(cargos);
            Cargos.Sort(StringComparer.Ordinal);
            Targets.AddRange(targets);
            Targets.Sort(StringComparer.Ordinal);
            SourceCompanies.AddRange(sources);
            SourceCompanies.Sort(StringComparer.Ordinal);
        }

        static int ToInt(SiiValue v)
        {
            if (v == null) return 0;
            return (int)Convert.ToInt64(v.Data, CultureInfo.InvariantCulture);
        }

        static long ToLong(SiiValue v)
        {
            if (v == null) return 0;
            return Convert.ToInt64(v.Data, CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------
        public void Apply(FreightOffer offer, string cargo, string target, int distance, int urgency,
                          int minutesFromNow, int units, float fill)
        {
            SiiBlock block = offer.Block;
            if (!string.IsNullOrEmpty(cargo) && cargo != offer.Cargo)
            {
                File.SetString(block, "cargo", cargo);
                SiiBlock template = FindTemplate(cargo);
                if (template != null && template != block)
                {
                    CopyString(block, template, "trailer_variant");
                    CopyString(block, template, "trailer_definition");
                    CopyString(block, template, "company_truck");
                }
                offer.Cargo = cargo;
            }
            if (!string.IsNullOrEmpty(target)) { File.SetString(block, "target", target); offer.Target = target; }
            File.SetInt(block, "shortest_distance_km", distance);
            File.SetInt(block, "urgency", urgency);
            File.SetInt(block, "units_count", units);
            File.SetFloat(block, "fill_ratio", fill);
            if (minutesFromNow > 0)
            {
                long expiry = GameTime + minutesFromNow;
                File.SetInt(block, "expiration_time", expiry);
                offer.Expiry = expiry;
            }
            offer.Distance = distance;
            offer.Urgency = urgency;
            offer.Units = units;
            offer.Fill = fill;
        }

        void CopyString(SiiBlock target, SiiBlock source, string field)
        {
            object value = source.Get(field);
            if (value is string && target.GetValue(field) != null)
                File.SetString(target, field, (string)value);
        }

        public SiiBlock FindTemplate(string cargo)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                FreightOffer offer = Offers[i];
                if (offer.Cargo == cargo && offer.Block.Get("trailer_definition") is string)
                    return offer.Block;
            }
            return null;
        }

        // ------------------------------------------------------------------
        public FreightOffer Add(string sourceCompany, string cargo, string target, int distance,
                                int urgency, int minutesFromNow, int units, float fill)
        {
            SiiBlock company;
            if (!_byIdent.TryGetValue(sourceCompany, out company))
                throw new SiiException("找不到来源公司: " + sourceCompany);

            SiiStructure offerStructure = null;
            foreach (KeyValuePair<uint, SiiStructure> kv in File.Structures)
                if (kv.Value.Name == "job_offer_data") { offerStructure = kv.Value; break; }
            if (offerStructure == null) throw new SiiException("存档中没有 job_offer_data 结构");

            string newId = NewNamelessId();
            SiiBlock block = File.NewBlock(offerStructure.Id);
            block.Ident = newId;
            block.IdentLength = 0xFF;
            block.IdentParts = new ulong[] { ParseNameless(newId) };

            SiiBlock template = FindTemplate(cargo);
            SetStringDefault(block, "target", target);
            SetIntDefault(block, "expiration_time", GameTime + minutesFromNow);
            SetIntDefault(block, "urgency", urgency);
            SetIntDefault(block, "shortest_distance_km", distance);
            SetIntDefault(block, "ferry_time", template == null ? 0 : ToInt(template.GetValue("ferry_time")));
            SetIntDefault(block, "ferry_price", template == null ? 0 : ToInt(template.GetValue("ferry_price")));
            SetStringDefault(block, "cargo", cargo);
            SetStringDefault(block, "company_truck", template == null ? "daf_xd" : (template.Get("company_truck") as string));
            SetStringDefault(block, "trailer_variant", template == null ? "" : (template.Get("trailer_variant") as string));
            SetStringDefault(block, "trailer_definition", template == null ? "" : (template.Get("trailer_definition") as string));
            SetIntDefault(block, "units_count", units > 0 ? units : (template == null ? 1 : ToInt(template.GetValue("units_count"))));
            SetFloatDefault(block, "fill_ratio", fill);

            SiiValue arr = company.GetValue("job_offer");
            if (arr == null || arr.Type != SiiType.IdArray) throw new SiiException("来源公司没有 job_offer 数组");
            string[] ids = arr.Data as string[];
            string[] updated = new string[ids.Length + 1];
            Array.Copy(ids, updated, ids.Length);
            updated[ids.Length] = newId;
            arr.Data = updated;
            arr.Count = updated.Length;
            arr.Dirty = true;
            company.Dirty = true;

            _byIdent[newId] = block;
            _usedIds.Add(newId);

            FreightOffer offer = new FreightOffer();
            offer.Block = block;
            offer.Company = company;
            offer.Source = company.Ident;
            offer.Target = target;
            offer.Cargo = cargo;
            offer.Distance = distance;
            offer.Urgency = urgency;
            offer.Expiry = GameTime + minutesFromNow;
            offer.Units = units;
            offer.Fill = fill;
            Offers.Add(offer);
            return offer;
        }

        void SetStringDefault(SiiBlock block, string field, string value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) return;
            v.Data = value ?? "";
            v.Raw = System.Text.Encoding.UTF8.GetBytes(v.Data.ToString());
            v.Dirty = true;
            block.Dirty = true;
        }

        void SetIntDefault(SiiBlock block, string field, long value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) return;
            v.Data = value;
            v.Dirty = true;
            block.Dirty = true;
        }

        void SetFloatDefault(SiiBlock block, string field, float value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) return;
            v.Data = value;
            v.Dirty = true;
            block.Dirty = true;
        }

        public void ExtendAll(int minutes)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                FreightOffer offer = Offers[i];
                long expiry = GameTime + minutes;
                File.SetInt(offer.Block, "expiration_time", expiry);
                offer.Expiry = expiry;
            }
        }

        // ------------------------------------------------------------------
        string NewNamelessId()
        {
            while (true)
            {
                _idCounter++;
                ulong value = 0x0002B90000000000UL | (ulong)(uint)(_idCounter * 0x40 + 0x100);
                string text = SiiParser.FormatId(0xFF, new ulong[] { value }, 3);
                if (!_usedIds.Contains(text)) return text;
            }
        }

        static ulong ParseNameless(string text)
        {
            ulong value = 0;
            string rest = text.Substring("_nameless".Length);
            if (rest.StartsWith(".")) rest = rest.Substring(1);
            foreach (string part in rest.Split('.'))
                value = (value << 16) | Convert.ToUInt64(part, 16);
            return value;
        }
    }
}
