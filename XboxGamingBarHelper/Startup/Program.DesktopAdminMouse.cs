using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Input.Preview.Injection;
using XboxGamingBarHelper.Labs;

namespace XboxGamingBarHelper
{
    internal partial class Program
    {
        private static readonly Logger DesktopAdminMouseLogger = LogManager.GetCurrentClassLogger();

        private static CancellationTokenSource _desktopAdminMouseCts;
        private static int _desktopAdminMouseMode;
        private static int _desktopAdminMouseSens = 50;
        // Slider 50 == pre-rescale speed at 70 (70/50 = 1.4x baseline).
        private const float DesktopMouseSensDivisor = 50f * 50f / 70f;

        // Trackball-style cursor state (Steam-like: smooth ramp + sub-pixel carry + light coast).
        private static float _mouseVelX;
        private static float _mouseVelY;
        private static float _mouseAccumX;
        private static float _mouseAccumY;
        private static DateTime _lastDesktopMouseMoveUtc = DateTime.MinValue;
        private static readonly TimeSpan DesktopMousePriorityWindow = TimeSpan.FromMilliseconds(800);

        internal static void StartDesktopAdminMouse(int mode, int sensitivity)
        {
            StopDesktopAdminMouse();
            if (mode == 0) return;

            _desktopAdminMouseMode = mode;
            _desktopAdminMouseSens = Math.Max(10, Math.Min(100, sensitivity));
            ResetDesktopMouseMotionState();
            _desktopAdminMouseCts = new CancellationTokenSource();
            var token = _desktopAdminMouseCts.Token;
            Task.Run(() => DesktopAdminMouseLoop(token));
            DesktopAdminMouseLogger.Info($"Desktop admin mouse started (mode={mode}, sens={_desktopAdminMouseSens})");
        }

        internal static void StopDesktopAdminMouse()
        {
            if (_desktopAdminMouseCts == null) return;
            try
            {
                _desktopAdminMouseCts.Cancel();
                _desktopAdminMouseCts.Dispose();
            }
            catch { }
            finally
            {
                _desktopAdminMouseCts = null;
                ResetDesktopMouseMotionState();
            }
            DesktopAdminMouseLogger.Info("Desktop admin mouse stopped");
        }

        internal static bool IsDesktopAdminMouseRunning() => _desktopAdminMouseCts != null;

        /// <summary>
        /// True while the user is actively steering the desktop cursor (stick deflected,
        /// trackball still coasting, or a move happened very recently). Used to give RT
        /// left-click priority over widget tab-nav without losing tab cycling at rest.
        /// </summary>
        internal static bool IsDesktopMouseCursorPriorityActive()
        {
            if (!IsDesktopAdminMouseRunning()) return false;

            if (LegionButtonMonitor.TryGetLatestGamepadSample(out var sample))
            {
                const int stickDeadzone = 8000;
                short rawX, rawY;
                if (_desktopAdminMouseMode == 2)
                {
                    rawX = sample.RightStickX;
                    rawY = sample.RightStickY;
                }
                else if (_desktopAdminMouseMode == 1)
                {
                    rawX = sample.LeftStickX;
                    rawY = sample.LeftStickY;
                }
                else
                {
                    return false;
                }

                if (Math.Abs(rawX) > stickDeadzone || Math.Abs(rawY) > stickDeadzone)
                    return true;
            }

            const float coastVelThreshold = 0.45f;
            float velSq = _mouseVelX * _mouseVelX + _mouseVelY * _mouseVelY;
            if (velSq > coastVelThreshold * coastVelThreshold) return true;

            return (DateTime.UtcNow - _lastDesktopMouseMoveUtc) < DesktopMousePriorityWindow;
        }

        private static void MarkDesktopMouseMoved()
        {
            _lastDesktopMouseMoveUtc = DateTime.UtcNow;
        }

