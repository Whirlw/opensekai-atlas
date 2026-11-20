package com.opensekai;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.*;
import android.widget.*;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;

/**
 * A chart website in the system WebView, in its own Activity. The bar on top matches the game:
 * a back button (the page's history first, then back to the game), the site's name and what is
 * happening, reload and close. Only the site's own host (and its subdomains, plus the storage
 * hosts the built-in site is known to use) can be navigated to; downloads go to DownloadManager.
 */
public final class ChartBrowserActivity extends Activity {
    static final String EXTRA_URL = "com.opensekai.url";
    static final String EXTRA_TITLE = "com.opensekai.title";
    private static final int GROUND = Color.rgb(12, 15, 20);
    private static final int BAR = Color.rgb(21, 26, 34);
    private static final int TEXT = Color.rgb(242, 244, 247);
    private static final int MUTED = Color.rgb(154, 166, 181);
    private static final int ACCENT = Color.rgb(157, 184, 255);

    private WebView web;
    private TextView title;
    private TextView status;
    private ProgressBar progress;
    private final Set<String> hosts = new HashSet<>();
    private String startUrl;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        Intent intent = getIntent();
        startUrl = intent.getStringExtra(EXTRA_URL);
        if (startUrl == null || startUrl.isEmpty()) { finish(); return; }
        String name = intent.getStringExtra(EXTRA_TITLE);
        Uri start = Uri.parse(startUrl);
        if (!"https".equalsIgnoreCase(start.getScheme()) || start.getHost() == null) { finish(); return; }
        hosts.add(start.getHost().toLowerCase(Locale.ROOT));
        hosts.addAll(MiniWebView.extraHostsFor(start.getHost()));

        getWindow().setStatusBarColor(BAR);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(GROUND);
        root.setFitsSystemWindows(true);
        setContentView(root);

        // ---- bar: back · title / status · reload · close
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setBackgroundColor(BAR);
        bar.setPadding(dp(8), 0, dp(8), 0);
        root.addView(bar, new LinearLayout.LayoutParams(-1, dp(60)));

        ImageView back = barButton(BarIcon.BACK, "返回");
        back.setOnClickListener(v -> goBack());
        bar.addView(back, new LinearLayout.LayoutParams(dp(48), dp(48)));

        LinearLayout labels = new LinearLayout(this);
        labels.setOrientation(LinearLayout.VERTICAL);
        labels.setPadding(dp(8), 0, dp(8), 0);
        title = new TextView(this);
        title.setText(TextUtils.isEmpty(name) ? start.getHost() : name);
        title.setTextColor(TEXT);
        title.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setSingleLine(true);
        title.setEllipsize(TextUtils.TruncateAt.END);
        labels.addView(title);
        status = new TextView(this);
        status.setText("选择谱面下载后，返回游戏即自动导入");
        status.setTextColor(MUTED);
        status.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        status.setSingleLine(true);
        status.setEllipsize(TextUtils.TruncateAt.END);
        labels.addView(status);
        bar.addView(labels, new LinearLayout.LayoutParams(0, -2, 1));

        ImageView reload = barButton(BarIcon.REFRESH, "刷新");
        reload.setOnClickListener(v -> { if (web != null) web.reload(); });
        bar.addView(reload, new LinearLayout.LayoutParams(dp(48), dp(48)));

        ImageView close = barButton(BarIcon.CLOSE, "返回游戏");
        close.setOnClickListener(v -> finish());
        bar.addView(close, new LinearLayout.LayoutParams(dp(48), dp(48)));

        progress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        progress.setMax(100);
        progress.setProgressTintList(android.content.res.ColorStateList.valueOf(ACCENT));
        progress.setProgressBackgroundTintList(android.content.res.ColorStateList.valueOf(BAR));
        root.addView(progress, new LinearLayout.LayoutParams(-1, dp(3)));

