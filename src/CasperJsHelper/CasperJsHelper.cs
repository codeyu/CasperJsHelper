using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CasperJsHelper
{
    public class CasperJsHelper : IDisposable
    {
        private Process _casperJsProcess;
        private const string DefaultExeName = "casperjs.exe";
		private readonly List<string> _errorLines = new List<string>();

		
		public string CustomArgs
		{
			get;
			set;
		}

		
		public TimeSpan? ExecutionTimeout
		{
			get;
			set;
		}

		
		public string CasperJsExeName
		{
			get;
			set;
		}

		
		public ProcessPriorityClass ProcessPriority
		{
			get;
			set;
		}

		
		public string TempFilesPath
		{
			get;
			set;
		}

		
		public string ToolPath
		{
			get;
			set;
		}

        public CasperJsHelper(string casperJsPath = "")
        {
            ToolPath = ResolveAppBinPath(casperJsPath);
            CasperJsExeName = DefaultExeName;
            ProcessPriority = ProcessPriorityClass.Normal;
        }
		
		public void Abort()
		{
			EnsureProcessStopped();
		}

		private static void CheckExitCode(int exitCode, List<string> errLines)
		{
			if (exitCode != 0)
			{
                throw new CasperJsException(exitCode, string.Join("\n", errLines.ToArray()));
			}
		}

		protected void CopyToStdIn(Stream inputStream)
		{
			byte[] numArray = new byte[8192];
			while (true)
			{
				int num = inputStream.Read(numArray, 0, numArray.Length);
				if (num <= 0)
				{
					break;
				}
                _casperJsProcess.StandardInput.BaseStream.Write(numArray, 0, num);
                _casperJsProcess.StandardInput.BaseStream.Flush();
			}
            _casperJsProcess.StandardInput.Close();
		}

		private void DeleteFileIfExists(string filePath)
		{
		    if (filePath == null || !File.Exists(filePath)) return;
		    try
		    {
		        File.Delete(filePath);
		    }
		    catch
		    {
		        // ignored
		    }
		}

        public void Dispose()
		{
			EnsureProcessStopped();
		}

		private void EnsureProcessStopped()
		{
		    if (_casperJsProcess == null || _casperJsProcess.HasExited) return;
		    try
		    {
		        _casperJsProcess.Kill();
		        _casperJsProcess.Dispose();
		        _casperJsProcess = null;
		    }
		    catch (Exception)
		    {
		        // ignored
		    }
		}

        private string GetTempPath()
		{
			if (!string.IsNullOrEmpty(TempFilesPath) && !Directory.Exists(TempFilesPath))
			{
				Directory.CreateDirectory(TempFilesPath);
			}
			return TempFilesPath ?? Path.GetTempPath();
		}

		private static string PrepareCmdArg(string arg)
		{
			var stringBuilder = new StringBuilder();
			stringBuilder.Append('\"');
			stringBuilder.Append(arg.Replace("\"", "\\\""));
			stringBuilder.Append('\"');
			return stringBuilder.ToString();
		}

		private static void ReadStdOutToStream(Process proc, Stream outputStream)
		{
			var numArray = new byte[32768];
			while (true)
			{
				var num = proc.StandardOutput.BaseStream.Read(numArray, 0, numArray.Length);
				var num1 = num;
				if (num <= 0)
				{
					break;
				}
				outputStream.Write(numArray, 0, num1);
			}
		}

		private static string ResolveAppBinPath(string casperJsPath= "")
		{
			var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var t in assemblies)
			{
			    var type = t.GetType("System.Web.HttpRuntime", false);
			    if (type == null) continue;
			    var property = type.GetProperty("AppDomainId", BindingFlags.Static | BindingFlags.Public);
			    if (property == null || property.GetValue(null, null) == null) continue;
			    var propertyInfo = type.GetProperty("BinDirectory", BindingFlags.Static | BindingFlags.Public);
			    if (propertyInfo == null)
			    {
			        break;
			    }
			    var value = propertyInfo.GetValue(null, null);
			    if (!(value is string))
			    {
			        break;
			    }
			    baseDirectory = (string)value;
			    break;
			}
            return string.IsNullOrEmpty(casperJsPath) ? baseDirectory : baseDirectory +  casperJsPath + "\\bin";
		}

		
		public void Run(string jsFile, string[] jsArgs)
		{
			Run(jsFile, jsArgs, null, null);
		}

		
		public void Run(string jsFile, string[] jsArgs, Stream inputStream, Stream outputStream)
		{
			if (jsFile == null)
			{
				throw new ArgumentNullException("jsFile");
			}
			RunInternal(jsFile, jsArgs, inputStream, outputStream);
			try
			{
				WaitProcessForExit();
                CheckExitCode(_casperJsProcess.ExitCode, _errorLines);
			}
			finally
			{
                _casperJsProcess.Close();
                _casperJsProcess = null;
			}
		}

		
		public Task<bool> RunAsync(string jsFile, string[] jsArgs)
		{
			if (jsFile == null)
			{
				throw new ArgumentNullException("jsFile");
			}
			var taskCompletionSource = new TaskCompletionSource<bool>();
			Action action = () => {
				try
				{
				    CheckExitCode(_casperJsProcess.ExitCode, _errorLines);
				    taskCompletionSource.SetResult(true);
				}
				catch (Exception exception)
				{
				    taskCompletionSource.TrySetException(exception);
				}
				finally
				{
                    _casperJsProcess.Close();
                    _casperJsProcess = null;
				}
			};
			RunInternal(jsFile, jsArgs, null, null);
            _casperJsProcess.Exited += (sender, args) => action();
            if (_casperJsProcess.HasExited)
			{
				action();
			}
			return taskCompletionSource.Task;
		}

		private void RunInternal(string jsFile, IEnumerable jsArgs, Stream inputStream, Stream outputStream)
		{
			_errorLines.Clear();
			try
			{
				var str = Path.Combine(ToolPath, CasperJsExeName);
				if (!File.Exists(str))
				{
					throw new FileNotFoundException(string.Concat("Cannot find CasperJS: ", str));
				}
				var stringBuilder = new StringBuilder();
				if (!string.IsNullOrEmpty(CustomArgs))
				{
					stringBuilder.AppendFormat(" {0} ", CustomArgs);
				}
				stringBuilder.AppendFormat(" {0}", PrepareCmdArg(jsFile));
				if (jsArgs != null)
				{
				    var strArrays = jsArgs;
				    foreach (string str1 in strArrays)
				    {
				        stringBuilder.AppendFormat(" {0}", PrepareCmdArg(str1));
				    }
				}
			    var processStartInfo = new ProcessStartInfo(str, stringBuilder.ToString())
				{
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
					UseShellExecute = false,
					WorkingDirectory = Path.GetDirectoryName(ToolPath),
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};
                _casperJsProcess = new Process()
				{
					StartInfo = processStartInfo,
					EnableRaisingEvents = true
				};
                _casperJsProcess.Start();
				if (ProcessPriority != ProcessPriorityClass.Normal)
				{
                    _casperJsProcess.PriorityClass = ProcessPriority;
				}
                _casperJsProcess.ErrorDataReceived += (o, args) =>
                {
                    if (args.Data == null)
                    {
                        return;
                    }
                    _errorLines.Add(args.Data);
                    if (ErrorReceived != null)
                    {
                        ErrorReceived(this, args);
                    }
                };
                _casperJsProcess.BeginErrorReadLine();
				if (outputStream == null)
				{
                    _casperJsProcess.OutputDataReceived += (o, args) =>
                    {
                        if (OutputReceived != null)
                        {
                            OutputReceived(this, args);
                        }
                    };
                    _casperJsProcess.BeginOutputReadLine();
				}
				if (inputStream != null)
				{
					CopyToStdIn(inputStream);
				}
				if (outputStream != null)
				{
					ReadStdOutToStream(_casperJsProcess, outputStream);
				}
			}
			catch (Exception exception1)
			{
				var exception = exception1;
				EnsureProcessStopped();
                throw new Exception(string.Concat("Cannot execute CasperJs: ", exception.Message), exception);
			}
		}

		
		public void RunScript(string javascriptCode, string[] jsArgs)
		{
			RunScript(javascriptCode, jsArgs, null, null);
		}

		
		public void RunScript(string javascriptCode, string[] jsArgs, Stream inputStream, Stream outputStream)
		{
			var tempPath = GetTempPath();
            var str = Path.Combine(tempPath, string.Concat("casperjs-", Path.GetRandomFileName(), ".js"));
			try
			{
				File.WriteAllBytes(str, Encoding.UTF8.GetBytes(javascriptCode));
				Run(str, jsArgs, inputStream, outputStream);
			}
			finally
			{
				DeleteFileIfExists(str);
			}
		}

		
		public Task RunScriptAsync(string javascriptCode, string[] jsArgs)
		{
			Task task;
			var tempPath = GetTempPath();
            var str = Path.Combine(tempPath, string.Concat("casperjs-", Path.GetRandomFileName(), ".js"));
			File.WriteAllBytes(str, Encoding.UTF8.GetBytes(javascriptCode));
			try
			{
				var task1 = RunAsync(str, jsArgs);
				task1.ContinueWith(t => DeleteFileIfExists(str), TaskContinuationOptions.ExecuteSynchronously);
				task = task1;
			}
			catch
			{
				DeleteFileIfExists(str);
				throw;
			}
			return task;
		}

		private void WaitProcessForExit()
		{
			var hasValue = ExecutionTimeout.HasValue;
			if (!hasValue)
			{
				_casperJsProcess.WaitForExit();
			}
			else
			{
                var casperJsProcess = _casperJsProcess;
				var value = ExecutionTimeout.Value;
                casperJsProcess.WaitForExit((int)value.TotalMilliseconds);
			}
            if (_casperJsProcess == null)
			{
                throw new CasperJsException(-1, "casperJs process was aborted");
			}
		    if (!hasValue || _casperJsProcess.HasExited) return;
		    EnsureProcessStopped();
            throw new CasperJsException(-2, string.Format("casperJs process exceeded execution timeout ({0}) and was aborted", ExecutionTimeout));
		}

		
		public void WriteEnd()
		{
            _casperJsProcess.StandardInput.Close();
		}

		
		public void WriteLine(string s)
		{
            if (_casperJsProcess == null)
			{
                throw new InvalidOperationException("CasperJs is not running");
			}
            _casperJsProcess.StandardInput.WriteLine(s);
            _casperJsProcess.StandardInput.Flush();
		}

		
		public event EventHandler<DataReceivedEventArgs> ErrorReceived;

		
		public event EventHandler<DataReceivedEventArgs> OutputReceived;
    }
}
