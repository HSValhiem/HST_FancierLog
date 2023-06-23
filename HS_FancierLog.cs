using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using File = System.IO.File;

namespace HS_FancierLog;

class HS_FancierLog
{
    public static string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
    public static string Version = "1.1";

    public static bool SkipValheimCheck;

    // Set Default Log Path to .\LogOutput.log
    public static string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogOutput.log");

    // Default Settings
    public static string DateTimeFormat = "hh:mm:ss.ffff tt";
    public static ConsoleColor ForegroundColor = ConsoleColor.White;
    public static ConsoleColor BackgroundColor = ConsoleColor.Black;
    public static Dictionary<string, ConsoleColor> ColorMappings = new ()
    {
        { "Error|Failed", ConsoleColor.Red },
        { "AzuAntiCheat", ConsoleColor.Black },
        { "Warning", ConsoleColor.Yellow },
        { "Steam game server initialized", ConsoleColor.Blue },
        { "Message", ConsoleColor.Cyan },
        { "Unity Log", ConsoleColor.Magenta },
        { "BepInEx", ConsoleColor.Green }
    };

    // Init Config with Default Settings
    static void InitConfig()
    {
        using (StreamWriter writer = new StreamWriter(ConfigPath))
        {
            // Init Log path Setting
            writer.WriteLine(LogPath);

            // Init SkipValheimCheck Setting
            writer.WriteLine("SkipValheimCheck = " + SkipValheimCheck);

            // Init Date Time Format Setting
            writer.WriteLine("DateTimeFormat = " + DateTimeFormat);
            
            // Init Color Settings
            writer.WriteLine("ForegroundColor = " + ForegroundColor);
            writer.WriteLine("BackgroundColor = " + BackgroundColor);

            foreach (var mapping in ColorMappings)
            {
                writer.WriteLine(mapping.Key + " = " + mapping.Value);
            }
        }
    }

    // Load Config from .\Config.txt
    static void LoadConfig()
    {
        try
        {
            // Read the contents of the config file
            string[] fileContent = File.ReadAllLines(ConfigPath);

            // Get Log Path
            LogPath = fileContent[0].Trim();

            // Get SkipValheimCheck Mode
            SkipValheimCheck = bool.Parse(fileContent[1].Substring(fileContent[1].IndexOf('=') + 1).Trim());

            // Get DateTimeFormat
            string? dateTimeFormatLine = fileContent.FirstOrDefault(line => line.StartsWith("DateTimeFormat"));
            if (dateTimeFormatLine != null)
            {
                DateTimeFormat = dateTimeFormatLine.Substring(dateTimeFormatLine.IndexOf('=') + 1).Trim();
            }

            // Get ForegroundColor
            string? foregroundColorLine = fileContent.FirstOrDefault(line => line.StartsWith("ForegroundColor"));
            if (foregroundColorLine != null)
            {
                string foregroundColorValue = foregroundColorLine.Substring(foregroundColorLine.IndexOf('=') + 1).Trim();
                Enum.TryParse(foregroundColorValue, out ForegroundColor);
            }

            // Get BackgroundColor
            string? backgroundColorLine = fileContent.FirstOrDefault(line => line.StartsWith("BackgroundColor"));
            if (backgroundColorLine != null)
            {
                string backgroundColorValue = backgroundColorLine.Substring(backgroundColorLine.IndexOf('=') + 1).Trim();
                Enum.TryParse(backgroundColorValue, out BackgroundColor);
            }

            // Get ColorMappings
            ColorMappings.Clear();
            foreach (var line in fileContent)
            {
                if (line.Contains('='))
                {
                    string[] parts = line.Split('=');
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    ConsoleColor color;
                    if (Enum.TryParse(value, out color))
                    {
                        ColorMappings[key] = color;
                    }
                }
            }
            DrawHeader();
        }
        catch (FileNotFoundException)
        {
            // if Config not Found, Initialize with default settings
            InitConfig();
            DrawHeader();
            LogMessage("No Config Found\nInitializing config with default settings");
        }
        catch (Exception ex)
        {
            DrawHeader();
            LogMessage("Error: " + ex.Message);
        }
    }

    static void DrawHeader()
    {
        LogMessage($"HS Fancier Log v{Version}: {LogPath}", true);
    }

