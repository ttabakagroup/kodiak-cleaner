// Kodiak Cleaner's Windows 11 right-click menu entry ("Open Kodiak Cleaner"), Microsoft Store copy only (Ted, 2026-10-26).
//
// Windows 11's short right-click menu only lists commands that come from an app with package identity and implement
// IExplorerCommand. This DLL is that command. The Store package registers it as an out-of-process COM server
// (com:SurrogateServer in packaging/store/AppxManifest.xml), so it runs in its own dllhost.exe with the package's identity
// and never inside File Explorer itself; if it fails, Explorer only loses the menu entry.
//
// It is offered in two places:
// - the empty space inside a folder (desktop5:ItemType "Directory\Background" in the manifest), but GetState hides it
//   everywhere except the Recycle Bin, so ordinary folders and the desktop don't get an extra entry;
// - the Recycle Bin icon, through the app's own registry verb (Integration.SetRecycleBinMenu sets ExplorerCommandHandler
//   to this class). Microsoft doesn't document this for the new menu, so it is an experiment; the verb keeps its command
//   key and still works under Show More Options either way.
//
// Plain COM with no libraries, so it builds with MSVC (CI, via CMakeLists.txt) and MinGW (a quick check on Linux).
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <new>

// {04C283A9-1A6F-4898-BE4C-60A428E89BD5}: also in AppxManifest.xml and Integration.ShellCommandClsid (a test keeps them equal).
static const CLSID CLSID_OpenKodiakCleaner = { 0x04c283a9, 0x1a6f, 0x4898, { 0xbe, 0x4c, 0x60, 0xa4, 0x28, 0xe8, 0x9b, 0xd5 } };
static const wchar_t RecycleBinParsingName[] = L"::{645FF040-5081-101B-9F08-00AA002F954E}";

static HMODULE g_module;
static LONG g_objects;
static LONG g_locks;

static HRESULT DupString(const wchar_t* text, LPWSTR* out) { return SHStrDupW(text, out); }

// The folder this DLL was loaded from is the package folder, which also holds KodiakCleaner.exe.
static bool PackageFile(const wchar_t* name, wchar_t* path, DWORD size)
{
    DWORD n = GetModuleFileNameW(g_module, path, size);
    if (n == 0 || n >= size) return false;
    wchar_t* slash = wcsrchr(path, L'\\');
    if (!slash) return false;
    slash[1] = 0;
    return wcscat_s(path, size, name) == 0;
}

// True when every item is the Recycle Bin (the icon, or its open window's background).
static bool IsRecycleBin(IShellItemArray* items)
{
    if (!items) return false;
    DWORD count = 0;
    if (FAILED(items->GetCount(&count)) || count != 1) return false;
    IShellItem* item = nullptr;
    if (FAILED(items->GetItemAt(0, &item))) return false;
    bool match = false;
    LPWSTR name = nullptr;
    if (SUCCEEDED(item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, &name)))
    {
        match = CompareStringOrdinal(name, -1, RecycleBinParsingName, -1, TRUE) == CSTR_EQUAL;
        CoTaskMemFree(name);
    }
    item->Release();
    return match;
}

class OpenCommand final : public IExplorerCommand
{
    LONG _refs = 1;
public:
    OpenCommand() { InterlockedIncrement(&g_objects); }
    ~OpenCommand() { InterlockedDecrement(&g_objects); }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == __uuidof(IExplorerCommand)) { *ppv = static_cast<IExplorerCommand*>(this); AddRef(); return S_OK; }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&_refs); }
    IFACEMETHODIMP_(ULONG) Release() override
    {
        LONG r = InterlockedDecrement(&_refs);
        if (r == 0) delete this;
        return r;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* name) override { return DupString(L"Open Kodiak Cleaner", name); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        wchar_t path[MAX_PATH + 4];
        if (!PackageFile(L"KodiakCleaner.exe", path, MAX_PATH)) { *icon = nullptr; return E_FAIL; }
        wcscat_s(path, ARRAYSIZE(path), L",0");
        return DupString(path, icon);
    }
    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* tip) override { *tip = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetCanonicalName(GUID* guid) override { *guid = CLSID_OpenKodiakCleaner; return S_OK; }
    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        *state = IsRecycleBin(items) ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }
    IFACEMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override
    {
        // Through the app alias, so Kodiak Cleaner starts as the Store app (with its settings), like the Start menu does.
        PWSTR appData = nullptr;
        if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &appData))) return E_FAIL;
        wchar_t exe[MAX_PATH];
        bool ok = wcscpy_s(exe, appData) == 0 && wcscat_s(exe, L"\\Microsoft\\WindowsApps\\kodiakcleaner.exe") == 0;
        CoTaskMemFree(appData);
        if (!ok) return E_FAIL;
        SHELLEXECUTEINFOW info{};
        info.cbSize = sizeof(info);
        info.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
        info.lpVerb = L"open";
        info.lpFile = exe;
        info.nShow = SW_SHOWNORMAL;
        return ShellExecuteExW(&info) ? S_OK : HRESULT_FROM_WIN32(GetLastError());
    }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override { *flags = ECF_DEFAULT; return S_OK; }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override { *commands = nullptr; return E_NOTIMPL; }
};

class Factory final : public IClassFactory
{
public:
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IClassFactory) { *ppv = static_cast<IClassFactory*>(this); return S_OK; }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return 2; }  // static object
    IFACEMETHODIMP_(ULONG) Release() override { return 1; }
    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        *ppv = nullptr;
        if (outer) return CLASS_E_NOAGGREGATION;
        auto* command = new (std::nothrow) OpenCommand();
        if (!command) return E_OUTOFMEMORY;
        HRESULT hr = command->QueryInterface(riid, ppv);
        command->Release();
        return hr;
    }
    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock) InterlockedIncrement(&g_locks); else InterlockedDecrement(&g_locks);
        return S_OK;
    }
};

static Factory g_factory;

extern "C" BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (clsid != CLSID_OpenKodiakCleaner) return CLASS_E_CLASSNOTAVAILABLE;
    return g_factory.QueryInterface(riid, ppv);
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return g_objects == 0 && g_locks == 0 ? S_OK : S_FALSE;
}
