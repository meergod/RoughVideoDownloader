using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace SoftStone {
  public static class StringUtils {
    public static bool IsNullOrEmpty(this string value) { return string.IsNullOrEmpty(value); }
    public static bool IsNullOrWhiteSpace(this string value) { return string.IsNullOrWhiteSpace(value); }

    public static string plusAtNewLine(this string orig, params string[] value) {
      if(value == null || value.Length == 0) return orig;

      var valueToAppend = string.Join(System.Environment.NewLine, value);
      if(string.IsNullOrEmpty(orig)) return valueToAppend;
      return orig + System.Environment.NewLine + valueToAppend;
    }
    public static StringBuilder AppendAtNewLine(this StringBuilder orig, params string[] value) {
      if(value == null || value.Length == 0) return orig;

      var valueToAppend = string.Join(System.Environment.NewLine, value);
      if(string.IsNullOrEmpty(orig.ToString())) return orig.Append(valueToAppend);
      return orig.AppendLine().Append(valueToAppend);
    }

    public static string JoinAsString<T>(this IEnumerable<T> array, string separator = "") {
      return string.Join(separator, array);
    }

    public static string DoubleQutoe(this string value) { return "\"" + value + "\""; }

    public static string padByTotal(this int index, int total) {
      return index.ToString().PadLeft(total.ToString().Length, '0');
    }

    public static string replaceInvalidFileNameChars(this string srcString, string replacement = "") {
      var invalidFileNameChars = Path.GetInvalidFileNameChars();
      var result = new StringBuilder();
      foreach(var chr in srcString) {
        if(!invalidFileNameChars.Contains(chr)) result.Append(chr);
        else result.Append(replacement);
      }
      return result.ToString();
    }

    public static string EnumNameToWords<T>(this T enumValue) where T: struct {
      var name = enumValue.ToString();
      return new string(name.SelectMany((c, i) =>
        i != 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])
        ? new char[] { ' ', c } : new char[] { c }
      ).ToArray());
    }

    public static ReadOnlyCollection<KeyValuePair<string, string>> QueryStringPairs(this Uri url) {
      var result = new List<KeyValuePair<string, string>>();
      var queryString = url.Query;
      if(queryString.StartsWith("?")) queryString = queryString.Remove(0, 1);
      foreach(var split1 in queryString.Split('&')) {
        var split2 = split1.Split(new[] { '=' }, 2);
        result.Add(new KeyValuePair<string, string>(split2[0], split2.Length == 2 ? split2[1] : null));
      }
      return result.AsReadOnly();
    }

    public static string significantValue(
      this IEnumerable<KeyValuePair<string, string>> queryStringPairs, string key
    ) {
      var v = queryStringPairs.FirstOrDefault(i => i.Key == key);
      if(v.Key == key && !v.Value.IsNullOrWhiteSpace()) return v.Value;
      return null;
    }

    public static string ToStringInBrackets(this DateTime datetimeValue) {
      return "[" + datetimeValue.ToString() + "]";
    }
  }
  public static class Utils {
    public static bool IsNullOrEmpty<T>(this IEnumerable<T> array) {
      return array == null || !array.Any();
    }

    public static bool AllOrNoneIsNull(params object[] objects) {
      bool? isnull = null;
      foreach(var o in objects) {
        var currentIsnull = o == null;
        if(isnull != null && isnull != currentIsnull) return false; else isnull = currentIsnull;
      }
      return true;
    }

    public static void DontWorry(Action action) {
      try { action(); } catch(Exception) { }
    }

    public static void Raise<T>(this EventHandler<T> handler, object sender, T e)
      where T: EventArgs { if(handler != null) handler(sender, e); }

    public static void DeserializeJSON<T, S>(this T target, string json)
      where T: ShadowDeserializable<S> {
      target.ShadowDeserialize(new JavaScriptSerializer().Deserialize<S>(json));
    }
    public static void DeserializeJSON<T, S>(this T target, FileInfo file)
      where T: ShadowDeserializable<S> {
      target.DeserializeJSON<T, S>(File.ReadAllText(file.FullName));
    }
  }

  public interface ShadowDeserializable<S> { void ShadowDeserialize(S shadow); }

  namespace Environment {
    public static class Utils {
      [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      static extern ThreadExecutionState SetThreadExecutionState(ThreadExecutionState esFlags);
      public static ThreadExecutionState KeepComputerAwake(bool? flag = null) {
        if(flag == null) return SetThreadExecutionState(
          ThreadExecutionState.SYSTEM_REQUIRED | ThreadExecutionState.CONTINUOUS);
        else if(flag.Value) return SetThreadExecutionState(
          ThreadExecutionState.DISPLAY_REQUIRED | ThreadExecutionState.CONTINUOUS);
        return SetThreadExecutionState(ThreadExecutionState.CONTINUOUS);
      }

      [DllImport("kernel32.dll")]
      public static extern IntPtr GetConsoleWindow();
      [DllImport("user32.dll")]
      public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCmd nCmdShow);
      public static void clearConsole() { try { Console.Clear(); } catch(Exception) { } }
    }
    [FlagsAttribute]
    public enum ThreadExecutionState: uint {
      CONTINUOUS = 0x80000000, DISPLAY_REQUIRED = 0x00000002, SYSTEM_REQUIRED = 0x00000001
    }
    public enum ShowWindowCmd: int { //google "Windows ShowWindow" for more options and explanations
      HIDE = 0, SHOW = 5, SHOWNORMAL = 1, MAXIMIZE = 3, MINIMIZE = 6, RESTORE = 9
    }

    public class AssemblyPathInfo {
      public Assembly assembly { get; private set; }
      public string fullPath { get; private set; }
      public string dir { get { return Path.GetDirectoryName(fullPath); } }
      public string name { get { return Path.GetFileNameWithoutExtension(fullPath); } }

      public AssemblyPathInfo(Assembly assembly) {
        this.assembly = assembly; this.fullPath = new Uri(assembly.CodeBase).LocalPath;
      }
      public AssemblyPathInfo() : this(Assembly.GetEntryAssembly()) { }
      public AssemblyPathInfo(Type type) : this(type.Assembly) { }
    }

    public class SimpleRedirectedProcess: IDisposable {
      public SimpleRedirectedProcess(
        string FileName = "", string Arguments = "", string WorkingDirectory = ""
      ) {
        this.p = new Process();
        this.p.OutputDataReceived += this.keepInStdoutLines;
        this.p.ErrorDataReceived += this.keepInStderrLines;
        this.p.Exited += (s, e) => this.rightAfterExited(); this.p.EnableRaisingEvents = true;
        if(!FileName.IsNullOrWhiteSpace()) this.howToStart.FileName = FileName;
        else throw new ArgumentNullException();
        if(!Arguments.IsNullOrWhiteSpace()) this.howToStart.Arguments = Arguments;
        if(!WorkingDirectory.IsNullOrWhiteSpace()) this.howToStart.WorkingDirectory = WorkingDirectory;
        this.howToStart.CreateNoWindow = true;
        this.catchError = true;
      }
      public event Action<string> Starting;
      public event DataReceivedEventHandler OutputDataReceived {
        add { this.p.OutputDataReceived += value; this._OutputDataReceived += value; }
        remove { this.p.OutputDataReceived -= value; this._OutputDataReceived -= value; }
      }
      public event DataReceivedEventHandler ErrorDataReceived {
        add { this.p.ErrorDataReceived += value; this._ErrorDataReceived += value; }
        remove { this.p.ErrorDataReceived -= value; this._ErrorDataReceived -= value; }
      }
      public event EventHandler Exited {
        add { this.p.Exited += value; }
        remove { this.p.Exited -= value; }
      }
      public bool keepStdoutLines { get; set; }
      public bool keepStderrLines { get; set; }
      public bool catchError { get; set; }
      public bool deleteCreatedWorkingDir { get; set; }
      public bool KillOnDispose { get; set; }
      public Process process {
        get { if(this.started) return this.p; else throw new InvalidOperationException(); }
      }

      public int Start(bool WaitForExit = true) {
        if(this.started) throw new InvalidOperationException();

        if(this.keepStdoutLines) this._stdoutLines = new List<string>();
        else this.p.OutputDataReceived -= this.keepInStdoutLines;
        if(this.keepStdoutLines || this._OutputDataReceived != null) {
          this.howToStart.RedirectStandardOutput = true;
          this.howToStart.UseShellExecute = false;
        } else this.howToStart.RedirectStandardOutput = false;
        if(this.keepStderrLines || this.catchError) this._stderrLines = new List<string>();
        else this.p.ErrorDataReceived -= this.keepInStderrLines;
        if(this.keepStderrLines || this.catchError || this._ErrorDataReceived != null) {
          this.howToStart.RedirectStandardError = true;
          this.howToStart.UseShellExecute = false;
        } else this.howToStart.RedirectStandardError = false;

        if(!this.howToStart.WorkingDirectory.IsNullOrWhiteSpace()
          && !Directory.Exists(this.howToStart.WorkingDirectory)) {
          Directory.CreateDirectory(this.howToStart.WorkingDirectory);
          workingDirCreated = true;
        }
        if(this.Starting != null)
          this.Starting(this.howToStart.FileName + " " + this.howToStart.Arguments);
        this.p.Start();
        this.started = true;

        if(this.howToStart.RedirectStandardOutput) this.p.BeginOutputReadLine();
        if(this.howToStart.RedirectStandardError) this.p.BeginErrorReadLine();

        if(WaitForExit) return this.WaitForExit(); else return 0;
      }

      public int WaitForExit() {
        this.p.EnableRaisingEvents = false;
        this.p.WaitForExit(); this.rightAfterExited();
        return this.p.ExitCode;
      }

      public void rightAfterExited() {
        if(this.catchError && this._stderrLines.Any() && this.p.ExitCode != 0)
          throw new ProcessExitFailureException(this);
        try {
          if(this.deleteCreatedWorkingDir && this.workingDirCreated
            && Path.GetFullPath(this.howToStart.WorkingDirectory) != Directory.GetCurrentDirectory()
            && !Directory.EnumerateFileSystemEntries(this.howToStart.WorkingDirectory).Any())
            Directory.Delete(this.howToStart.WorkingDirectory);
        } catch(Exception) { }
      }

      public IEnumerable<string> stdoutLines {
        get {
          if(this._stdoutLines != null) return this._stdoutLines.AsReadOnly();
          else return Enumerable.Empty<string>();
        }
      }
      public IEnumerable<string> stderrLines {
        get {
          if(this._stderrLines != null) return this._stderrLines.AsReadOnly();
          else return Enumerable.Empty<string>();
        }
      }

      public void Dispose() {
        if(KillOnDispose) try { this.p.Kill(); } catch(Exception) { }
        this.p.Dispose();
      }

      Process p;
      ProcessStartInfo howToStart { get { return this.p.StartInfo; } }
      DataReceivedEventHandler _OutputDataReceived, _ErrorDataReceived;
      bool workingDirCreated, started;
      List<string> _stdoutLines, _stderrLines;
      void keepInStdoutLines(object sender, DataReceivedEventArgs e) {
        if(e.Data != null) this._stdoutLines.Add(e.Data);
      }
      void keepInStderrLines(object sender, DataReceivedEventArgs e) {
        if(e.Data != null) this._stderrLines.Add(e.Data);
      }
    }

    public class ProcessExitFailureException: Exception {
      public int ExitCode { get; private set; }
      public IEnumerable<string> stdoutLines { get; private set; }
      public IEnumerable<string> stderrLines { get; private set; }
      public ProcessExitFailureException(SimpleRedirectedProcess p)
        : base(p.stderrLines.JoinAsString(System.Environment.NewLine)) {
        this.ExitCode = p.process.ExitCode;
        this.Source = p.process.StartInfo.FileName;
        this.stdoutLines = p.stdoutLines;
        this.stderrLines = p.stderrLines;
      }
    }

    public abstract class SimpleCommandlineExeBase {
      public abstract string ExeName { get; }
      public virtual DirectoryInfo WorkingDir { get; set; }
      public event Action<string> OnProcessStarting {
        add { this._OnProcessStarting += value; if(this.p != null)this.p.Starting += value; }
        remove { this._OnProcessStarting -= value; if(this.p != null)this.p.Starting -= value; }
      }
      public event DataReceivedEventHandler OnStdout {
        add { this._OnStdout += value; if(this.p != null)this.p.OutputDataReceived += value; }
        remove { this._OnStdout -= value; if(this.p != null)this.p.OutputDataReceived -= value; }
      }
      public event DataReceivedEventHandler OnStderr {
        add { this._OnStderr += value; if(this.p != null)this.p.ErrorDataReceived += value; }
        remove { this._OnStderr -= value; if(this.p != null)this.p.ErrorDataReceived -= value; }
      }

      protected virtual List<string> commonArgs { get { return _commonArgs; } }
      protected Action<string> _OnProcessStarting { get; private set; }
      protected DataReceivedEventHandler _OnStdout { get; private set; }
      protected DataReceivedEventHandler _OnStderr { get; private set; }
      protected virtual SimpleRedirectedProcess newProcess(string additionalArg = null) {
        var arg = this.commonArgs.JoinAsString(" ") + " " + additionalArg;
        SimpleRedirectedProcess p;
        if(this.WorkingDir == null) p = new SimpleRedirectedProcess(this.ExeName, arg);
        else p = new SimpleRedirectedProcess(this.ExeName, arg, this.WorkingDir.FullName);
        p.Starting += this._OnProcessStarting;
        p.OutputDataReceived += this._OnStdout; p.ErrorDataReceived += this._OnStderr;
        this.p = p;
        return p;
      }

      List<string> _commonArgs = new List<string>();
      SimpleRedirectedProcess p { get; set; }
    }
  }
}
