using SoftStone.Environment;
using SoftStone.FileSystem;
using System;
using System.IO;
using System.Linq;

namespace SoftStone.AV {
  public static class Utility {
    public static string AVfileContainerType(
      this FileInfo mediaFile, bool copyIfPathCharsetIssue = true
    ) {
      var format = "";
      using(var mediaInfo = new MediaInfoDotNet.MediaFile(mediaFile.FullName)) {
        format = mediaInfo.format;
      }
      if(format == "") {
        if(copyIfPathCharsetIssue) {
          var tmpPath = Path.GetTempFileName();
          try {
            mediaFile = mediaFile.CopyTo(tmpPath, true);
            using(var mediaInfo = new MediaInfoDotNet.MediaFile(mediaFile.FullName)) {
              format = mediaInfo.format;
            }
          } finally { File.Delete(tmpPath); }
        } else throw new NotSupportedException();
      }
      if(format == "Flash Video") return ".flv";
      if(format == "MPEG-4") return ".mp4";
      throw new NotSupportedException("Unknown Container Format: " + format);
    }
  }

  public class VideoScale {
    public int width { get; private set; }
    public int height { get; private set; }
    public VideoScale(int width, int height) {
      if(width <= 0 || height <= 0) throw new ArgumentException();
      this.width = width; this.height = height;
    }
    public override string ToString() { return width + "x" + height; }
  }

  public class FFmpeg: SimpleCommandlineExeBase {
    public string join(
      DirectoryInfo inputDir, string outputFilePath = null, bool deleteInputDir = false
    ) {
      var files = inputDir.EnumerateFiles().OrderBy(i => i.Name);
      var containerType = files.First().AVfileContainerType();
      if(!outputFilePath.IsNullOrWhiteSpace())
        outputFilePath = Path.Combine(inputDir.FullName, outputFilePath + containerType);
      else outputFilePath = inputDir.FullName.TrimEnd(Path.DirectorySeparatorChar) + containerType;

      if(files.Count() > 1) {
        var listFilePath = Path.Combine(Path.GetTempPath()
          , "ffmpegConcat" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        var outputFilePathExisted = true;
        try {
          File.WriteAllLines(listFilePath
            , files.Select(i => "file '" + i.FullName + "'"));
          using(var process = this.newProcess(
            "-f concat -i " + listFilePath.DoubleQutoe() + " -c copy " + outputFilePath.DoubleQutoe()
          )) {
            outputFilePathExisted = File.Exists(outputFilePath);
            process.Start();
          }
          if(deleteInputDir) inputDir.tryDelete(true);
        } catch(Exception) {
          if(!outputFilePathExisted) File.Delete(outputFilePath);
          throw;
        } finally { File.Delete(listFilePath); }
      } else if(deleteInputDir) {
        files.Single().MoveTo(outputFilePath);
        inputDir.tryDelete();
      } else files.Single().CopyTo(outputFilePath);
      return outputFilePath;
    }
    
    public FFmpeg() { this.commonArgs.Add("-loglevel error -n"); }
    public override string ExeName { get { return "ffmpeg.exe"; } }
  }
}
