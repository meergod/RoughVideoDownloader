if(window.document.forms.m3uForm !== undefined) {
  var m3uForm = window.document.forms["m3uForm"];
  var urls = m3uForm.elements["inf"].value.split("|");
  if(urls[urls.length - 1] == "") urls.pop();
  var items = [];
  for(var i = 0; i < urls.length; i++) {
    items[i] = { url: urls[i] };
  }

  chrome.runtime.sendMessage({
    nativeMsg: {
      videoTitle: m3uForm.elements["filename"].value
      , items: items
      , parsingUrl: window.document.documentURI
    }
  });
} else chrome.runtime.sendMessage({ activateTab: true });