using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Ets2Save
{
    /// <summary>Loads the original TS SE Tool artwork shipped in the assets folder.</summary>
    public static class UiAssets
    {
        static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static string Root
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"); }
        }

        public static Image Get(string name)
        {
            Image image;
            if (Cache.TryGetValue(name, out image)) return image;
            try
            {
                string path = Path.Combine(Root, name + ".png");
                if (File.Exists(path))
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        image = Image.FromStream(fs);
                    }
                    Cache[name] = image;
                    return image;
                }
            }
            catch
            {
            }
            Cache[name] = null;
            return null;
        }

        public static Image Get(string name, int width, int height)
        {
            Image src = Get(name);
            if (src == null) return null;
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, width, height);
            }
            return bmp;
        }
    }
}
