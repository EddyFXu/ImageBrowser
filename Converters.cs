using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ImageBrowser
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        
        public BooleanToVisibilityConverter() { }
        public BooleanToVisibilityConverter(bool invert) { Invert = invert; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool) flag = (bool)value;
            if (Invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ImagePathConverter : IValueConverter
    {
        private static readonly object _lock = new object();
        private static readonly System.Collections.Generic.LinkedList<string> _keys = new System.Collections.Generic.LinkedList<string>();
        private static readonly System.Collections.Generic.Dictionary<string, BitmapSource> _cache = new System.Collections.Generic.Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                int decodeW = 0;
                if (parameter != null)
                {
                    if (parameter is int) decodeW = (int)parameter;
                    else
                    {
                        int tmp;
                        if (int.TryParse(parameter.ToString(), out tmp)) decodeW = tmp;
                    }
                }

                string key = decodeW > 0 ? (path + "|" + decodeW.ToString()) : path;
                if (decodeW > 0)
                {
                    lock (_lock)
                    {
                        BitmapSource cached;
                        if (_cache.TryGetValue(key, out cached)) return cached;
                    }
                }

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                if (decodeW > 0) bi.DecodePixelWidth = decodeW;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();

                if (decodeW > 0)
                {
                    lock (_lock)
                    {
                        if (!_cache.ContainsKey(key))
                        {
                            _cache[key] = bi;
                            _keys.AddLast(key);
                            if (_keys.Count > 300)
                            {
                                string oldest = _keys.First.Value;
                                _keys.RemoveFirst();
                                _cache.Remove(oldest);
                            }
                        }
                    }
                }

                return bi;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long)
            {
                long bytes = (long)value;
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double len = bytes;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return string.Format("{0:0.##} {1}", len, sizes[order]);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ItemTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ItemType)
            {
                switch ((ItemType)value)
                {
                    case ItemType.Drive: return "驱动器";
                    case ItemType.Folder: return "文件夹";
                    case ItemType.Image: return "图片";
                    default: return "未知";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ZoomConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double)
            {
                double size = (double)value;
                // Base size is 100.0, so 100.0 -> 1.0 scale
                return size / 100.0;
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double)
            {
                double scale = (double)value;
                return scale * 100.0;
            }
            return 100.0;
        }
    }

    public class SortArrowVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string currentSort = value as string;
            string targetSort = parameter as string;
            return (currentSort == targetSort) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SortArrowDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDescending = (bool)value;
            return isDescending ? "▼" : "▲";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
