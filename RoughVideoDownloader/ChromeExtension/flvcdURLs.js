var openedByBackground = false;
chrome.runtime.onMessage.addListener(function(msg, sender, sendResponse) { openedByBackground = true; });

var urlParser = document.createElement('a');
var hostsToParseByFlvcd = [];
$("a[href^='http://www.flvcd.com/parse.php?'").filter(function() {
  return $(this).prevAll("a[name]:first").text().indexOf('支持清晰度选择') > -1;
}).each(function() {
  urlParser.href = $(this).text();
  if(hostsToParseByFlvcd.indexOf(urlParser.hostname) == -1) hostsToParseByFlvcd.push(urlParser.hostname);
});
chrome.runtime.sendMessage({ hostsToParseByFlvcd: hostsToParseByFlvcd, closeTab: openedByBackground });