        web = new WebView(this);
        web.setBackgroundColor(GROUND);
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
            @Override public void onPageFinished(WebView view, String url) {
                CookieManager.getInstance().flush();
                progress.setVisibility(View.INVISIBLE);
            }
            @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request.isForMainFrame()) status.setText("网页加载失败，请检查网络后点刷新");
            }
        });
        web.setWebChromeClient(new WebChromeClient() {
            @Override public void onProgressChanged(WebView view, int value) {
                progress.setVisibility(value >= 100 ? View.INVISIBLE : View.VISIBLE);
                progress.setProgress(value);
            }
        });
        web.setDownloadListener((url, agent, disposition, mime, size) -> {
            try {
                MiniWebView.download(this, url, agent, disposition, mime, web.getUrl(), hosts);
                status.setText("已开始下载，返回游戏后自动导入");
                Toast.makeText(this, "下载已开始，可继续选谱或返回游戏", Toast.LENGTH_LONG).show();
            } catch (Exception e) { status.setText(e.getMessage()); }
        });
        if (state == null || web.restoreState(state) == null) web.loadUrl(startUrl);
        if (android.os.Build.VERSION.SDK_INT >= 33)
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(0, () -> goBack());
    }

    private ImageView barButton(int icon, String description) {
        ImageView button = new ImageView(this);
        button.setImageDrawable(new BarIcon(icon, TEXT));
        button.setScaleType(ImageView.ScaleType.FIT_CENTER);
        // The icon is drawn on a 24-unit grid; 12 dp of padding leaves a 24 dp glyph in a 48 dp button.
        button.setPadding(dp(12), dp(12), dp(12), dp(12));
        button.setContentDescription(description);
        GradientDrawable shape = new GradientDrawable();
        shape.setShape(GradientDrawable.OVAL);
        shape.setColor(Color.argb(15, 255, 255, 255));
        button.setBackground(new android.graphics.drawable.RippleDrawable(
            android.content.res.ColorStateList.valueOf(Color.argb(60, 255, 255, 255)), shape, null));
        button.setClickable(true);
        button.setFocusable(true);
        return button;
    }

    /**
     * The bar's icons drawn as strokes on the same 24-unit grid as the game's own icons
     * (LibraryShapes), so they sit exactly in the middle of their round buttons. Text glyphs
     * do not: each font places arrows and symbols on its own baseline.
     */
    static final class BarIcon extends android.graphics.drawable.Drawable {
        static final int BACK = 0, REFRESH = 1, CLOSE = 2;
        private final int icon;
        private final android.graphics.Paint paint = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);

        BarIcon(int icon, int color) {
            this.icon = icon;
            paint.setColor(color);
            paint.setStyle(android.graphics.Paint.Style.STROKE);
            paint.setStrokeCap(android.graphics.Paint.Cap.ROUND);
            paint.setStrokeJoin(android.graphics.Paint.Join.ROUND);
        }

        @Override public void draw(android.graphics.Canvas canvas) {
            android.graphics.Rect bounds = getBounds();
            float size = Math.min(bounds.width(), bounds.height());
            float unit = size / 24f;
            canvas.save();
            canvas.translate(bounds.exactCenterX() - size / 2f, bounds.exactCenterY() - size / 2f);
            canvas.scale(unit, unit);
            paint.setStrokeWidth(2.1f);
            android.graphics.Path path = new android.graphics.Path();
            switch (icon) {
                case BACK:
                    path.moveTo(19, 12); path.lineTo(5, 12);
                    path.moveTo(11, 6); path.lineTo(5, 12); path.lineTo(11, 18);
                    break;
                case REFRESH:
                    // A circle of radius 8 open at the upper right, with the arrowhead on its end.
                    path.addArc(new android.graphics.RectF(4, 4, 20, 20), 0f, 312.5f);
                    path.moveTo(20, 4); path.lineTo(20, 9); path.lineTo(15, 9);
                    break;
                default:
                    path.moveTo(6.5f, 6.5f); path.lineTo(17.5f, 17.5f);
                    path.moveTo(17.5f, 6.5f); path.lineTo(6.5f, 17.5f);
                    break;
            }
            canvas.drawPath(path, paint);
            canvas.restore();
        }

        @Override public int getIntrinsicWidth() { return 96; }
        @Override public int getIntrinsicHeight() { return 96; }
        @Override public void setAlpha(int alpha) { paint.setAlpha(alpha); invalidateSelf(); }
        @Override public void setColorFilter(android.graphics.ColorFilter filter) { paint.setColorFilter(filter); invalidateSelf(); }
        @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    }

    private int dp(int value) {
        return Math.round(TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, value, getResources().getDisplayMetrics()));
    }

    private boolean block(String url) {
        if (MiniWebView.trusted(url, hosts)) return false;
        status.setText("已拦截站外链接：仅允许 " + title.getText() + " 的 HTTPS 网页");
        return true;
    }

    /** The page's own history first; at its first page, back to the game. */
    private void goBack() { if (web != null && web.canGoBack()) web.goBack(); else finish(); }
    @Override public void onBackPressed() { goBack(); }
    @Override protected void onSaveInstanceState(Bundle state) { if (web != null) web.saveState(state); super.onSaveInstanceState(state); }
    @Override protected void onPause() { if (web != null) web.onPause(); CookieManager.getInstance().flush(); super.onPause(); }
    @Override protected void onResume() { super.onResume(); if (web != null) web.onResume(); }
    @Override protected void onDestroy() {
        if (web != null) {
            ((ViewGroup)web.getParent()).removeView(web); web.stopLoading();
            web.setDownloadListener(null); web.setWebViewClient(new WebViewClient()); web.setWebChromeClient(null);
            web.loadUrl("about:blank"); web.removeAllViews(); web.destroy(); web = null;
        }
        super.onDestroy();
    }
}
