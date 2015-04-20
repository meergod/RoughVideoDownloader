using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;

namespace SoftStone.AV {
  public abstract class VideoDownloadJob: IDisposable {
    public bool locked { get; private set; }
    public IEnumerable<string> errors { get { return this._errors.AsReadOnly(); } }
    private List<string> _errors { get; set; }

    public static VideoDownloadJob load(FileInfo newJobFile, bool deleteNewJobFile) {
      var noProblem = true;
      try {
        VideoDownloadJob newJob;
        try { newJob = DirectVideoDownloadJob.Deserialize(newJobFile); } catch(Exception err1) {
          try { newJob = PartedVideoDownloadJob.Deserialize(newJobFile); } catch(Exception err2) {
            throw new AggregateException(err1, err2);
          }
        }

        if(!rootDir.Exists) return newJob;
        var savedJobFile = new FileInfo(newJob.jobFilePath);
        if(!savedJobFile.Exists) return newJob;
        var savedJob = newJob.loadSaved(savedJobFile);
        if(savedJob.locked)
          throw new SavedJobException(SavedJobException.ErrorTypes.Locked, savedJob.jobFilePath);

        return newJob;
      } catch(Exception) {
        noProblem = false;
        throw;
      } finally {
        if(noProblem && deleteNewJobFile) newJobFile.Delete();
      }
    }
    public abstract string name { get; }
    public string doit() {
      try {
        Environment.Utility.KeepComputerAwake();
        this.locked = true; this.serialize();
        var downloadedPath = this._doit();
        this.unlock();
        File.Delete(this.jobFilePath);
        return downloadedPath;
      } catch(Exception err) {
        if(!(err is PartedVideoDownloadJob.IncompeletedException))
          this._errors.Add(DateTime.Now.ToStringInBrackets() + err.ToString());
        throw;
      } finally {
        this.unlock();
        Environment.Utility.KeepComputerAwake(false);
      }
    }
    public void Dispose() { this.unlock(); }
    public abstract event Action<string> onProcessStarting;
    public abstract event DataReceivedEventHandler onStdout, onStderr;

    protected string jobFilePath {
      get { return Path.Combine(rootDir.FullName, this.name + jobFileExt); }
    }
    protected void serialize() {
      rootDir.Create();
      File.WriteAllText(this.jobFilePath, new JavaScriptSerializer().Serialize(this));
    }
    protected abstract VideoDownloadJob loadSaved(FileInfo savedJobFile);
    protected abstract string _doit();
    void unlock() { if(this.locked) { this.locked = false; this.serialize(); } }

    public static readonly DirectoryInfo rootDir =
      new DirectoryInfo(Path.Combine(KnownFolders.Downloads.Path, typeof(VideoDownloadJob).Name));
    const string jobFileExt = ".vdj";

    protected bool ShadowDeserialized { get; private set; }
    protected void ShadowDeserialize(VideoDownloadJob.Serialization shadow) {
      this._errors = shadow.errors ?? new List<string>();
      this.locked = shadow.locked ?? false;
      this.ShadowDeserialized = true;
    }
    public abstract class Serialization {
      public bool? locked { get; set; }
      public List<string> errors { get; set; }
    }

    public class SavedJobException : Exception {
      public enum ErrorTypes { Locked, FilenameConflicted }
      public ErrorTypes error { get; private set; }
      public string jobFilePath { get; private set; }
      public SavedJobException(ErrorTypes error, string jobFilePath)
        : base("Saved Job " + error.EnumNameToWords() + ": " + jobFilePath) {
        this.error = error; this.jobFilePath = jobFilePath;
      }
    }
  }

  public class DirectVideoDownloadJob
    : VideoDownloadJob, ShadowDeserializable<DirectVideoDownloadJob.Serialization> {
    public Uri url { get; private set; }
    public bool audioOnly { get; private set; }
    public bool noPlaylist { get; private set; }
    public bool verbose { get; private set; }
    public override string name {
      get { return YoutubeDL.getWorkingDirName(this.url, this.audioOnly, this.noPlaylist); }
    }
    public override event Action<string> onProcessStarting;
    public override event DataReceivedEventHandler onStdout, onStderr;

    protected override VideoDownloadJob loadSaved(FileInfo savedJobFile) {
      return Deserialize(savedJobFile);
    }
    protected override string _doit() {
      var youtubeDL = new YoutubeDL(this.url, this.audioOnly, this.noPlaylist) {
        saveDir = rootDir, verbose = this.verbose
      };
      youtubeDL.OnProcessStarting += this.onProcessStarting;
      youtubeDL.OnStdout += this.onStdout; youtubeDL.OnStderr += this.onStderr;
      return youtubeDL.download();
    }

    internal static DirectVideoDownloadJob Deserialize(FileInfo jobFile) {
      var job = new DirectVideoDownloadJob();
      job.DeserializeJSON<DirectVideoDownloadJob, DirectVideoDownloadJob.Serialization>(jobFile);
      return job;
    }
    private DirectVideoDownloadJob() { }
    public void ShadowDeserialize(DirectVideoDownloadJob.Serialization shadow) {
      if(this.ShadowDeserialized) throw new InvalidOperationException();
      if(shadow.url == null || !shadow.url.IsAbsoluteUri) throw new InvalidCastException();
      this.url = shadow.url;
      this.audioOnly = shadow.audioOnly ?? false;
      this.noPlaylist = shadow.noPlaylist ?? false;
      this.verbose = shadow.verbose ?? false;
      base.ShadowDeserialize(shadow);
    }
    public class Serialization : VideoDownloadJob.Serialization {
      public Uri url { get; set; }
      public bool? audioOnly { get; set; }
      public bool? noPlaylist { get; set; }
      public bool? verbose { get; set; }
    }
  }

