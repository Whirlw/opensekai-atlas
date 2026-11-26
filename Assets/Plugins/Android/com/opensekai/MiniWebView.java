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
import java.util.HashSet;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import com.unity3d.player.UnityPlayer;

/** No Activity/WebView references are retained. Download records survive process death. */
public final class MiniWebView {
    private static Context context() { return UnityPlayer.currentActivity.getApplicationContext(); }
    private static SharedPreferences records(Context c) { return c.getSharedPreferences("chart-downloads", Context.MODE_PRIVATE); }
    /** Storage hosts a site is known to hand its downloads to; reviewed per site. */
    static Set<String> extraHostsFor(String host) {
        Set<String> extra = new HashSet<>();
        String h = host == null ? "" : host.toLowerCase(Locale.ROOT);
        if (h.equals("opensekai.com") || h.endsWith(".opensekai.com")) extra.add("tzi998.oss-cn-shanghai.aliyuncs.com");
        return extra;
    }

    /** HTTPS on the default port to one of the hosts or a subdomain of one, without user info. */
    static boolean trusted(String url, Set<String> hosts) {
        if (url == null) return false;
        Uri uri = Uri.parse(url);
        String host = uri.getHost();
        if (!"https".equalsIgnoreCase(uri.getScheme()) || host == null || uri.getUserInfo() != null) return false;
        if (uri.getPort() != -1 && uri.getPort() != 443) return false;
        host = host.toLowerCase(Locale.ROOT);
        for (String allowed : hosts) {
            if (host.equals(allowed) || host.endsWith("." + allowed)) return true;
        }
        return false;
    }

    /** Opens a chart website; only its host (and known storage hosts) can be browsed. */
    public static void open(String url, String title) {
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            Intent intent = new Intent(activity, ChartBrowserActivity.class);
            intent.putExtra(ChartBrowserActivity.EXTRA_URL, url);
            intent.putExtra(ChartBrowserActivity.EXTRA_TITLE, title);
            activity.startActivity(intent);
        });
    }
    static void download(Activity activity, String url, String agent, String disposition, String mime, String referer,
                         Set<String> hosts, String chartZone, int chartLevel) throws Exception {
        // The page that started the download must be the site itself; the file may come from its
        // CDN over HTTPS. What arrives is checked as a chart package before it is imported.
        Uri target = Uri.parse(url);
        if (!"https".equalsIgnoreCase(target.getScheme()) || target.getUserInfo() != null || !trusted(referer, hosts))
            throw new SecurityException("仅支持本站页面发起的 HTTPS 下载；不支持 blob/data 链接。");
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
        // Cookies only go to the site's own hosts, never to a third-party CDN.
        String cookie = trusted(url, hosts) ? CookieManager.getInstance().getCookie(url) : null;
        if (cookie != null && !cookie.isEmpty()) request.addRequestHeader("Cookie", cookie);
        request.addRequestHeader("Referer", referer);
        Context c = activity.getApplicationContext();
        DownloadManager manager = (DownloadManager)c.getSystemService(Context.DOWNLOAD_SERVICE);
        JSONObject task = new JSONObject();
        task.put("path", file.getCanonicalPath());
        task.put("pageUrl", referer);
        task.put("downloadUrl", url);
        if (chartZone != null) {
            task.put("chartZone", chartZone);
            task.put("chartLevel", chartLevel);
        }
        long id = manager.enqueue(request);
        if (!records(c).edit().putString(Long.toString(id), task.toString()).commit()) {
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
            String saved = (String)record.getValue();
            JSONObject item = saved.startsWith("{") ? new JSONObject(saved) : new JSONObject();
            if (!item.has("path")) item.put("path", saved); // Downloads queued before source tracking.
            item.put("id", record.getKey());
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
