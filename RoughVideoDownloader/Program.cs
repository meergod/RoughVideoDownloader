using Microsoft.WindowsAPICodePack.Shell;
using SoftStone.Environment;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SoftStone.AV.RoughVideoDownloader {
  class Program {
    static void Main(string[] args) {
      var exePath = new Environment.AssemblyPathInfo();
      #region Chrome native messaging host
      if(args.Any(i => i.StartsWith("chrome-extension://"))) {
        try {
          var buffer = new byte[4];
          using(var stdin = Console.OpenStandardInput()) {
            stdin.Read(buffer, 0, 4);
            var msgLen = BitConverter.ToInt32(buffer, 0);
            buffer = new byte[msgLen];
            stdin.Read(buffer, 0, msgLen);
          }
          var jobFile = Path.GetTempFileName();
          File.WriteAllText(jobFile, Encoding.UTF8.GetString(buffer));
          using(Process.Start(exePath.fullPath, jobFile)) { }
        } catch(Exception err) {
          File.AppendAllLines(
            Path.Combine(Path.GetTempPath(), exePath.name + ".log")
            , new[] { "", DateTime.Now.ToString(), err.ToString() });
        }
        return;
      }
      #endregion

      bool allOk = false, showConsoleAtEnd = true;
      var logPath = "";
      var logLines = new List<string>();
      Action<string> toLog = msg => { if(msg != null) logLines.Add(msg); };
      Action<string> toStdout = msg => { if(msg != null) Console.Out.WriteLine(msg); };
      Action<string> toStderr = msg => { if(msg != null) Console.Error.WriteLine(msg); };
      Action<string> toLogAndStderr = msg => { if(logLines.Any()) toLog(msg); toStderr(msg); };
      var consoleWindow = Environment.Utility.GetConsoleWindow();
      try {
        Environment.Utility.ShowWindow(consoleWindow, Environment.ShowWindowCmd.MINIMIZE);
        var destPath = "";
        using(var job = VideoDownloadJob.load(new FileInfo(args[0]), true)) {
          logPath = Path.Combine(VideoDownloadJob.rootDir.FullName, job.name + ".log");
          AppDomain.CurrentDomain.ProcessExit += (s, e) => { job.Dispose(); };
          Console.CancelKeyPress += (s, e) => { job.Dispose(); };

          var directJob = job as DirectVideoDownloadJob;
          if(directJob != null) {
            if(directJob.verbose) {
              job.onProcessStarting += commandline => toLog(commandline);
              job.onStdout += (s, e) => toLog(e.Data);
              job.onStderr += (s, e) => toLog(e.Data);
            }
          } else {
            var partedJob = job as PartedVideoDownloadJob;
            if(partedJob != null) {
              partedJob.itemProgressed += (s, e) => {
                switch(e.progressMade) {
                  case PartedVideoDownloadJob.Item.ProgressState.Started:
                    Console.Write(e.progressTime.ToStringInBrackets()
                      + ((PartedVideoDownloadJob.Item)s).sn + ": ");
                    break;
                  case PartedVideoDownloadJob.Item.ProgressState.Finished:
                    toStdout(e.progressTime.ToStringInBrackets() + "OK."); break;
                  case PartedVideoDownloadJob.Item.ProgressState.Aborted:
                    toStderr(e.abortLog); break;
                }
              };
              Console.Title =
                partedJob.items.Count(i => i.downloaded != true) + "/" + partedJob.items.Count()
                + " " + partedJob.videoTitle;
            } else throw new NotImplementedException();
          }
          job.onProcessStarting += commandline => toStdout(commandline);
          job.onStdout += (s, e) => toStdout(e.Data);
          job.onStderr += (s, e) => toStderr(e.Data);

          var downloadedPath = job.doit();
          destPath = Path.Combine(KnownFolders.Downloads.Path, Path.GetFileName(downloadedPath));
          FileSystem.Utils.Move(downloadedPath, destPath);
        }
        allOk = true;
        if(!VideoDownloadJob.rootDir.EnumerateFileSystemInfos().Any()) VideoDownloadJob.rootDir.Delete();
        Environment.Utility.clearConsole();
        toStdout("Downloaded: " + destPath.DoubleQutoe());
      } catch(PartedVideoDownloadJob.IncompeletedException err) {
        using(var browser = Process.Start(err.urlForRenewal.OriginalString)) { }
        showConsoleAtEnd = false;
      } catch(YoutubeDL.PlaylistDownloadException errs) {
        toLogAndStderr(errs.GetType().Name + ":");
        foreach(var err in errs.exceptions) {
          toLogAndStderr("@" + err.entryInfo.playlist_index + "(" + err.entryInfo.webpage_url + ")");
          if(err.exception is ProcessExitFailureException) toLogAndStderr(err.exception.Message);
          else toLogAndStderr(err.exception.ToString());
        }
      } catch(ProcessExitFailureException) {
      } catch(Exception err) {
        var errMsg = err.ToString();
        if(err is YoutubeDL.UnsupportedUrlException || err is VideoDownloadJob.SavedJobException) {
          errMsg = err.Message; Environment.Utility.clearConsole();
        }
        toLogAndStderr(errMsg);
      } finally {
        if(logLines.Any() && !allOk) File.WriteAllLines(logPath, logLines);
        if(showConsoleAtEnd) {
          Environment.Utility.ShowWindow(consoleWindow, Environment.ShowWindowCmd.RESTORE);
          Console.ReadKey(false);
        }
      }
    }
  }
}
