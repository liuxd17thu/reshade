/*
 * Copyright (C) 2026 AuroraShade contributors
 * SPDX-License-Identifier: BSD-3-Clause
 */

// Run against both GUI and headless DLLs. Uses a hidden window and D3D11 WARP;
// keyboard messages are posted only to the test window, never to the desktop.
#include <Windows.h>
#include <d3d11.h>
#include <wrl/client.h>
#include "reshade_api.hpp"
#include <chrono>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>

using Microsoft::WRL::ComPtr;
using reshade::api::effect_runtime;
namespace fs = std::filesystem;

static void require(bool condition, const char *message)
{
	if (!condition)
		throw std::runtime_error(message);
}

static void write_file(const fs::path &path, const std::string &text)
{
	std::ofstream stream(path, std::ios::binary);
	stream << text;
	require(stream.good(), "Could not write test fixture");
}

static std::string read_file(const fs::path &path)
{
	std::ifstream stream(path, std::ios::binary);
	return { std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>() };
}

static std::string shader(bool extra)
{
	return std::string(
		"#ifndef BIND_MODE\n#define BIND_MODE 0\n#endif\n"
		"#ifndef EXTRA\n#define EXTRA 0\n#endif\n"
		"uniform float Gain = 0.25;\n"
		"uniform bool Flag = false;\n"
		"uniform int Mode < ui_bind = \"BIND_MODE\"; > = BIND_MODE;\n") +
		(extra ? "uniform int Extra < ui_bind = \"EXTRA\"; > = EXTRA;\n" : "") +
		"void VS(uint id : SV_VertexID, out float4 pos : SV_Position) {\n"
		"pos = float4(id == 2 ? 3.0 : -1.0, id == 1 ? 3.0 : -1.0, 0.0, 1.0); }\n"
		"float4 PS() : SV_Target { return float4(Gain, Flag ? 1.0 : 0.0, Mode * 0.1, 1.0); }\n"
		"technique Main { pass { VertexShader = VS; PixelShader = PS; } }\n";
}

