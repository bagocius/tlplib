﻿using com.tinylabproductions.TLPLib.Concurrent;
using System;
using com.tinylabproductions.TLPLib.Logger;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Android {
#if UNITY_ANDROID
  public static class AndroidView {
    private const string FLAG_HIDE_NAVIGATION = "SYSTEM_UI_FLAG_HIDE_NAVIGATION";
    private const string FLAG_STABLE_LAYOUT = "SYSTEM_UI_FLAG_LAYOUT_STABLE";
    private const string FLAG_IMMERSIVE_STICKY = "SYSTEM_UI_FLAG_IMMERSIVE_STICKY";
    private const string FLAG_FULLSCREEN = "SYSTEM_UI_FLAG_FULLSCREEN";

    private static readonly AndroidJavaClass view;

    static AndroidView() {
      if (Application.isEditor) return;
      view = new AndroidJavaClass("android.view.View");
    }

    /* If the layout is stable, screen space never changes, but navigation buttons just 
     * become a black stripe and never hides. */
    public static Future<bool> hideNavigationBar(bool stableLayout) {
      if (Application.platform != RuntimePlatform.Android) return Future.successful(false);

      Log.debug("Trying to hide android navigation bar.");
      var activity = AndroidActivity.current;
      var future = new FutureImpl<bool>("[AndroidView.hideNavigationBar]");
      activity.Call("runOnUiThread", new JavaRunnable(() => {
        try {
          var flags =
            view.GetStatic<int>(FLAG_HIDE_NAVIGATION) |
            view.GetStatic<int>(FLAG_IMMERSIVE_STICKY) |
            view.GetStatic<int>(FLAG_FULLSCREEN);
          if (stableLayout) flags |= view.GetStatic<int>(FLAG_STABLE_LAYOUT);
          var decor = activity.
            Call<AndroidJavaObject>("getWindow").
            Call<AndroidJavaObject>("getDecorView");
          decor.Call("setSystemUiVisibility", flags);
          Log.debug("Hiding android navigation bar succeeded.");
          future.completeSuccess(true);
        }
        catch (Exception e) {
          Log.debug(
            "Error while trying to hide navigation bar on android: " + e
          );
          future.completeSuccess(false);
        }
      }));

      return future;
    }
  }
#endif
}
