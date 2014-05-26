#include <windows.h>
#include "WindowsMainWindow.h"

// Windows entry point...
int WINAPI WinMain(HINSTANCE hInstance,	HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd)
{
	WindowsMainWindow window(hInstance, nShowCmd, 800, 600);
	window.Run();
	return 0;
}