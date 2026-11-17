package com.opensekai;

import android.os.Build;
import android.os.SystemClock;
import android.view.MotionEvent;

/**
 * Diagnostic side channel for touch edges seen by the Activity. Gameplay input still goes through
 * Unity; this only copies DOWN / UP / CANCEL facts into preallocated arrays while a latency capture
 * is active. Unity drains the arrays once at the end of each capture segment.
 *
 * Clock: MotionEvent event time and System.nanoTime() both use CLOCK_MONOTONIC on Android, the same
 * base as SystemClock.uptimeMillis(). Unity maps this domain with sandwich anchors (see nowNanos).
 */
public final class TouchProbe {
    public static final int ACTION_DOWN = 0;
    public static final int ACTION_UP = 1;
    public static final int ACTION_CANCEL = 3;

    private static final Object lock = new Object();
    private static volatile boolean active;

    private static long[] eventNs = new long[0];
    private static long[] receiveNs = new long[0];
    private static int[] action = new int[0];
    private static int[] pointerId = new int[0];
    private static int[] pointerCount = new int[0];
    private static float[] x = new float[0];
    private static float[] y = new float[0];
    private static int count;
    private static int overflow;
    private static int moveEvents;
    private static int moveHistory;

    // Snapshot handed to Unity by drain(). Reading it does not race with new captures.
    private static long[] outEventNs = new long[0];
    private static long[] outReceiveNs = new long[0];
    private static int[] outAction = new int[0];
    private static int[] outPointerId = new int[0];
    private static int[] outPointerCount = new int[0];
    private static float[] outX = new float[0];
    private static float[] outY = new float[0];
    private static int outOverflow;
    private static int outMoveEvents;
    private static int outMoveHistory;

    private TouchProbe() {
    }

    public static long nowNanos() {
        return System.nanoTime();
    }

    /** uptimeMillis and nanoTime read back to back, used once per capture to confirm a shared base. */
    public static long uptimeMinusNanoTimeMicros() {
        long a = System.nanoTime();
        long up = SystemClock.uptimeMillis();
        long b = System.nanoTime();
        return up * 1000L - (a + (b - a) / 2) / 1000L;
    }

    public static boolean nanosEventTime() {
        return Build.VERSION.SDK_INT >= 34;
    }

    public static void start(int capacity) {
        synchronized (lock) {
            if (eventNs.length != capacity) {
                eventNs = new long[capacity];
                receiveNs = new long[capacity];
                action = new int[capacity];
                pointerId = new int[capacity];
                pointerCount = new int[capacity];
                x = new float[capacity];
                y = new float[capacity];
            }
            count = 0;
            overflow = 0;
            moveEvents = 0;
            moveHistory = 0;
            active = true;
        }
    }

    /** Stops recording and moves the rows to the drain snapshot. Returns the number of rows. */
    public static int drain() {
        synchronized (lock) {
            active = false;
            int n = count;
            outEventNs = java.util.Arrays.copyOf(eventNs, n);
            outReceiveNs = java.util.Arrays.copyOf(receiveNs, n);
            outAction = java.util.Arrays.copyOf(action, n);
            outPointerId = java.util.Arrays.copyOf(pointerId, n);
            outPointerCount = java.util.Arrays.copyOf(pointerCount, n);
            outX = java.util.Arrays.copyOf(x, n);
            outY = java.util.Arrays.copyOf(y, n);
            outOverflow = overflow;
            outMoveEvents = moveEvents;
            outMoveHistory = moveHistory;
            count = 0;
            return n;
        }
    }

    public static long[] drainedEventNs() { return outEventNs; }
    public static long[] drainedReceiveNs() { return outReceiveNs; }
    public static int[] drainedAction() { return outAction; }
    public static int[] drainedPointerId() { return outPointerId; }
    public static int[] drainedPointerCount() { return outPointerCount; }
    public static float[] drainedX() { return outX; }
    public static float[] drainedY() { return outY; }
    public static int drainedOverflow() { return outOverflow; }
    public static int drainedMoveEvents() { return outMoveEvents; }
    public static int drainedMoveHistory() { return outMoveHistory; }

    static void record(MotionEvent event) {
        if (!active) {
            return;
        }
        long received = System.nanoTime();
        int masked = event.getActionMasked();
        long eventTime = Build.VERSION.SDK_INT >= 34
                ? event.getEventTimeNanos()
                : event.getEventTime() * 1_000_000L;
        int pointers = event.getPointerCount();
        synchronized (lock) {
            if (!active) {
                return;
            }
            switch (masked) {
                case MotionEvent.ACTION_DOWN:
                case MotionEvent.ACTION_POINTER_DOWN:
                    add(event, eventTime, received, ACTION_DOWN, event.getActionIndex(), pointers);
                    break;
                case MotionEvent.ACTION_UP:
                case MotionEvent.ACTION_POINTER_UP:
                    add(event, eventTime, received, ACTION_UP, event.getActionIndex(), pointers);
                    break;
                case MotionEvent.ACTION_CANCEL:
                    for (int i = 0; i < pointers; i++) {
                        add(event, eventTime, received, ACTION_CANCEL, i, pointers);
                    }
                    break;
                case MotionEvent.ACTION_MOVE:
                    moveEvents++;
                    moveHistory += event.getHistorySize();
                    break;
                default:
                    break;
            }
        }
    }

    private static void add(MotionEvent event, long eventTime, long received, int kind, int index, int pointers) {
        if (count == eventNs.length) {
            overflow++;
            return;
        }
        eventNs[count] = eventTime;
        receiveNs[count] = received;
        action[count] = kind;
        pointerId[count] = event.getPointerId(index);
        pointerCount[count] = pointers;
        x[count] = event.getX(index);
        y[count] = event.getY(index);
        count++;
    }
}