        private static void ResetDesktopMouseMotionState()
        {
            _mouseVelX = 0;
            _mouseVelY = 0;
            _mouseAccumX = 0;
            _mouseAccumY = 0;
            _lastDesktopMouseMoveUtc = DateTime.MinValue;
        }

        internal static void InjectMouseButtonClick(int mouseButton, bool down)
        {
            if (inputInjector == null) return;

            InjectedInputMouseOptions opts;
            switch (mouseButton)
            {
                case 1:
                    opts = down ? InjectedInputMouseOptions.RightDown : InjectedInputMouseOptions.RightUp;
                    break;
                case 2:
                    opts = down ? InjectedInputMouseOptions.MiddleDown : InjectedInputMouseOptions.MiddleUp;
                    break;
                default:
                    opts = down ? InjectedInputMouseOptions.LeftDown : InjectedInputMouseOptions.LeftUp;
                    break;
            }

            try
            {
                inputInjector.InjectMouseInput(new[]
                {
                    new InjectedInputMouseInfo { MouseOptions = opts }
                });
            }
            catch (Exception ex)
            {
                DesktopAdminMouseLogger.Debug($"Mouse button inject failed: {ex.Message}");
            }
        }

        private static async Task DesktopAdminMouseLoop(CancellationToken token)
        {
            const int intervalMs = 4;
            const int stickDeadzone = 8000;
            const int scrollDeadzone = 12000;
            const byte triggerScrollSuppressThreshold = 48;
            const ushort xinputLeftThumb = 0x0040;
            int cursorBoostTick = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (inputInjector == null
                        || legionManager == null
                        || !IsDesktopAdminMouseAllowed()
                        || _desktopAdminMouseMode == 0)
                    {
                        ResetDesktopMouseMotionState();
                        await Task.Delay(intervalMs, token);
                        continue;
                    }

                    if (!LegionButtonMonitor.TryGetLatestGamepadSample(out var sample))
                    {
                        ApplyDesktopMouseFriction();
                        await Task.Delay(intervalMs, token);
                        continue;
                    }

                    if (IsLegionLHoldMouseActive())
                    {
                        cursorBoostTick++;
                        if (cursorBoostTick % 30 == 0)
                            Windows.User32.BoostHoldMouseCursorVisibility(1);
                    }
                    else
                    {
                        cursorBoostTick = 0;
                    }

                    bool triggerHeld = sample.LeftTrigger >= triggerScrollSuppressThreshold
                        || sample.RightTrigger >= triggerScrollSuppressThreshold;
                    bool leftThumbHeld = (sample.Buttons & xinputLeftThumb) != 0;

                    if (_desktopAdminMouseMode == 2)
                    {
                        InjectStickMouseTrackball(sample.RightStickX, sample.RightStickY, stickDeadzone);
                        if (!triggerHeld && !leftThumbHeld)
                            InjectStickScroll(sample.LeftStickX, sample.LeftStickY, scrollDeadzone);
                    }
                    else if (_desktopAdminMouseMode == 1)
                    {
                        InjectStickMouseTrackball(sample.LeftStickX, sample.LeftStickY, stickDeadzone);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DesktopAdminMouseLogger.Debug($"Desktop admin mouse loop error: {ex.Message}");
                }

                await Task.Delay(intervalMs, token);
            }
        }

