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
          var msg = SoftStone.Net.Utils.recvMsgFromChrome();
          var done = true;
          try {
            var job = new DirectVideoDownloadJob();
            job.DeserializeJSON<DirectVideoDownloadJob, DirectVideoDownloadJob.Serialization>(msg);
            if(job.command == "getSubLangs")
              SoftStone.Net.Utils.sendMsgToChrome(
                new { subLangs = job.getSubtitleLanguages() }
              );
            else done = false;
          } catch(InvalidCastException err) {
            done = false;
          } catch(YoutubeDL.UnsupportedUrlException) {
            SoftStone.Net.Utils.sendMsgToChrome(new { unsupported = true });
          } catch(ProcessExitFailureException err) {
            SoftStone.Net.Utils.sendMsgToChrome(new { errMsg = err.Message });
          }
          if(done) return;

          var jobFile = Path.GetTempFileName();
          File.WriteAllText(jobFile, msg);
          using(Process.Start(exePath.fullPath, jobFile)) { }
        } catch(Exception err) {
          File.AppendAllLines(
            Path.Combine(Path.GetTempPath(), exePath.name + ".log")
            , new[] { "", DateTime.Now.ToString(), err.ToString() });
        }
        return;
      }
      #endregion

      var resultPath = "";
      bool allOk = false, showConsoleAtEnd = true;
      Exception exception = null;
      var logPath = ""; var logLines = new List<string>();
      Action<string> toLog = msg => { if(msg != null) logLines.Add(msg); };
      Action<string> toStdout = msg => { if(msg != null) Console.Out.WriteLine(msg); };
      Action<string> toStderr = msg => { if(msg != null) Console.Error.WriteLine(msg); };
      Action<string> toLogAndStderr = msg => { if(logLines.Any()) toLog(msg); toStderr(msg); };
      var consoleWindow = Environment.Utils.GetConsoleWindow();
      try {
        Environment.Utils.ShowWindow(consoleWindow, Environment.ShowWindowCmd.MINIMIZE);
        using(var job = VideoDownloadJob.load(new FileInfo(args[0]))) {
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
          var saveDir = KnownFolders.Downloads.Path;
          resultPath = Path.Combine(saveDir, Path.GetFileName(downloadedPath));
          FileSystem.Utils.Move(downloadedPath, resultPath);
          foreach(var subtitleFilePath in job.subtitleFilePaths ?? new string[0])
            File.Move(subtitleFilePath
              , Path.Combine(saveDir, Path.GetFileName(subtitleFilePath)));
        }
        allOk = true;
        Utils.DontWorry(() => {
          File.Delete(args[0]);
          if(!VideoDownloadJob.rootDir.EnumerateFileSystemInfos().Any()) VideoDownloadJob.rootDir.Delete();
        });
        Environment.Utils.clearConsole();
      } catch(PartedVideoDownloadJob.IncompeletedException err) {
        using(var browser = Process.Start(err.urlForRenewal.OriginalString)) { }
        showConsoleAtEnd = false;
      } catch(YoutubeDL.PlaylistDownloadException errs) {
        toLogAndStderr(errs.GetType().Name + ":");
        foreach(var err in errs.exceptions) {
          toLogAndStderr("@" + err.entryInfo.playlist_index + "(" + err.entryInfo.webpage_url + ")");
          //if(err.exception is ProcessExitFailureException) toLogAndStderr(err.exception.Message);
          //else toLogAndStderr(err.exception.ToString());
          toLogAndStderr(err.exception.GetType().FullName + ": " + err.exception.Message);
        }
      } catch(ProcessExitFailureException) {
      } catch(Exception err) {
        var errMsg = err.ToString();
        if(err is VideoDownloadJob.SavedJobException || err is YoutubeDL.UnsupportedUrlException) {
          errMsg = err.Message; Environment.Utils.clearConsole();
          exception = err;
        }
        toLogAndStderr(errMsg);
      } finally {
        var logWritten = false;
        if(logLines.Any() && !allOk) {
          File.WriteAllLines(logPath, logLines);
          logWritten = true;
        }
        if(showConsoleAtEnd) {
          Environment.Utils.ShowWindow(consoleWindow, Environment.ShowWindowCmd.RESTORE);
          if(!allOk) {
            while(true) {
              Console.WriteLine();
              var procToStart = "";
              if(exception is YoutubeDL.UnsupportedUrlException) {
                Console.WriteLine("Press L to browse the URL.");
                procToStart = (exception as YoutubeDL.UnsupportedUrlException).url;
              } else if(logWritten) {
                Console.WriteLine("Press L to open log file.");
                procToStart = logPath;
              }
              Console.Write("Press R to retry. Press anything else to exit.");
              var keyPressed = Console.ReadKey(true).Key;
              if(procToStart != "" && keyPressed == ConsoleKey.L) {
                using(Process.Start(procToStart)) { }
              } else {
                if(keyPressed == ConsoleKey.R) using(Process.Start(exePath.fullPath, args[0])) { }
                break;
              }
            }
          } else {
            toStdout("Press ENTER to open downloaded video:");
            toStdout(Path.GetFileName(resultPath));
            if(Console.ReadKey(true).Key == ConsoleKey.Enter) using(Process.Start(resultPath)) { }
          }
        }
      }
    }
  }
}
