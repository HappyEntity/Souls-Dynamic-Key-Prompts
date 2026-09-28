// Proxy loader for Dark Souls II: Scholar of the First Sin. Built as dinput8.dll or
// xinput1_3.dll (see Loader.vcxproj, property Proxy); the forwarded exports live in
// proxy_<name>.cpp, everything else is shared.
//
// In DllMain it asks dearxan to neuter Arxan; dearxan invokes our callback at the
// game's entry point, where we load the managed core and let it install hooks.
// Under Seamless Co-op the start is different, see the Seamless Co-op section below.
//
// Everything here must fail soft: if anything is missing the game runs vanilla.

#include "loader.h"
#include <stdio.h>
#include <string>
#include <intrin.h>

#include "dearxan.h"
#include "MinHook.h"
#include "loader_api.h"

static HMODULE g_self;
static std::wstring g_mod_dir;
static SRWLOCK g_log_lock = SRWLOCK_INIT;
static FILE* g_log;
static DkpLoaderApi g_api;

void log_line(const char* line)
{
    AcquireSRWLockExclusive(&g_log_lock);
    if (g_log) {
        SYSTEMTIME t;
        GetLocalTime(&t);
        fprintf(g_log, "%02d:%02d:%02d.%03d [%5lu] %s\n", t.wHour, t.wMinute, t.wSecond,
                t.wMilliseconds, GetCurrentThreadId(), line);
        fflush(g_log);
    }
    ReleaseSRWLockExclusive(&g_log_lock);
}

void logf(const char* fmt, ...)
{
    char buf[1024];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(buf, sizeof buf, fmt, ap);
    va_end(ap);
    log_line(buf);
}

HMODULE load_system_dll(const wchar_t* name)
{
    wchar_t path[MAX_PATH];
    UINT n = GetSystemDirectoryW(path, MAX_PATH);
    if (n == 0 || n + 1 + wcslen(name) >= MAX_PATH)
        return nullptr;
    wcscat_s(path, L"\\");
    wcscat_s(path, name);
    return LoadLibraryW(path);
}

// ---------------------------------------------------------------- hooks API for the core

static int api_create_hook(void* target, void* detour, void** original)
{
    return MH_CreateHook(target, detour, original);
}
static int api_enable_hook(void* target) { return MH_EnableHook(target); }
static int api_disable_hook(void* target) { return MH_DisableHook(target); }

// ---------------------------------------------------------------- core exception handler guard
//
// The .NET NativeAOT runtime in the core registers a process-wide vectored exception handler
// (for faults in managed code). It then sees every first-chance exception of the game and of
// other mods, and on some systems it turns exceptions that the game or Seamless Co-op handle
// themselves into a crash (STATUS_INVALID_DISPOSITION). While the core initialises, its
// registration is replaced by a filter that forwards only exceptions raised inside the core.

using RtlAddVehFn = PVOID(NTAPI*)(ULONG, PVECTORED_EXCEPTION_HANDLER);
static RtlAddVehFn g_real_add_veh;
static void* g_add_veh_target;
static DWORD g_core_init_thread;
static PVECTORED_EXCEPTION_HANDLER g_core_veh;
static volatile uintptr_t g_core_lo, g_core_hi;

static LONG CALLBACK core_veh_filter(PEXCEPTION_POINTERS p)
{
    uintptr_t ip = static_cast<uintptr_t>(p->ContextRecord->Rip);
    if (ip < g_core_lo || ip >= g_core_hi || !g_core_veh)
        return EXCEPTION_CONTINUE_SEARCH;
    return g_core_veh(p);
}

static PVOID NTAPI add_veh_hook(ULONG first, PVECTORED_EXCEPTION_HANDLER handler)
{
    if (GetCurrentThreadId() == g_core_init_thread && !g_core_veh) {
        g_core_veh = handler;
        log_line("loader: core exception handler registered behind a filter");
        return g_real_add_veh(first, core_veh_filter);
    }
    return g_real_add_veh(first, handler);
}

static void guard_core_veh(bool on)
{
    if (on) {
        HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
        g_add_veh_target = ntdll ? reinterpret_cast<void*>(GetProcAddress(ntdll, "RtlAddVectoredExceptionHandler")) : nullptr;
        g_core_init_thread = GetCurrentThreadId();
        if (!g_add_veh_target
            || MH_CreateHook(g_add_veh_target, reinterpret_cast<void*>(&add_veh_hook), reinterpret_cast<void**>(&g_real_add_veh)) != MH_OK
            || MH_EnableHook(g_add_veh_target) != MH_OK)
            log_line("loader: WARNING: cannot guard the core exception handler");
    } else if (g_add_veh_target) {
        MH_DisableHook(g_add_veh_target);
        MH_RemoveHook(g_add_veh_target);
        g_core_init_thread = 0;
    }
}

