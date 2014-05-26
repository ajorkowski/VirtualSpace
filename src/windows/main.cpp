#include <memory>
#include <windows.h>
#include "..\Constants.h"
#include "WindowsMainWindow.h"

// Windows entry point...
int WINAPI WinMain(HINSTANCE hInstance,	HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd)
{
	std::unique_ptr<WindowsMainWindow> window(new WindowsMainWindow(hInstance, nShowCmd, GRAPHICS_WIDTH, GRAPHICS_HEIGHT));
	window->Run();
	return 0;
}