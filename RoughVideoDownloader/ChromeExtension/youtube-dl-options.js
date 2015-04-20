var form = window.document.forms[0];

form.btnStart.onclick = function() {
  chrome.runtime.sendMessage({
    nativeMsg: {
      url: form.url.value
      , audioOnly: form.audioOnly.checked
      , noPlaylist: form.noPlaylist.checked
      , verbose: true
    }
  });
};

if(window.location.search.length > 1) {
  var videoUrl = URI(decodeURIComponent(window.location.search.substring(1)));
  if(!(videoUrl.is('absolute') && videoUrl.is('url'))) {
    window.alert('Invalid URL: ' + videoUrl.href());
    window.close();
  }
  else form.url.value = videoUrl.href();
} else window.close();
