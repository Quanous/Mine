using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HD
{
    // 저장되는 디데이 한 개 (예전 버전의 data.json 과 같은 형식이라 그대로 이어서 사용 가능)
    public class EventItem
    {
        public string name { get; set; }
        public string date { get; set; }   // yyyy-MM-dd
        public string image { get; set; }  // 저장된 이미지 파일명 (없으면 빈 값)
    }

    public class AppData
    {
        public List<EventItem> events { get; set; }
        public double wx { get; set; }
        public double wy { get; set; }
        public double ww { get; set; }   // 0 이면 저장된 창 크기 없음
        public double wh { get; set; }
        public bool pin { get; set; }

        public AppData() { events = new List<EventItem>(); }
    }

    public static class Store
    {
        public static string Root = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HD");

        public static string DataFile { get { return System.IO.Path.Combine(Root, "data.json"); } }
        public static string ImgDir { get { return System.IO.Path.Combine(Root, "images"); } }

        public static void UseTempRoot()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HD_demo_" + Guid.NewGuid().ToString("N"));
        }

        public static AppData Load()
        {
            try
            {
                if (File.Exists(DataFile))
                {
                    string json = File.ReadAllText(DataFile, Encoding.UTF8);
                    AppData d = new JavaScriptSerializer().Deserialize<AppData>(json);
                    if (d != null)
                    {
                        if (d.events == null) d.events = new List<EventItem>();
                        d.events.RemoveAll(e => e == null || string.IsNullOrEmpty(e.name) || string.IsNullOrEmpty(e.date));
                        return d;
                    }
                }
            }
            catch (Exception ex) { LogError(ex); }
            return new AppData();
        }

        public static void Save(AppData d)
        {
            try
            {
                Directory.CreateDirectory(Root);
                string tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
                File.Copy(tmp, DataFile, true);
                File.Delete(tmp);
            }
            catch (Exception ex) { LogError(ex); }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Root);
                File.AppendAllText(System.IO.Path.Combine(Root, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
        }
    }

    // 이미지 읽기/가져오기 (고화질 유지 + 파일 잠금 없음)
    public static class Img
    {
        static readonly Dictionary<string, BitmapSource> cache = new Dictionary<string, BitmapSource>();

        public static BitmapSource LoadPack(string relative, int maxWidth)
        {
            try
            {
                Uri uri = new Uri("pack://application:,,,/" + relative, UriKind.Absolute);
                int srcW = BitmapFrame.Create(uri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = uri;
                if (maxWidth > 0 && srcW > maxWidth) bi.DecodePixelWidth = maxWidth;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex) { Store.LogError(ex); return null; }
        }

        public static BitmapSource LoadFile(string path, int maxWidth)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int srcW = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;
                    fs.Position = 0;
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = fs;
                    if (maxWidth > 0 && srcW > maxWidth) bi.DecodePixelWidth = maxWidth;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch (Exception ex) { Store.LogError(ex); return null; }
        }

        public static BitmapSource LoadStored(string fileName, int maxWidth)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string key = fileName + "|" + maxWidth;
            BitmapSource bs;
            if (cache.TryGetValue(key, out bs)) return bs;
            string path = System.IO.Path.Combine(Store.ImgDir, fileName);
            if (!File.Exists(path)) return null;
            bs = LoadFile(path, maxWidth);
            if (bs != null) cache[key] = bs;
            return bs;
        }

        // 사용자가 고른 사진을 앱 폴더로 복사 (너무 크면 줄이고, 폰 사진 회전도 바로잡음). 저장된 파일명을 돌려줌.
        public static string Import(string srcPath)
        {
            try
            {
                Directory.CreateDirectory(Store.ImgDir);
                BitmapSource bmp = LoadFile(srcPath, 1400);
                if (bmp == null) return null;
                bmp = FixOrientation(srcPath, bmp);

                string ext = System.IO.Path.GetExtension(srcPath).ToLowerInvariant();
                bool usePng = ext == ".png" || ext == ".gif" || ext == ".ico" || ext == ".webp"
                              || bmp.Format == PixelFormats.Bgra32 || bmp.Format == PixelFormats.Pbgra32
                              || bmp.Format == PixelFormats.Indexed8;

                BitmapEncoder enc;
                string name = Guid.NewGuid().ToString("N");
                if (usePng)
                {
                    bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                    enc = new PngBitmapEncoder();
                    name += ".png";
                }
                else
                {
                    bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
                    enc = new JpegBitmapEncoder { QualityLevel = 93 };
                    name += ".jpg";
                }
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (FileStream fs = File.Create(System.IO.Path.Combine(Store.ImgDir, name))) enc.Save(fs);
                return name;
            }
            catch (Exception ex) { Store.LogError(ex); return null; }
        }

        static BitmapSource FixOrientation(string path, BitmapSource bmp)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    BitmapFrame frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    BitmapMetadata md = frame.Metadata as BitmapMetadata;
                    if (md == null) return bmp;
                    object o = md.GetQuery("/app1/ifd/{ushort=274}");
                    if (o == null) return bmp;
                    int orient = Convert.ToInt32(o);
                    double angle = orient == 3 ? 180 : orient == 6 ? 90 : orient == 8 ? 270 : 0;
                    if (angle == 0) return bmp;
                    TransformedBitmap tb = new TransformedBitmap(bmp, new RotateTransform(angle));
                    tb.Freeze();
                    return tb;
                }
            }
            catch { return bmp; }
        }

        public static void Delete(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName)) return;
                string path = System.IO.Path.Combine(Store.ImgDir, fileName);
                if (File.Exists(path)) File.Delete(path);
                List<string> keys = new List<string>();
                foreach (string k in cache.Keys) if (k.StartsWith(fileName + "|")) keys.Add(k);
                foreach (string k in keys) cache.Remove(k);
            }
            catch { }
        }
    }
}