static void set_core_range(HMODULE m)
{
    auto base = reinterpret_cast<uintptr_t>(m);
    auto nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + reinterpret_cast<const IMAGE_DOS_HEADER*>(base)->e_lfanew);
    g_core_hi = base + nt->OptionalHeader.SizeOfImage;
    g_core_lo = base;
}

// ---------------------------------------------------------------- Seamless Co-op
//
// Seamless Co-op's launcher starts the game suspended and loads its DLL (ds2sc.dll / ds3sc.dll)
// from a remote thread; that same thread loads us first while it initialises the process. With
// dearxan Seamless crashes on some systems, so under Seamless we wait until that thread exits (its
// DLL is loaded by then and the game's main thread is still suspended) and start without dearxan.
//
// ModEngine2 (Dark Souls III) starts the game itself and prepares it on the main thread right after
// our DllMain; dearxan patching the entry point as well makes the Steam DRM stub refuse to start
// the game, so under ModEngine2 we start without dearxan too.
//
// [Loader] section of DynamicKeyPrompts.ini, for diagnosing conflicts:
//   Defer=0     under Seamless, start the usual way (dearxan, right away)
//   Dearxan=1   under Seamless or ModEngine2, still use dearxan

static int ini_int(const wchar_t* key, int def)
{
    std::wstring ini = g_mod_dir + L"\\DynamicKeyPrompts.ini";
    return static_cast<int>(GetPrivateProfileIntW(L"Loader", key, def, ini.c_str()));
}

// Seamless Co-op for Dark Souls II and III (same author, same launcher design).
static const wchar_t* const kSeamless[][2] = {
    { L"ds2sc_launcher.exe", L"ds2sc.dll" },
    { L"ds3sc_launcher.exe", L"ds3sc.dll" },
};

static bool seamless_loaded()
{
    for (auto& s : kSeamless)
        if (GetModuleHandleW(s[1])) return true;
    return false;
}

// File name of the process that started the game ("steam.exe", "ds3sc_launcher.exe", ...).
static std::wstring parent_name()
{
    struct BasicInfo { LONG exit; PVOID peb; ULONG_PTR affinity; LONG prio; ULONG_PTR pid, parent; } info{};
    using QueryFn = LONG(NTAPI*)(HANDLE, int, PVOID, ULONG, PULONG);
    auto query = reinterpret_cast<QueryFn>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryInformationProcess"));
    if (!query || query(GetCurrentProcess(), 0, &info, sizeof info, nullptr) < 0)
        return L"";
    HANDLE parent = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, static_cast<DWORD>(info.parent));
    if (!parent)
        return L"";
    wchar_t path[MAX_PATH];
    DWORD n = MAX_PATH;
    std::wstring name;
    if (QueryFullProcessImageNameW(parent, 0, path, &n)) {
        const wchar_t* slash = wcsrchr(path, L'\\');
        name = slash ? slash + 1 : path;
    }
    CloseHandle(parent);
    return name;
}

enum class Launch { Normal, Seamless, ModEngine };

// Detected by the parent process: the Seamless DLL itself is loaded only after our DllMain has run.
static Launch detect_launch()
{
    std::wstring parent = parent_name();
    logf("loader: started by %ls", parent.empty() ? L"(unknown)" : parent.c_str());
    // ModEngine2's launcher starts the game through Detours and exits; it leaves MODENGINE_CONFIG in
    // the game's environment. Checked first: ModEngine2 may also have loaded Seamless already.
    if (GetEnvironmentVariableW(L"MODENGINE_CONFIG", nullptr, 0) > 0
        || _wcsicmp(parent.c_str(), L"modengine2_launcher.exe") == 0)
        return Launch::ModEngine;
    if (seamless_loaded())
        return Launch::Seamless;
    for (auto& s : kSeamless)
        if (_wcsicmp(parent.c_str(), s[0]) == 0) return Launch::Seamless;
    return Launch::Normal;
}

// ---------------------------------------------------------------- startup

static HMODULE g_core_preloaded;

