﻿using System.Text;
using static MethodBox.MBLog.Interfaces;
using static MethodBox.MBLog.DataType;

namespace MethodBox.MBLog
{
    /// <summary>
    /// 表示MBLog中的日志系统执行类。
    /// </summary>
    public class Logger : ILogger
    {
        private static ILogger? _logger = null;
        private static readonly object ThreadLocker = new();
        private string _logFileName;
        private DataType.LogFileType _logFileType;
        private StringBuilder _dataBuffer = new();
        /// <summary>
        /// 表示一个可用于自定义生成日志字符串方法的委托。
        /// </summary>
        public delegate string
            LogStringHandler(LogType logType,LogStruct logStruct);
        private LogStringHandler? _stringHandler;

        private Logger(DataType.LogFileType logFileType, string logFileName)
        {
            this._logFileName = logFileName;
            this._logFileType = logFileType;
        }

        /// <summary>
        /// 以单例模式返回一个Logger的实例化对象。（经过浅拷贝）
        /// </summary>
        /// <param name="logFileType">表示日志文件类型</param>
        /// <param name="logFileName">表示要存储日志的文件</param>
        /// <returns>分配的Logger的实例化对象</returns>
        /// <example>
        /// 以下示例将生成一个将日志文件存储"D:\Log\log.txt中的在Logger类型的日志实例化对象。
        /// <code>
        /// ILogger loggerInstance = GetLoggerInstance(LogFileType.TextFile, @"D:\Log\log.txt");
        /// Logger Logger = (Logger)loggerInstance;
        /// </code>
        /// </example>
        public static ILogger GetLoggerInstance(DataType.LogFileType logFileType, string logFileName)
        {
            if (_logger is null)
            {
                lock (ThreadLocker)
                {
                    if (_logger is null)
                    {
                        _logger = new Logger(logFileType, logFileName);
                        return _logger;
                    }

                }
            }
            return _logger;
        }

        /// <summary>
        /// 完成进行日志记录的一系列操作事件。
        /// </summary>
        /// <param name="logType">日志的等级类型</param>
        /// <param name="logStruct">日志结构体</param>
        /// <example>
        /// 该示例将生成一个由“Console”提示的、类型为“警告”的日志记录，并将其打印在控制台上，最后存储在D:\Log\log.txt中。
        /// <code>
        /// ILogger loggerInstance = GetLoggerInstance(LogFileType.TextFile, @"D:\Log\log.txt");
        /// LogStruct logStruct = new();
        /// logStruct.CallerInfoStrings = new[] { "Console" };
        /// logStruct.LogInfo = "用户输入了具有破坏性的指令";
        /// logStruct.Save = true;
        /// logStruct.Print = true;
        /// ((Logger)loggerInstance).Log(LogType.Warning, logStruct);
        /// </code>
        /// </example>
        /// <see cref="LogType"/>
        /// <see cref="LogStruct"/>
        public void Log(LogType logType, LogStruct logStruct)
        {
            // Change console font color
            switch (logType)
            {
                case LogType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogType.Caution:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
                case LogType.Information:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
            }

            // Weight：(2,1)
            // Get status in order to switch what
            int statusType = (logStruct.Print, logStruct.Save) switch
            {
                (true, true) => 3,
                (true, false) => 2,
                (false, true) => 1,
                (false, false) => 0
            };

            // Build string and do behavior
            switch (statusType)
            {
                case 0:
                    return;
                case 1:
                    WriteToFile(BuildLogString(logType, logStruct));
                    break;
                case 2:
                    Console.WriteLine(BuildLogString(logType, logStruct));
                    break;
                case 3:
                    Console.WriteLine(BuildLogString(logType, logStruct));
                    WriteToFile(BuildLogString(logType, logStruct));
                    break;
                default:
                    throw new ArgumentException("传入了错误的行动类型");
            }
        }

        /// <summary>
        /// 将LogType类型转换为相应的日志等级的字符串形式。
        /// </summary>
        /// <param name="_"><c>LogType的实例化对象</c></param>
        /// <returns>转换后的日志等级字符串结果</returns>
        /// <exception cref="NotImplementedException">如果传入了错误的LogType枚举值，将引发此异常。</exception>
        private string GetTypeString(LogType _)
        {
            return _ switch
            {
                LogType.Error => "Error",
                LogType.Warning => "Warning",
                LogType.Caution => "Caution",
                LogType.Information => "Information",
                _ => throw new NotImplementedException("This type is not implemented now")
            };
        }