        /// <summary>
        /// Steam-like trackball cursor: radial deadzone, smooth ramp, sub-pixel carry,
        /// and light friction when the stick returns to center. Still uses elevated InputInjector.
        /// </summary>
        private static void InjectStickMouseTrackball(short rawX, short rawY, int deadzone)
        {
            const float maxDeflection = 32768f;
            const float followRate = 0.38f;
            const float friction = 0.84f;
            const float stopThreshold = 0.015f;

            float nx = rawX / maxDeflection;
            float ny = -rawY / maxDeflection;
            float mag = (float)Math.Sqrt(nx * nx + ny * ny);
            float deadzoneNorm = deadzone / maxDeflection;

            bool stickActive = mag > deadzoneNorm;
            if (stickActive)
            {
                float scaled = (mag - deadzoneNorm) / (1f - deadzoneNorm);
                if (scaled > 1f) scaled = 1f;
                scaled = scaled * scaled * (3f - 2f * scaled);
                float scale = scaled / mag;
                nx *= scale;
                ny *= scale;

                float sens = _desktopAdminMouseSens / DesktopMouseSensDivisor;
                float maxVel = 11f * sens;
                float targetVx = nx * maxVel;
                float targetVy = ny * maxVel;
                _mouseVelX += (targetVx - _mouseVelX) * followRate;
                _mouseVelY += (targetVy - _mouseVelY) * followRate;
            }
            else
            {
                ApplyDesktopMouseFriction(friction, stopThreshold);
            }

            _mouseAccumX += _mouseVelX;
            _mouseAccumY += _mouseVelY;

            int dx = (int)_mouseAccumX;
            int dy = (int)_mouseAccumY;
            _mouseAccumX -= dx;
            _mouseAccumY -= dy;

            if (dx == 0 && dy == 0) return;

            MarkDesktopMouseMoved();

            try
            {
                inputInjector.InjectMouseInput(new[]
                {
                    new InjectedInputMouseInfo
                    {
                        DeltaX = dx,
                        DeltaY = dy,
                        MouseOptions = InjectedInputMouseOptions.Move,
                    }
                });
            }
            catch (Exception ex)
            {
                DesktopAdminMouseLogger.Debug($"Stick mouse inject failed: {ex.Message}");
            }
        }

        private static void ApplyDesktopMouseFriction(
            float friction = 0.84f,
            float stopThreshold = 0.015f)
        {
            _mouseVelX *= friction;
            _mouseVelY *= friction;
            if (Math.Abs(_mouseVelX) < stopThreshold) _mouseVelX = 0;
            if (Math.Abs(_mouseVelY) < stopThreshold) _mouseVelY = 0;

            if (_mouseVelX == 0 && _mouseVelY == 0) return;

            _mouseAccumX += _mouseVelX;
            _mouseAccumY += _mouseVelY;

            int dx = (int)_mouseAccumX;
            int dy = (int)_mouseAccumY;
            _mouseAccumX -= dx;
            _mouseAccumY -= dy;

            if ((dx == 0 && dy == 0) || inputInjector == null) return;

            MarkDesktopMouseMoved();

            try
            {
                inputInjector.InjectMouseInput(new[]
                {
                    new InjectedInputMouseInfo
                    {
                        DeltaX = dx,
                        DeltaY = dy,
                        MouseOptions = InjectedInputMouseOptions.Move,
                    }
                });
            }
            catch (Exception ex)
            {
                DesktopAdminMouseLogger.Debug($"Stick mouse friction inject failed: {ex.Message}");
            }
        }

        private static void InjectStickScroll(short x, short y, int deadzone)
        {
            if (Math.Abs(x) < deadzone) x = 0;
            if (Math.Abs(y) < deadzone) y = 0;
            if (x == 0 && y == 0) return;

            try
            {
                if (Math.Abs(y) >= Math.Abs(x) && y != 0)
                {
                    int delta = (int)(y / 32768f * 240f);
                    if (Math.Abs(delta) < 30) return;
                    inputInjector.InjectMouseInput(new[]
                    {
                        new InjectedInputMouseInfo
                        {
                            MouseOptions = InjectedInputMouseOptions.Wheel,
                            MouseData = unchecked((uint)delta),
                        }
                    });
                }
                else if (x != 0)
                {
                    int delta = (int)(x / 32768f * 240f);
                    if (Math.Abs(delta) < 30) return;
                    inputInjector.InjectMouseInput(new[]
                    {
                        new InjectedInputMouseInfo
                        {
                            MouseOptions = InjectedInputMouseOptions.HWheel,
                            MouseData = unchecked((uint)delta),
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                DesktopAdminMouseLogger.Debug($"Stick scroll inject failed: {ex.Message}");
            }
        }
    }
}
