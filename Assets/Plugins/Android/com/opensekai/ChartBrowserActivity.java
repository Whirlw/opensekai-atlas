package com.opensekai;

import android.app.Activity;
import android.os.Bundle;
import android.graphics.Color;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.*;
import android.widget.*;

/** Separate Activity owns its WebView and Android back navigation. */
public final class ChartBrowserActivity extends Activity {
    private WebView web;
    private TextView status;
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Color.WHITE);
        root.setFitsSystemWindows(true);
        setContentView(root);
        LinearLayout bar = new LinearLayout(this);
        Button close = new Button(this); close.setText("关闭 · 返回游戏");
        close.setOnClickListener(v -> finish()); bar.addView(close);
        status = new TextView(this); status.setText("选择谱面下载后自动导入");
        status.setTextColor(Color.BLACK); bar.addView(status);
        root.addView(bar);
        web = new WebView(this);
        root.addView(web, new LinearLayout.LayoutParams(-1, 0, 1));
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true); settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(false); settings.setAllowContentAccess(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setSupportMultipleWindows(false);
        CookieManager.getInstance().setAcceptCookie(true);
        web.setWebViewClient(new WebViewClient() {
            @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                // Turnstile and other website-owned HTTPS iframes do not receive native privileges.
                if (!request.isForMainFrame()) return !"https".equalsIgnoreCase(request.getUrl().getScheme());
                return block(request.getUrl().toString());
            }
            @Override public boolean shouldOverrideUrlLoading(WebView view, String url) { return block(url); }
            @Override public void onPageFinished(WebView view, String url) { CookieManager.getInstance().flush(); }
            @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request.isForMainFrame()) status.setText("网页加载失败，请检查网络后重新打开");
            }
        });
        web.setDownloadListener((url, agent, disposition, mime, size) -> {
            try {
                MiniWebView.download(this, url, agent, disposition, mime, web.getUrl());
                status.setText("已开始下载，关闭网页后自动导入");
                Toast.makeText(this, "下载已开始，可继续选谱或返回游戏", Toast.LENGTH_LONG).show();
            } catch (Exception e) { status.setText(e.getMessage()); }
        });
        if (state == null || web.restoreState(state) == null) web.loadUrl("https://opensekai.com/");
        if (android.os.Build.VERSION.SDK_INT >= 33)
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(0, () -> goBack());
    }
    private boolean block(String url) {
        if (MiniWebView.trusted(url)) return false;
        status.setText("仅允许 OpenSekai HTTPS 网页和下载"); return true;
    }
    private void goBack() { if (web != null && web.canGoBack()) web.goBack(); else finish(); }
    @Override public void onBackPressed() { goBack(); }
    @Override protected void onSaveInstanceState(Bundle state) { if (web != null) web.saveState(state); super.onSaveInstanceState(state); }
    @Override protected void onPause() { if (web != null) web.onPause(); CookieManager.getInstance().flush(); super.onPause(); }
    @Override protected void onResume() { super.onResume(); if (web != null) web.onResume(); }
    @Override protected void onDestroy() {
        if (web != null) {
            ((ViewGroup)web.getParent()).removeView(web); web.stopLoading();
            web.setDownloadListener(null); web.setWebViewClient(new WebViewClient());
            web.loadUrl("about:blank"); web.removeAllViews(); web.destroy(); web = null;
        }
        super.onDestroy();
    }
}
