using SoftStone.Environment;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SoftStone.AV {
  public class YoutubeDL: SimpleCommandlineExeBase {
    public YoutubeDL(Uri url, bool audioOnly = false, bool noPlaylist = false
      , bool writeSub = false, string subLang = null
    ) {
      this.url = url;
      this.audioOnly = audioOnly;
      this.noPlaylist = noPlaylist;
      this.writeSub = writeSub;
      this.subLang = subLang;
      this.commonArgs.Add(
        Path.Combine(new AssemblyPathInfo(this.GetType()).dir, "youtube-dl").DoubleQutoe());
      this.timesToRetryIfTimedOut = 9;
    }

    public string download(string filenamePrefix = "") {
      this.saveDir = this.saveDir ?? new DirectoryInfo(Directory.GetCurrentDirectory());
      base.WorkingDir = new DirectoryInfo(Path.Combine(this.saveDir.FullName, this.workingDirName));
      if(this.noPlaylist) this.commonArgs.Add("--no-playlist");
      if(this._OnStdout != null && this._OnStderr != null && this.verbose) this.commonArgs.Add("-v");

      try {
        var resultPath = "";
        var playlistInfo = this.getPlaylistInfo();
        if(playlistInfo.isPlaylist) {
          var doneEntries = new List<DoneEntry>();
          var doneEntriesFilePath = Path.Combine(this.WorkingDir.FullName, "DoneEntries");
          if(File.Exists(doneEntriesFilePath))
            File.ReadAllLines(doneEntriesFilePath).Where(i => !i.IsNullOrWhiteSpace()).ToList()
              .ForEach(i => doneEntries.Add(new DoneEntry(i)));

          var errors = new List<PlaylistDownloadExceptionItem>();
          foreach(var entry in
            playlistInfo.entries.Where(i => !doneEntries.Any(j => j.index == i.playlist_index))
          ) {
            try {
              var subDownload = new YoutubeDL(
                new Uri(entry.webpage_url), this.audioOnly, this.noPlaylist
                , this.writeSub, this.subLang
              ) { saveDir = this.WorkingDir, verbose = this.verbose };
              subDownload.OnProcessStarting += this._OnProcessStarting;
              subDownload.OnStdout += this._OnStdout;
              subDownload.OnStderr += this._OnStderr;

              var newDoneEntries = new List<DoneEntry> {
                new DoneEntry(entry.playlist_index, subDownload.download(
                  entry.playlist_index.padByTotal(playlistInfo.entries.Count()) + ") "))
              };
              foreach(var subtitleFilePath in subDownload.subtitleFilePaths)
                newDoneEntries.Add(new DoneEntry(entry.playlist_index, subtitleFilePath));
              File.AppendAllLines(doneEntriesFilePath, newDoneEntries.Select(i => i.ToString()));
              doneEntries.AddRange(newDoneEntries);
            } catch(Exception err) { errors.Add(new PlaylistDownloadExceptionItem(entry, err)); }
          }
          if(!errors.Any()) {
            resultPath = filenamePrefix + playlistInfo.title.replaceInvalidFileNameChars(" ");
            if(!playlistInfo.id.IsNullOrWhiteSpace())
              resultPath += " _" + playlistInfo.id.replaceInvalidFileNameChars();
            resultPath = Path.Combine(this.saveDir.FullName, resultPath.Trim());

            foreach(var entry in doneEntries) {
              FileSystem.Utils.Move(entry.downloadedPath
                , Path.Combine(resultPath, Path.GetFileName(entry.downloadedPath)));
            }
          } else throw new PlaylistDownloadException(playlistInfo, errors);
        } else {
          string additionalArg = "", safeFilename = "";
          try {
            if(this.audioOnly) additionalArg = "-f bestaudio --embed-thumbnail";
            else if(this.possibleBestViedoQuality > this.defaultBestViedoQuality)
              additionalArg = "-f bestvideo+bestaudio";
            safeFilename = this.executeDownload(additionalArg);
          } catch(ProcessExitFailureException err) {
            if(err.Message.Contains("ERROR: requested format not available")) {
              if(this.audioOnly) additionalArg = "-x --embed-thumbnail";
              else additionalArg = "";
              safeFilename = this.executeDownload(additionalArg);
            } else if(err.Message.Contains("ERROR: Could not write header for output file #0 (incorrect codec parameters ?)")) {
              additionalArg += " --merge-output-format mkv";
              safeFilename = this.executeDownload(additionalArg);
            } else throw;
          }
          this.commonArgs.Add(additionalArg);

          var resultPathNoExt = Path.Combine(this.saveDir.FullName
            , filenamePrefix + Path.GetFileNameWithoutExtension(this.filename)
          );
          resultPath = resultPathNoExt + Path.GetExtension(safeFilename);
          File.Move(Path.Combine(this.WorkingDir.FullName, safeFilename)
            , resultPath);
          for(var i = 0; i < this._subtitleFilePaths.Count; i++) {
            var subtitleFilePath = resultPathNoExt
              + this._subtitleFilePaths[i].Remove(0
                  , Path.GetFileNameWithoutExtension(safeFilename).Length);
            File.Move(Path.Combine(this.WorkingDir.FullName, this._subtitleFilePaths[i])
              , subtitleFilePath);
            this._subtitleFilePaths[i] = subtitleFilePath;
          }
        }
        this.WorkingDir.Delete(true);
        return resultPath;
      } catch(ProcessExitFailureException err) {
        ThrowIfUnsupport(err);
        throw;
      }
    }
    public IEnumerable<string> getSubtitleLanguages() {
      try {
        base.WorkingDir = new DirectoryInfo(Path.GetTempPath());
        return this.getPlaylistInfo("--all-subs").subtitleLanguages;
      } catch(ProcessExitFailureException err) {
        ThrowIfUnsupport(err);
        throw;
      }
    }

    Uri url;
    bool audioOnly;
    bool noPlaylist;
    bool writeSub;
    string subLang;
    public DirectoryInfo saveDir { get; set; }
    public bool verbose { get; set; }
    public byte timesToRetryIfTimedOut { get; set; }
    VideoFormat _defaultBestViedoQuality, _possibleBestViedoQuality;
    VideoFormat defaultBestViedoQuality {
      get {
        if(this._defaultBestViedoQuality == null)
          this._defaultBestViedoQuality = new VideoFormat(this.info.format);
        return this._defaultBestViedoQuality;
      }
    }
    VideoFormat possibleBestViedoQuality {
      get {
        if(this._possibleBestViedoQuality == null)
          this._possibleBestViedoQuality =
            new VideoFormat(this.query("--get-format -f bestvideo").Single());
        return this._possibleBestViedoQuality;
      }
    }
    string _filename; string filename {
      get {
        if(this._filename == null) this._filename = this.info._filename;
        return this._filename;
      }
    }
    ExtractionInfo _info; ExtractionInfo info {
      get {
        if(this._info == null)
          this._info = Utils.DeserializeJSON<ExtractionInfo, ExtractionInfo.JSON>(this.query("-j").ToArray());
        return this._info;
      }
    }
    List<string> _subtitleFilePaths; public IEnumerable<string> subtitleFilePaths {
      get {
        if(this._subtitleFilePaths == null) return Enumerable.Empty<string>();
        return _subtitleFilePaths.AsReadOnly();
      }
    }

    public static string getWorkingDirName(Uri url, bool audioOnly = false, bool noPlaylist = false) {
      var suffix = "";
      if(audioOnly) suffix += "a";
      if(noPlaylist) suffix += "n";
      if(suffix != "") suffix = "~" + suffix;
      return url.GetComponents(
        UriComponents.Host | UriComponents.Port | UriComponents.PathAndQuery | UriComponents.Fragment
        , UriFormat.Unescaped
      ).replaceInvalidFileNameChars() + suffix;
    }
    public string workingDirName {
      get { return getWorkingDirName(this.url, this.audioOnly, this.noPlaylist); }
    }
    public override DirectoryInfo WorkingDir { set { throw new NotSupportedException(); } }
    public override string ExeName { get { return "py"; } }

    PlaylistInfo getPlaylistInfo(string additionalArg = null) {
      PlaylistInfo result = null;
      try {
        result =
          Utils.DeserializeJSON<PlaylistInfo, PlaylistInfo.JSON>(
            this.query("-J -i " + additionalArg).ToArray());
      } catch(ProcessExitFailureException err) {
        try {
          result =
            Utils.DeserializeJSON<PlaylistInfo, PlaylistInfo.JSON>(
              err.stdoutLines.ToArray());
        } catch(Exception) { throw err; }
      }
      return result;
    }
    IEnumerable<string> query(string additionalArg = null) {
      return this.executeProcess(additionalArg, executedProcess => executedProcess.stdoutLines);
    }
    string executeDownload(string additionalArg = null) {
      var argsForDownload = " --restrict-filenames";
      if(this.writeSub) {
        argsForDownload += " --write-sub";
        if(!this.subLang.IsNullOrWhiteSpace()) argsForDownload += " --sub-lang " + this.subLang;
      }
      return this.executeProcess(additionalArg + argsForDownload, executedProcess => {
        var stdoutContent = executedProcess.stdoutLines.Reverse().JoinAsString("\n");
        
        var matched =
          Regex.Match(stdoutContent, @"^\[.+\] Merging formats into ""(.+)""$", RegexOptions.Multiline);
        if(!matched.Success)
          matched = Regex.Match(stdoutContent, @"^\[.+\] Destination: (.+)$", RegexOptions.Multiline);
        if(!matched.Success)
          matched = Regex.Match(stdoutContent, @"^\[download\] (.+) has already been downloaded and merged$"
            , RegexOptions.Multiline);
        if(!matched.Success) throw new NotSupportedException();
        var safeFilename = matched.Groups[1].Value;

        this._subtitleFilePaths = new List<string>();
        foreach(Match matched2 in Regex.Matches(stdoutContent, @"^\[info\] Writing video subtitles to: (.+)$"
          , RegexOptions.Multiline))
          this._subtitleFilePaths.Add(matched2.Groups[1].Value);

        return safeFilename;
      });
    }
    T executeProcess<T>(string additionalArg, Func<SimpleRedirectedProcess, T> getInfoFromExecutedProcess) {
      int timesTried = 0;
      do {
        try {
          using(var p = this.newProcess(additionalArg)) {
            p.Start(); return getInfoFromExecutedProcess(p);
          }
        } catch(ProcessExitFailureException err) {
          if(err.stderrLines.Any(i =>
            i.StartsWith("ERROR: Unable to download ")
            && i.Contains(": <urlopen error ") && i.Contains("(caused by URLError(")
          ) || err.stderrLines.Any(i =>
            i.StartsWith("ERROR: unable to download video data: ") && i.EndsWith(" timed out")
          ) || err.stderrLines.Any(i => i.StartsWith("ERROR: content too short (expected "))
          ) {
            timesTried += 1; if(timesTried > this.timesToRetryIfTimedOut) throw;
          } else throw;
        }
      } while(true);
    }
    protected override SimpleRedirectedProcess newProcess(string additionalArg = null) {
      var p = base.newProcess(additionalArg + " " + this.url.AbsoluteUri);
      p.keepStdoutLines = p.KillOnDispose = true;
      return p;
    }

    void ThrowIfUnsupport(ProcessExitFailureException err) {
      var prefixPattern = "ERROR: Unsupported URL: ";
      var unsupported = err.stderrLines.LastOrDefault(i => i.StartsWith(prefixPattern));
      if(unsupported != null) throw new UnsupportedUrlException(
        Regex.Match(unsupported, prefixPattern + "(.+)$").Groups[1].Value
      );
    }

#region helping types
    public class PlaylistInfo : ShadowDeserializable<PlaylistInfo.JSON> {
      public bool isPlaylist { get { return _type == "playlist"; } }

      public string _type { get; private set; }
      public string title { get; private set; }
      public IEnumerable<ExtractionInfo> entries { get; private set; }
      public string id { get; private set; }
      public IEnumerable<string> subtitleLanguages { get; private set; }

      public void ShadowDeserialize(PlaylistInfo.JSON shadow) {
        if(this.ShadowDeserialized) throw new InvalidOperationException();

        this._type = shadow._type;
        this.title = shadow.title;
        if(shadow.entries != null) {
          var entries = new ExtractionInfo[shadow.entries.Length];
          for(var i = 0; i < entries.Length; i++) {
            if(shadow.entries[i] != null) {
              var entry = new ExtractionInfo();
              entry.ShadowDeserialize(shadow.entries[i]);
              entries[i] = entry;
            }
          }
          this.entries = entries.Where(i => i != null);
        } else this.entries = Enumerable.Empty<ExtractionInfo>();
        this.id = shadow.id;
        if(!shadow.requested_subtitles.IsNullOrEmpty()) {
          this.subtitleLanguages = shadow.requested_subtitles.Keys;
        } else if(this.isPlaylist) {
          this.subtitleLanguages = new HashSet<string>();
          foreach(var entry in this.entries ?? new ExtractionInfo[0])
            this.subtitleLanguages =
              this.subtitleLanguages.Union(entry.subtitleLanguages);
        } else this.subtitleLanguages = Enumerable.Empty<string>();

        this.ShadowDeserialized = true;
      }
      private bool ShadowDeserialized { get; set; }
      public class JSON {
        public string _type { get; set; }
        public string title { get; set; }
        public ExtractionInfo.JSON[] entries { get; set; }
        public string id { get; set; }
        public Dictionary<string, object> requested_subtitles { get; set; }
      }
    }
    public class ExtractionInfo : ShadowDeserializable<ExtractionInfo.JSON> {
      public string _filename { get; private set; }
      public string format { get; private set; }
      public int playlist_index { get; private set; }
      public string webpage_url { get; private set; }
      public IEnumerable<string> subtitleLanguages { get; private set; }

      public void ShadowDeserialize(ExtractionInfo.JSON shadow) {
        if(this.ShadowDeserialized) throw new InvalidOperationException();

        this._filename = shadow._filename;
        this.format = shadow.format;
        this.playlist_index = shadow.playlist_index.GetValueOrDefault();
        this.webpage_url = shadow.webpage_url;
        if(!shadow.requested_subtitles.IsNullOrEmpty()) {
          this.subtitleLanguages = shadow.requested_subtitles.Keys;
        } else this.subtitleLanguages = Enumerable.Empty<string>();

        this.ShadowDeserialized = true;
      }
      private bool ShadowDeserialized { get; set; }
      public class JSON {
        public string _filename { get; set; }
        public string format { get; set; }
        public int? playlist_index { get; set; }
        public string webpage_url { get; set; }
        public Dictionary<string, object> requested_subtitles { get; set; }
      }
    }

    class VideoFormat : IComparable<VideoFormat> {
      string code;
      VideoScale scale;
      string remark;
      internal VideoFormat(string str) {
        var match = Regex.Match(str, @"^([a-zA-Z0-9]+) - (\d+)x(\d+)( .+)?");
        if(!match.Success) throw new FormatException();
        code = match.Groups[1].Value;
        scale = new VideoScale(int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        remark = match.Groups[4].Value.Trim();
      }
      public int CompareTo(VideoFormat theOther) {
        if(this.scale.height < theOther.scale.height) return -1;
        if(this.scale.height > theOther.scale.height) return 1;
        if(this.scale.width < theOther.scale.width) return -1;
        if(this.scale.width > theOther.scale.width) return 1;
        if(this.remark.Contains("DASH") && !theOther.remark.Contains("DASH")) return -1;
        if(!this.remark.Contains("DASH") && theOther.remark.Contains("DASH")) return 1;
        return 0;
      }
      #region operators
      public static bool operator <(VideoFormat first, VideoFormat second) {
        return first.CompareTo(second) < 0;
      }
      public static bool operator <=(VideoFormat first, VideoFormat second) {
        return first.CompareTo(second) <= 0;
      }
      public static bool operator >(VideoFormat first, VideoFormat second) {
        return first.CompareTo(second) > 0;
      }
      public static bool operator >=(VideoFormat first, VideoFormat second) {
        return first.CompareTo(second) >= 0;
      }
      #endregion
    }

    class DoneEntry {
      internal int index { get; private set; }
      internal string downloadedPath { get; private set; }
      internal DoneEntry(int index, string downloadedPath) {
        this.index = index; this.downloadedPath = downloadedPath;
      }
      internal DoneEntry(string stringRepresentation) {
        var parts = stringRepresentation.Split('\t');
        this.index = int.Parse(parts[0]);
        this.downloadedPath = parts[1];
      }
      public override string ToString() {
        return this.index + "\t" + this.downloadedPath;
      }
    }

    public class UnsupportedUrlException : NotSupportedException {
      public string url { get; private set; }
      public UnsupportedUrlException(string url) : base("Unsupported URL: " + url) { this.url = url; }
    }
    public class PlaylistDownloadException : Exception {
      public PlaylistInfo playlistInfo { get; private set; }
      public ReadOnlyCollection<PlaylistDownloadExceptionItem> exceptions { get; private set; }
      public string downloadedPath { get; private set; }
      internal PlaylistDownloadException(
        PlaylistInfo playlistInfo
        , List<PlaylistDownloadExceptionItem> exceptions
        , string downloadedPath = null
      ) {
        this.playlistInfo = playlistInfo;
        this.exceptions = exceptions.AsReadOnly();
        this.downloadedPath = downloadedPath;
      }
    }
    public class PlaylistDownloadExceptionItem {
      public ExtractionInfo entryInfo { get; private set; }
      public Exception exception { get; private set; }
      internal PlaylistDownloadExceptionItem(ExtractionInfo entryInfo, Exception exception) {
        this.entryInfo = entryInfo; this.exception = exception;
      }
    }
#endregion
  }
}
