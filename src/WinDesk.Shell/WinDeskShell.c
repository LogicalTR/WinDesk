#define COBJMACROS
#define WIN32_LEAN_AND_MEAN
#define CONST_VTABLE
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0A00
#endif
#include <windows.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <exdisp.h>
#include <shldisp.h>
#include <shlguid.h>
#include <servprov.h>
#include <wchar.h>

// Native COM only: Explorer never loads the application's CLR or WPF runtime.
static const CLSID CLSID_WinDeskMenu = {0xa3252e45,0xb842,0x4fcb,{0x88,0xfa,0x72,0x3f,0x98,0x63,0x9c,0x55}};
static HINSTANCE module;
static LONG objects, locks;
typedef struct { IExplorerCommand iface; LONG refs; int kind; } Command;
typedef struct { IEnumExplorerCommand iface; LONG refs; ULONG cursor; } Enumerator;
typedef struct { IClassFactory iface; LONG refs; } Factory;
static HRESULT new_command(int kind, IExplorerCommand **result);
static HRESULT new_enumerator(ULONG cursor, IEnumExplorerCommand **result);

static HRESULT copy_text(const wchar_t *value, wchar_t **result) {
    if (!result) return E_POINTER;
    size_t size = (wcslen(value) + 1) * sizeof(wchar_t);
    *result = CoTaskMemAlloc(size);
    if (!*result) return E_OUTOFMEMORY;
    CopyMemory(*result, value, size); return S_OK;
}
static BOOL app_path(wchar_t *path, DWORD size) {
    DWORD length = GetModuleFileNameW(module, path, size);
    if (!length || length >= size) return FALSE;
    return PathRemoveFileSpecW(path) && PathRemoveFileSpecW(path) && PathAppendW(path, L"WinDesk.exe");
}
static HRESULT STDMETHODCALLTYPE command_query(IExplorerCommand *self, REFIID iid, void **result) {
    if (!result) return E_POINTER; *result = NULL;
    if (!IsEqualIID(iid, &IID_IUnknown) && !IsEqualIID(iid, &IID_IExplorerCommand)) return E_NOINTERFACE;
    *result = self; IExplorerCommand_AddRef(self); return S_OK;
}
static ULONG STDMETHODCALLTYPE command_addref(IExplorerCommand *self) { return InterlockedIncrement(&((Command*)self)->refs); }
static ULONG STDMETHODCALLTYPE command_release(IExplorerCommand *self) {
    ULONG count = InterlockedDecrement(&((Command*)self)->refs);
    if (!count) { HeapFree(GetProcessHeap(), 0, self); InterlockedDecrement(&objects); } return count;
}
static HRESULT STDMETHODCALLTYPE command_title(IExplorerCommand *self, IShellItemArray *items, LPWSTR *title) {
    static const wchar_t *titles[] = { L"WinDesk", L"Yeni grup", L"Panelleri gizle / göster", L"Ayarlar" };
    return copy_text(titles[((Command*)self)->kind], title);
}
static HRESULT STDMETHODCALLTYPE command_icon(IExplorerCommand *self, IShellItemArray *items, LPWSTR *icon) {
    wchar_t path[MAX_PATH]; if (!icon) return E_POINTER; *icon = NULL;
    if (!app_path(path, MAX_PATH) || wcslen(path) + 3 >= MAX_PATH) return E_FAIL;
    wcscat(path, L",0"); return copy_text(path, icon);
}
static HRESULT STDMETHODCALLTYPE command_tip(IExplorerCommand *self, IShellItemArray *items, LPWSTR *tip) {
    return copy_text(L"Masaüstü gruplarını yönet", tip);
}
static HRESULT STDMETHODCALLTYPE command_name(IExplorerCommand *self, GUID *name) {
    if (!name) return E_POINTER; *name = CLSID_WinDeskMenu; name->Data1 += ((Command*)self)->kind; return S_OK;
}
static HRESULT STDMETHODCALLTYPE command_state(IExplorerCommand *self, IShellItemArray *items, BOOL slow, EXPCMDSTATE *state) {
    if (!state) return E_POINTER; *state = ECS_ENABLED; return S_OK;
}
static HRESULT STDMETHODCALLTYPE command_invoke(IExplorerCommand *self, IShellItemArray *items, IBindCtx *context) {
    static const wchar_t *verbs[] = { L"settings", L"new-group", L"toggle-panels", L"settings" };
    wchar_t path[MAX_PATH], arguments[96], directory[MAX_PATH];
    if (!app_path(path, MAX_PATH)) return E_FAIL;
    wcscpy(directory, path); PathRemoveFileSpecW(directory);
    _snwprintf(arguments, 96, L"--command %ls", verbs[((Command*)self)->kind]);
    // Ask the existing desktop Explorer process to launch the Win32 app.
    // CreateProcess here inherits this surrogate's package identity and would
    // redirect workspace.json to an unrelated, initially empty package store.
    IShellWindows *windows = NULL;
    IDispatch *desktop = NULL, *view_dispatch = NULL, *application = NULL;
    IServiceProvider *provider = NULL;
    IShellBrowser *browser = NULL;
    IShellView *view = NULL;
    IShellFolderViewDual *folder = NULL;
    IShellDispatch2 *shell = NULL;
    VARIANT location, root, params, working, operation, show;
    VariantInit(&location); VariantInit(&root); VariantInit(&params);
    VariantInit(&working); VariantInit(&operation); VariantInit(&show);
    V_VT(&location) = VT_I4; V_I4(&location) = CSIDL_DESKTOP;
    V_VT(&show) = VT_I4; V_I4(&show) = SW_SHOWNORMAL;
    BSTR file = SysAllocString(path);
    V_VT(&params) = VT_BSTR; V_BSTR(&params) = SysAllocString(arguments);
    V_VT(&working) = VT_BSTR; V_BSTR(&working) = SysAllocString(directory);
    V_VT(&operation) = VT_BSTR; V_BSTR(&operation) = SysAllocString(L"open");
    HRESULT result = E_OUTOFMEMORY;
    LONG desktop_handle = 0;
    if (!file || !V_BSTR(&params) || !V_BSTR(&working) || !V_BSTR(&operation)) goto cleanup;
    result = CoCreateInstance(&CLSID_ShellWindows, NULL, CLSCTX_LOCAL_SERVER, &IID_IShellWindows, (void**)&windows);
    if (FAILED(result)) goto cleanup;
    result = IShellWindows_FindWindowSW(windows, &location, &root, SWC_DESKTOP, &desktop_handle, SWFO_NEEDDISPATCH, &desktop);
    if (FAILED(result)) goto cleanup;
    if (!desktop) { result = E_FAIL; goto cleanup; }
    result = IDispatch_QueryInterface(desktop, &IID_IServiceProvider, (void**)&provider);
    if (FAILED(result)) goto cleanup;
    result = IServiceProvider_QueryService(provider, &SID_STopLevelBrowser, &IID_IShellBrowser, (void**)&browser);
    if (FAILED(result)) goto cleanup;
    result = IShellBrowser_QueryActiveShellView(browser, &view);
    if (FAILED(result)) goto cleanup;
    result = IShellView_GetItemObject(view, SVGIO_BACKGROUND, &IID_IDispatch, (void**)&view_dispatch);
    if (FAILED(result)) goto cleanup;
    result = IDispatch_QueryInterface(view_dispatch, &IID_IShellFolderViewDual, (void**)&folder);
    if (FAILED(result)) goto cleanup;
    result = IShellFolderViewDual_get_Application(folder, &application);
    if (FAILED(result)) goto cleanup;
    result = IDispatch_QueryInterface(application, &IID_IShellDispatch2, (void**)&shell);
    if (FAILED(result)) goto cleanup;
    result = IShellDispatch2_ShellExecute(shell, file, params, working, operation, show);
cleanup:
    if (shell) IShellDispatch2_Release(shell);
    if (application) IDispatch_Release(application);
    if (folder) IShellFolderViewDual_Release(folder);
    if (view_dispatch) IDispatch_Release(view_dispatch);
    if (view) IShellView_Release(view);
    if (browser) IShellBrowser_Release(browser);
    if (provider) IServiceProvider_Release(provider);
    if (desktop) IDispatch_Release(desktop);
    if (windows) IShellWindows_Release(windows);
    SysFreeString(file); VariantClear(&params); VariantClear(&working); VariantClear(&operation);
    return result;
}
static HRESULT STDMETHODCALLTYPE command_flags(IExplorerCommand *self, EXPCMDFLAGS *flags) {
    if (!flags) return E_POINTER; *flags = ((Command*)self)->kind == 0 ? ECF_HASSUBCOMMANDS : ECF_DEFAULT; return S_OK;
}
static HRESULT STDMETHODCALLTYPE command_children(IExplorerCommand *self, IEnumExplorerCommand **result) {
    if (!result) return E_POINTER; *result = NULL;
    return ((Command*)self)->kind == 0 ? new_enumerator(0, result) : E_NOTIMPL;
}
static const IExplorerCommandVtbl command_vtable = {
    command_query, command_addref, command_release, command_title, command_icon, command_tip,
    command_name, command_state, command_invoke, command_flags, command_children
};
static HRESULT new_command(int kind, IExplorerCommand **result) {
    *result = NULL; Command *command = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(Command));
    if (!command) return E_OUTOFMEMORY;
    command->iface.lpVtbl = &command_vtable; command->refs = 1; command->kind = kind;
    InterlockedIncrement(&objects); *result = &command->iface; return S_OK;
}
static HRESULT STDMETHODCALLTYPE enum_query(IEnumExplorerCommand *self, REFIID iid, void **result) {
    if (!result) return E_POINTER; *result = NULL;
    if (!IsEqualIID(iid, &IID_IUnknown) && !IsEqualIID(iid, &IID_IEnumExplorerCommand)) return E_NOINTERFACE;
    *result = self; IEnumExplorerCommand_AddRef(self); return S_OK;
}
static ULONG STDMETHODCALLTYPE enum_addref(IEnumExplorerCommand *self) { return InterlockedIncrement(&((Enumerator*)self)->refs); }
static ULONG STDMETHODCALLTYPE enum_release(IEnumExplorerCommand *self) {
    ULONG count = InterlockedDecrement(&((Enumerator*)self)->refs);
    if (!count) { HeapFree(GetProcessHeap(), 0, self); InterlockedDecrement(&objects); } return count;
}
static HRESULT STDMETHODCALLTYPE enum_next(IEnumExplorerCommand *self, ULONG requested, IExplorerCommand **commands, ULONG *fetched) {
    if (!commands || (requested != 1 && !fetched)) return E_POINTER;
    if (fetched) *fetched = 0;
    Enumerator *enumerator = (Enumerator*)self; ULONG count = 0;
    while (count < requested && enumerator->cursor + count < 3) {
        HRESULT hr = new_command((int)(enumerator->cursor + count + 1), &commands[count]);
        if (FAILED(hr)) { while (count) { count--; IExplorerCommand_Release(commands[count]); commands[count] = NULL; } return hr; }
        count++;
    }
    enumerator->cursor += count; if (fetched) *fetched = count; return count == requested ? S_OK : S_FALSE;
}
static HRESULT STDMETHODCALLTYPE enum_skip(IEnumExplorerCommand *self, ULONG count) {
    Enumerator *enumerator = (Enumerator*)self; ULONG remaining = 3 - enumerator->cursor;
    enumerator->cursor += min(count, remaining); return count <= remaining ? S_OK : S_FALSE;
}
static HRESULT STDMETHODCALLTYPE enum_reset(IEnumExplorerCommand *self) { ((Enumerator*)self)->cursor = 0; return S_OK; }
static HRESULT STDMETHODCALLTYPE enum_clone(IEnumExplorerCommand *self, IEnumExplorerCommand **result) {
    if (!result) return E_POINTER; return new_enumerator(((Enumerator*)self)->cursor, result);
}
static const IEnumExplorerCommandVtbl enum_vtable = { enum_query, enum_addref, enum_release, enum_next, enum_skip, enum_reset, enum_clone };
static HRESULT new_enumerator(ULONG cursor, IEnumExplorerCommand **result) {
    *result = NULL; Enumerator *enumerator = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(Enumerator));
    if (!enumerator) return E_OUTOFMEMORY;
    enumerator->iface.lpVtbl = &enum_vtable; enumerator->refs = 1; enumerator->cursor = cursor;
    InterlockedIncrement(&objects); *result = &enumerator->iface; return S_OK;
}
static HRESULT STDMETHODCALLTYPE factory_query(IClassFactory *self, REFIID iid, void **result) {
    if (!result) return E_POINTER; *result = NULL;
    if (!IsEqualIID(iid, &IID_IUnknown) && !IsEqualIID(iid, &IID_IClassFactory)) return E_NOINTERFACE;
    *result = self; IClassFactory_AddRef(self); return S_OK;
}
static ULONG STDMETHODCALLTYPE factory_addref(IClassFactory *self) { return InterlockedIncrement(&((Factory*)self)->refs); }
static ULONG STDMETHODCALLTYPE factory_release(IClassFactory *self) {
    ULONG count = InterlockedDecrement(&((Factory*)self)->refs);
    if (!count) { HeapFree(GetProcessHeap(), 0, self); InterlockedDecrement(&objects); } return count;
}
static HRESULT STDMETHODCALLTYPE factory_create(IClassFactory *self, IUnknown *outer, REFIID iid, void **result) {
    if (!result) return E_POINTER; *result = NULL; if (outer) return CLASS_E_NOAGGREGATION;
    IExplorerCommand *command; HRESULT hr = new_command(0, &command); if (FAILED(hr)) return hr;
    hr = IExplorerCommand_QueryInterface(command, iid, result); IExplorerCommand_Release(command); return hr;
}
static HRESULT STDMETHODCALLTYPE factory_lock(IClassFactory *self, BOOL lock) {
    if (lock) InterlockedIncrement(&locks); else InterlockedDecrement(&locks); return S_OK;
}
static const IClassFactoryVtbl factory_vtable = { factory_query, factory_addref, factory_release, factory_create, factory_lock };
HRESULT WINAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void **result) {
    if (!result) return E_POINTER; *result = NULL;
    if (!IsEqualCLSID(clsid, &CLSID_WinDeskMenu)) return CLASS_E_CLASSNOTAVAILABLE;
    Factory *factory = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(Factory)); if (!factory) return E_OUTOFMEMORY;
    factory->iface.lpVtbl = &factory_vtable; factory->refs = 1; InterlockedIncrement(&objects);
    HRESULT hr = IClassFactory_QueryInterface(&factory->iface, iid, result); IClassFactory_Release(&factory->iface); return hr;
}
HRESULT WINAPI DllCanUnloadNow(void) { return objects == 0 && locks == 0 ? S_OK : S_FALSE; }
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, void *reserved) {
    if (reason == DLL_PROCESS_ATTACH) { module = instance; DisableThreadLibraryCalls(instance); } return TRUE;
}
