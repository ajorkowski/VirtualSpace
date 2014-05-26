#pragma once

#include <memory>
#include "D3DDriver.h"

class WindowsRenderer
{
public:
	WindowsRenderer();
	~WindowsRenderer();

	bool Initialise(int width, int height, HWND hwnd);

	bool Frame();

private:
	D3DDriver* m_d3dDriver;

	bool Render();
};