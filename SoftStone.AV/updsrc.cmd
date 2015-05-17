cd /d %~dp0
copy /y %appdata%\RoughVideoDownloader\youtube-dl .
FOR /F "delims=" %%i IN ('py youtube-dl --version') DO set v=%%i
py youtube-dl --help > %1\youtube-dl.%v%.txt