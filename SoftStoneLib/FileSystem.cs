using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using VBFileIO = Microsoft.VisualBasic.FileIO;

namespace SoftStone.FileSystem {
  public static class Utils {
    public static void Recycle(string path) {
      if(File.Exists(path)) {
        VBFileIO.FileSystem.DeleteFile(
          path, VBFileIO.UIOption.OnlyErrorDialogs, VBFileIO.RecycleOption.SendToRecycleBin);
      } else if(Directory.Exists(path)) {
        VBFileIO.FileSystem.DeleteDirectory(
          path, VBFileIO.UIOption.OnlyErrorDialogs, VBFileIO.RecycleOption.SendToRecycleBin);
      }
    }

    public static bool IsNotEmpty(this DirectoryInfo dir) {
      return dir.Exists && dir.EnumerateFileSystemInfos().Any();
    }

    public static void tryDelete(this FileSystemInfo file) {
      try { file.Delete(); } catch(Exception) { }
    }
    public static void tryDelete(this DirectoryInfo dir, bool recursive) {
      try { dir.Delete(recursive); } catch(Exception) { }
    }

    public static void Move(string srcPath, string dstPath) {
      if(File.Exists(srcPath)) File.Move(srcPath, dstPath);
      else if(Directory.Exists(srcPath)) Directory.Move(srcPath, dstPath);
      else throw new ArgumentException();
    }

    [DllImport("kernel32.dll")]
    public static extern
      bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

    public static string HumanReadableSizeFromBytes(long i) {
      string sign = (i < 0 ? "-" : "");
      double readable = (i < 0 ? -i : i);
      string suffix;
      if(i >= 0x1000000000000000) {
        suffix = "EB"; readable = (double)(i >> 50);
      } else if(i >= 0x4000000000000) {
        suffix = "PB"; readable = (double)(i >> 40);
      } else if(i >= 0x10000000000) {
        suffix = "TB"; readable = (double)(i >> 30);
      } else if(i >= 0x40000000) {
        suffix = "GB"; readable = (double)(i >> 20);
      } else if(i >= 0x100000) {
        suffix = "MB"; readable = (double)(i >> 10);
      } else if(i >= 0x400) {
        suffix = "KB"; readable = (double)i;
      } else {
        return i.ToString(sign + "0B"); // Byte
      }
      readable = readable / 1024;
      return sign + readable.ToString("0.##") + suffix;
    }
  }
}
