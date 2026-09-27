using System.Runtime.InteropServices;
using System.Text;

namespace DynamicKeyPrompts;

/// Helpers for working inside the game process, not tied to one game.
static unsafe class Native
{
    /// IMAGE_FILE_HEADER.TimeDateStamp of a loaded module: identifies the exact game build.
    public static uint TimeDateStamp(nint module)
    {
        byte* b = (byte*)module;
        return *(uint*)(b + *(int*)(b + 0x3C) + 8);
    }

    public static bool BytesMatch(nint address, ReadOnlySpan<byte> expected) =>
        new ReadOnlySpan<byte>((void*)address, expected.Length).SequenceEqual(expected);

    /// True if the code starts with a jump another mod placed there (jmp rel32 / jmp [rip+x]).
    public static bool IsDetoured(nint address)
    {
        byte* p = (byte*)address;
        return p[0] == 0xE9 || p[0] == 0xFF && p[1] == 0x25;
    }

    [DllImport("kernel32.dll")] internal static extern int VirtualProtect(nint address, nint size, uint protect, out uint old);
}

/// Safe reads of our own process memory (never faults on bad pointers).
static unsafe class Memory
{
    [DllImport("kernel32.dll")] static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern int ReadProcessMemory(nint proc, nint addr, void* buf, nint size, out nint read);

    public static byte[]? Read(nint addr, int size)
    {
        var buf = new byte[size];
        fixed (byte* p = buf)
            return ReadProcessMemory(GetCurrentProcess(), addr, p, size, out nint n) != 0 && n == size ? buf : null;
    }

    public static bool TryReadPtr(nint addr, out nint value)
    {
        nint v = 0;
        bool ok = ReadProcessMemory(GetCurrentProcess(), addr, &v, sizeof(nint), out nint n) != 0 && n == sizeof(nint);
        value = v;
        return ok;
    }

    public static string Hex(ReadOnlySpan<byte> b)
    {
        var sb = new StringBuilder(b.Length * 3);
        for (int i = 0; i < b.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(b[i].ToString("X2"));
        }
        return sb.ToString();
    }
}

/// Patches entries of a module's import address table.
static unsafe class Iat
{
    /// Replaces the IAT slot(s) of dll!function in module; returns the previous target (0 if not found).
    public static nint Patch(nint module, string dll, string function, nint replacement)
    {
        byte* b = (byte*)module;
        int pe = *(int*)(b + 0x3C);
        int importRva = *(int*)(b + pe + 0x18 + 0x70 + 8); // OptionalHeader64.DataDirectory[1]
        if (importRva == 0) return 0;
        nint previous = 0;
        for (int* d = (int*)(b + importRva); d[3] != 0; d += 5) // IMAGE_IMPORT_DESCRIPTOR
        {
            if (!new string((sbyte*)(b + d[3])).Equals(dll, StringComparison.OrdinalIgnoreCase)) continue;
            long* names = (long*)(b + (d[0] != 0 ? d[0] : d[4]));
            nint* slots = (nint*)(b + d[4]);
            for (int i = 0; names[i] != 0; i++)
            {
                if (names[i] < 0) continue; // by ordinal
                if (new string((sbyte*)(b + (int)names[i] + 2)) != function) continue;
                nint slot = (nint)(&slots[i]);
                Native.VirtualProtect(slot, sizeof(nint), 0x04 /*PAGE_READWRITE*/, out uint old);
                previous = slots[i];
                slots[i] = replacement;
                Native.VirtualProtect(slot, sizeof(nint), old, out _);
            }
        }
        return previous;
    }
}
