#include "WindowsRenderer.h"
#include "..\Constants.h"

WindowsRenderer::WindowsRenderer() : 
	m_d3dDriver(NULL)
{
}

WindowsRenderer::~WindowsRenderer()
{
	if (m_d3dDriver) 
	{
		delete m_d3dDriver;
		m_d3dDriver = NULL;
	}
}

bool WindowsRenderer::Initialise(int width, int height, HWND hwnd)
{
	m_d3dDriver = new D3DDriver();
	if (!m_d3dDriver)
	{
		return false;
	}

	// Initialize the Direct3D object.
	bool result = m_d3dDriver->Initialise(width, height, hwnd, GRAPHICS_FULL_SCREEN);
	if (!result)
	{
		MessageBox(hwnd, L"Could not initialize Direct3D", L"Error", MB_OK);
		return false;
	}

	return true;
}

bool WindowsRenderer::Frame()
{
	bool result;

	// Render the graphics scene.
	result = Render();
	if (!result)
	{
		return false;
	}

	return true;
}

bool WindowsRenderer::Render()
{
	// Clear the buffers to begin the scene.
	m_d3dDriver->BeginScene(0, 0, 0.5f, 1.0f);


	// Present the rendered scene to the screen.
	m_d3dDriver->EndScene();

	return true;
}