static void load_core(bool arxan_detected, int arxan_status)
{
    MH_STATUS mh = MH_Initialize();
    if (mh != MH_OK && mh != MH_ERROR_ALREADY_INITIALIZED) {
        logf("loader: MH_Initialize failed: %d", mh);
        return;
    }

    std::wstring core = g_mod_dir + L"\\DynamicKeyPrompts.dll";
    guard_core_veh(true);
    HMODULE m = g_core_preloaded ? g_core_preloaded : LoadLibraryW(core.c_str());
    if (!m) {
        guard_core_veh(false);
        logf("loader: cannot load core DLL (error %lu) - running vanilla", GetLastError());
        return;
    }
    set_core_range(m);
    auto init = reinterpret_cast<DkpInitFn>(GetProcAddress(m, "DKP_Init"));
    if (!init) {
        guard_core_veh(false);
        log_line("loader: core has no DKP_Init export");
        return;
    }

    g_api.size = sizeof g_api;
    g_api.version = DKP_LOADER_API_VERSION;
    g_api.game_base = GetModuleHandleW(nullptr);
    g_api.mod_dir = g_mod_dir.c_str();
    g_api.arxan_detected = arxan_detected ? 1 : 0;
    g_api.arxan_status = arxan_status;
    g_api.log = log_line;
    g_api.create_hook = api_create_hook;
    g_api.enable_hook = api_enable_hook;
    g_api.disable_hook = api_disable_hook;

    int rc = init(&g_api); // the .NET runtime starts here and registers its exception handler
    guard_core_veh(false);
    logf("loader: core init returned %d", rc);
}

static void start_mod()
{
    // dearxan must be called before the game's entry point runs; the callback runs at
    // the entry point on the main thread, after DllMain has returned.
    try {
        dearxan::neuter_arxan([](const dearxan::DearxanResult& r) {
            logf("loader: dearxan status=%d arxan_detected=%d blocking_entrypoint=%d%s%s",
                 r.status(), r.is_arxan_detected(), r.is_executing_entrypoint(),
                 r.status() == dearxan::detail::DearxanSuccess ? "" : " error=",
                 r.status() == dearxan::detail::DearxanSuccess ? "" : r.error_msg().c_str());
            try {
                load_core(r.is_arxan_detected(), r.status());
            } catch (...) {
                log_line("loader: exception while loading core");
            }
        });
    } catch (...) {
        log_line("loader: exception from neuter_arxan");
    }
}

// Under Seamless Co-op the core is started without dearxan (on some systems dearxan and Seamless
// crash together) and without patching the game (SteamStub checks its own entry code). Instead
// the first GetSystemTimeAsFileTime call made from the game's .text is caught: that is the CRT's
// security cookie setup at the real entry point, after SteamStub has unpacked the game.
using GetTimeFn = void(WINAPI*)(LPFILETIME);
static GetTimeFn g_real_get_time;
static void* g_get_time_target;
static uintptr_t g_text_lo, g_text_hi;
static volatile LONG g_startup_caught;

static void WINAPI get_time_hook(LPFILETIME ft)
{
    g_real_get_time(ft);
    auto caller = reinterpret_cast<uintptr_t>(_ReturnAddress());
    if (caller < g_text_lo || caller >= g_text_hi || InterlockedExchange(&g_startup_caught, 1))
        return;
    MH_DisableHook(g_get_time_target);
    log_line("loader: game code reached - starting without dearxan");
    try {
        load_core(false, 0);
    } catch (...) {
        log_line("loader: exception while loading core");
    }
}

static void start_at_game_code()
{
    auto base = reinterpret_cast<uintptr_t>(GetModuleHandleW(nullptr));
    auto nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + reinterpret_cast<const IMAGE_DOS_HEADER*>(base)->e_lfanew);
    const IMAGE_SECTION_HEADER* s = IMAGE_FIRST_SECTION(nt);
    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; i++, s++) {
        if (memcmp(s->Name, ".text", 6) == 0) {
            g_text_lo = base + s->VirtualAddress;
            g_text_hi = g_text_lo + s->Misc.VirtualSize;
            break;
        }
    }
    g_get_time_target = reinterpret_cast<void*>(GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "GetSystemTimeAsFileTime"));
    MH_STATUS mh = MH_Initialize();
    if (g_text_lo && g_get_time_target && (mh == MH_OK || mh == MH_ERROR_ALREADY_INITIALIZED)
        && MH_CreateHook(g_get_time_target, reinterpret_cast<void*>(&get_time_hook), reinterpret_cast<void**>(&g_real_get_time)) == MH_OK
        && MH_EnableHook(g_get_time_target) == MH_OK)
        log_line("loader: waiting for the game code to start");
    else
        log_line("loader: cannot catch the game start - running vanilla");
}

// Start without dearxan and without touching the game's code: map the core now, run it when the
// game's own code starts (see start_at_game_code).
static void start_without_dearxan()
{
    // Loading the core at the game's start fails in some setups (error 18), so it is mapped now;
    // the .NET runtime only starts when DKP_Init is called.
    std::wstring core = g_mod_dir + L"\\DynamicKeyPrompts.dll";
    g_core_preloaded = LoadLibraryW(core.c_str());
    if (!g_core_preloaded)
        logf("loader: cannot load core DLL (error %lu) - running vanilla", GetLastError());
    else
        start_at_game_code();
}

