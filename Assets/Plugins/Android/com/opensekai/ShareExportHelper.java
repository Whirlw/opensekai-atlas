package com.opensekai;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Intent;
import android.net.Uri;
import android.provider.MediaStore;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

public class ShareExportHelper {
    private static final int REQUEST_CODE_SAVE = 2001;
    private static final int REQUEST_CODE_OPEN = 2002;
    public static void SaveAndShare(String sourcePath, String callbackObject, String callbackMethod) {
        pick(REQUEST_CODE_SAVE, sourcePath, callbackObject, callbackMethod, Long.MAX_VALUE);
    }

    public static void OpenFile(String callbackObject, String callbackMethod) {
        pick(REQUEST_CODE_OPEN, null, callbackObject, callbackMethod, Long.MAX_VALUE);
    }

    public static void OpenPlayerBackup(String callbackObject, String callbackMethod) {
        pick(REQUEST_CODE_OPEN, null, callbackObject, callbackMethod, 65L * 1024 * 1024);
    }

    private static Request pending;

    private static class Request {
        final int code;
        final String source, object, method;
        final long limit;

        Request(int code, String source, String object, String method, long limit) {
            this.code = code;
            this.source = source;
            this.object = object;
            this.method = method;
            this.limit = limit;
        }

        void finish(String result) {
            UnityPlayer.UnitySendMessage(object, method, result);
        }
    }

    private static void pick(int code, String source, String object, String method, long limit) {
        Activity activity = UnityPlayer.currentActivity;
        Request request = new Request(code, source, object, method, limit);
        activity.runOnUiThread(() -> {
            if (pending != null) {
                request.finish("error:File picker is busy");
                return;
            }
            pending = request;
            try {
                Intent intent = new Intent(code == REQUEST_CODE_SAVE ? Intent.ACTION_CREATE_DOCUMENT : Intent.ACTION_OPEN_DOCUMENT);
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                intent.setType("application/zip");
                if (source != null) intent.putExtra(Intent.EXTRA_TITLE, new File(source).getName());
                activity.startActivityForResult(intent, code);
            } catch (Exception e) {
                pending = null;
                request.finish("error:" + e.getMessage());
            }
        });
    }

    public static boolean OnActivityResult(int code, int result, Intent data) {
        Request request = pending;
        if (request == null || request.code != code) return false;
        pending = null;
        if (result == Activity.RESULT_CANCELED) {
            request.finish("cancelled");
            return true;
        }
        Uri uri = data == null ? null : data.getData();
        if (result != Activity.RESULT_OK || uri == null) {
            request.finish("error:No document returned");
            return true;
        }
        Activity activity = UnityPlayer.currentActivity;
        new Thread(() -> {
            File temporary = null;
            try {
                ContentResolver resolver = activity.getContentResolver();
                if (code == REQUEST_CODE_SAVE) {
                    try (InputStream input = new FileInputStream(request.source);
                         OutputStream output = resolver.openOutputStream(uri, "wt")) {
                        copy(input, output, request.limit);
                    }
                    request.finish("success");
                } else {
                    temporary = File.createTempFile("opensekai-import-", ".zip", activity.getCacheDir());
                    try (InputStream input = resolver.openInputStream(uri);
                         OutputStream output = new FileOutputStream(temporary)) {
                        copy(input, output, request.limit);
                    }
                    request.finish("success:" + temporary.getAbsolutePath());
                }
            } catch (Exception e) {
                if (temporary != null) temporary.delete();
                request.finish("error:" + e.getMessage());
            }
        }, "OpenSekaiDocumentCopy").start();
        return true;
    }

