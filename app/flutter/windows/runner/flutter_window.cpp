#include "flutter_window.h"

#include <optional>

#include "flutter/generated_plugin_registrant.h"

FlutterWindow::FlutterWindow(const flutter::DartProject& project)
    : project_(project) {}

FlutterWindow::~FlutterWindow() {}

bool FlutterWindow::OnCreate() {
  if (!Win32Window::OnCreate()) {
    return false;
  }

  RECT frame = GetClientArea();

  // The size here must match the window dimensions to avoid unnecessary surface
  // creation / destruction in the startup path.
  flutter_controller_ = std::make_unique<flutter::FlutterViewController>(
      frame.right - frame.left, frame.bottom - frame.top, project_);
  // Ensure that basic setup of the controller was successful.
  if (!flutter_controller_->engine() || !flutter_controller_->view()) {
    return false;
  }
  RegisterPlugins(flutter_controller_->engine());
  SetChildContent(flutter_controller_->view()->GetNativeWindow());

  window_channel_ = std::make_unique<flutter::MethodChannel<flutter::EncodableValue>>(
      flutter_controller_->engine()->messenger(), "mini32/window",
      &flutter::StandardMethodCodec::GetInstance());
  window_channel_->SetMethodCallHandler(
      [this](const flutter::MethodCall<flutter::EncodableValue>& call,
             std::unique_ptr<flutter::MethodResult<flutter::EncodableValue>> result) {
        if (call.method_name() == "background") {
          const auto* enabled = call.arguments() ? std::get_if<bool>(call.arguments()) : nullptr;
          background_enabled_ = enabled && *enabled;
          result->Success();
        } else if (call.method_name() == "show") {
          ::ShowWindow(GetHandle(), SW_RESTORE);
          ::SetForegroundWindow(GetHandle());
          result->Success();
        } else if (call.method_name() == "minimize") {
          ::ShowWindow(GetHandle(), SW_MINIMIZE);
          result->Success();
        } else if (call.method_name() == "isMaximized") {
          result->Success(::IsZoomed(GetHandle()) != FALSE);
        } else if (call.method_name() == "toggleMaximize") {
          ::ShowWindow(GetHandle(), ::IsZoomed(GetHandle()) ? SW_RESTORE : SW_MAXIMIZE);
          result->Success(::IsZoomed(GetHandle()) != FALSE);
        } else if (call.method_name() == "drag") {
          if (::IsZoomed(GetHandle())) ::ShowWindow(GetHandle(), SW_RESTORE);
          ::ReleaseCapture();
          ::SendMessage(GetHandle(), WM_NCLBUTTONDOWN, HTCAPTION, 0);
          result->Success(::IsZoomed(GetHandle()) != FALSE);
        } else if (call.method_name() == "closeWindow") {
          result->Success();
          ::PostMessage(GetHandle(), WM_CLOSE, 0, 0);
        } else if (call.method_name() == "exit") {
          allow_exit_ = true;
          result->Success();
          ::PostMessage(GetHandle(), WM_CLOSE, 0, 0);
        } else {
          result->NotImplemented();
        }
      });

  flutter_controller_->engine()->SetNextFrameCallback([&]() {
    if (std::wstring(::GetCommandLineW()).find(L"--smoke-test") != std::wstring::npos) {
      return;
    }
    this->Show();
    RECT work_area{};
    if (::SystemParametersInfo(SPI_GETWORKAREA, 0, &work_area, 0)) {
      RECT window_area{};
      ::GetWindowRect(GetHandle(), &window_area);
      if (window_area.right > work_area.right || window_area.bottom > work_area.bottom) {
        ::ShowWindow(GetHandle(), SW_MAXIMIZE);
      }
    }
  });

  // Flutter can complete the first frame before the "show window" callback is
  // registered. The following call ensures a frame is pending to ensure the
  // window is shown. It is a no-op if the first frame hasn't completed yet.
  flutter_controller_->ForceRedraw();

  return true;
}

void FlutterWindow::OnDestroy() {
  window_channel_ = nullptr;
  if (flutter_controller_) {
    flutter_controller_ = nullptr;
  }

  Win32Window::OnDestroy();
}

LRESULT
FlutterWindow::MessageHandler(HWND hwnd, UINT const message,
                              WPARAM const wparam,
                              LPARAM const lparam) noexcept {
  if (message == WM_CLOSE && !allow_exit_ && background_enabled_) {
    ::ShowWindow(hwnd, SW_HIDE);
    return 0;
  }
  if (message == WM_GETMINMAXINFO) {
    auto* info = reinterpret_cast<MINMAXINFO*>(lparam);
    const UINT dpi = ::GetDpiForWindow(hwnd);
    info->ptMinTrackSize.x = MulDiv(780, static_cast<int>(dpi), 96);
    info->ptMinTrackSize.y = MulDiv(680, static_cast<int>(dpi), 96);
    // Captionless windows otherwise maximize beneath the taskbar.
    MONITORINFO monitor_info{sizeof(MONITORINFO)};
    if (::GetMonitorInfo(::MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST),
                         &monitor_info)) {
      const RECT& work = monitor_info.rcWork;
      const RECT& screen = monitor_info.rcMonitor;
      info->ptMaxPosition.x = work.left - screen.left;
      info->ptMaxPosition.y = work.top - screen.top;
      info->ptMaxSize.x = work.right - work.left;
      info->ptMaxSize.y = work.bottom - work.top;
    }
    return 0;
  }
  // Give Flutter, including plugins, an opportunity to handle window messages.
  if (flutter_controller_) {
    std::optional<LRESULT> result =
        flutter_controller_->HandleTopLevelWindowProc(hwnd, message, wparam,
                                                      lparam);
    if (result) {
      return *result;
    }
  }

  switch (message) {
    case WM_FONTCHANGE:
      flutter_controller_->engine()->ReloadSystemFonts();
      break;
  }

  return Win32Window::MessageHandler(hwnd, message, wparam, lparam);
}
