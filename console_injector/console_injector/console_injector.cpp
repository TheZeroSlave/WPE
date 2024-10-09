// console_injector.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <windows.h>
#include <vector>

// Check windows
#if _WIN32 || _WIN64
#if _WIN64
#define ENVIRONMENT64
#else
#define ENVIRONMENT32
#endif
#endif

// Check GCC
#if __GNUC__
#if __x86_64__ || __ppc64__
#define ENVIRONMENT64
#else
#define ENVIRONMENT32
#endif
#endif

#ifdef ENVIRONMENT64
const char* dllName = "spy_hook_lib_x64.dll";
#else
const char* dllName = "spy_hook_lib_x86.dll";
#endif

bool acquireDebugPrivileges()
{
	bool success = false;

	HANDLE hToken;
	TOKEN_PRIVILEGES tokenPriv;
	LUID luidDebug;
	if (OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES, &hToken))
	{
		if (LookupPrivilegeValue(L"", SE_DEBUG_NAME, &luidDebug))
		{
			tokenPriv.PrivilegeCount = 1;
			tokenPriv.Privileges[0].Luid = luidDebug;
			tokenPriv.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

			if (AdjustTokenPrivileges(hToken, FALSE, &tokenPriv, sizeof(tokenPriv), NULL, NULL) == 0)
			{
				// error msg
			}
			else
			{
				success = true;
			}
		}
		else
		{
			std::cout << "can't lookup " << std::endl;
		}
	}
	else
	{
		std::cout << "can't open process current " << std::endl;
	}

	return success;
}

#include <fstream>
#include <tlhelp32.h>

void printUsage()
{
	std::cout << "arguments: attach | detach" << std::endl <<
		"-pid PID" << std::endl;
}

std::string getFullLibName()
{
	char maxPath[MAX_PATH];
	GetCurrentDirectoryA(MAX_PATH, maxPath);
	std::string path(maxPath);
	if (path[path.size() - 1] != '\\')
	{
		path += "\\";
	}
	return path + dllName;
}

int main(int argc, char* argv[])
{
	if (argc < 3)
	{
		printUsage();
		return -1;
	}

	bool attachMode = false;

	if (std::string(argv[1]) == "attach")
	{
		attachMode = true;
	}
	else if (std::string(argv[1]) == "detach")
	{
		attachMode = false;
	}
	else
	{
		printUsage();
		return -100;
	}

	if (std::string(argv[2]) != std::string("-pid"))
	{
		printUsage();
		return -2;
	}


	const DWORD pid = atoi(argv[3]);

	if (pid == 0)
	{
		std::cout << "you passed zero pid" << std::endl;
		return -3;
	}

	const std::string dllPath = getFullLibName();

	if (!acquireDebugPrivileges())
	{
		std::cout << "can't get priv" << std::endl;
		return -4;
	}

	HANDLE hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION |
		PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION, FALSE, pid);

	if (hProcess == 0)
	{
		std::cout << "can't open the process " << GetLastError() << std::endl;
		return -5;
	}

	LPVOID LoadLibraryAddr = (LPVOID)GetProcAddress(GetModuleHandle(L"kernel32.dll"),
		"LoadLibraryA");

	if (LoadLibraryAddr == 0)
	{
		std::cout << "can't get address of loaddr" << std::endl;
		return -6;
	}

	LPVOID LLParam = (LPVOID)VirtualAllocEx(hProcess, NULL, 256,
		MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

	if (LLParam == 0)
	{
		std::cout << "cannot allocate memory " << std::endl;
		return -7;
	}

	SIZE_T written = 0;
	WriteProcessMemory(hProcess, LLParam, dllPath.data(), dllPath.size() + 1, &written);

	if (written == 0)
	{
		std::cout << "can't write to process memory" << std::endl;
		return -8;
	}
	auto handle = CreateRemoteThread(hProcess, NULL, NULL, (LPTHREAD_START_ROUTINE)LoadLibraryAddr,
		LLParam, NULL, NULL);
	if (handle == 0)
	{
		std::cout << "cannot create remote thread " << GetLastError() << std::endl;
		return -9;
	}
	WaitForSingleObject(handle, 1000);
	DWORD exitCode = 0;
	if (!GetExitCodeThread(handle, &exitCode))
	{
		return -11;
	}
	// if everything is ok, code will be different from 0
	if (exitCode == 0 && exitCode != STILL_ACTIVE)
	{
		char lastErr[10] = { 0 };
		_itoa_s(exitCode, lastErr, 10);
		MessageBoxA(0, lastErr, "", 0);
		return -10;
	}
	CloseHandle(hProcess);
}