    static bool IsValheimRunning()
    {
        Process[] processes = Process.GetProcessesByName("valheim");
        return processes.Length > 0;
    }

    // Augment Fancier Log Program output into Log Format
    static void LogMessage(string text, bool header = false)
    {

        string[] lines = text.Split(new[] { "\n" }, StringSplitOptions.None);
        var tempFgColor = ForegroundColor;
        var tempBgColor = BackgroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = BackgroundColor;

        foreach (string line in lines)
        {
            if (!header)
                Console.WriteLine($"[{DateTime.Now.ToString(DateTimeFormat)}]: (HS_FL) {line}");
            else
            {
                Console.SetCursorPosition((Console.WindowWidth - text.Length) / 2, Console.CursorTop);
                Console.WriteLine(line);
                Console.WriteLine();
            }
        }
        Console.ForegroundColor = tempFgColor;
        Console.BackgroundColor = tempBgColor;
    }

    static void Main()
    {
        // Load Config Settings
        LoadConfig();

        if (!SkipValheimCheck && !IsValheimRunning())
        {
            // Clear Old LogFile
            File.WriteAllText(LogPath, string.Empty);
            Console.Clear();
            DrawHeader();

            // Wait if Valheim not Started
            LogMessage("Valheim not Detected\nWaiting for Valheim to start...");
            while (!IsValheimRunning()) { Thread.Sleep(1); }
        }


        int count = 0;

        // Continuously display the log file with color
        using (var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            long currentPosition = stream.Position;

            while (true)
            {
                // Detect if Valheim Closed and we need to Clear the Log
                if (!SkipValheimCheck && !IsValheimRunning())
                {
                    if (File.Exists(LogPath)) { File.Delete(LogPath); }
                    
                    LogMessage("Valheim Close Detected.  Clearing Log.\nWaiting for Valheim to start...");
                    File.WriteAllText(LogPath, string.Empty);
                    while (!IsValheimRunning()) { Thread.Sleep(1); }
                }

                

                // Check if the file size has changed
                if (stream.Length < currentPosition)
                {
                    currentPosition = 0;
                    Console.Clear();
                    DrawHeader();
                }

                // Set the stream position to the current position
                stream.Position = currentPosition;

                // Set BG to Default
                var bgColor = BackgroundColor;

                string? line;
                while ((line = reader.ReadLine()) != null)
                {

                    // Setup Colors
                    foreach (var mapping in ColorMappings)
                    {
                        if (Regex.IsMatch(line, mapping.Key))
                        {
                            ForegroundColor = mapping.Value;
        
                            // Exception for Azu to set BG to 
                            if (mapping.Key == "AzuAntiCheat")
                            {
                                bgColor = ConsoleColor.Red;
                            }

                            break;
                        }
                    }


                    // Check if mod name or other info exists, to include in the output later.
                    string pattern = "(?<=:)[^\\]]+(?=\\])";
                    string name = Regex.Match(line, pattern).Value.TrimStart();

                    // Remove everything between the first set of brackets.
                    line = Regex.Replace(line, "\\[.*?\\] ", "");

                    // Create a regular expression that matches timestamps "MM/dd/yyyy HH:mm:ss: "
                    string regex = @"^([0-9]{2}/[0-9]{2}/[0-9]{4} [0-9]{2}:[0-9]{2}:[0-9]{2}):\s";

                    // Check if a timestamp already exists and remove it.
                    if (Regex.IsMatch(line, regex))
                    {
                        // Remove the timestamp from the line
                        line = Regex.Replace(line, regex, "");
                    }

                    // Remove leading spaces from the line that are sometimes left after removing the timestamp
                    line = line.TrimStart();

                    // Add timestamps and mod names (or other info) back in, to maintain consistency in the log.
                    string timestamp = DateTime.Now.ToString(DateTimeFormat);
                    if (!string.IsNullOrEmpty(name))
                    {
                        line = $"[{timestamp}]: ({name}) {line}";
                    }
                    else
                    {
                        line = $"[{timestamp}]: {line}";
                    }

                    // Write the colored line
                    Console.ForegroundColor = ForegroundColor;
                    Console.BackgroundColor = bgColor;
                    Console.WriteLine(line);
                }

                // Update the current position to the current stream position
                currentPosition = stream.Position;

                // Wait for a second before reading the file again
                System.Threading.Thread.Sleep(1);
            }
        }
    }
}