  public class PartedVideoDownloadJob
    : VideoDownloadJob, ShadowDeserializable<PartedVideoDownloadJob.Serialization> {
    public string videoTitle { get; private set; }
    public IEnumerable<Item> items { get { return Array.AsReadOnly(this._items); } }
    private Item[] _items { get; set; }
    public Uri parsingUrl { get; private set; }
    public override string name { get { return videoTitle.replaceInvalidFileNameChars(" "); } }
    public override event Action<string> onProcessStarting;
    public override event DataReceivedEventHandler onStdout, onStderr;

    protected override VideoDownloadJob loadSaved(FileInfo savedJobFile) {
      var savedJob = Deserialize(savedJobFile);
      if(this.name != savedJob.name || this._items.Length != savedJob._items.Length
        || !SoftStone.Utility.AllOrNoneIsNull(this.parsingUrl, savedJob.parsingUrl)
        || (this.parsingUrl != null && this.parsingUrl != savedJob.parsingUrl))
        throw new SavedJobException(
          SavedJobException.ErrorTypes.FilenameConflicted, savedJob.jobFilePath);
      for(var i = 0; i < this._items.Length; i++) {
        var item = savedJob._items[i];
        if(!item.downloaded) item.url = this._items[i].url;
        this._items[i] = item;
      }
      return savedJob;
    }
    protected override string _doit() {
      var workingDir = rootDir.CreateSubdirectory(this.name);
      foreach(var item in this._items.Where(i => !i.downloaded)) {
        var patience = initialPatience + item.timeoutTimes;
        if(patience > patienceUpperLimit) patience = patienceUpperLimit;
        var lastTimeDownloadProgressed = DateTime.Now;
        using(var web = new WebClient()) {
          try {
            web.DownloadProgressChanged += (s, e) => lastTimeDownloadProgressed = DateTime.Now;
            web.DownloadFileCompleted += (s, e) => {
              if(e.Cancelled) {
                item.timeoutTimes += 1;
                var itemProgressEvent = new Item.ProgressEventArgs(Item.ProgressState.Aborted, patience);
                item._errors.Add(itemProgressEvent.abortLog);
                this.serialize();
                this.itemProgressed.Raise(item, itemProgressEvent);
              } else if(e.Error != null) {
                handleItemException(item, e.Error);
              } else {
                item.downloaded = true; this.serialize();
                this.itemProgressed.Raise(item, new Item.ProgressEventArgs(Item.ProgressState.Finished));
              }
            };

            web.DownloadFileAsync(item.url, Path.Combine(workingDir.FullName, item.filename));
            this.itemProgressed.Raise(item, new Item.ProgressEventArgs(Item.ProgressState.Started));
            while(web.IsBusy) {
              Thread.Sleep(TimeSpan.FromSeconds(5));
              if(DateTime.Now - lastTimeDownloadProgressed > TimeSpan.FromMinutes(patience)
                && web.IsBusy) web.CancelAsync();
            }
          } catch(Exception err) { handleItemException(item, err); }
        }
      }

      if(this._items.Any(i => !i.downloaded)) throw new IncompeletedException(this);

      var ffmpeg = new FFmpeg();
      ffmpeg.OnProcessStarting += this.onProcessStarting;
      ffmpeg.OnStdout += this.onStdout; ffmpeg.OnStderr += this.onStderr;
      var finalVideoFilePath = ffmpeg.join(workingDir, null, true);
      return finalVideoFilePath;
    }
    const int initialPatience = 3, patienceUpperLimit = 7;

    public event EventHandler<Item.ProgressEventArgs> itemProgressed;
    public class Item : ShadowDeserializable<Item.Serialization> {
      public Uri url { get; internal set; }
      public bool downloaded { get; internal set; }
      public int timeoutTimes { get; internal set; }
      public IEnumerable<string> errors { get { return this._errors.AsReadOnly(); } }
      internal List<string> _errors { get; private set; }

      public int sn { get; internal set; }
      internal string filename { get; set; }
      internal Item() { }

      public class ProgressEventArgs : EventArgs {
        public DateTime progressTime { get; private set; }
        public ProgressState progressMade { get; private set; }
        public Exception error { get; private set; }
        public int timeout { get; private set; }
        private ProgressEventArgs() { progressTime = DateTime.Now; }
        internal ProgressEventArgs(ProgressState state)
          : this() {
          if(state == ProgressState.Aborted) throw new ArgumentException();
          this.progressMade = state;
        }
        internal ProgressEventArgs(ProgressState state, Exception error)
          : this() {
          if((state != ProgressState.Aborted && error != null)
            || (state == ProgressState.Aborted && error == null)) throw new ArgumentException();
          this.progressMade = state; this.error = error;
        }
        internal ProgressEventArgs(ProgressState state, int timeout)
          : this() {
          if((state != ProgressState.Aborted && timeout != 0)
            || (state == ProgressState.Aborted && timeout < initialPatience))
            throw new ArgumentException();
          this.progressMade = state; this.timeout = timeout;
        }
        public string abortLog {
          get {
            if(this.progressMade != ProgressState.Aborted) return "";
            if(error != null) {
              var msgTolog = this.error.ToString();//.Split(new[] { "--- 內部例外狀況堆疊追蹤的結尾 ---" }, StringSplitOptions.None)[0].TrimEnd()
              var webException = this.error as WebException;
              if(webException != null) {
                if(webException.Status == WebExceptionStatus.ProtocolError) {
                  var errResponse = webException.Response as HttpWebResponse;
                  if(errResponse != null && errResponse.StatusCode == HttpStatusCode.NotFound)
                    msgTolog = "URL outdated.";
                } else msgTolog = "[" + webException.Status.ToString() + "]" + msgTolog;
              }
              return progressTime.ToStringInBrackets() + msgTolog;
            } else return progressTime.ToStringInBrackets()
              + "Aborted after the download being idle for " + this.timeout + " mins.";
          }
        }
      }
      public enum ProgressState { Started, Finished, Aborted }

      public void ShadowDeserialize(Item.Serialization shadow) {
        if(this.ShadowDeserialized) throw new InvalidOperationException();

        try {
          WebRequest.Create(shadow.url);
        } catch(Exception err) { throw new InvalidCastException(null, err); }
        this.url = shadow.url;
        this.downloaded = shadow.downloaded ?? false;
        this.timeoutTimes = shadow.timeoutTimes ?? 0;
        this._errors = shadow.errors ?? new List<string>();

        this.ShadowDeserialized = true;
      }
      private bool ShadowDeserialized { get; set; }
      public class Serialization {
        public Uri url { get; set; }
        public bool? downloaded { get; set; }
        public int? timeoutTimes { get; set; }
        public List<string> errors { get; set; }
      }
    }
    void handleItemException(Item item, Exception err) {
      var itemProgressEvent = new Item.ProgressEventArgs(Item.ProgressState.Aborted, err);
      item._errors.Add(itemProgressEvent.abortLog); this.serialize();
      this.itemProgressed.Raise(item, itemProgressEvent);
    }

    internal static PartedVideoDownloadJob Deserialize(FileInfo jobFile) {
      var job = new PartedVideoDownloadJob();
      job.DeserializeJSON<PartedVideoDownloadJob, PartedVideoDownloadJob.Serialization>(jobFile);
      return job;
    }
    private PartedVideoDownloadJob() { }
    public void ShadowDeserialize(PartedVideoDownloadJob.Serialization shadow) {
      if(this.ShadowDeserialized) throw new InvalidOperationException();
      if(shadow.videoTitle.IsNullOrWhiteSpace() || shadow.items.IsNullOrEmpty())
        throw new InvalidCastException();
      this.videoTitle = shadow.videoTitle;
      this.parsingUrl = shadow.parsingUrl;
      var items = new Item[shadow.items.Length];
      for(var i = 0; i < items.Length; i++) {
        var item = new Item();
        item.ShadowDeserialize(shadow.items[i]);
        item.sn = i + 1; item.filename = (i + 1).padByTotal(items.Length);
        items[i] = item;
      }
      this._items = items;
      base.ShadowDeserialize(shadow);
    }
    public class Serialization : VideoDownloadJob.Serialization {
      public string videoTitle { get; set; }
      public Item.Serialization[] items { get; set; }
      public Uri parsingUrl { get; set; }
    }

    public class IncompeletedException: Exception {
      public Uri urlForRenewal { get; private set; }
      public IncompeletedException(PartedVideoDownloadJob job)
        : base() { this.urlForRenewal = job.parsingUrl; }
    }
  }
}
