var form = window.document.forms[0];
form.onsubmit = function() { return false; }

var areaSubLangs = $('#areaSubLangs');
chrome.runtime.onMessage.addListener(
  function(msg, sender, sendResponse) {
    if('subLangs' in msg) {
      var subLangs = msg.subLangs;
      if(subLangs.length > 0) {
        for(var i = 0; i < subLangs.length; i++)
          $('<label><input type="checkbox" name="availSubLang" value="' + subLangs[i] + '" />' + subLangs[i] + '</label><br />')
            .appendTo(areaSubLangs);
      } else $('<span>No subtitle available.</span>').appendTo(areaSubLangs);
    } else if(msg.unsupported) {
      window.alert('This URL is not supported.')
      window.close();
    } else if('errMsg' in msg) {
      var pre = $('<pre></pre>');
      pre.text(msg.errMsg).appendTo(areaSubLangs);
    }
    areaSubLangs.css("border", "1px dotted silver");
  }
);

var makeMsg = function() {
  return {
    nativeMsg: {
      url: form.url.value
      , audioOnly: form.audioOnly.checked
      , writeSub: form.writeSub.checked
      , subLang: form.subLang.value
      , noPlaylist: form.noPlaylist.checked
      , verbose: true
    }
  };
}

form.btnStart.onclick = function() {
  areaSubLangs.find('input:checkbox:checked').each(function() {
    form.subLang.value += "," + this.value;
    form.writeSub.checked = true;
  });
  var splitted = form.subLang.value.split(',');
  var subLangs = [];
  for(var i = 0; i < splitted.length; i++) {
    var value = splitted[i].trim();
    if(value != '' && subLangs.indexOf(value) == -1) subLangs.push(value);
  }
  form.subLang.value = subLangs.join();
  localStorage.setItem('subLang', form.subLang.value);
  chrome.runtime.sendMessage(makeMsg());
};

form.btnListSubLangs.onclick = function() {
  var msg = makeMsg();
  msg.nativeMsg.command = "getSubLangs";
  chrome.runtime.sendMessage(msg);
  this.disabled = true;
}

form.btnSetDefault.onclick = function() {
  localStorage.setItem('writeSub', JSON.stringify(form.writeSub.checked));
}

if(window.location.search.length > 1) {
  var videoUrl = URI(decodeURIComponent(window.location.search.substring(1)));
  if(!(videoUrl.is('absolute') && videoUrl.is('url'))) {
    window.alert('Invalid URL: ' + videoUrl.href());
    window.close();
  }
  else {
    form.url.value = videoUrl.href();
    if(JSON.parse(localStorage.getItem('writeSub'))) form.writeSub.checked = true;
    form.subLang.value = localStorage.getItem('subLang');
    if(form.subLang.value == null) form.subLang.value = '';
  }
} else window.close();