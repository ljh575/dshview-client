using System;

namespace DshView
{
    // Injected into every document (AddScriptToExecuteOnDocumentCreated) so the
    // page tells us when something we mirror changes -- instead of us polling or
    // waiting for a navigation.
    //
    // Why this exists: the dsh web shell is a SPA. Switching the UI language (or
    // the theme) inside it only mutates <html>/<body>, so NavigationCompleted
    // never fires and a navigation-only probe would keep the stale language
    // until the user reloaded by hand. @deepseek-ai/dsh-client-locale mirrors the
    // active locale onto document.documentElement.lang, and the theme plugin
    // mirrors the palette onto html/body attributes, so watching attributes is
    // enough and stays cheap: streaming chat output mutates the body subtree,
    // which is deliberately NOT observed.
    internal static class PageObserver
    {
        /// <summary>Message the script posts; compared verbatim on the host side.</summary>
        public const string Message = "dshview:changed";

        /// <summary>Installs the observer once per document and debounces notifications.</summary>
        public const string Script = @"
(function(){
  try {
    if (window.__dshviewWatch) return;
    var pending = false;
    function notify(){
      if (pending) return;
      pending = true;
      setTimeout(function(){
        pending = false;
        try { window.chrome.webview.postMessage('" + Message + @"'); } catch (e) {}
      }, 150);
    }
    function install(){
      try {
        if (window.__dshviewWatch) return;
        var mo = new MutationObserver(notify);
        var root = document.documentElement;
        if (root) {
          mo.observe(root, { attributes: true,
            attributeFilter: ['lang', 'class', 'style', 'data-theme', 'data-color-scheme'] });
        }
        if (document.body) {
          mo.observe(document.body, { attributes: true,
            attributeFilter: ['class', 'style', 'data-theme', 'data-color-scheme'] });
        }
        try {
          var mq = window.matchMedia('(prefers-color-scheme: dark)');
          if (mq.addEventListener) mq.addEventListener('change', notify);
        } catch (e) {}
        window.__dshviewWatch = true;
        notify();
      } catch (e) {}
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', install, { once: true });
    else install();
  } catch (e) {}
})();
";
    }
}