    private static void copy(InputStream input, OutputStream output, long limit) throws java.io.IOException {
        if (input == null || output == null) throw new java.io.IOException("Cannot open document");
        byte[] buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = input.read(buffer)) != -1) {
            total += read;
            if (total > limit) throw new java.io.IOException("Document exceeds size limit");
            output.write(buffer, 0, read);
        }
    }

    public static void ShareFile(String sourcePath) {
        Activity activity = UnityPlayer.currentActivity;
        File sourceFile = new File(sourcePath);
        try {
            String fileName = sourceFile.getName();

            // Use MediaStore to create a shareable URI
            ContentValues values = new ContentValues();
            values.put(MediaStore.Downloads.DISPLAY_NAME, fileName);
            values.put(MediaStore.Downloads.MIME_TYPE, "application/zip");
            values.put(MediaStore.Downloads.IS_PENDING, 1);

            Uri collection = MediaStore.Downloads.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY);
            Uri destUri = activity.getContentResolver().insert(collection, values);

            if (destUri == null) {
                // Fallback: try cache dir with file provider approach
                ShareFileViaCache(sourceFile, activity);
                return;
            }

            // Copy file to MediaStore
            OutputStream outputStream = activity.getContentResolver().openOutputStream(destUri);
            if (outputStream == null) {
                activity.getContentResolver().delete(destUri, null, null);
                ShareFileViaCache(sourceFile, activity);
                return;
            }

            FileInputStream inputStream = new FileInputStream(sourceFile);
            byte[] buffer = new byte[8192];
            int length;
            while ((length = inputStream.read(buffer)) > 0) {
                outputStream.write(buffer, 0, length);
            }
            inputStream.close();
            outputStream.close();

            // Clear pending flag
            values.clear();
            values.put(MediaStore.Downloads.IS_PENDING, 0);
            activity.getContentResolver().update(destUri, values, null, null);

            // Share the file
            Intent shareIntent = new Intent(Intent.ACTION_SEND);
            shareIntent.setType("application/zip");
            shareIntent.putExtra(Intent.EXTRA_STREAM, destUri);
            shareIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.startActivity(Intent.createChooser(shareIntent, "分享谱面"));
        } catch (Exception e) {
            e.printStackTrace();
            // Fallback: try sharing via cache file
            try {
                ShareFileViaCache(sourceFile, activity);
            } catch (Exception ex) {
                ex.printStackTrace();
            }
        }
    }

    private static void ShareFileViaCache(File sourceFile, Activity activity) throws Exception {
        File cacheDir = activity.getExternalFilesDir(null);
        if (cacheDir == null) {
            cacheDir = activity.getCacheDir();
        }
        File shareFile = new File(cacheDir, sourceFile.getName());

        // Copy to cache
        FileInputStream input = new FileInputStream(sourceFile);
        OutputStream output = new FileOutputStream(shareFile);
        byte[] buffer = new byte[8192];
        int length;
        while ((length = input.read(buffer)) > 0) {
            output.write(buffer, 0, length);
        }
        input.close();
        output.close();

        shareFile.setReadable(true, false);

        Uri shareUri = Uri.fromFile(shareFile);
        Intent shareIntent = new Intent(Intent.ACTION_SEND);
        shareIntent.setType("application/zip");
        shareIntent.putExtra(Intent.EXTRA_STREAM, shareUri);
        shareIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.startActivity(Intent.createChooser(shareIntent, "分享谱面"));
    }

    /**
     * SubTask 8.5: 安卓端：保存视频到相册
     * 使用MediaStore API保存到指定相册目录
     *
     * @param sourcePath 源视频文件路径（Unity的临时文件路径）
     * @param filename   文件名（包含.mp4扩展名）
     * @param albumName  相册名称（如"OpenSekai_Rec"）
     */
    public static void saveVideoToGallery(String sourcePath, String filename, String albumName) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            android.util.Log.e("ShareExportHelper", "Activity is null, cannot save video");
            return;
        }

        File sourceFile = new File(sourcePath);
        if (!sourceFile.exists()) {
            android.util.Log.e("ShareExportHelper", "Source file does not exist: " + sourcePath);
            return;
        }

        ContentResolver resolver = activity.getContentResolver();

        try {
            // SubTask 8.4: 使用MediaStore.Video.Media.EXTERNAL_CONTENT_URI
            Uri videoCollection;
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                // Android 10+ (API 29+): 使用RELATIVE_PATH
                videoCollection = MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY);
            } else {
                // Android 9及以下：使用EXTERNAL_CONTENT_URI
                videoCollection = MediaStore.Video.Media.EXTERNAL_CONTENT_URI;
            }

            // 创建ContentValues设置视频元数据
            ContentValues videoValues = new ContentValues();

            // 设置文件名
            videoValues.put(MediaStore.Video.Media.DISPLAY_NAME, filename);

            // 设置MIME类型为视频MP4
            videoValues.put(MediaStore.Video.Media.MIME_TYPE, "video/mp4");

            // Android 10+ (API 29+): 设置相对路径到Movies或DCIM目录
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                // 优先使用Movies目录，更符合视频存储规范
                String relativePath = "Movies/" + albumName;
                videoValues.put(MediaStore.Video.Media.RELATIVE_PATH, relativePath);

                // 设置IS_PENDING标记，表示文件正在写入
                videoValues.put(MediaStore.Video.Media.IS_PENDING, 1);
            }

            // 使用ContentResolver.insert插入记录，获取视频URI
            Uri videoUri = resolver.insert(videoCollection, videoValues);

            if (videoUri == null) {
                android.util.Log.e("ShareExportHelper", "Failed to create video entry in MediaStore");
                return;
            }

            android.util.Log.d("ShareExportHelper", "Video URI created: " + videoUri.toString());

            // 使用ContentResolver.openOutputStream写入文件数据
            OutputStream outputStream = resolver.openOutputStream(videoUri);
            if (outputStream == null) {
                android.util.Log.e("ShareExportHelper", "Failed to open output stream for video URI");
                resolver.delete(videoUri, null, null);
                return;
            }

            // 从源文件读取数据并写入到MediaStore
            FileInputStream inputStream = new FileInputStream(sourceFile);
            byte[] buffer = new byte[8192];
            int bytesRead;
            long totalBytes = 0;

            while ((bytesRead = inputStream.read(buffer)) > 0) {
                outputStream.write(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }

            inputStream.close();
            outputStream.flush();
            outputStream.close();

            android.util.Log.d("ShareExportHelper", "Video saved successfully: " + totalBytes + " bytes");

            // Android 10+: 清除IS_PENDING标记，表示文件已完成
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                ContentValues updateValues = new ContentValues();
                updateValues.put(MediaStore.Video.Media.IS_PENDING, 0);
                resolver.update(videoUri, updateValues, null, null);
            }

            android.util.Log.i("ShareExportHelper", "Video saved to gallery: " + albumName + "/" + filename);

        } catch (Exception e) {
            android.util.Log.e("ShareExportHelper", "Error saving video to gallery: " + e.getMessage());
            e.printStackTrace();
        }
    }
}
