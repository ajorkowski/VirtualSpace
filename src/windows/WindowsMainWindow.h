#pragma once
#include <memory>
#include <windows.h>
#include "..\MainWindow.h"
#include "WindowsRenderer.h"

class WindowsMainWindow: public MainWindow
{
public:
	WindowsMainWindow(HINSTANCE instance, int showCmd, int width, int height);
	~WindowsMainWindow();

	int Run(void);
private:
	LPCTSTR m_wndClassName;
	HWND m_hwnd;
	int m_width;
	int m_height;

	WindowsRenderer* m_renderer;

	bool InitializeWindow(HINSTANCE instance, int showWnd, int width, int height);

	LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
	static LRESULT CALLBACK StaticWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
};