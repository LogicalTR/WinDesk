#define COBJMACROS
#define CONST_VTABLE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shobjidl.h>
#include <stdio.h>
#include <wchar.h>

static const CLSID menu_class = {0xa3252e45,0xb842,0x4fcb,{0x88,0xfa,0x72,0x3f,0x98,0x63,0x9c,0x55}};
typedef HRESULT (WINAPI *GetClass)(REFCLSID, REFIID, void **);
typedef HRESULT (WINAPI *CanUnload)(void);
static int checks;
#define CHECK(condition, message) do { if (!(condition)) { fprintf(stderr, "FAIL: %s (line %d)\n", message, __LINE__); return 1; } checks++; printf("PASS: %s\n", message); } while(0)

int wmain(int argc, wchar_t **argv) {
    CHECK(argc >= 2, "DLL path or --registered provided");
    CHECK(SUCCEEDED(CoInitializeEx(NULL, COINIT_APARTMENTTHREADED)), "STA initialized");
    BOOL registered = wcscmp(argv[1], L"--registered") == 0;
    HMODULE library = NULL; CanUnload can_unload = NULL;
    IExplorerCommand *root = NULL;
    if (registered) {
        HRESULT hr = CoCreateInstance(&menu_class, NULL, CLSCTX_LOCAL_SERVER, &IID_IExplorerCommand, (void**)&root);
        if (FAILED(hr)) fprintf(stderr, "COM activation HRESULT: 0x%08lx\n", (unsigned long)hr);
        CHECK(SUCCEEDED(hr), "Installed package activates its native COM surrogate");
    } else {
        library = LoadLibraryW(argv[1]);
        CHECK(library != NULL, "Native DLL loads without the CLR");
        GetClass get_class = (GetClass)(void*)GetProcAddress(library, "DllGetClassObject");
        can_unload = (CanUnload)(void*)GetProcAddress(library, "DllCanUnloadNow");
        CHECK(get_class && can_unload, "Standard COM entry points exported");
        CHECK(can_unload() == S_OK, "Unused DLL can unload");
        IClassFactory *factory = NULL;
        CHECK(get_class(&menu_class, &IID_IClassFactory, (void**)&factory) == S_OK, "Class factory is available");
        CHECK(can_unload() == S_FALSE, "Live factory prevents premature unload");
        CHECK(IClassFactory_CreateInstance(factory, NULL, &IID_IExplorerCommand, (void**)&root) == S_OK, "Root explorer command created");
        IClassFactory_Release(factory);
    }
    wchar_t *text = NULL;
    CHECK(IExplorerCommand_GetTitle(root, NULL, &text) == S_OK && wcscmp(text, L"WinDesk") == 0, "Root title is WinDesk");
    CoTaskMemFree(text); text = NULL;
    EXPCMDFLAGS flags;
    CHECK(IExplorerCommand_GetFlags(root, &flags) == S_OK && flags == ECF_HASSUBCOMMANDS, "Root requests a native submenu");
    EXPCMDSTATE state;
    CHECK(IExplorerCommand_GetState(root, NULL, FALSE, &state) == S_OK && state == ECS_ENABLED, "Desktop background with no file selection is enabled");
    CHECK(IExplorerCommand_GetIcon(root, NULL, &text) == S_OK && wcsstr(text, L"WinDesk.exe,0"), "Menu icon resolves to installed executable");
    CoTaskMemFree(text); text = NULL;
    IEnumExplorerCommand *enumerator = NULL, *clone = NULL;
    CHECK(IExplorerCommand_EnumSubCommands(root, &enumerator) == S_OK, "Subcommands enumerate");
    IExplorerCommand *children[4] = {0}; ULONG fetched = 0;
    CHECK(IEnumExplorerCommand_Next(enumerator, 4, children, &fetched) == S_FALSE && fetched == 3, "Exactly three actions are returned");
    static const wchar_t *titles[] = {L"Yeni grup", L"Panelleri gizle / göster", L"Ayarlar"};
    for (ULONG i = 0; i < 3; i++) {
        CHECK(IExplorerCommand_GetTitle(children[i], NULL, &text) == S_OK && wcscmp(text, titles[i]) == 0, "Action title and order are correct");
        CoTaskMemFree(text); text = NULL;
        CHECK(IExplorerCommand_GetFlags(children[i], &flags) == S_OK && flags == ECF_DEFAULT, "Actions are ordinary clickable leaves");
        GUID id;
        CHECK(IExplorerCommand_GetCanonicalName(children[i], &id) == S_OK && id.Data1 == menu_class.Data1 + i + 1, "Action has a stable distinct identity");
    }
    if (argc == 4 && wcscmp(argv[2], L"--invoke") == 0) {
        int action = _wtoi(argv[3]);
        CHECK(action >= 1 && action <= 3, "Requested action is in range");
        CHECK(IExplorerCommand_Invoke(children[action - 1], NULL, NULL) == S_OK, "Native action starts the WinDesk command client");
    }
    for (ULONG i = 0; i < 3; i++) IExplorerCommand_Release(children[i]);
    CHECK(IEnumExplorerCommand_Reset(enumerator) == S_OK && IEnumExplorerCommand_Skip(enumerator, 1) == S_OK, "Enumeration can reset and skip");
    CHECK(IEnumExplorerCommand_Clone(enumerator, &clone) == S_OK, "Enumeration cursor can be cloned");
    IExplorerCommand *child = NULL;
    CHECK(IEnumExplorerCommand_Next(clone, 1, &child, &fetched) == S_OK && fetched == 1, "Cloned cursor continues from the second action");
    CHECK(IExplorerCommand_GetTitle(child, NULL, &text) == S_OK && wcscmp(text, titles[1]) == 0, "Cloned enumeration preserves ordering");
    CoTaskMemFree(text); IExplorerCommand_Release(child);
    CHECK(IEnumExplorerCommand_Skip(clone, 100) == S_FALSE, "Skipping beyond the last action is bounded");
    IEnumExplorerCommand_Release(clone); IEnumExplorerCommand_Release(enumerator); IExplorerCommand_Release(root);
    if (can_unload) { CHECK(can_unload() == S_OK, "All COM references are released cleanly"); FreeLibrary(library); }
    CoUninitialize(); printf("%d native shell checks passed.\n", checks); return 0;
}