        /// <summary>
        /// 将生成的日志内容写入指定的文件的缓冲区。
        /// </summary>
        /// <param name="content">需要写入文件的内容</param>
        private void WriteToFile(object content)
        {
            if (_logFileType == LogFileType.TextFile)
            {
                _dataBuffer.AppendLine((string)content);
            }
            else if (_logFileType == LogFileType.Json)
            {
                // Content is a class
                string jsonElement = 
                    System.Text.Json.JsonSerializer.Serialize
                    <DataType.Log>((Log)content);
                // Add to buffer
                _dataBuffer.Append(jsonElement);
            }
        }

        /// <summary>
        /// 将缓冲区内的内容全部写入文件，并清空当前缓冲区。
        /// </summary>
        public void FlushBuffer()
        {
            using FileStream fileStream =
                new(_logFileName, FileMode.Append);
            // Write to specific file
            byte[] buffer = Encoding.Default.GetBytes(
                _dataBuffer.ToString());
            fileStream.Write(buffer, 0, buffer.Length);
            fileStream.Flush();
            fileStream.Close();
            // Clear data buffer
            _dataBuffer.Clear();
        }

        /// <summary>
        /// 将指定的内容打印在控制台上并重置控制台颜色。
        /// </summary>
        /// <param name="content">需要打印的内容</param>
        private void Print(string content)
        {
            Console.WriteLine(content);
            Console.ResetColor();
        }

        /// <summary>
        /// 使用给定的数据以通用日志格式生成格式化字符串，当用户设置了自定义
        /// 的日志字符串生成方法时，该方法将自动调起含有委托的重载。
        /// </summary>
        /// <param name="logStruct">输入的日志结构</param>
        /// <param name="logType">输入的日志类型</param>
        /// <example>
        /// 以下示例将在2023年7月23日 07:55生成一个来自Console的警告字符串：
        /// 2023-07-23 07:55:00 [WARNING][Console]用户输入了具有破坏性的指令
        /// <code>
        /// ILogger loggerInstance = GetLoggerInstance(LogFileType.TextFile,@"D:\Log\log.txt");
        /// LogStruct logStruct = new LogStruct();
        /// logStruct.CallerInfoStrings = new[] { "Console" };
        /// logStruct.LogInfo = "用户输入了具有破坏性的指令";
        /// logStruct.Save = true;
        /// logStruct.Print = true;
        /// string logString = ((Logger)loggerInstance).
        /// BuildLogString(LogType.Warning, logStruct);
        /// Console.WriteLine(logString);
        /// </code>
        /// </example>
        /// <returns>格式化后的日志字符串</returns>
        /// <see cref="LogType"/>
        /// <see cref="LogStruct"/>
        /// <seealso cref="Logger"/>
        public string BuildLogString(LogType logType, LogStruct logStruct)
        {
            // Check weather use DIY method
            if (_stringHandler != null)
            {
                BuildLogString(logType, logStruct, _stringHandler);
                #pragma warning disable CS8603
                return null;
                #pragma warning restore CS8603
            }
            // Init fields
            string[] callerInfoStrings = logStruct.CallerInfoStrings;
            string logInfo = logStruct.LogInfo;
            StringBuilder logBuilder = new();
            // Using specific format to format log string
            logBuilder.Append(DateTime.Now.ToString("yy-MM-dd HH:mm:ss"));
            logBuilder.Append($@" [{GetTypeString(logType).ToUpper()}]");
            foreach (var callerInfoString in callerInfoStrings)
            {
                logBuilder.Append($@"[{callerInfoString}]");
            }
            logBuilder.Append(logInfo);
            // Return formatted string result
            return logBuilder.ToString();
        }

        /// <summary>
        /// 给定一个应用程序方法的扩展，用于定义自定义的日志字符串生成方法。
        /// </summary>
        /// <param name="logType">输入的日志类型</param>
        /// <param name="logStruct">输入的日志结构</param>
        /// <param name="handleFunc">处理方法</param>
        /// <returns>格式化后的日志字符串</returns>
        /// <see cref="LogType"/>
        /// <see cref="LogStruct"/>
        /// <see cref="LogStringHandler"/>
        /// <seealso cref="Logger"/>
        public string BuildLogString(LogType logType, 
            LogStruct logStruct,
            LogStringHandler handleFunc)
        {
            return handleFunc(logType,logStruct);
        }

        /// <summary>
        /// 给定一个应用程序方法的扩展，用于给定用户以设置自定义的
        /// 日志字符串生成方法的实例化委托。
        /// </summary>
        /// <param name="handleFunc">自定义的日志字符串生成方法</param>
        /// <see cref="LogStringHandler"/>
        public void SetHandler(LogStringHandler handleFunc)
        {
            this._stringHandler = handleFunc;
        }
    }
}