using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using JetBrains.Annotations;
using NLog;

namespace Exceptions
{
    public class ConverterProgram
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public static void Main(params string[] args)
        {
            try
            {
                var filenames = args.Any() ? args : new[] { "text.txt" };
                var settings = LoadSettings();
                ConvertFiles(filenames, settings);
            }
            catch (Exception e)
            {
                //TODO объяснить
                //throw e;
                //throw new Exception("11", e);
                //throw;

                log.Error(e);
            }
        }

        private static void ConvertFiles(string[] filenames, Settings settings)
        {
            var tasks = filenames
                .Select(fn => Task.Run(() => ConvertFile(fn, settings))/*.ContinueWith(task => HandleExceptions(task)*/)
                .ToArray();
            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException exc)
            {
                foreach (var e in exc.InnerExceptions)
                {
                    log.Error(e);
                }
            }
        }

        private static Settings LoadSettings()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Settings));

                var settingsfilename = "settings.xml";
                if (!File.Exists("settings.xml"))
                {
                    log.Info($"Файл настроек {settingsfilename} отсутствует.");
                    return Settings.Default;
                }

                var content = File.ReadAllText(settingsfilename);
                return (Settings)serializer.Deserialize(new StringReader(content));
            }
            catch (Exception exc)
            {
                //log.Trace("XmlException: Не удалось прочитать файл настроек");
                throw new XmlException("Не удалось прочитать файл настроек", exc);
            }
        }

        private static void ConvertFile(string filename, Settings settings)
        {
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(settings.SourceCultureName);
                if (settings.Verbose)
                {
                    log.Info("Processing file " + filename);
                    log.Info("Source Culture " + Thread.CurrentThread.CurrentCulture.Name);
                }
                IEnumerable<string> lines;
                try
                {
                    lines = PrepareLines(filename);
                }
                catch
                {
                    log.Error($"File {filename} not found");
                    return;
                }
                var convertedLines = lines
                    .Select(ConvertLine)
                    .Select(s => s.Length + " " + s);
                File.WriteAllLines(filename + ".out", convertedLines);
            }
            catch (FileNotFoundException e)
            {
                log.Error($"FileNotFoundException. Не удалось сконвертировать {filename}");
                //throw new FileNotFoundException($"Не удалось сконвертировать {filename}", e);
            }
            catch (Exception)
            {
                log.Error("Некорректная строка");
                ///throw new FormatException("Некорректная строка");
            }
        }

        private static IEnumerable<string> PrepareLines(string filename)
        {
            var lineIndex = 0;
            foreach (var line in File.ReadLines(filename))
            {
                if (line == "") continue;
                yield return line.Trim();
                lineIndex++;
            }
            yield return lineIndex.ToString();
        }

        [CanBeNull]
        public static string ConvertLine(string arg)
        {
            if (ConvertAsDateTime(arg, out string datetime)) return datetime;
            if (ConvertAsDouble(arg, out string sDouble)) return sDouble;
            if (ConvertAsCharIndexInstruction(arg, out string chInd)) return chInd;
            return null;
        }

        private static bool ConvertAsCharIndexInstruction(string s, out string chInd)
        {
            chInd = string.Empty;

            var parts = s.Split();
            if (parts.Length < 2) return false;
            var charIndex = int.Parse(parts[0]);
            if ((charIndex < 0) || (charIndex >= parts[1].Length))
                return false;
            var text = parts[1];
            chInd = text[charIndex].ToString();
            return true;
        }

        private static bool ConvertAsDateTime(string arg, out string datetimeS)
        {
            datetimeS = "";
            if (DateTime.TryParse(arg, out DateTime datetime))
            {
                datetimeS = datetime.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            return false;
        }

        private static bool ConvertAsDouble(string arg, out string doubleStr)
        {
            doubleStr = "";
            if (Double.TryParse(arg, out double d))
            {
                doubleStr = d.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            return false;
        }
    }
}