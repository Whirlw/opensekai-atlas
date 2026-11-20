package com.opensekai;

import android.content.Intent;
import android.view.MotionEvent;
import com.unity3d.player.UnityPlayerActivity;

public class CustomUnityPlayerActivity extends UnityPlayerActivity {
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (!ShareExportHelper.OnActivityResult(requestCode, resultCode, data)) {
            super.onActivityResult(requestCode, resultCode, data);
        }
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent event) {
        // Diagnostic copy only; Unity still receives every event through the normal dispatch.
        TouchProbe.record(event);
        return super.dispatchTouchEvent(event);
    }
}
