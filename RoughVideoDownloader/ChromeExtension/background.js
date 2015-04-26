var hostsToParseByFlvcd = [];
chrome.runtime.onMessage.addListener(function(msg, sender, sendResponse) {
  if(msg.nativeMsg) {
    chrome.runtime.sendNativeMessage("softstone.av.rough_video_downloader", msg.nativeMsg);
    chrome.tabs.remove(sender.tab.id);
  } else if(msg.hostsToParseByFlvcd) {
    hostsToParseByFlvcd = msg.hostsToParseByFlvcd;
    localStorage.setItem('hostsToParseByFlvcd', JSON.stringify(hostsToParseByFlvcd));
  }
  if(msg.activateTab) chrome.tabs.update(sender.tab.id, { active: true });
  if(msg.closeTab) chrome.tabs.remove(sender.tab.id);
});

hostsToParseByFlvcd = localStorage.getItem('hostsToParseByFlvcd');
if(hostsToParseByFlvcd === null) {
  chrome.tabs.create(
    { url: "http://www.flvcd.com/url.php", active: false }
    , function(tabFlvcdURLs) { chrome.tabs.sendMessage(tabFlvcdURLs.id, { openedByBackground: true }); }
  );
} else hostsToParseByFlvcd = JSON.parse(hostsToParseByFlvcd);

var urlParser = document.createElement("a");
chrome.contextMenus.create({
  title: chrome.runtime.getManifest().name,
  contexts: ["page", "link"],
  onclick: function(clickInfo, thisTab) {
    var urlToParse = "";
    if(clickInfo.linkUrl) urlToParse = clickInfo.linkUrl;
//    else if(clickInfo.frameUrl) urlToParse = clickInfo.frameUrl;
    else urlToParse = clickInfo.pageUrl;

    var iGoogleDotCom = urlToParse.indexOf("google.com");
    var ampersandUrlEquals = "&url=";
    var iAmpersandUrlEquals = urlToParse.indexOf(ampersandUrlEquals);
    if(iGoogleDotCom > -1 && iAmpersandUrlEquals > iGoogleDotCom) {
      var iRealUrlStart = iAmpersandUrlEquals + ampersandUrlEquals.length;
      urlToParse = decodeURIComponent(
        urlToParse.substring(iRealUrlStart, urlToParse.indexOf("&", iRealUrlStart))
      );
    }

    urlParser.href = urlToParse;
    if(hostsToParseByFlvcd.indexOf(urlParser.hostname) == -1) {
      chrome.tabs.create({
        url: "youtube-dl-options.htm?" + encodeURIComponent(urlToParse)
        , active: true, index: thisTab.index + 1
      });
    } else chrome.tabs.create({
      url: "http://www.flvcd.com/parse.php?kw=" + encodeURIComponent(urlToParse)
      , active: false, index: thisTab.index + 1
    });
  }
});
/*
chrome.contextMenus.create({
  title: "Yahoo奇摩字典",
  contexts: ["selection"],
  onclick: function(clickInfo) {
    chrome.tabs.query({active: true, currentWindow: true}, function(result) {
      chrome.tabs.create({
        url: "https://tw.dictionary.yahoo.com/dictionary?p=" + encodeURIComponent(clickInfo.selectionText)
        , index: result[0].index + 1
      });
    });
  }
});
*/