int main(int argc, char **argv)
{
	try
	{
		require(argc == 3, "Usage: runtime_gui_parity <DLL> <new-output-directory>");
		const fs::path dll_path = fs::absolute(argv[1]);
		const fs::path root = fs::absolute(argv[2]);
		require(!fs::exists(root), "Use a new output directory to preserve existing fixtures");
		fs::create_directories(root);
		SetEnvironmentVariableW(L"RESHADE_DISABLE_GRAPHICS_HOOK", L"1");
		SetEnvironmentVariableW(L"RESHADE_DISABLE_LOADING_CHECK", L"1");
		const HMODULE module = LoadLibraryW(dll_path.c_str());
		require(module != nullptr, "Could not load ReShade DLL");
		const auto create = reinterpret_cast<bool (*)(reshade::api::device_api, void *, void *, void *, const char *, effect_runtime **)>(GetProcAddress(module, "ReShadeCreateEffectRuntime"));
		const auto destroy = reinterpret_cast<void (*)(effect_runtime *)>(GetProcAddress(module, "ReShadeDestroyEffectRuntime"));
		const auto present = reinterpret_cast<void (*)(effect_runtime *)>(GetProcAddress(module, "ReShadeUpdateAndPresentEffectRuntime"));
		require(create && destroy && present, "Missing effect runtime exports");

		const HWND window = CreateWindowW(L"STATIC", L"AuroraShade runtime regression", WS_OVERLAPPEDWINDOW, 0, 0, 320, 240, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
		require(window != nullptr, "Could not create hidden test window");
		DXGI_SWAP_CHAIN_DESC desc = {};
		desc.BufferDesc.Width = 320;
		desc.BufferDesc.Height = 240;
		desc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
		desc.SampleDesc.Count = 1;
		desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
		desc.BufferCount = 1;
		desc.OutputWindow = window;
		desc.Windowed = TRUE;
		ComPtr<ID3D11Device> device;
		ComPtr<ID3D11DeviceContext> context;
		ComPtr<IDXGISwapChain> swapchain;
		require(SUCCEEDED(D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &desc, &swapchain, &device, nullptr, &context)), "Could not create WARP device");

		for (int scenario = 0; scenario != 4; ++scenario)
		{
			const bool auto_save = scenario != 0;
			const int feature = scenario < 2 ? 1 : scenario + 1;
			const fs::path directory = root / std::to_string(scenario);
			fs::create_directory(directory);
			const fs::path preset = directory / "preset.ini";
			const fs::path config = directory / "ReShade.ini";
			write_file(directory / "Parity.fx", shader(true));
			write_file(preset,
				std::string("Techniques=Main@Parity.fx") + (feature == 3 ? ",Main@Parity.fx+copy" : "") +
				"\nTechniqueGroup1=Main@Parity.fx\nKeyMain@Parity.fx=119,0,0,0\n" +
				(feature == 4 ? "Flairs=Night\nCurrentFlair=Night\n" : "") +
				"[Parity.fx]\nGain=0.25\nFlag=0\nKeyFlag=118,0,0,0\nMode=1\nExtra=2\nPreprocessorDefinitions=BIND_MODE=1,EXTRA=2\n" +
				(feature == 3 ? "[Parity.fx+copy]\nGain=0.6\n" : "") +
				(feature == 4 ? "[Parity.fx|Night]\nGain=0.5\nFlag=0\n" : ""));
			const std::string original_preset = read_file(preset);
			write_file(config,
				"[GENERAL]\nEffectSearchPaths=" + directory.u8string() + "\nPresetPath=" + preset.u8string() +
				"\nIntermediateCachePath=" + directory.u8string() + "\nNoEffectCache=1\nEffectTimeDelay=250\nAuroraFeature=" + std::to_string(feature) +
				"\n[OVERLAY]\nTutorialProgress=4\nShowFPS=0\nAutoSavePreset=" + (auto_save ? "1" : "0") +
				"\n[INPUT]\nKeyEffects=117,0,0,0\nKeyNextFlair=120,0,0,0\n" +
				(scenario == 1 ? "" : "[SCREENSHOT]\nPostSaveCommandEnabled=0\n"));

			effect_runtime *runtime = nullptr;
			require(create(reshade::api::device_api::d3d11, device.Get(), context.Get(), swapchain.Get(), config.u8string().c_str(), &runtime), "Could not initialize effect runtime");
			const auto tick = [&]() {
				MSG message;
				while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
					DispatchMessageW(&message);
				present(runtime);
				Sleep(10);
			};
			const auto run_for = [&](int milliseconds) {
				const auto end = std::chrono::steady_clock::now() + std::chrono::milliseconds(milliseconds);
				do { tick(); } while (std::chrono::steady_clock::now() < end);
			};
			const auto key = [&](WPARAM code) {
				PostMessageW(window, WM_KEYDOWN, code, 1);
				tick();
				PostMessageW(window, WM_KEYUP, code, (1LL << 31) | (1LL << 30) | 1);
				tick();
			};
			tick();
			require(runtime->find_uniform_variable("Parity.fx", "Gain") == 0, "Initial loading delay was ignored");
			run_for(1500);
			auto gain = runtime->find_uniform_variable("Parity.fx", "Gain");
			require(gain != 0, "Effects did not load after the delay");
			float value = 0;
			runtime->get_uniform_value_float(gain, &value, 1);
			require(value == (feature == 4 ? 0.5f : 0.25f), "Preset/flair uniform value is incorrect");
			int mode = 0;
			runtime->get_uniform_value_int(runtime->find_uniform_variable("Parity.fx", "Mode"), &mode, 1);
			require(mode == 1, "ui_bind preset value did not load");
			key(VK_F6);
			require(!runtime->get_effects_state(), "Effects shortcut did not disable effects");
			key(VK_F6);
			require(runtime->get_effects_state(), "Effects shortcut did not enable effects");
			key(VK_F7);
			bool flag = false;
			runtime->get_uniform_value_bool(runtime->find_uniform_variable("Parity.fx", "Flag"), &flag, 1);
			require(flag, "Uniform shortcut did not change the value");
			const auto technique = runtime->find_technique("Parity.fx", "Main");
			key(VK_F8);
			require(!runtime->get_technique_state(technique), "Technique shortcut did not disable the technique");
			key(VK_F8);
			require(runtime->get_technique_state(technique), "Technique shortcut did not enable the technique");
			run_for(1200);
			require(auto_save ? read_file(preset).find("Flag=1") != std::string::npos : read_file(preset) == original_preset, "Shortcut auto-save policy was not honored");

			if (feature == 4)
			{
				key(VK_F9);
				run_for(100);
				runtime->get_uniform_value_float(gain, &value, 1);
				require(value == 0.25f, "Flair shortcut did not return to the base flair");
			}
			if (feature == 3)
			{
				unsigned int count = 0;
				runtime->enumerate_techniques(nullptr, [](effect_runtime *, reshade::api::effect_technique, void *data) { ++*static_cast<unsigned int *>(data); }, &count);
				require(count == 2, "Preset clone was not restored");
			}

			runtime->set_uniform_value_float(gain, 0.75f);
			runtime->save_current_preset();
			run_for(1200);
			require(auto_save ? read_file(preset).find("Gain=0.750000") != std::string::npos : read_file(preset) == original_preset, "API save policy was not honored");
			write_file(directory / "Parity.fx", shader(false));
			runtime->reload_effect_next_frame("Parity.fx");
			run_for(1500);
			require(runtime->find_uniform_variable("Parity.fx", "Gain") != 0 && runtime->find_uniform_variable("Parity.fx", "Extra") == 0, "Single-effect reload failed");
			const fs::path exported = directory / "exported.ini";
			runtime->export_current_preset(exported.u8string().c_str());
			// An untouched clone still uses EXTRA, so it must remain in the shared base section.
			if (feature != 3)
				require(read_file(exported).find("EXTRA=") == std::string::npos, "Orphan ui_bind macro was retained without opening the overlay");
			else
				require(read_file(exported).find("EXTRA=2") != std::string::npos, "A macro still used by an untouched clone was removed");
			require(read_file(exported).find("TechniqueGroup1=Main@Parity.fx") != std::string::npos, "Technique group was lost");
			destroy(runtime);
			require(read_file(config).find(scenario == 1 ? "PostSaveCommandEnabled=1" : "PostSaveCommandEnabled=0") != std::string::npos, "Screenshot command default/persistence is incorrect");
			std::cout << "PASS feature=" << feature << " auto_save=" << auto_save << '\n';
		}
		DestroyWindow(window);
		// Keep the module loaded until process exit, after all COM wrappers are released.
		return 0;
	}
	catch (const std::exception &error)
	{
		std::cerr << "FAIL: " << error.what() << '\n';
		return 1;
	}
}
