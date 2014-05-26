#include <windows.h>
#include <functional>
#include "WindowsMainWindow.h"
#include "WindowsRenderer.h"

WindowsMainWindow::WindowsMainWindow(HINSTANCE instance, int showCmd, int width, int height)
	: _wndClassName(L"testWindow"),
	_hwnd(NULL)
{
	InitializeWindow(instance, showCmd, width, height, true);
}

WindowsMainWindow::~WindowsMainWindow(void)
{
	if(_hwnd) {
		DestroyWindow(_hwnd);
		_hwnd = NULL;
	}
}

int WindowsMainWindow::Run(void)
{
	if(!_hwnd) { return 0; }

	MSG msg;
	ZeroMemory(&msg, sizeof(MSG));
	WindowsRenderer renderer;

	while(true)
    {
		if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
        {
			if (msg.message == WM_QUIT) break;

			TranslateMessage(&msg);	
            DispatchMessage(&msg);
		}
		else
		{
			renderer.Run();
		}
	}

	return msg.wParam;
}

bool WindowsMainWindow::InitializeWindow(HINSTANCE instance, int showCmd, int width, int height, bool windowed)
{
	WNDCLASSEX wc;
	wc.cbSize = sizeof(WNDCLASSEX);
	wc.style = CS_HREDRAW | CS_VREDRAW;
	wc.lpfnWndProc = &WindowsMainWindow::StaticWndProc;
	wc.cbClsExtra = NULL;
	wc.cbWndExtra = sizeof(WindowsMainWindow*);
	wc.hInstance = instance;
	wc.hIcon = LoadIcon(NULL, IDI_APPLICATION);
	wc.hIconSm = LoadIcon(NULL, IDI_APPLICATION);
	wc.hCursor = LoadCursor(NULL, IDC_ARROW);
	wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 2);
	wc.lpszMenuName = NULL;
	wc.lpszClassName = _wndClassName;

	if (!RegisterClassEx(&wc))
	{
		MessageBox(NULL, L"Error registering class", L"Error", MB_OK | MB_ICONERROR);
		return false;
	}

	_hwnd = CreateWindowEx(NULL, 
		_wndClassName, 
		L"Test App", 
		WS_OVERLAPPEDWINDOW, 
		CW_USEDEFAULT, 
		CW_USEDEFAULT, 
		width, 
		height, 
		NULL, 
		NULL, 
		instance,
		static_cast<LPVOID>(this));

	if(!_hwnd) 
	{
		MessageBox(NULL, L"Error creating window", L"Error", MB_OK | MB_ICONERROR);
		return false;
	}

	ShowWindow(_hwnd, showCmd);
	UpdateWindow(_hwnd);
	return true;
}

LRESULT CALLBACK WindowsMainWindow::WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
	switch(msg) {
	case WM_KEYDOWN:
		if(wParam == VK_ESCAPE) {
			if(MessageBox(0, L"Are you sure you want to exit?", L"Really?", MB_YESNO | MB_ICONQUESTION) == IDYES) {
				PostQuitMessage(0);
			}
		}
		return 0;
	case WM_DESTROY:
		PostQuitMessage(0);
		return 0;
	}

	return DefWindowProc(hWnd, msg, wParam, lParam);
}

LRESULT CALLBACK WindowsMainWindow::StaticWndProc( HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam )
{
    // Store instance pointer while handling the first message
    if ( uMsg == WM_NCCREATE )
    {
        CREATESTRUCT* pCS = reinterpret_cast<CREATESTRUCT*>(lParam);
        LPVOID pThis = pCS->lpCreateParams;
        SetWindowLongPtrW(hwnd, 0, reinterpret_cast<LONG_PTR>(pThis));
    }

    // At this point the instance pointer will always be available
    WindowsMainWindow* pWnd = reinterpret_cast<WindowsMainWindow*>(GetWindowLongPtrW(hwnd, 0));
    // see Note 1a below
	return pWnd->WndProc(hwnd, uMsg, wParam, lParam);
}