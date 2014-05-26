#pragma once
#include <windows.h>
#include "..\MainWindow.h"

class WindowsMainWindow: public MainWindow
{
public:
	WindowsMainWindow(HINSTANCE instance, int showCmd, int width, int height);
	~WindowsMainWindow();

	int Run(void);
private:
	LPCTSTR _wndClassName;
	HWND _hwnd;

	bool InitializeWindow(HINSTANCE instance, int showWnd, int width, int height, bool windowed);

	LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
	static LRESULT CALLBACK StaticWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
};