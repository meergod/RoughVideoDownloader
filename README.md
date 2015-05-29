# RoughVideoDownloader
A [Chrome](https://www.google.com/chrome/browser/desktop/index.html) extension to easily call [youtube-dl](http://rg3.github.io/youtube-dl/) to download videos (single or playlist) from websites such as YouTube and Dailymotion. For some Chinese sites like youku.com, the service by www.flvcd.com is used instead to bypass regional restrictions set by these sites, but in this case playlists and subtitles are not supported.

*Things you probably should know:*

- This is just a simple tool to fulfill my own video downloading needs. I have no intention to make it full-featured, nor any warranty is provided. Use it at your own risk, though it's not likely to harm any data in your computer anyway.
- This software doesn't affiliate with youtube-dl nor flvcd.com in any way. It just takes advantage of the great works the aforementioned parties provide.

## System Requirements
- Windows 7 (not tested on Vista, 8 nor above)
- .NET Framework 4 or above
- [Python 3](https://www.python.org/) (Windows version, of course, and it must have been installed "for all users", which is default and hence the usual case.)
 - This requirement is to avoid [a current limitation of youtube-dl on Windows](https://github.com/rg3/youtube-dl/issues/5045).

## Installation
Installation is a bit hassle. This is because extension developers are demanded to pay to put their works on Chrome Web Store even when the work is intended to be free for public use. Even worse, Chrome Web Store actually blocks extensions which download YouTube videos. Hence this extension has to be manually configured into Chrome, unless Google changes their rules someday.

1. Execute [RoughVideoDownloader.msi](https://github.com/casinero/RoughVideoDownloader/raw/master/RoughVideoDownloader/Setup/Express/SingleImage/DiskImages/DISK1/RoughVideoDownloader.msi).
2. Open the [Extensions page of Chrome](chrome://extensions/) and activate `Developer mode`.
3. Open `%appdata%\RoughVideoDownloader` folder in your Windows, drag the `ChromeExtension` sub-folder icon onto anywhere in Chrome's Extensions page, and then copy the value of the extension ID shown.
 - After this, each time you open Chrome there'll be a warning at the top-right corner asking you to "Disable developer mode extensions", which means to disable extensions not installed from Chrome Web Store. As you can see you have to click `Cancel`. Again this inconvenience is due to Google.
4. Back to `%appdata%\RoughVideoDownloader` folder, open `chromeNativeMessagingHost.json` with any text editor, replace the value between `"chrome-extension://` and `/"` (in `allowed_origins` section) with the ID just copied in the previous step, and save the file.
 - This has to be done every time you reinstall this software for whatever reasons, including version update (if that ever happens).
5. Close and restart Chrome before making first downloading.

#### Optional
If you do download videos from Chinese video sites, go to http://www.flvcd.com/myset.php, choose the first option `超清优先`, and then click the final button `保存`, in order to get highest resolution possible when downloading. Supported sites are as listed in http://www.flvcd.com/url.php (those marked with "支持清晰度选择").

## Usage
Simply right click on a video page or on a link to a video page, and then click `RoughVideoDownloader` in the context menu. Then,

- a page shows up offering some options. After clicking `Start Downloading`, youtube-dl will be started doing its job in a minimized window.
- but if the page you specified belongs to selected Chinese sites, instead a tab would open aside, in which `flvcd.com/parse.php` parses the video page into directly downloadable URL(s). (You might need to click an ad to enable parsing.) Downloading starts in a minimized window if parsed successfully, or the parsing page shows up describing what went wrong (in Chinese).
 - If downloading of some part(s) fails or has been idle for certain minutes (due to network issue, for example), the parsing page opens in Chrome again to update the URL(s) and re-download those failed parts. (If Chrome is not your default web browser then this won't work.)

#### Output
- When downloading, temporary data are placed in `VideoDownloadJob` folder under your default Downloads folder. They'll be cleared later if no error occurs.
- If there's an error, the window shows up with error logs. You can press R key to try again if you believe it's just a temporary error such as a temporary network failure.
- Successfully downloaded file (or folder in the case of playlist) is in your default Downloads folder.