// Loaded by another mod's loader (e.g. as a plugin of Ultimate ASI Loader): it loads its plugins from
// the game's entry point or later, so the entry point dearxan waits for is already running. Seen as
// a dynamic load (LoadLibrary, unlike the proxy being imported by the game) that is not Seamless
// Co-op or ModEngine2, or as the /GS security cookie the CRT replaces at the entry point having
// changed already.
static bool game_started()
{
    auto base = reinterpret_cast<uintptr_t>(GetModuleHandleW(nullptr));
    auto nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + reinterpret_cast<const IMAGE_DOS_HEADER*>(base)->e_lfanew);
    const IMAGE_DATA_DIRECTORY& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG];
    if (!dir.VirtualAddress || dir.Size < offsetof(IMAGE_LOAD_CONFIG_DIRECTORY64, SecurityCookie) + sizeof(ULONGLONG))
        return false;
    auto cfg = reinterpret_cast<const IMAGE_LOAD_CONFIG_DIRECTORY64*>(base + dir.VirtualAddress);
    if (!cfg->SecurityCookie)
        return false;
    ULONGLONG cookie = *reinterpret_cast<const ULONGLONG*>(cfg->SecurityCookie);
    return cookie != 0x00002B992DDFA232ull; // DEFAULT_SECURITY_COOKIE of the x64 CRT
}

// The core cannot start inside DllMain (loader lock); a thread starts it once DllMain has returned.
static DWORD WINAPI start_late_thread(LPVOID)
{
    try {
        load_core(false, 0);
    } catch (...) {
        log_line("loader: exception while loading core");
    }
    return 0;
}

static void start_late(const char* why)
{
    logf("loader: %s - starting without dearxan", why);
    if (HANDLE t = CreateThread(nullptr, 0, start_late_thread, nullptr, 0, nullptr))
        CloseHandle(t);
    else
        logf("loader: cannot start the core thread (error %lu) - running vanilla", GetLastError());
}

static DWORD g_deferred_thread;

// dynamic: loaded with LoadLibrary rather than as an import of the game (DllMain's lpReserved).
static void attach(bool dynamic)
{
    // Both variants (dinput8.dll and xinput1_3.dll) may be installed by mistake: only the
    // first one to load starts the mod, the other just forwards its exports.
    wchar_t mutexName[64];
    swprintf_s(mutexName, L"Local\\DynamicKeyPrompts.Loader.%lu", GetCurrentProcessId());
    CreateMutexW(nullptr, FALSE, mutexName);
    if (GetLastError() == ERROR_ALREADY_EXISTS)
        return;

    wchar_t path[MAX_PATH];
    DWORD n = GetModuleFileNameW(g_self, path, MAX_PATH);
    std::wstring dir(path, n);
    dir.resize(dir.find_last_of(L"\\/"));
    g_mod_dir = dir + L"\\DynamicKeyPrompts";
    CreateDirectoryW(g_mod_dir.c_str(), nullptr);

    std::wstring log_path = g_mod_dir + L"\\DynamicKeyPrompts.log";
    g_log = _wfsopen(log_path.c_str(), L"w", _SH_DENYWR);
    logf("loader: DynamicKeyPrompts loader attached as %s (%s)", g_proxy_name, dynamic ? "loaded by LoadLibrary" : "imported by the game");

    if (game_started()) {
        start_late("loaded after the game has started");
        return;
    }

    switch (detect_launch()) {
    case Launch::Seamless:
        if (!ini_int(L"Defer", 1)) break;
        g_deferred_thread = GetCurrentThreadId();
        log_line("loader: started by Seamless Co-op - waiting for it to finish loading");
        return;
    case Launch::ModEngine:
        // ModEngine2 prepares the game on its main thread right after us; with dearxan patching
        // the entry point as well, the game's Steam DRM stub refuses to start.
        if (ini_int(L"Dearxan", 0)) break;
        log_line("loader: started by ModEngine2 - starting without dearxan");
        start_without_dearxan();
        return;
    case Launch::Normal:
        if (dynamic && !ini_int(L"Dearxan", 0)) {
            start_late("loaded by another loader (e.g. an ASI loader)");
            return;
        }
        break;
    }
    start_mod();
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH) {
        g_self = inst;
        attach(reserved == nullptr);
        if (!g_deferred_thread)
            DisableThreadLibraryCalls(inst);
    } else if (reason == DLL_THREAD_DETACH && g_deferred_thread == GetCurrentThreadId()) {
        g_deferred_thread = 0;
        logf("loader: Seamless Co-op loaded (%s)", seamless_loaded() ? "its DLL is present" : "its DLL not found");
        if (ini_int(L"Dearxan", 0))
            start_mod();
        else
            start_without_dearxan();
        DisableThreadLibraryCalls(inst);
    }
    return TRUE;
}
