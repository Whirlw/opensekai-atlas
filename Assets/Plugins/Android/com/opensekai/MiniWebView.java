package com.opensekai;

import android.app.Activity;
import android.app.DownloadManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.webkit.CookieManager;
import android.webkit.URLUtil;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.File;
import java.util.Map;
import java.util.UUID;
import com.unity3d.player.UnityPlayer;

/** No Activity/WebView references are retained. Download records survive process death. */
public final class MiniWebView {
    private static Context context() { return UnityPlayer.currentActivity.getApplicationContext(); }
    private static SharedPreferences records(Context c) { return c.getSharedPreferences("chart-downloads", Context.MODE_PRIVATE); }
    static boolean trusted(String url) {
        Uri uri = Uri.parse(url);
        String host = uri.getHost();
        return "https".equalsIgnoreCase(uri.getScheme()) && host != null &&
            (host.equalsIgnoreCase("opensekai.com") || host.toLowerCase(java.util.Locale.ROOT).endsWith(".opensekai.com") ||
             host.equalsIgnoreCase("tzi998.oss-cn-shanghai.aliyuncs.com")) &&
            (uri.getPort() == -1 || uri.getPort() == 443) && uri.getUserInfo() == null;
    }
    public static void open() {
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> activity.startActivity(new Intent(activity, ChartBrowserActivity.class)));
    }
    static void download(Activity activity, String url, String agent, String disposition, String mime, String referer) throws Exception {
        if (!trusted(url) || !trusted(referer)) throw new SecurityException("仅支持 OpenSekai 的 HTTPS 下载；不支持 blob/data 链接。");
        String name = URLUtil.guessFileName(url, disposition, mime).replaceAll("[^a-zA-Z0-9._\\-\\p{L}\\p{N}]", "_");
        // Leave room for UUID and multi-byte UTF-8 names under common 255-byte filesystem limits.
        if (name.length() > 48) name = name.substring(name.length() - 48);
        // The ZIP signature is validated by Unity, including extensionless attachment endpoints.
        File appFiles = activity.getExternalFilesDir(null);
        if (appFiles == null) throw new java.io.IOException("应用下载存储不可用。");
        File directory = new File(appFiles, "Downloads");
        if (!directory.isDirectory() && !directory.mkdirs()) throw new java.io.IOException("无法创建下载目录。");
        File file = new File(directory, UUID.randomUUID().toString() + "_" + name);
        DownloadManager.Request request = new DownloadManager.Request(Uri.parse(url));
        request.setDestinationUri(Uri.fromFile(file));
        request.setTitle(name);
        request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE);
        request.setAllowedOverMetered(true);
        if (mime != null && !mime.isEmpty()) request.setMimeType(mime);
        if (agent != null && !agent.isEmpty()) request.addRequestHeader("User-Agent", agent);
        String cookie = CookieManager.getInstance().getCookie(url);
        if (cookie != null && !cookie.isEmpty()) request.addRequestHeader("Cookie", cookie);
        request.addRequestHeader("Referer", referer);
        Context c = activity.getApplicationContext();
        DownloadManager manager = (DownloadManager)c.getSystemService(Context.DOWNLOAD_SERVICE);
        long id = manager.enqueue(request);
        if (!records(c).edit().putString(Long.toString(id), file.getCanonicalPath()).commit()) {
            manager.remove(id);
            throw new java.io.IOException("无法保存下载任务。");
        }
    }
    public static String query() throws Exception {
        Context c = context();
        DownloadManager manager = (DownloadManager)c.getSystemService(Context.DOWNLOAD_SERVICE);
        JSONArray result = new JSONArray();
        for (Map.Entry<String, ?> record : records(c).getAll().entrySet()) {
            long id = Long.parseLong(record.getKey());
            JSONObject item = new JSONObject();
            item.put("id", record.getKey()); item.put("path", record.getValue());
            try (Cursor cursor = manager.query(new DownloadManager.Query().setFilterById(id))) {
                if (cursor != null && cursor.moveToFirst()) {
                    item.put("status", cursor.getInt(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_STATUS)));
                    item.put("bytes", cursor.getLong(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_BYTES_DOWNLOADED_SO_FAR)));
                    item.put("total", cursor.getLong(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_TOTAL_SIZE_BYTES)));
                    item.put("reason", cursor.getInt(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_REASON)));
                } else { item.put("status", DownloadManager.STATUS_FAILED); item.put("reason", "任务已被系统删除"); }
            }
            result.put(item);
        }
        return result.toString();
    }
    public static void acknowledge(String id) { records(context()).edit().remove(id).commit(); }
    public static void cancel(String id) {
        Context c = context();
        ((DownloadManager)c.getSystemService(Context.DOWNLOAD_SERVICE)).remove(Long.parseLong(id));
        acknowledge(id);
    }
}
